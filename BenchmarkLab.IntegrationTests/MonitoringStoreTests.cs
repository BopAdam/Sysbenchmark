using BenchmarkLab.App;
using MySqlConnector;
using Xunit;

namespace BenchmarkLab.IntegrationTests;

public sealed class MonitoringStoreTests
{
    [Fact]
    public void Session_SavesSamplesAndFinishes()
    {
        using var database = new TestDatabase();

        var store = new MonitoringStore(database.ConnectionString);

        // Az ismételt inicializálás is működjön.
        _ = new MonitoringStore(database.ConnectionString);

        Guid sessionId = Guid.NewGuid();

        var started = new DateTimeOffset(
            2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

        DeviceInfo device = CreateDevice();

        store.Start(device, sessionId, started, 1000);

        Assert.Equal(
            "Running",
            Convert.ToString(database.Scalar(
                "SELECT Status FROM MonitoringSessions;")));

        store.SaveSample(CreateSample(sessionId, 1, started.AddSeconds(1)));

        store.SaveSample(
            CreateSample(sessionId, 2, started.AddSeconds(2)) with
            {
                CpuTemperatureC = 61.5
            });

        store.Finish(
            sessionId,
            started.AddSeconds(3),
            "Stopped",
            2,
            databaseComplete: true);

        Assert.Equal(2L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSamples;"));

        Assert.Equal(1L, database.Count("""
            SELECT COUNT(*)
            FROM MonitoringSessions s
            JOIN Devices d ON d.Id = s.DeviceId;
            """));

        Assert.Equal(
            device.DeviceUid.ToString("D"),
            Convert.ToString(database.Scalar(
                "SELECT DeviceUid FROM Devices;")));

        Assert.Equal(
            "Stopped",
            Convert.ToString(database.Scalar(
                "SELECT Status FROM MonitoringSessions;")));

        Assert.Equal(
            "Complete",
            Convert.ToString(database.Scalar(
                "SELECT PersistenceStatus FROM MonitoringSessions;")));

        Assert.Equal(2L, database.Count(
            "SELECT LocalSampleCount FROM MonitoringSessions;"));

        Assert.Null(database.Scalar("""
            SELECT CpuTemperatureC
            FROM MonitoringSamples
            WHERE SequenceNumber = 1;
            """));

        Assert.Equal(
            61.5,
            Convert.ToDouble(database.Scalar("""
                SELECT CpuTemperatureC
                FROM MonitoringSamples
                WHERE SequenceNumber = 2;
                """)));

        DateTime savedTime = Assert.IsType<DateTime>(
            database.Scalar("""
                SELECT MeasuredAtUtc
                FROM MonitoringSamples
                WHERE SequenceNumber = 1;
                """));

        Assert.Equal(
            started.AddSeconds(1).UtcDateTime.Ticks,
            savedTime.Ticks);

        // Lezárt munkamenethez már nem írhatunk új mintát.
        Assert.Throws<InvalidOperationException>(() =>
            store.SaveSample(
                CreateSample(sessionId, 3, started.AddSeconds(4))));

        Assert.Equal(2L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSamples;"));
    }

    [Fact]
    public void DuplicateSample_IsRejected_AndIncompleteStateCanBeSaved()
    {
        using var database = new TestDatabase();
        var store = new MonitoringStore(database.ConnectionString);

        Guid sessionId = Guid.NewGuid();
        DateTimeOffset started = DateTimeOffset.UtcNow;

        store.Start(CreateDevice(), sessionId, started, 1000);

        MonitoringSample sample = CreateSample(
            sessionId,
            1,
            started.AddSeconds(1));

        store.SaveSample(sample);

        MySqlException error = Assert.Throws<MySqlException>(
            () => store.SaveSample(sample));

        Assert.Equal(1062, error.Number);

        store.Finish(
            sessionId,
            started.AddSeconds(3),
            "Stopped",
            localSampleCount: 2,
            databaseComplete: false);

        Assert.Equal(1L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSamples;"));

        Assert.Equal(
            "Incomplete",
            Convert.ToString(database.Scalar(
                "SELECT PersistenceStatus FROM MonitoringSessions;")));

        Assert.Equal(2L, database.Count(
            "SELECT LocalSampleCount FROM MonitoringSessions;"));
    }

    [Fact]
    public void SessionInsertFailure_RollsBackDeviceInsert()
    {
        using var database = new TestDatabase();
        var store = new MonitoringStore(database.ConnectionString);

        database.Execute("""
            CREATE TRIGGER FailMonitoringSession
            BEFORE INSERT ON MonitoringSessions
            FOR EACH ROW
            SIGNAL SQLSTATE '45000'
                SET MYSQL_ERRNO = 1644,
                    MESSAGE_TEXT = 'Integration test failure';
            """);

        MySqlException error = Assert.Throws<MySqlException>(() =>
            store.Start(
                CreateDevice(),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                1000));

        Assert.Equal(1644, error.Number);

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSessions;"));
    }

    private static DeviceInfo CreateDevice()
    {
        return new DeviceInfo(
            "MonitoringTestMachine",
            "TestOS",
            "TestCPU",
            16)
        {
            DeviceUid = Guid.NewGuid()
        };
    }

    private static MonitoringSample CreateSample(
        Guid sessionId,
        long sequence,
        DateTimeOffset time)
    {
        return new MonitoringSample(
            SessionId: sessionId,
            Sequence: sequence,
            MeasuredAtUtc: time,
            CpuUsagePercent: 25,
            TotalMemoryGiB: 16,
            AvailableMemoryGiB: 10,
            UsedMemoryGiB: 6,
            CpuTemperatureC: null);
    }
}