namespace BenchmarkLab.App;

public static class DeviceIdentity
{
    public static Guid LoadOrCreate()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Sysbenchmark");

        Directory.CreateDirectory(directory);

        string path = Path.Combine(directory, "device-id.txt");

        if (File.Exists(path))
        {
            string text = File.ReadAllText(path).Trim();

            if (Guid.TryParse(text, out Guid existing)
                && existing != Guid.Empty)
            {
                return existing;
            }

            throw new InvalidOperationException(
                $"Érvénytelen eszközazonosító ebben a fájlban: {path}");
        }

        Guid created = Guid.NewGuid();

        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        using var writer = new StreamWriter(stream);
        writer.Write(created.ToString("D"));

        return created;
    }
}