using System.Data.Common;
using BenchmarkLab.Core;

namespace BenchmarkLab.App;

public static class BenchmarkRunner
{
    public static BenchmarkRun Run(
        string runType,
        Action<List<BenchmarkResult>> execute,
        List<BenchmarkResult> allResults,
        IBenchmarkStore? store,
        DeviceInfo device)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(allResults);
        ArgumentNullException.ThrowIfNull(device);

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
            // A mérési művelet határán megtartjuk
            // az addig elkészült eredményeket.
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
                    .Version?.ToString() ?? "unknown");

        for (int index = 0; index < currentResults.Count; index++)
        {
            currentResults[index] = currentResults[index] with
            {
                RunId = runId
            };
        }

        allResults.AddRange(currentResults);

        Console.WriteLine(
            $"Futtatás: {runId} | Állapot: {status} | " +
            $"Elkészült mérések: {currentResults.Count}");

        if (store is null)
        {
            Console.WriteLine(
                "[MENTÉS] Nincs adatbázis-kapcsolat. " +
                "Az elkészült mérésekhez használd a 4-es exportot.");

            return run;
        }

        try
        {
            store.SaveRun(device, run, currentResults);

            Console.WriteLine(
                "[MENTÉS] A futtatás és az eredményei adatbázisba mentve.");
        }
        catch (DbException)
        {
            Console.WriteLine(
                "[MENTÉS] Az adatbázis-mentés nem igazolható.");

            Console.WriteLine(
                "Az elkészült mérések a memóriában vannak. " +
                "Kilépés előtt használd a 4-es exportot.");
        }

        return run;
    }
}