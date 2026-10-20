using System.Globalization;
using System.Runtime.CompilerServices;

namespace BenchmarkLab.Hardware;

public static class CpuTemperatureReader
{
    public static double? ReadCelsius()
    {
        using var session = new CpuTemperatureSession();
        return session.ReadCelsius();
    }
}

internal interface ICpuTemperatureBackend : IDisposable
{
    double? ReadCelsius();
}

public sealed class CpuTemperatureSession : IDisposable
{
    private readonly ICpuTemperatureBackend? _backend;
    private bool _disposed;

    public CpuTemperatureSession()
    {
        if (OperatingSystem.IsWindows())
        {
            _backend = CreateWindowsBackend();
        }
        else if (OperatingSystem.IsLinux())
        {
            _backend = new LinuxCpuTemperatureBackend();
        }
    }

    // A Windowsos típus létrehozása külön metódusban marad.
    // Linuxon ezt a metódust nem hívjuk meg.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ICpuTemperatureBackend CreateWindowsBackend()
    {
        return new WindowsCpuTemperatureBackend();
    }

    public double? ReadCelsius()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _backend?.ReadCelsius();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _backend?.Dispose();
    }
}

internal static class CpuTemperatureLabels
{
    public static bool IsCpuTemperature(string name)
    {
        return name.Contains(
                   "Package", StringComparison.OrdinalIgnoreCase)
               || name.Contains(
                   "Tctl", StringComparison.OrdinalIgnoreCase)
               || name.Contains(
                   "Tdie", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class LinuxCpuTemperatureBackend
    : ICpuTemperatureBackend
{
    public double? ReadCelsius()
    {
        const string root = "/sys/class/hwmon";

        try
        {
            if (!Directory.Exists(root))
                return null;

            double? highestTemperature = null;

            foreach (string directory in
                     Directory.GetDirectories(root, "hwmon*"))
            {
                try
                {
                    foreach (string inputFile in
                             Directory.GetFiles(directory, "temp*_input"))
                    {
                        double? value = ReadSensor(inputFile);

                        if (!value.HasValue)
                            continue;

                        highestTemperature = highestTemperature.HasValue
                            ? Math.Max(
                                highestTemperature.Value,
                                value.Value)
                            : value;
                    }
                }
                catch (IOException)
                {
                    // Az eszköz időközben eltűnhetett.
                }
                catch (UnauthorizedAccessException)
                {
                    // Tovább keresünk olvasható szenzort.
                }
            }

            return highestTemperature;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static double? ReadSensor(string inputFile)
    {
        string labelFile = inputFile.Replace("_input", "_label");

        try
        {
            if (!File.Exists(labelFile))
                return null;

            string label = File.ReadAllText(labelFile).Trim();

            if (!CpuTemperatureLabels.IsCpuTemperature(label))
                return null;

            bool parsed = double.TryParse(
                File.ReadAllText(inputFile).Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double millidegrees);

            return parsed && double.IsFinite(millidegrees)
                ? millidegrees / 1000.0
                : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        // A fájlolvasások maguk lezárják a fájlokat.
        // Nem tartunk nyitva erőforrást a minták között.
    }
}