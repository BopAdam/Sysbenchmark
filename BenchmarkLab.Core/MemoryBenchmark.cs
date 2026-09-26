using System.Diagnostics;
using System.Drawing;

namespace BenchmarkLab.Core;

public static class MemoryBenchmark
{
    public static BenchmarkResult RunSequentialBandwidthTest(int sizeInMb, int passes)
    {
        int elementCount = (sizeInMb * 1024 *1024) / sizeof(long);
        long[] buffer = new long[elementCount];
       // Memória előmelegítése (page-faultok elkerülése a mérés alatt)
        Array.Fill(buffer, 42L);

        GC.Collect();
        GC.WaitForPendingFinalizers();

        long startTimestamp = Stopwatch.GetTimestamp();

        long checksum = 0;
        for(int p = 0; p < passes; p++)
        {
            for(int i = 0; i < buffer.Length; i++)
            {
                checksum += buffer[i];
            }
        }
        long endTimestamp = Stopwatch.GetTimestamp();
        TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);

        double totalBytesRead = (double)sizeInMb * 1024 * 1024 * passes;
        double throughputGbSec = (totalBytesRead / (1024.0 * 1024.0 * 1024.0)) / elapsed.TotalSeconds;


        if (checksum == -1) Console.WriteLine(checksum);

        return new BenchmarkResult(
            TestName: $"Memória Szekvenciális Olvasás ({sizeInMb} MB)",
            ElapsedMilliseconds: elapsed.TotalMilliseconds,
            OperationsPerSecond: (double)elementCount * passes / elapsed.TotalSeconds,
            ThroughputGbPerSec: throughputGbSec
        );


    }
}