using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BenchmarkLab.Hardware;
using MySqlConnector;

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
            // hiba után visszatérünk a főmenübe.
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
        DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

        string directory = Path.Combine(
            Directory.GetCurrentDirectory(),
            "monitoring-data");

        Directory.CreateDirectory(directory);

        string path = Path.Combine(
            directory,
            $"monitoring_{startedAtUtc:yyyyMMdd_HHmmss}_" +
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
            StartedAtUtc = startedAtUtc,
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

        MonitoringStore? monitoringStore = null;
        bool databaseSaveFailed = false;

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            Interlocked.Exchange(ref stopRequested, 1);
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            monitoringStore = TryStartDatabase(
                device,
                sessionId,
                startedAtUtc);

            using var temperatureReader = new CpuTemperatureSession();

            SystemMonitor monitor = AppStartup.CreateMonitor();

            // Beállítjuk a CPU-kihasználtság kezdő számlálóit.
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

                // Először mindig a helyi naplóba írunk.
                WriteRecord(writer, new
                {
                    Type = "Sample",
                    Sample = sample
                });

                sampleCount = sample.Sequence;

                // Csak sikeres helyi mentés után írunk az adatbázisba.
                if (monitoringStore is not null && !databaseSaveFailed)
                {
                    try
                    {
                        monitoringStore.SaveSample(sample);
                    }
                    catch (MySqlException ex)
                    {
                        databaseSaveFailed = true;

                        Console.WriteLine(
                            $"[MONITOR DB] A mentés nem igazolható. " +
                            $"Hibakód: {ex.Number}");

                        Console.WriteLine(
                            "[MONITOR DB] Ebben a munkamenetben " +
                            "a további minták csak JSONL-be kerülnek.");
                    }
                }

                ShowSample(sample);

                // Nincs párhuzamos vagy felhalmozódó mintavétel.
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
            DateTimeOffset finishedAtUtc = DateTimeOffset.UtcNow;

            try
            {
                WriteSessionFinished(
                    writer,
                    sessionId,
                    finishedAtUtc,
                    status,
                    sampleCount,
                    errorType);

                FinishDatabase(
                    monitoringStore,
                    sessionId,
                    finishedAtUtc,
                    status,
                    sampleCount,
                    databaseSaveFailed);
            }
            finally
            {
                // Újraindításkor nem marad fent korábbi eseménykezelő.
                Console.CancelKeyPress -= cancelHandler;
            }
        }

        Console.WriteLine(
            $"[MONITOR] Leállítva. JSONL-be mentett minták: {sampleCount}");

        Console.WriteLine($"[MONITOR] Fájl: {path}");
    }

    private static MonitoringStore? TryStartDatabase(
        DeviceInfo device,
        Guid sessionId,
        DateTimeOffset startedAtUtc)
    {
        string? connectionString =
            Environment.GetEnvironmentVariable("SYSBENCHMARK_DB");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine(
                "[MONITOR DB] Nincs adatbázis-beállítás. " +
                "Automatikus mentés JSONL-be.");

            return null;
        }

        try
        {
            var store = new MonitoringStore(connectionString);

            store.Start(
                device,
                sessionId,
                startedAtUtc,
                checked((int)SampleInterval.TotalMilliseconds));

            Console.WriteLine(
                "[MONITOR DB] A munkamenet létrejött. " +
                "Mentés JSONL-be és MariaDB-be.");

            return store;
        }
        catch (MySqlException ex)
        {
            Console.WriteLine(
                $"[MONITOR DB] Az inicializálás nem igazolható. " +
                $"Hibakód: {ex.Number}");
        }
        catch (ArgumentException)
        {
            Console.WriteLine(
                "[MONITOR DB] Hibás adatbázis-beállítás " +
                "vagy munkamenetadat.");
        }

        Console.WriteLine(
            "[MONITOR DB] Ez a munkamenet csak JSONL-be ment.");

        return null;
    }

    private static void WriteSessionFinished(
        StreamWriter writer,
        Guid sessionId,
        DateTimeOffset finishedAtUtc,
        string status,
        long sampleCount,
        string? errorType)
    {
        try
        {
            WriteRecord(writer, new
            {
                Type = "SessionFinished",
                SessionId = sessionId,
                FinishedAtUtc = finishedAtUtc,
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
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine(
                "[MONITOR] Nincs jogosultság a lezáró bejegyzés írásához.");
        }
    }

    private static void FinishDatabase(
        MonitoringStore? store,
        Guid sessionId,
        DateTimeOffset finishedAtUtc,
        string status,
        long sampleCount,
        bool databaseSaveFailed)
    {
        if (store is null)
            return;

        try
        {
            store.Finish(
                sessionId,
                finishedAtUtc,
                status,
                sampleCount,
                databaseComplete: !databaseSaveFailed);

            Console.WriteLine(
                databaseSaveFailed
                    ? "[MONITOR DB] Lezárva, hiányos adatbázisos mentéssel."
                    : "[MONITOR DB] Lezárva, minden minta mentése igazolt.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine(
                $"[MONITOR DB] A lezárás mentése nem igazolható. " +
                $"Hibakód: {ex.Number}");

            Console.WriteLine(
                "[MONITOR DB] Az adatbázisban Running állapot " +
                "maradhat; a helyi napló tartalmazza a lezárást, " +
                "ha annak kiírása sikerült.");
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine(
                "[MONITOR DB] A munkamenet nem található " +
                "vagy már lezárult.");
        }
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