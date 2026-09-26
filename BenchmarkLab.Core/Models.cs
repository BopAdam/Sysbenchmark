namespace BenchmarkLab.Core;

public readonly record struct BenchmarkResult(
    string TestName,
    double ElapsedMilliseconds,
    double OperationsPerSecond,
    double ThroughputGbPerSec
);

