using System.Text.Json;
using MySqlConnector;
using System.Diagnostics.CodeAnalysis;

namespace BenchmarkLab.App;

public sealed record MonitoringImportResult(
    Guid SessionId,
    int AddedSamples,
    int ExistingSamples,
    int TotalSamples);

public static class MonitoringImporter
{
    private const int MaxSamples = 100_000;
    private const long MaxFileBytes = 64L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        RespectRequiredConstructorParameters = true
    };

    public static MonitoringImportResult Import(
        string path,
        string connectionString)
    {
        // Előbb a teljes fájlt ellenőrizzük.
        // Hibás napló miatt nem kezdünk adatbázis-módosításba.
        LogData log = ReadLog(path);

        // A meglévő séma-inicializálást használjuk.
        _ = new MonitoringStore(connectionString);

        using var connection = new MySqlConnection(connectionString);
        connection.Open();

        using var transaction = connection.BeginTransaction();

        MySqlCommand Command(
            string sql,
            params (string Name, object? Value)[] parameters)
        {
            var command = new MySqlCommand(sql, connection, transaction);

            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(
                    parameter.Name,
                    parameter.Value ?? DBNull.Value);
            }

            return command;
        }

        void Execute(
            string sql,
            params (string Name, object? Value)[] parameters)
        {
            using var command = Command(sql, parameters);
            command.ExecuteNonQuery();
        }

        StartRecord start = log.Start;
        EndRecord end = log.End;

        string sessionId = start.SessionId.ToString("D");
        string deviceUid = start.Device.DeviceUid.ToString("D");

        // Régi napló importálásakor a már regisztrált eszköz
        // aktuális nevét és hardveradatait nem írjuk vissza régebbire.
        Execute("""
            INSERT INTO Devices (
                DeviceUid, MachineName, OperatingSystem,
                CpuModel, TotalMemoryGb
            )
            VALUES (@uid, @machine, @os, @cpu, @memory)
            ON DUPLICATE KEY UPDATE Id = Id;
            """,
            ("@uid", deviceUid),
            ("@machine", start.Device.MachineName),
            ("@os", start.Device.OperatingSystem),
            ("@cpu", start.Device.CpuModel),
            ("@memory", start.Device.TotalMemoryGb));

        // Teljesen offline készült munkamenetet is létre tudunk hozni.
        // Meglévő munkamenetnél itt még semmit nem írunk felül.
        Execute("""
            INSERT INTO MonitoringSessions (
                Id, DeviceId, StartedAtUtc,
                SampleIntervalMilliseconds,
                Status, PersistenceStatus
            )
            SELECT
                @id, d.Id, @started, @interval,
                'Running', 'Active'
            FROM Devices d
            WHERE d.DeviceUid = @uid
            ON DUPLICATE KEY UPDATE Id = VALUES(Id);
            """,
            ("@id", sessionId),
            ("@started", DbTime(start.StartedAtUtc)),
            ("@interval", start.SampleIntervalMilliseconds),
            ("@uid", deviceUid));

        // A munkamenet sorát az importálás végéig zároljuk.
        using (var command = Command("""
            SELECT
                d.DeviceUid,
                s.StartedAtUtc,
                s.SampleIntervalMilliseconds,
                s.FinishedAtUtc,
                s.Status,
                s.LocalSampleCount
            FROM MonitoringSessions s
            JOIN Devices d ON d.Id = s.DeviceId
            WHERE s.Id = @id
            FOR UPDATE;
            """,
            ("@id", sessionId)))
        using (var reader = command.ExecuteReader())
        {
            Require(reader.Read(), "A munkamenet nem található.");

            Require(
                reader.GetGuid(0) == start.Device.DeviceUid &&
                reader.GetDateTime(1).Ticks ==
                    DbTime(start.StartedAtUtc).Ticks &&
                reader.GetInt32(2) ==
                    start.SampleIntervalMilliseconds,
                "A munkamenet alapadatai eltérnek a naplótól.");

            if (reader.IsDBNull(3))
            {
                Require(
                    reader.GetString(4) == "Running" &&
                    reader.IsDBNull(5),
                    "A munkamenet lezárási adatai ellentmondásosak.");
            }
            else
            {
                Require(
                    reader.GetDateTime(3).Ticks ==
                        DbTime(end.FinishedAtUtc).Ticks &&
                    reader.GetString(4) == end.Status &&
                    !reader.IsDBNull(5) &&
                    reader.GetInt64(5) == end.SampleCount,
                    "A tárolt lezárás eltér a napló lezárásától.");
            }
        }

        var existingSequences = new HashSet<long>();

        using (var command = Command("""
            SELECT
                SequenceNumber,
                MeasuredAtUtc,
                CpuUsagePercent,
                TotalMemoryGiB,
                AvailableMemoryGiB,
                UsedMemoryGiB,
                CpuTemperatureC
            FROM MonitoringSamples
            WHERE SessionId = @id
            FOR UPDATE;
            """,
            ("@id", sessionId)))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                long sequence = reader.GetInt64(0);

                Require(
                    sequence >= 1 && sequence <= log.Samples.Count,
                    "Az adatbázis a naplóban nem szereplő mintát tartalmaz.");

                MonitoringSample expected =
                    log.Samples[checked((int)sequence - 1)];

                double? savedTemperature = reader.IsDBNull(6)
                    ? null
                    : reader.GetDouble(6);

                Require(
                    reader.GetDateTime(1).Ticks ==
                        DbTime(expected.MeasuredAtUtc).Ticks &&
                    reader.GetDouble(2) == expected.CpuUsagePercent &&
                    reader.GetDouble(3) == expected.TotalMemoryGiB &&
                    reader.GetDouble(4) == expected.AvailableMemoryGiB &&
                    reader.GetDouble(5) == expected.UsedMemoryGiB &&
                    savedTemperature == expected.CpuTemperatureC,
                    $"A(z) {sequence}. minta eltér az adatbázisban.");

                existingSequences.Add(sequence);
            }
        }

        int addedSamples = 0;

        foreach (MonitoringSample sample in log.Samples)
        {
            if (existingSequences.Contains(sample.Sequence))
                continue;

            Execute("""
                INSERT INTO MonitoringSamples (
                    SessionId, SequenceNumber, MeasuredAtUtc,
                    CpuUsagePercent, TotalMemoryGiB,
                    AvailableMemoryGiB, UsedMemoryGiB,
                    CpuTemperatureC
                )
                VALUES (
                    @session, @sequence, @time,
                    @cpu, @total, @available, @used, @temperature
                );
                """,
                ("@session", sessionId),
                ("@sequence", sample.Sequence),
                ("@time", DbTime(sample.MeasuredAtUtc)),
                ("@cpu", sample.CpuUsagePercent),
                ("@total", sample.TotalMemoryGiB),
                ("@available", sample.AvailableMemoryGiB),
                ("@used", sample.UsedMemoryGiB),
                ("@temperature", sample.CpuTemperatureC));

            addedSamples++;
        }

        using (var command = Command("""
            SELECT COUNT(*)
            FROM MonitoringSamples
            WHERE SessionId = @id;
            """,
            ("@id", sessionId)))
        {
            long savedCount = Convert.ToInt64(command.ExecuteScalar());

            Require(
                savedCount == end.SampleCount,
                "A végső adatbázisos mintaszám nem egyezik a naplóval.");
        }

        // Csak az összes minta ellenőrzése és pótlása után
        // állítjuk Complete állapotba.
        Execute("""
            UPDATE MonitoringSessions
            SET FinishedAtUtc = @finished,
                Status = @status,
                PersistenceStatus = 'Complete',
                LocalSampleCount = @count
            WHERE Id = @id;
            """,
            ("@finished", DbTime(end.FinishedAtUtc)),
            ("@status", end.Status),
            ("@count", end.SampleCount),
            ("@id", sessionId));

        transaction.Commit();

        return new MonitoringImportResult(
            start.SessionId,
            addedSamples,
            existingSequences.Count,
            log.Samples.Count);
    }

    private static LogData ReadLog(string path)
    {
        using var file = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        Require(
            file.Length <= MaxFileBytes,
            "Az importálható fájl legfeljebb 64 MiB lehet.");

        using var reader = new StreamReader(file);

        StartRecord? start = null;
        EndRecord? end = null;
        var samples = new List<MonitoringSample>();
        int lineNumber = 0;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            Require(
                !string.IsNullOrWhiteSpace(line),
                $"Üres naplósor: {lineNumber}.");

            Require(
                end is null,
                "A lezáró bejegyzés után további adat szerepel.");

            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;

                string? type = root.GetProperty("Type").GetString();

                switch (type)
                {
                    case "SessionStarted":
                        Require(
                            start is null && lineNumber == 1,
                            "A kezdő bejegyzés csak az első sor lehet.");

                        start = Deserialize<StartRecord>(root);
                        break;

                    case "Sample":
                        Require(start is not null, "Hiányzik a kezdő sor.");
                        Require(
                            samples.Count < MaxSamples,
                            "Egy import legfeljebb 100 000 mintát fogadhat.");

                        var sample = Deserialize<MonitoringSample>(
                            root.GetProperty("Sample"));

                        Require(
                            sample.SessionId == start!.SessionId &&
                            sample.Sequence == samples.Count + 1L,
                            "Eltérő munkamenet, hiányzó vagy ismétlődő sorszám.");

                        ValidateSample(sample);
                        samples.Add(sample);
                        break;

                    case "SessionFinished":
                        Require(start is not null, "Hiányzik a kezdő sor.");
                        end = Deserialize<EndRecord>(root);
                        break;

                    default:
                        throw new InvalidDataException(
                            $"Ismeretlen bejegyzéstípus: {type}");
                }
            }
            catch (Exception ex) when (
                ex is JsonException or
                KeyNotFoundException or
                InvalidOperationException)
            {
                throw new InvalidDataException(
                    $"Hibás JSONL-bejegyzés a(z) {lineNumber}. sorban.",
                    ex);
            }
        }

        Require(
            start is not null && end is not null,
            "Hiányzik a kezdő vagy lezáró sor. " +
            "Csak befejezett napló importálható.");

        Require(
            start!.SchemaVersion == 1,
            "Nem támogatott naplóverzió.");

        Require(
            start.SessionId != Guid.Empty &&
            start.Device is not null &&
            start.Device.DeviceUid != Guid.Empty &&
            start.SampleIntervalMilliseconds > 0,
            "Hiányos munkamenet- vagy eszközadat.");

        Require(
            !string.IsNullOrWhiteSpace(start.Device.MachineName) &&
            !string.IsNullOrWhiteSpace(start.Device.OperatingSystem) &&
            !string.IsNullOrWhiteSpace(start.Device.CpuModel) &&
            double.IsFinite(start.Device.TotalMemoryGb) &&
            start.Device.TotalMemoryGb >= 0,
            "Érvénytelen eszközadat.");

        Require(
            end!.SessionId == start.SessionId &&
            end.SampleCount == samples.Count &&
            end.Status is "Stopped" or "Failed",
            "Hibás lezárási adat vagy eltérő mintaszám.");

        Require(
            end.FinishedAtUtc >= start.StartedAtUtc,
            "A lezárás időpontja korábbi az indulásnál.");

        return new LogData(start, end, samples);
    }

    private static T Deserialize<T>(JsonElement element) where T : class
    {
        return element.Deserialize<T>(JsonOptions)
            ?? throw new InvalidDataException("Hiányzó JSON-objektum.");
    }

    private static void ValidateSample(MonitoringSample sample)
    {
        Require(
            double.IsFinite(sample.CpuUsagePercent) &&
            sample.CpuUsagePercent >= 0 &&
            sample.CpuUsagePercent <= 100,
            "Érvénytelen CPU-kihasználtság.");

        Require(
            double.IsFinite(sample.TotalMemoryGiB) &&
            double.IsFinite(sample.AvailableMemoryGiB) &&
            double.IsFinite(sample.UsedMemoryGiB) &&
            sample.TotalMemoryGiB >= 0 &&
            sample.AvailableMemoryGiB >= 0 &&
            sample.UsedMemoryGiB >= 0 &&
            sample.AvailableMemoryGiB <= sample.TotalMemoryGiB &&
            sample.UsedMemoryGiB <= sample.TotalMemoryGiB,
            "Érvénytelen memóriaadat.");

        Require(
            sample.CpuTemperatureC is null ||
            double.IsFinite(sample.CpuTemperatureC.Value),
            "Érvénytelen hőmérséklet.");
    }

    private static DateTime DbTime(DateTimeOffset value)
    {
        // A MariaDB DATETIME(6) mikroszekundumos pontosságú.
        long ticks = value.UtcDateTime.Ticks;
        return new DateTime(ticks - ticks % 10, DateTimeKind.Utc);
    }

    private static void Require(
    [DoesNotReturnIf(false)] bool condition,
    string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}

    private sealed record StartRecord(
        int SchemaVersion,
        Guid SessionId,
        DateTimeOffset StartedAtUtc,
        int SampleIntervalMilliseconds,
        DeviceInfo Device);

    private sealed record EndRecord(
        Guid SessionId,
        DateTimeOffset FinishedAtUtc,
        string Status,
        long SampleCount);

    private sealed record LogData(
        StartRecord Start,
        EndRecord End,
        List<MonitoringSample> Samples);
}