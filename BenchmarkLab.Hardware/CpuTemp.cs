using LibreHardwareMonitor.Hardware;

namespace BenchmarkLab.Hardware;

public static class CpuTemperatureReader
{
    public static double? ReadCelsius()
    {
        if (OperatingSystem.IsWindows())
            return ReadWindows();

        if (OperatingSystem.IsLinux())
            return ReadLinux();

        return null;
    }

    private static double? ReadWindows()
    {
       var computer = new Computer { IsCpuEnabled = true };
    computer.Open();

    try
    {
        foreach (IHardware hardware in computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu)
                continue;

            hardware.Update();

            foreach (ISensor sensor in hardware.Sensors)
            {
                if (sensor.SensorType == SensorType.Temperature &&
                    sensor.Value.HasValue &&
                    (sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase) ||
                     sensor.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
                     sensor.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)))
                {
                    return sensor.Value.Value;
                }
            }
        }

        return null;
    }
    finally
    {
        computer.Close();
    }
    }

    private static double? ReadLinux()
    {
        const string hwmonRoot = "/sys/class/hwmon";

        if (!Directory.Exists(hwmonRoot))
            return null;

        foreach (string deviceDirectory in Directory.GetDirectories(hwmonRoot, "hwmon*"))
        {
            foreach (string inputFile in Directory.GetFiles(deviceDirectory, "temp*_input"))
            {
                string labelFile = inputFile.Replace("_input", "_label");

                if (!File.Exists(labelFile))
                    continue;

                try
                {
                    string label = File.ReadAllText(labelFile).Trim();

                    if (!label.Contains("Package", StringComparison.OrdinalIgnoreCase) &&
                        !label.Contains("Tctl", StringComparison.OrdinalIgnoreCase) &&
                        !label.Contains("Tdie", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (double.TryParse(
                            File.ReadAllText(inputFile).Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out double millidegrees))
                    {
                        return millidegrees / 1000.0;
                    }
                }
                catch (IOException)
                {
                    // A szenzor időközben eltűnhetett; megnézzük a következőt.
                }
                catch (UnauthorizedAccessException)
                {
                    // Ehhez a szenzorhoz nincs hozzáférés.
                }
            }
        }

        return null;
    }
}