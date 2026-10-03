using BenchmarkLab.Core;

namespace BenchmarkLab.App;

public sealed record DeviceInfo(
    string MachineName,
    string OperatingSystem,
    string CpuModel,
    double TotalMemoryGb
)
{
    public Guid DeviceUid { get; init; }
}

public sealed record BenchmarkExport(
    int SchemaVersion,
    DateTimeOffset ExportedAtUtc,
    DeviceInfo Device,
    IReadOnlyList<BenchmarkResult> Results
);