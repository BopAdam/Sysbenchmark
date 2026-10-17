using MySqlConnector;

namespace BenchmarkLab.App;

public static class MonitoringImportConsole
{
    public static void Run()
    {
        string? connectionString =
            Environment.GetEnvironmentVariable("SYSBENCHMARK_DB");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine(
                "[IMPORT] Hiányzik a SYSBENCHMARK_DB beállítás.");

            return;
        }

        Console.Write(
            "A lezárt JSONL-fájl útvonala (üresen: vissza): ");

        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            return;

        string path = input.Trim().Trim('"');

        try
        {
            MonitoringImportResult result =
                MonitoringImporter.Import(path, connectionString);

            Console.WriteLine($"[IMPORT] Munkamenet: {result.SessionId}");
            Console.WriteLine(
                $"[IMPORT] Újonnan mentett minták: {result.AddedSamples}");
            Console.WriteLine(
                $"[IMPORT] Már meglévő minták: {result.ExistingSamples}");
            Console.WriteLine(
                $"[IMPORT] Összes minta: {result.TotalSamples}");
            Console.WriteLine(
                "[IMPORT] Kész. Adatbázisos mentés: Complete.");
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine($"[IMPORT] Ellenőrzési hiba: {ex.Message}");
        }
        catch (MySqlException ex)
        {
            Console.WriteLine(
                $"[IMPORT] Adatbázishiba. Hibakód: {ex.Number}");

            Console.WriteLine(
                "Az import sikerét nem lehet igazolni. " +
                "A kapcsolat helyreállítása után ugyanaz a fájl " +
                "újra importálható.");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"[IMPORT] Fájlkezelési hiba: {ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine(
                "[IMPORT] Nincs jogosultság a fájl olvasásához.");
        }
        catch (ArgumentException)
        {
            Console.WriteLine(
                "[IMPORT] Hibás útvonal vagy adatbázis-beállítás.");
        }
    }
}