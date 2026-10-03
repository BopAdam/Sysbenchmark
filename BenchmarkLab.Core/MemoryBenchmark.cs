using System.Diagnostics;

namespace BenchmarkLab.Core;

public static class MemoryBenchmark
{
    // A sizeInMb név kompatibilitás miatt marad.
    // A számítás ténylegesen MiB egységet használ.
    public const int MaxBufferSizeMiB = 1024;
    public const int MaxPasses = 10_000;

    private const long BytesPerMiB = 1024L * 1024L;
    private const double BytesPerGiB = 1024.0 * 1024.0 * 1024.0;
    private const long FillValue = 42;

    public static BenchmarkResult RunSequentialBandwidthTest(
        int sizeInMb,
        int passes)
    {
        ValidateArguments(sizeInMb, passes);

        long bufferBytes = checked((long)sizeInMb * BytesPerMiB);

        int elementCount = checked(
            (int)(bufferBytes / sizeof(long)));

        long totalBytesRead = checked(bufferBytes * passes);

        long expectedChecksum = checked(
            (long)elementCount * passes * FillValue);

        long[] buffer = new long[elementCount];

        // A tömb feltöltése a mért szakaszon kívül történik.
        Array.Fill(buffer, FillValue);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long startTimestamp = Stopwatch.GetTimestamp();

        long checksum = 0;

        for (int pass = 0; pass < passes; pass++)
        {
            for (int index = 0; index < buffer.Length; index++)
            {
                checksum += buffer[index];
            }
        }

        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);

        // A megadott korlátok mellett a checksum long típusba belefér.
        // Az ellenőrzés egyúttal fel is használja a számítás eredményét.
        if (checksum != expectedChecksum)
        {
            throw new InvalidOperationException(
                "A memóriateszt ellenőrzőösszege hibás.");
        }

        if (elapsed.TotalSeconds <= 0)
        {
            throw new InvalidOperationException(
                "A memóriamérés időtartama nem lehet nulla.");
        }

        double throughputGiBPerSecond =
            totalBytesRead / BytesPerGiB / elapsed.TotalSeconds;

        return new BenchmarkResult(
            TestName:
                $"Memory Sequential Bandwidth ({sizeInMb} MiB, {passes} passes)",
            ElapsedMilliseconds: elapsed.TotalMilliseconds,
            OperationsPerSecond: 0,
            ThroughputGbPerSec: throughputGiBPerSecond,
            MeasuredAtUtc: DateTimeOffset.UtcNow);
    }

    private static void ValidateArguments(int sizeInMb, int passes)
    {
        if (sizeInMb < 1 || sizeInMb > MaxBufferSizeMiB)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sizeInMb),
                sizeInMb,
                $"A pufferméret 1 és {MaxBufferSizeMiB} MiB között legyen.");
        }

        if (passes < 1 || passes > MaxPasses)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passes),
                passes,
                $"A bejárások száma 1 és {MaxPasses} között legyen.");
        }
    }
}