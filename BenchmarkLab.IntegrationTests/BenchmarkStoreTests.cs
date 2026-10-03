using BenchmarkLab.App;
using BenchmarkLab.Core;
using MySqlConnector;
using Xunit;

namespace BenchmarkLab.IntegrationTests;

public sealed class BenchmarkStoreTests
{
    [Fact]
    public void EmptyDatabase_CreatesSchema_AndCanInitializeAgain()
    {
        using var database = new TestDatabase();

        Assert.Equal(0L, database.Count("""
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE();
            """));

        _ = new BenchmarkStore(database.ConnectionString);

        Assert.Equal(3L, database.Count("""
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME IN (
                  'Devices', 'BenchmarkRuns', 'Measurements');
            """));

        Assert.Equal(3L, database.Count("""
            SELECT COUNT(*)
            FROM information_schema.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND CONSTRAINT_TYPE = 'FOREIGN KEY';
            """));

        // Ismételt inicializáláskor sem keletkezhet hiba.
        _ = new BenchmarkStore(database.ConnectionString);

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM Measurements;"));
    }

    [Fact]
    public void SaveRun_PersistsRelationsTemperaturesAndUtcTime()
    {
        using var database = new TestDatabase();
        var store = new BenchmarkStore(database.ConnectionString);

        var device = CreateDevice();
        var run = CreateRun();

        var cpu = CreateMeasurement(run.Id, "CPU") with
        {
            CpuTempBeforeC = null,
            CpuTempAfterC = 61.5
        };

        var memory = CreateMeasurement(run.Id, "Memory");

        store.SaveRun(device, run, new[] { cpu, memory });

        Assert.Equal(1L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(1L, database.Count(
            "SELECT COUNT(*) FROM BenchmarkRuns;"));

        Assert.Equal(2L, database.Count("""
            SELECT COUNT(*)
            FROM Measurements m
            JOIN BenchmarkRuns r ON r.Id = m.RunId
            JOIN Devices d ON d.Id = r.DeviceId
            WHERE m.DeviceId = d.Id;
            """));

        Assert.Equal(
            device.DeviceUid.ToString("D"),
            Convert.ToString(database.Scalar(
                "SELECT DeviceUid FROM Devices;")));

        Assert.Equal(
            run.Id.ToString("D"),
            Convert.ToString(database.Scalar(
                "SELECT Id FROM BenchmarkRuns;")));

        Assert.Null(database.Scalar("""
            SELECT CpuTempBeforeC
            FROM Measurements
            WHERE TestName = 'CPU';
            """));

        Assert.Equal(
            61.5,
            Convert.ToDouble(database.Scalar("""
                SELECT CpuTempAfterC
                FROM Measurements
                WHERE TestName = 'CPU';
                """)));

        var savedTime = Assert.IsType<DateTime>(
            database.Scalar("""
                SELECT MeasuredAtUtc
                FROM Measurements
                WHERE TestName = 'CPU';
                """));

        Assert.Equal(
            cpu.MeasuredAtUtc.UtcDateTime.Ticks,
            savedTime.Ticks);
    }

    [Fact]
    public void SameDeviceUid_AfterRename_ReusesDeviceRow()
    {
        using var database = new TestDatabase();
        var store = new BenchmarkStore(database.ConnectionString);

        var device = CreateDevice();
        var firstRun = CreateRun();

        store.SaveRun(
            device,
            firstRun,
            new[] { CreateMeasurement(firstRun.Id, "First") });

        long originalDeviceId = Convert.ToInt64(
            database.Scalar("SELECT Id FROM Devices;"));

        var renamedDevice = device with
        {
            MachineName = "RenamedMachine"
        };

        var secondRun = CreateRun();

        store.SaveRun(
            renamedDevice,
            secondRun,
            new[] { CreateMeasurement(secondRun.Id, "Second") });

        Assert.Equal(1L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(
            originalDeviceId,
            Convert.ToInt64(database.Scalar(
                "SELECT Id FROM Devices;")));

        Assert.Equal(
            "RenamedMachine",
            Convert.ToString(database.Scalar(
                "SELECT MachineName FROM Devices;")));

        Assert.Equal(2L, database.Count(
            "SELECT COUNT(*) FROM BenchmarkRuns;"));
    }

    [Fact]
    public void SameMachineName_WithDifferentUids_CreatesTwoDevices()
    {
        using var database = new TestDatabase();
        var store = new BenchmarkStore(database.ConnectionString);

        var firstDevice = CreateDevice();

        var secondDevice = firstDevice with
        {
            DeviceUid = Guid.NewGuid()
        };

        var firstRun = CreateRun();
        var secondRun = CreateRun();

        store.SaveRun(
            firstDevice,
            firstRun,
            new[] { CreateMeasurement(firstRun.Id, "First") });

        store.SaveRun(
            secondDevice,
            secondRun,
            new[] { CreateMeasurement(secondRun.Id, "Second") });

        Assert.Equal(2L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(2L, database.Count(
            "SELECT COUNT(DISTINCT DeviceId) FROM BenchmarkRuns;"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureOnSecondMeasurement_RollsBackEntireSave(
        bool deviceAlreadyExists)
    {
        using var database = new TestDatabase();
        var store = new BenchmarkStore(database.ConnectionString);

        var originalDevice = CreateDevice();

        if (deviceAlreadyExists)
        {
            var originalRun = CreateRun();

            store.SaveRun(
                originalDevice,
                originalRun,
                new[]
                {
                    CreateMeasurement(originalRun.Id, "Baseline")
                });
        }

        // Csak a tesztadatbázisban létrehozott trigger.
        // Egy megadott nevű mérés beszúrásakor hibát dob.
        database.Execute("""
            CREATE TRIGGER FailSelectedMeasurement
            BEFORE INSERT ON Measurements
            FOR EACH ROW
            BEGIN
                IF NEW.TestName = 'FORCE_FAILURE' THEN
                    SIGNAL SQLSTATE '45000'
                        SET MYSQL_ERRNO = 1644,
                            MESSAGE_TEXT = 'Integration test failure';
                END IF;
            END;
            """);

        var changedDevice = originalDevice with
        {
            MachineName = "MustNotRemain",
            CpuModel = "MustNotRemainCPU"
        };

        var failedRun = CreateRun();

        var error = Assert.Throws<MySqlException>(
            () => store.SaveRun(
                changedDevice,
                failedRun,
                new[]
                {
                    CreateMeasurement(failedRun.Id, "FirstInserted"),
                    CreateMeasurement(failedRun.Id, "FORCE_FAILURE")
                }));

        Assert.Equal(1644, error.Number);

        long expectedRows = deviceAlreadyExists ? 1L : 0L;

        Assert.Equal(expectedRows, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(expectedRows, database.Count(
            "SELECT COUNT(*) FROM BenchmarkRuns;"));

        Assert.Equal(expectedRows, database.Count(
            "SELECT COUNT(*) FROM Measurements;"));

        Assert.Equal(
            0L,
            Convert.ToInt64(database.Scalar(
                "SELECT COUNT(*) FROM BenchmarkRuns WHERE Id = @id;",
                ("@id", failedRun.Id.ToString("D")))));

        Assert.Equal(0L, database.Count("""
            SELECT COUNT(*)
            FROM Measurements
            WHERE TestName IN ('FirstInserted', 'FORCE_FAILURE');
            """));

        if (deviceAlreadyExists)
        {
            // A korábbi adatokat az elbukott mentés
            // nem változtathatja meg.
            Assert.Equal(
                originalDevice.MachineName,
                Convert.ToString(database.Scalar(
                    "SELECT MachineName FROM Devices;")));

            Assert.Equal(
                originalDevice.CpuModel,
                Convert.ToString(database.Scalar(
                    "SELECT CpuModel FROM Devices;")));

            Assert.Equal(
                "Baseline",
                Convert.ToString(database.Scalar(
                    "SELECT TestName FROM Measurements;")));
        }
    }

    [Fact]
    public void MismatchedRunId_IsRejectedWithoutWritingData()
    {
        using var database = new TestDatabase();
        var store = new BenchmarkStore(database.ConnectionString);

        var run = CreateRun();
        var measurement = CreateMeasurement(
            Guid.NewGuid(),
            "WrongRun");

        Assert.Throws<ArgumentException>(
            () => store.SaveRun(
                CreateDevice(),
                run,
                new[] { measurement }));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM BenchmarkRuns;"));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM Measurements;"));
    }

    private static DeviceInfo CreateDevice()
    {
        return new DeviceInfo(
            MachineName: "IntegrationMachine",
            OperatingSystem: "TestOS",
            CpuModel: "TestCPU",
            TotalMemoryGb: 16)
        {
            DeviceUid = Guid.NewGuid()
        };
    }

    private static BenchmarkRun CreateRun()
    {
        var started = new DateTimeOffset(
            2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        return new BenchmarkRun(
            Id: Guid.NewGuid(),
            RunType: "FullSuite",
            StartedAtUtc: started,
            FinishedAtUtc: started.AddSeconds(1),
            Status: "Completed",
            AppVersion: "integration-test");
    }

    private static BenchmarkResult CreateMeasurement(
        Guid runId,
        string name)
    {
        // A DATETIME(6) pontosságához illeszkedő időpont.
        var measuredAt = new DateTimeOffset(
            2026, 1, 1, 12, 0, 1, TimeSpan.Zero)
            .AddTicks(1_234_560);

        return new BenchmarkResult(
            TestName: name,
            ElapsedMilliseconds: 100,
            OperationsPerSecond: 1_000,
            ThroughputGbPerSec: 0,
            MeasuredAtUtc: measuredAt)
        {
            RunId = runId
        };
    }
}