using BenchmarkLab.App;
using Xunit;

namespace BenchmarkLab.Tests;

public sealed class DeviceIdentityTests
{
    [Fact]
    public void MissingIdentity_IsCreatedAndReused()
    {
        using var directory = new TemporaryDirectory();

        Guid first = DeviceIdentity.LoadOrCreate(
            directory.DirectoryPath);

        Guid second = DeviceIdentity.LoadOrCreate(
            directory.DirectoryPath);

        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, second);

        string path = Path.Combine(
            directory.DirectoryPath,
            "device-id.txt");

        Assert.Equal(
            first,
            Guid.Parse(File.ReadAllText(path).Trim()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void InvalidIdentity_ThrowsWithoutOverwriting(string content)
    {
        using var directory = new TemporaryDirectory();

        string path = Path.Combine(
            directory.DirectoryPath,
            "device-id.txt");

        File.WriteAllText(path, content);

        Assert.Throws<InvalidOperationException>(
            () => DeviceIdentity.LoadOrCreate(
                directory.DirectoryPath));

        Assert.Equal(content, File.ReadAllText(path));
    }

    [Fact]
    public void DifferentDirectories_ReceiveDifferentIdentities()
    {
        using var firstDirectory = new TemporaryDirectory();
        using var secondDirectory = new TemporaryDirectory();

        Guid first = DeviceIdentity.LoadOrCreate(
            firstDirectory.DirectoryPath);

        Guid second = DeviceIdentity.LoadOrCreate(
            secondDirectory.DirectoryPath);

        Assert.NotEqual(first, second);
    }
}