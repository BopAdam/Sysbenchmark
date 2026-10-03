using BenchmarkLab.Core;
using MySqlConnector;

namespace BenchmarkLab.App;

public static class BenchmarkRunner
{
    public static void Run(
        string runType,
        Action<List<BenchmarkResult>> execute,
        List<BenchmarkResult> allResults,
        BenchmarkStore? store,
        DeviceInfo device)
    {
        Guid runId = Guid.NewGuid();
        DateTimeOffset started = DateTimeOffset.UtcNow;

        var currentResults = new List<BenchmarkResult>();
        string status = "Completed";

        try
        {
            execute(currentResults);
        }
        catch (Exception ex)
        {
            // A menüből indított mérési művelet határán kezeljük a hibát.
            // Az addig elkészült eredményeket továbbra is megtartjuk.
            status = "Failed";

            Console.WriteLine(
                $"[MÉRÉS] A futtatás megszakadt: {ex.GetType().Name}");
        }

        var run = new BenchmarkRun(
            Id: runId,
            RunType: runType,
            StartedAtUtc: started,
            FinishedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            AppVersion:
                typeof(BenchmarkRunner).Assembly.GetName()
                    .Version?.ToString() ?? "unknown"
        );

        for (int index = 0; index < currentResults.Count; index++)
        {
            currentResults[index] = currentResults[index] with
            {
                RunId = runId
            };
        }

        // Az adatbázis elérése előtt megőrizzük a méréseket.
        allResults.AddRange(currentResults);

        Console.WriteLine(
            $"Futtatás: {runId} | Állapot: {status} | " +
            $"Elkészült mérések: {currentResults.Count}");

        if (store is null)
        {
            Console.WriteLine(
                "[MENTÉS] Nincs adatbázis-kapcsolat. " +
                "Az elkészült mérések mentéséhez használd a 4-es exportot.");

            return;
        }

        try
        {
            store.SaveRun(device, run, currentResults);

            Console.WriteLine(
                "[MENTÉS] A futtatás és az eredményei adatbázisba mentve.");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine(
                $"[MENTÉS] Az adatbázis-mentés nem igazolható. " +
                $"Hibakód: {ex.Number}");

            Console.WriteLine(
                "Az elkészült mérések a memóriában vannak. " +
                "Kilépés előtt használd a 4-es exportot.");
        }
    }
}