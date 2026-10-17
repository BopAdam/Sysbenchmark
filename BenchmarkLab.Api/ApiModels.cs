namespace BenchmarkLab.Api;

public sealed record DeviceDto(
    long Id,
    Guid? DeviceUid,
    string MachineName,
    string OperatingSystem,
    string CpuModel,
    double TotalMemoryGiB);

public sealed record SessionDto(
    Guid Id,
    long DeviceId,
    string MachineName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int SampleIntervalMilliseconds,
    string Status,
    string PersistenceStatus,
    long? LocalSampleCount);

public sealed record SampleDto(
    long Sequence,
    DateTimeOffset MeasuredAtUtc,
    double CpuUsagePercent,
    double TotalMemoryGiB,
    double AvailableMemoryGiB,
    double UsedMemoryGiB,
    double? CpuTemperatureC);

public sealed record PageDto<T>(
    IReadOnlyList<T> Items,
    bool HasMore);