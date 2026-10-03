using BenchmarkLab.Hardware;
using MySqlConnector;

namespace BenchmarkLab.App;

public static class AppStartup
{
    public static SystemMonitor CreateMonitor()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsSystemMonitor();

        if (OperatingSystem.IsLinux())
            return new LinuxSystemMonitor();

        throw new PlatformNotSupportedException(
            "A program jelenleg Windowst és Linuxot támogat.");
    }

    public static IBenchmarkStore? CreateStore()
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
                $"[ADATBÁZIS] Az inicializálás sikertelen. " +
                $"Hibakód: {ex.Number}");

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

    public static void RunSensorDiagnostics()
    {
        try
        {
            WindowsSensorDiagnostics.Print();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Szenzorolvasási hiba: {ex.GetType().Name}: {ex.Message}");

            Environment.ExitCode = 1;
        }
    }
}