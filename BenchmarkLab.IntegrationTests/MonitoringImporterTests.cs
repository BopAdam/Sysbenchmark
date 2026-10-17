using System.Text.Json;
using BenchmarkLab.App;
using MySqlConnector;
using Xunit;

namespace BenchmarkLab.IntegrationTests;

public sealed class MonitoringImporterTests
{
    [Theory]
    [InlineData("Offline")]
    [InlineData("Running")]
    [InlineData("Stopped")]
    public void Import_CompletesSession_AndCanBeRepeated(string initialState)
    {
        using var database = new TestDatabase();
        using var log = new TestLog();

        var store = new MonitoringStore(database.ConnectionString);

        if (initialState != "Offline")
        {
            store.Start(log.Device, log.Id, log.Started, 1000);
            store.SaveSample(log.Samples[0]);

            if (initialState == "Stopped")
            {
                store.Finish(
                    log.Id,
                    log.Finished,
                    "Stopped",
                    2,
                    databaseComplete: false);
            }
        }

        MonitoringImportResult first = MonitoringImporter.Import(
            log.Path,
            database.ConnectionString);

        Assert.Equal(
            initialState == "Offline" ? 2 : 1,
            first.AddedSamples);

        Assert.Equal(2, first.TotalSamples);

        MonitoringImportResult second = MonitoringImporter.Import(
            log.Path,
            database.ConnectionString);

        Assert.Equal(0, second.AddedSamples);
        Assert.Equal(2, second.ExistingSamples);

        Assert.Equal(1L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(1L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSessions;"));

        Assert.Equal(2L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSamples;"));

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
    }

    [Fact]
    public void ConflictingSample_IsRejectedWithoutOverwriting()
    {
        using var database = new TestDatabase();
        using var log = new TestLog();

        var store = new MonitoringStore(database.ConnectionString);

        store.Start(log.Device, log.Id, log.Started, 1000);

        store.SaveSample(log.Samples[1] with
        {
            CpuUsagePercent = 99
        });

        store.Finish(
            log.Id,
            log.Finished,
            "Stopped",
            2,
            databaseComplete: false);

        Assert.Throws<InvalidDataException>(() =>
            MonitoringImporter.Import(log.Path, database.ConnectionString));

        Assert.Equal(1L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSamples;"));

        Assert.Equal(
            99.0,
            Convert.ToDouble(database.Scalar(
                "SELECT CpuUsagePercent FROM MonitoringSamples;")));

        Assert.Equal(
            "Incomplete",
            Convert.ToString(database.Scalar(
                "SELECT PersistenceStatus FROM MonitoringSessions;")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidLog_IsRejectedBeforeWriting(bool wrongCount)
    {
        using var database = new TestDatabase();
        using var log = new TestLog();

        _ = new MonitoringStore(database.ConnectionString);

        var lines = log.CreateLines();

        if (wrongCount)
        {
            lines[^1] = JsonSerializer.Serialize(new
            {
                Type = "SessionFinished",
                SessionId = log.Id,
                FinishedAtUtc = log.Finished,
                Status = "Stopped",
                SampleCount = 999
            });
        }
        else
        {
            lines.RemoveAt(lines.Count - 1);
        }

        File.WriteAllLines(log.Path, lines);

        Assert.Throws<InvalidDataException>(() =>
            MonitoringImporter.Import(log.Path, database.ConnectionString));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSessions;"));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSamples;"));
    }

    [Fact]
    public void DatabaseFailure_RollsBackEntireImport()
    {
        using var database = new TestDatabase();
        using var log = new TestLog();

        _ = new MonitoringStore(database.ConnectionString);

        database.Execute("""
            CREATE TRIGGER FailSecondImportedSample
            BEFORE INSERT ON MonitoringSamples
            FOR EACH ROW
            BEGIN
                IF NEW.SequenceNumber = 2 THEN
                    SIGNAL SQLSTATE '45000'
                        SET MYSQL_ERRNO = 1644,
                            MESSAGE_TEXT = 'Import test failure';
                END IF;
            END;
            """);

        MySqlException error = Assert.Throws<MySqlException>(() =>
            MonitoringImporter.Import(log.Path, database.ConnectionString));

        Assert.Equal(1644, error.Number);

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM Devices;"));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSessions;"));

        Assert.Equal(0L, database.Count(
            "SELECT COUNT(*) FROM MonitoringSamples;"));
    }

    private sealed class TestLog : IDisposable
    {
        public Guid Id { get; } = Guid.NewGuid();

        public DateTimeOffset Started { get; } =
            new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Finished => Started.AddSeconds(3);

        public DeviceInfo Device { get; } = new(
            "ImportTestMachine",
            "TestOS",
            "TestCPU",
            16)
        {
            DeviceUid = Guid.NewGuid()
        };

        public MonitoringSample[] Samples { get; }

        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"sysbenchmark-import-test-{Guid.NewGuid():N}.jsonl");

        public TestLog()
        {
            Samples = new[]
            {
                new MonitoringSample(
                    Id, 1, Started.AddSeconds(1),
                    25, 16, 10, 6, null),

                new MonitoringSample(
                    Id, 2, Started.AddSeconds(2),
                    30, 16, 9, 7, 61.5)
            };

            File.WriteAllLines(Path, CreateLines());
        }

        public List<string> CreateLines()
        {
            var lines = new List<string>
            {
                JsonSerializer.Serialize(new
                {
                    Type = "SessionStarted",
                    SchemaVersion = 1,
                    SessionId = Id,
                    StartedAtUtc = Started,
                    SampleIntervalMilliseconds = 1000,
                    Device
                })
            };

            foreach (MonitoringSample sample in Samples)
            {
                lines.Add(JsonSerializer.Serialize(new
                {
                    Type = "Sample",
                    Sample = sample
                }));
            }

            lines.Add(JsonSerializer.Serialize(new
            {
                Type = "SessionFinished",
                SessionId = Id,
                FinishedAtUtc = Finished,
                Status = "Stopped",
                SampleCount = Samples.Length,
                ErrorType = (string?)null
            }));

            return lines;
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}