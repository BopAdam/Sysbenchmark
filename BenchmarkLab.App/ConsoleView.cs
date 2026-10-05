using BenchmarkLab.Core;
using BenchmarkLab.Hardware;

namespace BenchmarkLab.App;

public static class ConsoleView
{
    public static void ShowHeader()
    {
        // Átirányított kimenetnél nem próbáljuk törölni a terminált.
        if (!Console.IsOutputRedirected)
            Console.Clear();

        WriteColored(
            """
            ==================================================
               SYSBENCH-CORE | TELJESÍTMÉNY ELEMZŐ
            ==================================================
            """,
            ConsoleColor.Cyan);
    }

    public static void ShowSystemInfo(SystemInfo info)
    {
        Console.WriteLine($"Processzor: {info.CpuModel}");
        Console.WriteLine($"Logikai szálak: {info.LogicalCores} db");

        Console.WriteLine(
            $"Rendszermemória: {info.AvailableMemoryGb:F2} GiB elérhető / " +
            $"{info.TotalMemoryGb:F2} GiB összesen");

        Console.WriteLine(
            "--------------------------------------------------");
    }

    public static void ShowMenu()
    {   Console.WriteLine("0. Kilépés");
        Console.WriteLine();
        Console.WriteLine("Válassz egy menüpontot:");
        Console.WriteLine("1. [Teszt] CPU-teljesítmény (Többszálas SIMD)");
        Console.WriteLine("2. [Teszt] Memória-sávszélesség (Szekvenciális olvasás)");
        Console.WriteLine("3. [Összes] Teljes tesztcsomag futtatása");
        Console.WriteLine("4. [Export] Eredmények mentése JSON-fájlba");
        Console.WriteLine("5. [Monitor] Folyamatos rendszerfigyelés");
        Console.WriteLine("0. Kilépés");
        Console.Write("\nOpció: ");
    }

    public static void ShowResult(BenchmarkResult result)
    {
        string rate = result.ThroughputGbPerSec > 0
            ? $"Sávszélesség: {result.ThroughputGbPerSec:F2} GiB/s"
            : $"Művelet/sec: {result.OperationsPerSecond / 1_000_000:F2} MOps/s";

        WriteColored(
            $"-> {result.TestName}\n" +
            $"   Időpont UTC: {result.MeasuredAtUtc:yyyy-MM-dd HH:mm:ss}\n" +
            $"   Futási idő: {result.ElapsedMilliseconds:F2} ms\n" +
            $"   {rate}",
            ConsoleColor.Green);
    }

    public static void ShowCpuTemperature(BenchmarkResult result)
    {
        Console.WriteLine(
            $"CPU-hőmérséklet: induláskor " +
            $"{FormatTemperature(result.CpuTempBeforeC)}, " +
            $"a teszt után {FormatTemperature(result.CpuTempAfterC)}");
    }

    public static void WriteColored(string message, ConsoleColor color)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(message);
            return;
        }

        ConsoleColor previousColor = Console.ForegroundColor;

        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }
    }

    private static string FormatTemperature(double? temperature) =>
        temperature.HasValue
            ? $"{temperature.Value:F1} °C"
            : "nincs adat";
}