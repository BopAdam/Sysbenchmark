namespace BenchmarkLab.Core;

public readonly record struct BenchmarkResult(
    string TestName,
    double ElapsedMilliseconds,
    double OperationsPerSecond,
    double ThroughputGbPerSec,
    DateTimeOffset MeasuredAtUtc
)
{
    public double? CpuTempBeforeC { get; init; }
    public double? CpuTempAfterC { get; init; }
}