using System.Diagnostics;
using System.Numerics;
using System.Threading;

namespace BenchmarkLab.Core;

public static class CpuBenchmark
{
    // Alkalmazásszintű korlátok, nem a hardver fizikai határai.
    public const int MaxThreadCount = 256;
    public const int MaxIterationsPerThread = 1_000_000_000;

    private static float _resultSink;

    public static BenchmarkResult RunMultiThreadedTest(
        int threadCount,
        int iterationsPerThread)
    {
        ValidateArguments(threadCount, iterationsPerThread);

        // A számlálást még a mérés előtt elvégezzük.
        // A long típusra váltás a szorzás ELŐTT történik.
        long totalOperations = checked(
            (long)threadCount
            * iterationsPerThread
            * Vector<float>.Count);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startTimestamp = Stopwatch.GetTimestamp();

        Parallel.For(
            0,
            threadCount,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = threadCount
            },
            _ => ExecuteSimdWorkload(iterationsPerThread));

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        if (elapsed.TotalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "A CPU-mérés időtartama nem lehet nulla.");
        }

        double operationsPerSecond =
            totalOperations / elapsed.TotalSeconds;

        return new BenchmarkResult(
            TestName: $"CPU Multi-Core SIMD ({threadCount} szál)",
            ElapsedMilliseconds: elapsed.TotalMilliseconds,
            OperationsPerSecond: operationsPerSecond,
            ThroughputGbPerSec: 0,
            MeasuredAtUtc: DateTimeOffset.UtcNow);
    }

    private static void ValidateArguments(
        int threadCount,
        int iterationsPerThread)
    {
        if (threadCount < 1 || threadCount > MaxThreadCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(threadCount),
                threadCount,
                $"A szálak száma 1 és {MaxThreadCount} között legyen.");
        }

        if (iterationsPerThread < 1 ||
            iterationsPerThread > MaxIterationsPerThread)
        {
            throw new ArgumentOutOfRangeException(
                nameof(iterationsPerThread),
                iterationsPerThread,
                $"Az iterációszám 1 és " +
                $"{MaxIterationsPerThread} között legyen.");
        }
    }

    private static void ExecuteSimdWorkload(int iterations)
    {
        var multiplier = new Vector<float>(1.0f);
        var increment = new Vector<float>(0.99999f);
        var accumulator = new Vector<float>(0.5f);

        for (int index = 0; index < iterations; index++)
        {
            accumulator =
                Vector.Multiply(accumulator, multiplier) + increment;
        }

        // Megfigyelhetővé tesszük az eredményt, hogy a számítás
        // ne váljon felhasználatlan munkává az optimalizáló számára.
        Volatile.Write(ref _resultSink, accumulator[0]);
    }
}