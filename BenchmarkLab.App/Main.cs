using BenchmarkLab.Core;
using BenchmarkLab.Hardware;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BenchmarkLab.App;
using MySqlConnector;

Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==================================================");
Console.WriteLine("   SYSBENCH-CORE | TELJESÍTMÉNY ELEMZŐ  ");
Console.WriteLine("==================================================");
Console.ResetColor();
if (args.Contains("--sensors"))
{
    try
    {
        WindowsSensorDiagnostics.Print();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(
            $"Szenzorolvasási hiba: {ex.GetType().Name}: {ex.Message}"
        );

        Environment.ExitCode = 1;
    }

    return;
}
SystemMonitor monitor;

if (OperatingSystem.IsWindows())
{
    monitor = new WindowsSystemMonitor();
}
else if (OperatingSystem.IsLinux())
{
    monitor = new LinuxSystemMonitor();
}
else
{
    throw new PlatformNotSupportedException(
        "A program jelenleg Windowst és Linuxot támogat."
    );
}
var sysInfo = monitor.GetSystemInfo();

Console.WriteLine($"Processzor : {sysInfo.CpuModel}");
Console.WriteLine($"Logikai szálak: {sysInfo.LogicalCores} db");
Console.WriteLine($"Rendszermemória: {sysInfo.AvailableMemoryGb:F2} GB szabad / {sysInfo.TotalMemoryGb:F2} GB összesen");
Console.WriteLine("--------------------------------------------------");

if (args.Contains("--system-info"))
{
    Console.WriteLine("Rendszeradatok ellenőrzése kész.");
    return;
}

var device = new DeviceInfo(
    MachineName: Environment.MachineName,
    OperatingSystem:
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Windows"
            : "Linux",
    CpuModel: sysInfo.CpuModel,
    TotalMemoryGb: sysInfo.TotalMemoryGb
)
{
    DeviceUid = DeviceIdentity.LoadOrCreate()
};

Console.WriteLine($"Eszközazonosító: {device.DeviceUid}");
    
BenchmarkStore? store = CreateStore();
Console.WriteLine(
    $"Gép: {device.MachineName} ({device.OperatingSystem})");


var results = new List<BenchmarkResult>();

while (true)
{
    Console.WriteLine("\nVálassz egy menüpontot:");
    Console.WriteLine("1. [Teszt] CPU Teljesítmény (Többszálas SIMD)");
    Console.WriteLine("2. [Teszt] Memória Sávszélesség (Szekvenciális olvasás)");
    Console.WriteLine("3. [Összes] Teljes Tesztcsomag Futtatása");
    Console.WriteLine("4. [Export] Eredmények mentése JSON fájlba");
    Console.WriteLine("0. Kilépés");
    Console.Write("\nOpció: ");

    var key = Console.ReadLine();
    if (key is null or "0") break;
    switch (key)
    {
       case "1":
        BenchmarkRunner.Run(
            "Cpu",
            RunCpuTest,
            results,
            store,
            device);
        break;

    case "2":
        BenchmarkRunner.Run(
            "Memory",
            RunMemoryTest,
            results,
            store,
            device);
        break;

    case "3":
        BenchmarkRunner.Run(
            "FullSuite",
            currentResults =>
            {
                RunCpuTest(currentResults);
                RunMemoryTest(currentResults);
            },
            results,
            store,
            device);
        break;

    case "4":
        ExportResults(results, device);
        break;

    default:
        Console.WriteLine("Érvénytelen választás!");
        break;
    }
}

static void RunCpuTest(
    List<BenchmarkResult> results)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n[FUTTATÁS] CPU terhelés indítása...");
    Console.ResetColor();

    double? tempBefore = CpuTemperatureReader.ReadCelsius();

    var result = CpuBenchmark.RunMultiThreadedTest(
        threadCount: Environment.ProcessorCount,
        iterationsPerThread: 30_000_000);

    double? tempAfter = CpuTemperatureReader.ReadCelsius();

    result = result with
    {
        CpuTempBeforeC = tempBefore,
        CpuTempAfterC = tempAfter
    };

    results.Add(result);

    DisplayResult(result);

    Console.WriteLine(
        $"CPU-hőmérséklet: induláskor " +
        $"{tempBefore?.ToString("F1") ?? "nincs adat"} °C, " +
        $"a teszt után " +
        $"{tempAfter?.ToString("F1") ?? "nincs adat"} °C");
}

static void RunMemoryTest(List<BenchmarkResult> results)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(
        "\n[FUTTATÁS] Memória sávszélesség teszt (256 MB buffer)...");
    Console.ResetColor();

    var result = MemoryBenchmark.RunSequentialBandwidthTest(
        sizeInMb: 256,
        passes: 10);

    results.Add(result);

    DisplayResult(result);
}

static void DisplayResult(BenchmarkResult result)
{
    Console.ForegroundColor = ConsoleColor.Green;

    Console.WriteLine($"-> {result.TestName}");
    Console.WriteLine(
        $"   Időpont UTC: " +
        $"{result.MeasuredAtUtc:yyyy-MM-dd HH:mm:ss}");

    Console.WriteLine(
        $"   Futási idő : " +
        $"{result.ElapsedMilliseconds:F2} ms");

    if (result.ThroughputGbPerSec > 0)
    {
        Console.WriteLine(
            $"   Sávszélesség: " +
            $"{result.ThroughputGbPerSec:F2} GiB/s");
    }
    else
    {
        Console.WriteLine(
            $"   Művelet/sec : " +
            $"{result.OperationsPerSecond / 1_000_000:F2} MOps/s");
    }

    Console.ResetColor();
}

static void ExportResults(
    List<BenchmarkResult> results,
    DeviceInfo device)
{
    if (results.Count == 0)
    {
        Console.WriteLine(
            "Nincs még elmenthető mérési adat ebben a futásban!");

        return;
    }

    try
    {
        string path = JsonExporter.Save(device, results);

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(
            $"[SIKER] Eredmények elmentve: {path}");
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
    finally
    {
        Console.ResetColor();
    }
}



static BenchmarkStore? CreateStore()
{
    string? connectionString =
        Environment.GetEnvironmentVariable("SYSBENCHMARK_DB");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Console.WriteLine(
            "[ADATBÁZIS] Nincs SYSBENCHMARK_DB beállítás.");

        Console.WriteLine(
            "A mérések futtathatók. Mentéshez használd a JSON-exportot.");

        return null;
    }

    try
    {
        var store = new BenchmarkStore(connectionString);

        Console.WriteLine(
            "[ADATBÁZIS] MariaDB-kapcsolat létrejött.");

        return store;
    }
    catch (MySqlException ex)
    {
        Console.WriteLine(
            $"[ADATBÁZIS] Az inicializálás sikertelen. Hibakód: {ex.Number}");

        Console.WriteLine(
            "Ellenőrizd a szervert, a hálózatot és a belépési adatokat.");
    }
    catch (ArgumentException)
    {
        Console.WriteLine(
            "[ADATBÁZIS] Hibás kapcsolati karakterlánc.");
    }

    Console.WriteLine(
        "Ebben a futásban JSON-exporttal tudod menteni a méréseket.");

    return null;
}