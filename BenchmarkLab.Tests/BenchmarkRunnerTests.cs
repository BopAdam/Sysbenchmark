using System.Data.Common;
using BenchmarkLab.App;
using BenchmarkLab.Core;
using Xunit;

namespace BenchmarkLab.Tests;

public sealed class BenchmarkRunnerTests
{
    [Fact]
    public void FullSuite_GroupsResults_AndKeepsEarlierMeasurements()
    {
        var earlier = TestData.Measurement("Earlier") with
        {
            RunId = Guid.NewGuid()
        };

        var allResults = new List<BenchmarkResult> { earlier };
        var store = new RecordingStore();
        var device = TestData.Device();

        var run = BenchmarkRunner.Run(
            "FullSuite",
            results =>
            {
                results.Add(TestData.Measurement("CPU"));
                results.Add(TestData.Measurement("Memory"));
            },
            allResults,
            store,
            device);

        Assert.Equal("Completed", run.Status);
        Assert.Equal("FullSuite", run.RunType);
        Assert.NotEqual(Guid.Empty, run.Id);

        Assert.Equal(3, allResults.Count);
        Assert.Equal(earlier, allResults[0]);

        Assert.Equal(1, store.SaveCalls);
        Assert.Equal(device, store.SavedDevice);
        Assert.Equal(run, store.SavedRun);
        Assert.Equal(2, store.SavedResults.Count);

        Assert.All(
            store.SavedResults,
            result => Assert.Equal((Guid?)run.Id, result.RunId));

        Assert.Equal(allResults[1], store.SavedResults[0]);
        Assert.Equal(allResults[2], store.SavedResults[1]);
    }

    [Fact]
    public void SeparateExecutions_ReceiveDifferentRunIds()
    {
        var results = new List<BenchmarkResult>();
        var device = TestData.Device();

        var first = BenchmarkRunner.Run(
            "Cpu",
            items => items.Add(TestData.Measurement()),
            results,
            null,
            device);

        var second = BenchmarkRunner.Run(
            "Cpu",
            items => items.Add(TestData.Measurement()),
            results,
            null,
            device);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal((Guid?)first.Id, results[0].RunId);
        Assert.Equal((Guid?)second.Id, results[1].RunId);
    }

    [Fact]
    public void PartialFailure_KeepsAndSavesCompletedMeasurement()
    {
        var results = new List<BenchmarkResult>();
        var store = new RecordingStore();

        var run = BenchmarkRunner.Run(
            "FullSuite",
            items =>
            {
                items.Add(TestData.Measurement("CPU"));
                throw new InvalidOperationException("Test failure");
            },
            results,
            store,
            TestData.Device());

        Assert.Equal("Failed", run.Status);

        var measurement = Assert.Single(results);
        Assert.Equal("CPU", measurement.TestName);
        Assert.Equal((Guid?)run.Id, measurement.RunId);

        Assert.Equal(1, store.SaveCalls);
        Assert.Equal(run, store.SavedRun);
        Assert.Single(store.SavedResults);
    }

    [Fact]
    public void FailureBeforeFirstResult_SavesFailedEmptyRun()
    {
        var results = new List<BenchmarkResult>();
        var store = new RecordingStore();

        var run = BenchmarkRunner.Run(
            "Cpu",
            _ => throw new InvalidOperationException("Test failure"),
            results,
            store,
            TestData.Device());

        Assert.Equal("Failed", run.Status);
        Assert.Empty(results);
        Assert.Equal(1, store.SaveCalls);
        Assert.Equal(run, store.SavedRun);
        Assert.Empty(store.SavedResults);
    }

    [Fact]
    public void MissingStore_KeepsResultsForExport()
    {
        var results = new List<BenchmarkResult>();

        var run = BenchmarkRunner.Run(
            "Cpu",
            items => items.Add(TestData.Measurement()),
            results,
            null,
            TestData.Device());

        Assert.Equal("Completed", run.Status);
        Assert.Equal((Guid?)run.Id, Assert.Single(results).RunId);
    }

    [Fact]
    public void DatabaseFailure_DoesNotLoseCompletedResults()
    {
        var results = new List<BenchmarkResult>();
        var store = new RecordingStore
        {
            FailSave = true
        };

        var run = BenchmarkRunner.Run(
            "Cpu",
            items => items.Add(TestData.Measurement()),
            results,
            store,
            TestData.Device());

        Assert.Equal(1, store.SaveCalls);
        Assert.Equal("Completed", run.Status);
        Assert.Equal((Guid?)run.Id, Assert.Single(results).RunId);
    }

    private sealed class RecordingStore : IBenchmarkStore
    {
        public bool FailSave { get; init; }
        public int SaveCalls { get; private set; }

        public DeviceInfo? SavedDevice { get; private set; }
        public BenchmarkRun? SavedRun { get; private set; }

        public IReadOnlyList<BenchmarkResult> SavedResults
            { get; private set; } = Array.Empty<BenchmarkResult>();

        public void SaveRun(
            DeviceInfo device,
            BenchmarkRun run,
            IReadOnlyList<BenchmarkResult> results)
        {
            SaveCalls++;

            if (FailSave)
            {
                throw new TestDatabaseException();
            }

            SavedDevice = device;
            SavedRun = run;

            // Pillanatképet őrzünk meg a mentésre átadott adatokról.
            SavedResults = results.ToArray();
        }
    }

    private sealed class TestDatabaseException : DbException
    {
        public TestDatabaseException()
            : base("Simulated database failure")
        {
        }
    }
}