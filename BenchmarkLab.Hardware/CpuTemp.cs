using System.Globalization;

namespace BenchmarkLab.Hardware;

public static class CpuTemperatureReader
{
    public static double? ReadCelsius()
    {
        foreach (string zone in Directory.GetDirectories(
                     "/sys/class/thermal", "thermal_zone*"))
        {
            string typePath = Path.Combine(zone, "type");
            string tempPath = Path.Combine(zone, "temp");

            if (!File.Exists(typePath) || !File.Exists(tempPath))
                continue;

            string type = File.ReadAllText(typePath).Trim();

            // Ezek gyakori CPU-szenzornevek, de gépenként eltérhetnek.
            if (type != "x86_pkg_temp" && type != "cpu-thermal")
                continue;

            string raw = File.ReadAllText(tempPath).Trim();
            if (double.TryParse(raw, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double millidegrees))
                return millidegrees / 1000.0;
        }

        return null; // Ezen az útvonalon nem találtunk CPU-szenzort.
    }
}