using System.Globalization;
using LibreHardwareMonitor.Hardware;

namespace BenchmarkLab.Hardware;

// A meglévő benchmarkok továbbra is ezt használhatják.
public static class CpuTemperatureReader
{
    public static double? ReadCelsius()
    {
        using var session = new CpuTemperatureSession();
        return session.ReadCelsius();
    }
}

// Monitorozásnál egyetlen példány él az indítástól a leállításig.
public sealed class CpuTemperatureSession : IDisposable
{
    private Computer? _computer;
    private bool _disposed;

    public CpuTemperatureSession()
    {
        if (!OperatingSystem.IsWindows())
            return;

        _computer = new Computer
        {
            IsCpuEnabled = true
        };

        try
        {
            _computer.Open();
        }
        catch
        {
            try
            {
                _computer.Close();
            }
            finally
            {
                _computer = null;
            }

            throw;
        }
    }

    public double? ReadCelsius()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (OperatingSystem.IsWindows())
            return ReadWindows();

        if (OperatingSystem.IsLinux())
            return ReadLinux();

        return null;
    }

    private double? ReadWindows()
    {
        if (_computer is null)
            return null;

        double? highestTemperature = null;

        foreach (IHardware hardware in _computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu)
                continue;

            hardware.Update();

            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType != SensorType.Temperature ||
                    !sensor.Value.HasValue ||
                    !IsCpuTemperatureLabel(sensor.Name))
                {
                    continue;
                }

                double value = sensor.Value.Value;

                if (!double.IsFinite(value))
                    continue;

                highestTemperature = highestTemperature.HasValue
                    ? Math.Max(highestTemperature.Value, value)
                    : value;
            }
        }

        return highestTemperature;
    }

    private static double? ReadLinux()
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
                        double? value = ReadLinuxSensor(inputFile);

                        if (!value.HasValue)
                            continue;

                        highestTemperature = highestTemperature.HasValue
                            ? Math.Max(highestTemperature.Value, value.Value)
                            : value;
                    }
                }
                catch (IOException)
                {
                    // Az eszköz időközben eltűnhetett.
                }
                catch (UnauthorizedAccessException)
                {
                    // Másik olvasható szenzort keresünk.
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

    private static double? ReadLinuxSensor(string inputFile)
    {
        string labelFile = inputFile.Replace("_input", "_label");

        try
        {
            if (!File.Exists(labelFile))
                return null;

            string label = File.ReadAllText(labelFile).Trim();

            if (!IsCpuTemperatureLabel(label))
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

    private static bool IsCpuTemperatureLabel(string name)
    {
        return name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Tdie", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Computer? computer = _computer;
        _computer = null;

        computer?.Close();
    }
}