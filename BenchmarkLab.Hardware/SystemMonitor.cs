namespace BenchmarkLab.Hardware;

public readonly record struct SystemInfo(
    string CpuModel,
    int LogicalCores,
    double TotalMemoryGb,
    double AvailableMemoryGb
);


public interface SystemMonitor
{
    SystemInfo GetSystemInfo();
    double GetCpuUsagePercentage();
}
