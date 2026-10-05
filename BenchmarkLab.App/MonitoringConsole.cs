using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BenchmarkLab.Hardware;

namespace BenchmarkLab.App;

public static class MonitoringConsole
{
    private static readonly TimeSpan SampleInterval =
        TimeSpan.FromSeconds(1);

    public static void Run(DeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (Console.IsInputRedirected)
        {
            Console.WriteLine(
                "[MONITOR] Interaktív terminálból indítsd a monitorozást.");

            return;
        }

        try
        {
            RunSession(device);
        }
        catch (Exception ex)
        {
            // A konzolos funkció hibahatára:
            // a hiba után visszatérünk a főmenübe.
            Console.WriteLine(
                $"[MONITOR] A monitorozás hibával leállt: " +
                $"{ex.GetType().Name}: {ex.Message}");

            Console.WriteLine(
                "Ha már létrejött naplófájl, a korábban kiírt " +
                "teljes sorok megmaradnak.");
        }
    }

    private static void RunSession(DeviceInfo device)
    {
        Guid sessionId = Guid.NewGuid();

        string directory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "monitoring-data");

        Directory.CreateDirectory(directory);

        string path = Path.Combine(
            directory,
            $"monitoring_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_" +
            $"{sessionId:N}.jsonl");

        using var file = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);

        using var writer = new StreamWriter(
            file,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };

        WriteRecord(writer, new
        {
            Type = "SessionStarted",
            SchemaVersion = 1,
            SessionId = sessionId,
            StartedAtUtc = DateTimeOffset.UtcNow,
            SampleIntervalMilliseconds = SampleInterval.TotalMilliseconds,
            Device = device
        });

        Console.WriteLine();
        Console.WriteLine("[MONITOR] Indítás...");
        Console.WriteLine($"[MONITOR] Napló: {path}");
        Console.WriteLine(
            "[MONITOR] Leállítás: S, Esc vagy Ctrl+C. Enter nem szükséges.");

        long sampleCount = 0;
        int stopRequested = 0;
        string status = "Stopped";
        string? errorType = null;

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Interlocked.Exchange(ref stopRequested, 1);
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            using var temperatureReader = new CpuTemperatureSession();

            SystemMonitor monitor = AppStartup.CreateMonitor();

            // Az első kijelzett CPU-érték már két mintavétel különbsége.
            monitor.GetCpuUsagePercentage();

            var clock = Stopwatch.StartNew();
            TimeSpan nextSampleAt = SampleInterval;

            while (Volatile.Read(ref stopRequested) == 0)
            {
                if (StopKeyPressed())
                    break;

                if (clock.Elapsed < nextSampleAt)
                {
                    Thread.Sleep(100);
                    continue;
                }

                double cpuUsage = monitor.GetCpuUsagePercentage();
                SystemInfo info = monitor.GetSystemInfo();
                double? temperature = temperatureReader.ReadCelsius();

                var sample = new MonitoringSample(
                    SessionId: sessionId,
                    Sequence: checked(sampleCount + 1),
                    MeasuredAtUtc: DateTimeOffset.UtcNow,
                    CpuUsagePercent: cpuUsage,
                    TotalMemoryGiB: info.TotalMemoryGb,
                    AvailableMemoryGiB: info.AvailableMemoryGb,
                    UsedMemoryGiB: Math.Max(
                        0,
                        info.TotalMemoryGb - info.AvailableMemoryGb),
                    CpuTemperatureC: temperature);

                // Előbb mentünk, utána jelezzük a mintát a konzolon.
                WriteRecord(writer, new
                {
                    Type = "Sample",
                    Sample = sample
                });

                sampleCount = sample.Sequence;
                ShowSample(sample);

                // Lassabb szenzorolvasás után sem indítunk
                // egymásra torlódó, párhuzamos mintavételeket.
                nextSampleAt = clock.Elapsed + SampleInterval;
            }
        }
        catch (Exception ex)
        {
            status = "Failed";
            errorType = ex.GetType().Name;
            throw;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;

            try
            {
                WriteRecord(writer, new
                {
                    Type = "SessionFinished",
                    SessionId = sessionId,
                    FinishedAtUtc = DateTimeOffset.UtcNow,
                    Status = status,
                    SampleCount = sampleCount,
                    ErrorType = errorType
                });
            }
            catch (IOException)
            {
                Console.WriteLine(
                    "[MONITOR] A lezáró naplóbejegyzés írása sikertelen.");
            }
        }

        Console.WriteLine(
            $"[MONITOR] Leállítva. Elmentett minták: {sampleCount}");

        Console.WriteLine($"[MONITOR] Fájl: {path}");
    }

    private static bool StopKeyPressed()
    {
        while (Console.KeyAvailable)
        {
            ConsoleKey key = Console.ReadKey(intercept: true).Key;

            if (key is ConsoleKey.S or ConsoleKey.Escape)
                return true;
        }

        return false;
    }

    private static void ShowSample(MonitoringSample sample)
    {
        string temperature = sample.CpuTemperatureC.HasValue
            ? $"{sample.CpuTemperatureC.Value:F1} °C"
            : "nincs adat";

        Console.WriteLine(
            $"{sample.MeasuredAtUtc:HH:mm:ss} UTC | " +
            $"CPU: {sample.CpuUsagePercent,5:F1}% | " +
            $"RAM: {sample.UsedMemoryGiB:F2}/" +
            $"{sample.TotalMemoryGiB:F2} GiB | " +
            $"CPU-hő: {temperature}");
    }

    private static void WriteRecord<T>(StreamWriter writer, T record)
    {
        writer.WriteLine(JsonSerializer.Serialize(record));
    }
}

public sealed record MonitoringSample(
    Guid SessionId,
    long Sequence,
    DateTimeOffset MeasuredAtUtc,
    double CpuUsagePercent,
    double TotalMemoryGiB,
    double AvailableMemoryGiB,
    double UsedMemoryGiB,
    double? CpuTemperatureC);