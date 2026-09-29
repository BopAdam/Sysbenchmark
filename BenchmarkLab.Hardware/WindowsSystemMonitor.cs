using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace BenchmarkLab.Hardware;

[SupportedOSPlatform("windows")]
public sealed class WindowsSystemMonitor : SystemMonitor
{
    private (ulong idle, ulong total) _lastCpuTimes;

    public WindowsSystemMonitor()
    {
        _lastCpuTimes = ReadCpuTimes();
    }

    public SystemInfo GetSystemInfo()
    {
        string cpuModel = (
            Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString",
                null
            ) as string
        )?.Trim() ?? "Ismeretlen CPU";

        var memory = new MemoryStatus
        {
            Length = (uint)Marshal.SizeOf<MemoryStatus>()
        };

        if (!GlobalMemoryStatusEx(ref memory))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        const double bytesPerGiB = 1024.0 * 1024.0 * 1024.0;

        return new SystemInfo(
            CpuModel: cpuModel,
            LogicalCores: Environment.ProcessorCount,
            TotalMemoryGb: memory.TotalPhysical / bytesPerGiB,
            AvailableMemoryGb: memory.AvailablePhysical / bytesPerGiB
        );
    }

    public double GetCpuUsagePercentage()
    {
        var current = ReadCpuTimes();

        if (current.total <= _lastCpuTimes.total ||
            current.idle < _lastCpuTimes.idle)
        {
            _lastCpuTimes = current;
            return 0;
        }

        ulong idleDelta = current.idle - _lastCpuTimes.idle;
        ulong totalDelta = current.total - _lastCpuTimes.total;

        _lastCpuTimes = current;

        double usage = (1.0 - (double)idleDelta / totalDelta) * 100.0;

        return Math.Clamp(usage, 0.0, 100.0);
    }

    private static (ulong idle, ulong total) ReadCpuTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        // Windows alatt a kernelidő az üresjárati időt is tartalmazza.
        return (
            idle.ToTicks(),
            kernel.ToTicks() + user.ToTicks()
        );
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToTicks()
            => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(
        ref MemoryStatus memory
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out NativeFileTime idle,
        out NativeFileTime kernel,
        out NativeFileTime user
    );
}