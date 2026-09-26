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
        // Teszt futtatása 10 millió iterációval
        var result = CpuBenchmark.RunMultiThreadedTest(threadCount: 4, iterationsPerThread: 10_000_000);

        // Kiíratás a terminálra
        _output.WriteLine($"[EREDMÉNY] Idő: {result.ElapsedMilliseconds:F2} ms | Pont: {result.OperationsPerSecond:F0}");

    }
}