using System.Text.Json;
using BenchmarkLab.App;
using BenchmarkLab.Core;
using Xunit;

namespace BenchmarkLab.Tests;

public sealed class JsonExporterTests
{
    [Fact]
    public void Export_CanBeReadBackWithoutLosingData()
    {
        using var directory = new TemporaryDirectory();

        string path = Path.Combine(
            directory.DirectoryPath,
            "nested",
            "export.json");

        var device = TestData.Device();

        var measurement = TestData.Measurement() with
        {
            RunId = Guid.NewGuid(),
            CpuTempBeforeC = null,
            CpuTempAfterC = 61.5
        };

        string savedPath = JsonExporter.Save(
            device,
            new[] { measurement },
            path);

        var export = ReadExport(savedPath);

        Assert.Equal(Path.GetFullPath(path), savedPath);
        Assert.Equal(2, export.SchemaVersion);
        Assert.Equal(device, export.Device);
        Assert.Equal(measurement, Assert.Single(export.Results));
        Assert.Equal(TimeSpan.Zero, export.ExportedAtUtc.Offset);
    }

    [Fact]
    public void RepeatedExport_ReplacesPreviousResults()
    {
        using var directory = new TemporaryDirectory();

        string path = Path.Combine(
            directory.DirectoryPath,
            "export.json");

        var device = TestData.Device();

        JsonExporter.Save(
            device,
            new[]
            {
                TestData.Measurement("Old CPU"),
                TestData.Measurement("Old Memory")
            },
            path);

        var latest = TestData.Measurement("Latest");

        JsonExporter.Save(device, new[] { latest }, path);

        Assert.Equal(
            latest,
            Assert.Single(ReadExport(path).Results));

        Assert.Empty(
            Directory.GetFiles(directory.DirectoryPath, "*.tmp"));
    }

    [Fact]
    public void SerializationFailure_PreservesPreviousExport()
    {
        using var directory = new TemporaryDirectory();

        string path = Path.Combine(
            directory.DirectoryPath,
            "export.json");

        var device = TestData.Device();

        JsonExporter.Save(
            device,
            new[] { TestData.Measurement() },
            path);

        string previousContent = File.ReadAllText(path);

        var invalid = TestData.Measurement() with
        {
            OperationsPerSecond = double.NaN
        };

        Assert.Throws<ArgumentException>(
            () => JsonExporter.Save(device, new[] { invalid }, path));

        Assert.Equal(previousContent, File.ReadAllText(path));
    }

    [Fact]
    public void DestinationIsDirectory_ThrowsAndRemovesTemporaryFile()
    {
        using var directory = new TemporaryDirectory();

        string destination = Path.Combine(
            directory.DirectoryPath,
            "export.json");

        // Szándékosan mappát hozunk létre a célfájl helyén.
        Directory.CreateDirectory(destination);

        Exception? error = Record.Exception(
            () => JsonExporter.Save(
                TestData.Device(),
                new[] { TestData.Measurement() },
                destination));

        // Az operációs rendszer eltérő fájlhibát adhat.
        Assert.True(
            error is IOException or UnauthorizedAccessException);

        Assert.True(Directory.Exists(destination));

        Assert.Empty(
            Directory.GetFiles(directory.DirectoryPath, "*.tmp"));
    }

    private static BenchmarkExport ReadExport(string path)
    {
        return JsonSerializer.Deserialize<BenchmarkExport>(
            File.ReadAllText(path))
            ?? throw new InvalidOperationException(
                "Az export nem olvasható vissza.");
    }
}