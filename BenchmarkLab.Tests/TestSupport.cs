using BenchmarkLab.App;
using BenchmarkLab.Core;

namespace BenchmarkLab.Tests;

internal static class TestData
{
    public static DeviceInfo Device() =>
        new("TestMachine", "TestOS", "TestCPU", 16)
        {
            DeviceUid = Guid.NewGuid()
        };

    public static BenchmarkResult Measurement(string name = "CPU") =>
        new(
            TestName: name,
            ElapsedMilliseconds: 100,
            OperationsPerSecond: 1_000,
            ThroughputGbPerSec: 0,
            MeasuredAtUtc: new DateTimeOffset(
                2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
}

internal sealed class TemporaryDirectory : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(
        Path.GetTempPath(),
        "Sysbenchmark.Tests",
        Guid.NewGuid().ToString("N"));

    public TemporaryDirectory()
    {
        Directory.CreateDirectory(DirectoryPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}