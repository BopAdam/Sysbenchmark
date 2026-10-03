using BenchmarkLab.Core;
using BenchmarkLab.Hardware;

namespace BenchmarkLab.App;

public sealed class ConsoleApplication
{
    private const int CpuIterationsPerThread = 30_000_000;
    private const int MemoryBufferSizeMiB = 256;
    private const int MemoryPasses = 10;

    private readonly DeviceInfo _device;
    private readonly IBenchmarkStore? _store;
    private readonly List<BenchmarkResult> _results = new();

    public ConsoleApplication(
        DeviceInfo device,
        IBenchmarkStore? store)
    {
        ArgumentNullException.ThrowIfNull(device);

        _device = device;
        _store = store;
    }

    public void Run()
    {
        while (true)
        {
            ConsoleView.ShowMenu();

            string? choice = Console.ReadLine();

            if (choice is null or "0")
                return;

            switch (choice)
            {
                case "1":
                    RunBenchmark("Cpu", RunCpuTest);
                    break;

                case "2":
                    RunBenchmark("Memory", RunMemoryTest);
                    break;

                case "3":
                    RunBenchmark("FullSuite", RunFullSuite);
                    break;

                case "4":
                    ExportResults();
                    break;

                default:
                    Console.WriteLine("Érvénytelen választás!");
                    break;
            }
        }
    }

    private void RunBenchmark(
        string runType,
        Action<List<BenchmarkResult>> execute)
    {
        BenchmarkRunner.Run(
            runType,
            execute,
            _results,
            _store,
            _device);
    }

    private static void RunFullSuite(List<BenchmarkResult> results)
    {
        RunCpuTest(results);
        RunMemoryTest(results);
    }

    private static void RunCpuTest(List<BenchmarkResult> results)
    {
        ConsoleView.WriteColored(
            "\n[FUTTATÁS] CPU-terhelés indítása...",
            ConsoleColor.Yellow);

        double? temperatureBefore = CpuTemperatureReader.ReadCelsius();

        BenchmarkResult result = CpuBenchmark.RunMultiThreadedTest(
            threadCount: Environment.ProcessorCount,
            iterationsPerThread: CpuIterationsPerThread);

        double? temperatureAfter = CpuTemperatureReader.ReadCelsius();

        result = result with
        {
            CpuTempBeforeC = temperatureBefore,
            CpuTempAfterC = temperatureAfter
        };

        results.Add(result);

        ConsoleView.ShowResult(result);
        ConsoleView.ShowCpuTemperature(result);
    }

    private static void RunMemoryTest(List<BenchmarkResult> results)
    {
        ConsoleView.WriteColored(
            $"\n[FUTTATÁS] Memóriateszt ({MemoryBufferSizeMiB} MiB puffer)...",
            ConsoleColor.Yellow);

        BenchmarkResult result =
            MemoryBenchmark.RunSequentialBandwidthTest(
                sizeInMb: MemoryBufferSizeMiB,
                passes: MemoryPasses);

        results.Add(result);
        ConsoleView.ShowResult(result);
    }

    private void ExportResults()
    {
        if (_results.Count == 0)
        {
            Console.WriteLine(
                "Nincs még elmenthető mérési adat ebben a futásban!");

            return;
        }

        try
        {
            string path = JsonExporter.Save(_device, _results);

            ConsoleView.WriteColored(
                $"[SIKER] Eredmények elmentve: {path}",
                ConsoleColor.Magenta);
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine(
                "[EXPORT] Nincs jogosultság a fájl írásához. " +
                "Az eredmények továbbra is a memóriában vannak.");
        }
        catch (IOException)
        {
            Console.WriteLine(
                "[EXPORT] Fájlírási hiba történt. " +
                "Ellenőrizd a szabad helyet és a fájl elérhetőségét. " +
                "Az eredmények továbbra is a memóriában vannak.");
        }
    }
}