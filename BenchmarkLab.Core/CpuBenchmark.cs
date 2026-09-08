using System.Diagnostics;
using System.Numerics;


namespace BenchmarkLab.Core;

public class CpuBenchmark
{
    public static BenchmarkResult RunMultiThreadedTest(int threadCount, int iterationsPerThread)
    {
        if (threadCount <= 0)
            threadCount = Environment.ProcessorCount;


            //GARbage COLLECTION szemétgyüjtő , teszt elött lefut, hogy ne szakitsa meg a cpu-t
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startTimestamp = Stopwatch.GetTimestamp();

            //Párhuzamos szálak létrehozása és a vektoros számítás végrehajtása

            Parallel.For(0, threadCount, new ParallelOptions {MaxDegreeOfParallelism = threadCount},
           _ =>
           {
               ExecutedSimdWorkload(iterationsPerThread);
           });

           long endTimestamp = Stopwatch.GetTimestamp();
           TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);
           double totalOperations = (double)threadCount * iterationsPerThread * Vector<float>.Count;
           double opsPerSec = totalOperations / elapsed.TotalSeconds;
           long score =(long)(opsPerSec / 1_000_000); // Pontszám MOps/sec alapon

           return new BenchmarkResult(
            TestName: $"CPU Multi-Core SIMD ({threadCount} szál)",
            ElapsedMilliseconds: elapsed.TotalMilliseconds,
            OperationsPerSecond: opsPerSec,
            ThroughputGbPerSec: 0,
            Score: score
            
           );
        }

    private static void ExecutedSimdWorkload(int iterations)
    {
        var vecA = new Vector<float>(1.0000f);
        var vecB = new Vector<float>(0.99999f);
        var acc = new Vector<float>(0.5f);

        for (int i = 0; i < iterations; i++)
        {
            //Vektoros szorzas és összeadás egyszerre
            acc = Vector.Multiply(acc, vecA)+ vecB;
        }

        if(float.IsNaN(acc[0]))
          Console.WriteLine(acc[0]);
    }

}