using BenchmarkLab.Core;
using System.Numerics;
using Xunit;

namespace BenchmarkLab.Tests;

public sealed class BenchmarkValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(257)]
    [InlineData(int.MaxValue)]
    public void Cpu_InvalidThreadCount_Throws(int threadCount)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CpuBenchmark.RunMultiThreadedTest(
                threadCount: threadCount,
                iterationsPerThread: 1));

        Assert.Equal("threadCount", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_000_001)]
    [InlineData(int.MaxValue)]
    public void Cpu_InvalidIterationCount_Throws(int iterations)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CpuBenchmark.RunMultiThreadedTest(
                threadCount: 1,
                iterationsPerThread: iterations));

        Assert.Equal("iterationsPerThread", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1025)]
    [InlineData(2048)]
    [InlineData(int.MaxValue)]
    public void Memory_InvalidBufferSize_ThrowsBeforeAllocation(
        int sizeInMb)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => MemoryBenchmark.RunSequentialBandwidthTest(
                sizeInMb: sizeInMb,
                passes: 1));

        Assert.Equal("sizeInMb", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    [InlineData(int.MaxValue)]
    public void Memory_InvalidPassCount_Throws(int passes)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => MemoryBenchmark.RunSequentialBandwidthTest(
                sizeInMb: 1,
                passes: passes));

        Assert.Equal("passes", exception.ParamName);
    }

    [Fact]
    public void Cpu_ValidInput_ReturnsConsistentResult()
    {
        const int threadCount = 2;
        const int iterations = 100_000;

        var result = CpuBenchmark.RunMultiThreadedTest(
            threadCount,
            iterations);

        Assert.True(double.IsFinite(result.ElapsedMilliseconds));
        Assert.True(result.ElapsedMilliseconds > 0);

        Assert.True(double.IsFinite(result.OperationsPerSecond));
        Assert.True(result.OperationsPerSecond > 0);

        Assert.Equal(0d, result.ThroughputGbPerSec);
        Assert.Equal(TimeSpan.Zero, result.MeasuredAtUtc.Offset);

        double expectedOperations =
            (double)threadCount * iterations * Vector<float>.Count;

        double reconstructedOperations =
            result.OperationsPerSecond
            * result.ElapsedMilliseconds / 1000.0;

        Assert.InRange(
            reconstructedOperations,
            expectedOperations * 0.999999,
            expectedOperations * 1.000001);
    }

    [Fact]
    public void Memory_ValidInput_ReturnsConsistentResult()
    {
        const int sizeInMiB = 1;
        const int passes = 2;

        var result = MemoryBenchmark.RunSequentialBandwidthTest(
            sizeInMiB,
            passes);

        Assert.True(double.IsFinite(result.ElapsedMilliseconds));
        Assert.True(result.ElapsedMilliseconds > 0);

        Assert.True(double.IsFinite(result.ThroughputGbPerSec));
        Assert.True(result.ThroughputGbPerSec > 0);

        Assert.Equal(0d, result.OperationsPerSecond);
        Assert.Null(result.CpuTempBeforeC);
        Assert.Null(result.CpuTempAfterC);
        Assert.Equal(TimeSpan.Zero, result.MeasuredAtUtc.Offset);

        double expectedGiB = (double)sizeInMiB * passes / 1024.0;

        double reconstructedGiB =
            result.ThroughputGbPerSec
            * result.ElapsedMilliseconds / 1000.0;

        Assert.InRange(
            reconstructedGiB,
            expectedGiB * 0.999999,
            expectedGiB * 1.000001);
    }
}