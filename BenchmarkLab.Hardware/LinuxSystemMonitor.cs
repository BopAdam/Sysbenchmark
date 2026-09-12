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

        if (totalDelta == 0) return 0.0;

        double usage = (1.0 - ((double)idleDelta / totalDelta)) * 100.0;
        return Math.Clamp(usage, 0.0, 100.0);
    }

    private (long idle, long total) ReadCpuTimes()
    {
        if (!File.Exists("/proc/stat")) return (0, 0);

        string? firstLine = File.ReadLines("/proc/stat").FirstOrDefault();
        if (string.IsNullOrEmpty(firstLine) || !firstLine.StartsWith("cpu ")) return (0, 0);

        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // mezők: cpu, user, nice, system, idle, iowait, irq, softirq, steal
        long user = long.Parse(parts[1]);
        long nice = long.Parse(parts[2]);
        long system = long.Parse(parts[3]);
        long idle = long.Parse(parts[4]);
        long iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;

        long totalIdle = idle + iowait;
        long totalNonIdle = user + nice + system;
        long total = totalIdle + totalNonIdle;

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