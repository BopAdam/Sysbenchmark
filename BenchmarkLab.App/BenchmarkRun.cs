namespace BenchmarkLab.App;

public sealed record BenchmarkRun(
    Guid Id,
    string RunType,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    string Status,
    string AppVersion
);