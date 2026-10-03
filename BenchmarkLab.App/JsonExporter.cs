using System.Text.Json;
using BenchmarkLab.Core;

namespace BenchmarkLab.App;

public static class JsonExporter
{
    public static string Save(
        DeviceInfo device,
        IReadOnlyList<BenchmarkResult> results,
        string fileName = "benchmark_export.json")
    {
        string fullPath = Path.GetFullPath(fileName);

        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "Az export könyvtára nem határozható meg.");

        Directory.CreateDirectory(directory);

        var export = new BenchmarkExport(
            SchemaVersion: 2,
            ExportedAtUtc: DateTimeOffset.UtcNow,
            Device: device,
            Results: results
        );

        string json = JsonSerializer.Serialize(
            export,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        // Először ideiglenes fájlba írunk.
        // A korábbi exportot csak a teljes írás után cseréljük le.
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, json);

            File.Move(
                temporaryPath,
                fullPath,
                overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // A takarítás hibája ne fedje el az eredeti hibát.
            }
            catch (UnauthorizedAccessException)
            {
                // A takarítás hibája ne fedje el az eredeti hibát.
            }
        }

        return fullPath;
    }
}