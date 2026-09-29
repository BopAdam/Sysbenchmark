using System.Text.Json;
using BenchmarkLab.Core;
using BenchmarkLab.Hardware;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BenchmarkLab.App;

Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==================================================");
Console.WriteLine("   SYSBENCH-CORE | TELJESÍTMÉNY ELEMZŐ  ");
Console.WriteLine("==================================================");
Console.ResetColor();

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
);
string connectionString =
    Environment.GetEnvironmentVariable("SYSBENCHMARK_DB")
    ?? throw new InvalidOperationException(
        "Hiányzik a SYSBENCHMARK_DB környezeti változó.");

var store = new BenchmarkStore(connectionString);
Console.WriteLine("Adatbázis: MariaDB / sysbenchmark");

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
    if (key == "0") break;

    switch (key)
    {
       case "1":
    RunCpuTest(results, store, device);
    break;

case "2":
    RunMemoryTest(results, store, device);
    break;

case "3":
    RunCpuTest(results, store, device);
    RunMemoryTest(results, store, device);
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
    List<BenchmarkResult> results,
    BenchmarkStore store,
    DeviceInfo device)
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

    store.Save(device, result);
    results.Add(result);

    DisplayResult(result);

    Console.WriteLine(
        $"CPU-hőmérséklet: induláskor " +
        $"{tempBefore?.ToString("F1") ?? "nincs adat"} °C, " +
        $"a teszt után " +
        $"{tempAfter?.ToString("F1") ?? "nincs adat"} °C");
}

static void RunMemoryTest(
    List<BenchmarkResult> results,
    BenchmarkStore store,
    DeviceInfo device)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine(
        "\n[FUTTATÁS] Memória sávszélesség teszt (256 MB buffer)...");
    Console.ResetColor();

    var result = MemoryBenchmark.RunSequentialBandwidthTest(
        sizeInMb: 256,
        passes: 10);

    store.Save(device, result);
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

    string fileName = "benchmark_export.json";

    var export = new BenchmarkExport(
        SchemaVersion: 1,
        ExportedAtUtc: DateTimeOffset.UtcNow,
        Device: device,
        Results: results
    );

    string json = JsonSerializer.Serialize(
        export,
        new JsonSerializerOptions { WriteIndented = true });

    File.WriteAllText(fileName, json);

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine(
        $"[SIKER] Eredmények elmentve: " +
        $"{Path.GetFullPath(fileName)}");
    Console.ResetColor();
}