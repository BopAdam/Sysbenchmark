using Xunit;
using Xunit.Abstractions;
using BenchmarkLab.Core;

namespace BenchmarkLab.Tests;

public class BenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public BenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CpuBenchmark_ShouldRunAndReturnScore()
    {
        var result = CpuBenchmark.RunMultiThreadedTest(
        threadCount: 4,
        iterationsPerThread: 100_000);

    _output.WriteLine(
        $"[EREDMÉNY] Idő: {result.ElapsedMilliseconds:F2} ms | " +
        $"Művelet/sec: {result.OperationsPerSecond:F0}");

    Assert.True(result.ElapsedMilliseconds > 0);
    Assert.True(result.OperationsPerSecond > 0);
    }
    }

