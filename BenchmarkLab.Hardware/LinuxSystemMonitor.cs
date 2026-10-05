namespace BenchmarkLab.Hardware;

public class LinuxSystemMonitor : SystemMonitor
{
    private (long idle, long total) _lastCpuTimes;

    public LinuxSystemMonitor()
    {
        _lastCpuTimes = ReadCpuTimes();
    }

    public SystemInfo GetSystemInfo()
    {
        string cpuModel = "Ismeretlen CPU";
        double totalMemoryGb = 0;
        double availableMemoryGb = 0;

        // 1. CPU Típus beolvasása /proc/cpuinfo-ból
        if (File.Exists("/proc/cpuinfo"))
        {
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(':');
                    if (parts.Length > 1)
                    {
                        cpuModel = parts[1].Trim();
                        break;
                    }
                }
            }
        }

        // 2. Memória adatok beolvasása /proc/meminfo-ból
        if (File.Exists("/proc/meminfo"))
        {
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                    totalMemoryGb = ParseKbToGb(line);
                else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                    availableMemoryGb = ParseKbToGb(line);
            }
        }

        return new SystemInfo(
            CpuModel: cpuModel,
            LogicalCores: Environment.ProcessorCount,
            TotalMemoryGb: totalMemoryGb,
            AvailableMemoryGb: availableMemoryGb
        );
    }

    public double GetCpuUsagePercentage()
    {
        var currentTimes = ReadCpuTimes();
        long idleDelta = currentTimes.idle - _lastCpuTimes.idle;
        long totalDelta = currentTimes.total - _lastCpuTimes.total;
        _lastCpuTimes = currentTimes;

        if (totalDelta <=0 || idleDelta < 0) 
            return 0.0;

        double usage = (1.0 - ((double)idleDelta / totalDelta)) * 100.0;
        return Math.Clamp(usage, 0.0, 100.0);
    }

    private (long idle, long total) ReadCpuTimes()
    {
       string? line = File.ReadLines("/proc/stat").FirstOrDefault();

    if (line is null || !line.StartsWith("cpu "))
    {
        throw new IOException(
            "Nem olvasható a CPU összesített terhelési számlálója.");
    }

    string[] parts = line.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length < 5)
    {
        throw new IOException(
            "Hiányos CPU-adatok érkeztek a /proc/stat fájlból.");
    }

    long ReadCounter(int index)
    {
        if (index >= parts.Length)
            return 0;

        if (!long.TryParse(
                parts[index],
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out long value) ||
            value < 0)
        {
            throw new IOException(
                "Érvénytelen CPU-számláló a /proc/stat fájlban.");
        }

        return value;
    }

    long user = ReadCounter(1);
    long nice = ReadCounter(2);
    long system = ReadCounter(3);
    long idle = ReadCounter(4);
    long ioWait = ReadCounter(5);
    long irq = ReadCounter(6);
    long softIrq = ReadCounter(7);
    long steal = ReadCounter(8);

    long totalIdle = checked(idle + ioWait);

    long total = checked(
        user + nice + system + totalIdle + irq + softIrq + steal);

    return (totalIdle, total);
    }

    private static double ParseKbToGb(string memInfoLine)
    {
        var parts = memInfoLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && double.TryParse(parts[1], out double kb))
        {
            return kb / (1024.0 * 1024.0);
        }
        return 0;
    }
}