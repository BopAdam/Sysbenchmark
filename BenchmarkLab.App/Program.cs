using System.Text.Json;
using BenchmarkLab.Core;
using BenchmarkLab.Hardware;
using System.Collections.Generic;

Console.Clear();
Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("==================================================");
Console.WriteLine("   SYSBENCH-CORE | LINUX TELJESÍTMÉNY ELEMZŐ     ");
Console.WriteLine("==================================================");
Console.ResetColor();

SystemMonitor monitor = new LinuxSystemMonitor();
var sysInfo = monitor.GetSystemInfo();

Console.WriteLine($"Processzor : {sysInfo.CpuModel}");
Console.WriteLine($"Logikai szálak: {sysInfo.LogicalCores} db");
Console.WriteLine($"Rendszermemória: {sysInfo.AvailableMemoryGb:F2} GB szabad / {sysInfo.TotalMemoryGb:F2} GB összesen");
Console.WriteLine("--------------------------------------------------");

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
            RunCpuTest(results);
            break;
        case "2":
            RunMemoryTest(results);
            break;
        case "3":
            RunCpuTest(results);
            RunMemoryTest(results);
            break;
        case "4":
            ExportResults(results);
            break;
        default:
            Console.WriteLine("Érvénytelen választás!");
            break;
    }
}

static void RunCpuTest(List<BenchmarkResult> results)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n[FUTTATÁS] CPU terhelés indítása...");
    Console.ResetColor();

    var res = CpuBenchmark.RunMultiThreadedTest(threadCount: Environment.ProcessorCount, iterationsPerThread: 30_000_000);
    results.Add(res);

    DisplayResult(res);
}

static void RunMemoryTest(List<BenchmarkResult> results)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n[FUTTATÁS] Memória sávszélesség teszt (256 MB buffer)...");
    Console.ResetColor();

    var res = MemoryBenchmark.RunSequentialBandwidthTest(sizeInMb: 256, passes: 10);
    results.Add(res);

    DisplayResult(res);
}

static void DisplayResult(BenchmarkResult res)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"-> {res.TestName}");
    Console.WriteLine($"   Futási idő : {res.ElapsedMilliseconds:F2} ms");
    if (res.ThroughputGbPerSec > 0)
        Console.WriteLine($"   Sávszélesség: {res.ThroughputGbPerSec:F2} GB/s");
    else
        Console.WriteLine($"   Művelet/sec : {res.OperationsPerSecond / 1_000_000:F2} MOps/s");
    Console.WriteLine($"   Pontszám    : {res.Score} pont");
    Console.ResetColor();
}

static void ExportResults(List<BenchmarkResult> results)
{
    if (results.Count == 0)
    {
        Console.WriteLine("Nincs még elmenthető mérési adat!");
        return;
    }

    string fileName = $"benchmark_export_{DateTime.Now:yyyyMMdd_HHmmss}.json";
    string json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(fileName, json);

    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.WriteLine($"[SIKER] Eredmények elmentve: {fileName}");
    Console.ResetColor();
}