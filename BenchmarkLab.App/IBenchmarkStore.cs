using BenchmarkLab.Core;

namespace BenchmarkLab.App;

public interface IBenchmarkStore
{
    void SaveRun(
        DeviceInfo device,
        BenchmarkRun run,
        IReadOnlyList<BenchmarkResult> results);
}