using MySqlConnector;

namespace BenchmarkLab.App;

public sealed class MonitoringStore
{
    private readonly string _connectionString;

    public MonitoringStore(string connectionString)
    {
        var settings = new MySqlConnectionStringBuilder(connectionString)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 5
        };

        _connectionString = settings.ConnectionString;

        // A közös Devices tábla és annak DeviceUid mezője
        // üres adatbázison is rendelkezésre álljon.
        _ = new BenchmarkStore(_connectionString);

        using var connection = Open();

        Execute(connection, null, """
            CREATE TABLE IF NOT EXISTS MonitoringSessions (
                Id CHAR(36) NOT NULL PRIMARY KEY,
                DeviceId BIGINT NOT NULL,
                StartedAtUtc DATETIME(6) NOT NULL,
                FinishedAtUtc DATETIME(6) NULL,
                SampleIntervalMilliseconds INT NOT NULL,
                Status VARCHAR(20) NOT NULL,
                PersistenceStatus VARCHAR(20) NOT NULL,
                LocalSampleCount BIGINT NULL,

                CONSTRAINT FK_MonitoringSessions_Devices
                    FOREIGN KEY (DeviceId)
                    REFERENCES Devices(Id),

                INDEX IX_MonitoringSessions_Device_Time
                    (DeviceId, StartedAtUtc)
            ) ENGINE=InnoDB;
            """);

        Execute(connection, null, """
            CREATE TABLE IF NOT EXISTS MonitoringSamples (
                SessionId CHAR(36) NOT NULL,
                SequenceNumber BIGINT NOT NULL,
                MeasuredAtUtc DATETIME(6) NOT NULL,
                CpuUsagePercent DOUBLE NOT NULL,
                TotalMemoryGiB DOUBLE NOT NULL,
                AvailableMemoryGiB DOUBLE NOT NULL,
                UsedMemoryGiB DOUBLE NOT NULL,
                CpuTemperatureC DOUBLE NULL,

                PRIMARY KEY (SessionId, SequenceNumber),

                CONSTRAINT FK_MonitoringSamples_Sessions
                    FOREIGN KEY (SessionId)
                    REFERENCES MonitoringSessions(Id),

                CONSTRAINT CK_MonitoringSamples_Sequence
                    CHECK (SequenceNumber > 0),

                CONSTRAINT CK_MonitoringSamples_Cpu
                    CHECK (
                        CpuUsagePercent >= 0
                        AND CpuUsagePercent <= 100
                    ),

                INDEX IX_MonitoringSamples_Session_Time
                    (SessionId, MeasuredAtUtc)
            ) ENGINE=InnoDB;
            """);
    }

    public void Start(
        DeviceInfo device,
        Guid sessionId,
        DateTimeOffset startedAtUtc,
        int intervalMilliseconds)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (device.DeviceUid == Guid.Empty)
        {
            throw new ArgumentException(
                "Hiányzik az eszközazonosító.",
                nameof(device));
        }

        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Hiányzik a munkamenet azonosítója.",
                nameof(sessionId));
        }

        if (intervalMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMilliseconds));
        }

        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, """
            INSERT INTO Devices (
                DeviceUid,
                MachineName,
                OperatingSystem,
                CpuModel,
                TotalMemoryGb
            )
            VALUES (@uid, @machine, @os, @cpu, @memory)
            ON DUPLICATE KEY UPDATE
                MachineName = @machine,
                OperatingSystem = @os,
                CpuModel = @cpu,
                TotalMemoryGb = @memory;
            """,
            ("@uid", device.DeviceUid.ToString("D")),
            ("@machine", device.MachineName),
            ("@os", device.OperatingSystem),
            ("@cpu", device.CpuModel),
            ("@memory", device.TotalMemoryGb));

        int inserted = Execute(connection, transaction, """
            INSERT INTO MonitoringSessions (
                Id,
                DeviceId,
                StartedAtUtc,
                SampleIntervalMilliseconds,
                Status,
                PersistenceStatus
            )
            SELECT
                @id,
                Id,
                @started,
                @interval,
                'Running',
                'Active'
            FROM Devices
            WHERE DeviceUid = @uid;
            """,
            ("@id", sessionId.ToString("D")),
            ("@started", startedAtUtc.UtcDateTime),
            ("@interval", intervalMilliseconds),
            ("@uid", device.DeviceUid.ToString("D")));

        if (inserted != 1)
        {
            throw new InvalidOperationException(
                "A monitorozási munkamenet nem jött létre.");
        }

        transaction.Commit();
    }

    public void SaveSample(MonitoringSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        using var connection = Open();

        int inserted = Execute(connection, null, """
            INSERT INTO MonitoringSamples (
                SessionId,
                SequenceNumber,
                MeasuredAtUtc,
                CpuUsagePercent,
                TotalMemoryGiB,
                AvailableMemoryGiB,
                UsedMemoryGiB,
                CpuTemperatureC
            )
            SELECT
                @session,
                @sequence,
                @time,
                @cpu,
                @total,
                @available,
                @used,
                @temperature
            FROM MonitoringSessions
            WHERE Id = @session
              AND Status = 'Running';
            """,
            ("@session", sample.SessionId.ToString("D")),
            ("@sequence", sample.Sequence),
            ("@time", sample.MeasuredAtUtc.UtcDateTime),
            ("@cpu", sample.CpuUsagePercent),
            ("@total", sample.TotalMemoryGiB),
            ("@available", sample.AvailableMemoryGiB),
            ("@used", sample.UsedMemoryGiB),
            ("@temperature", sample.CpuTemperatureC));

        if (inserted != 1)
        {
            throw new InvalidOperationException(
                "A munkamenet nem létezik vagy már lezárult.");
        }
    }

    public void Finish(
        Guid sessionId,
        DateTimeOffset finishedAtUtc,
        string status,
        long localSampleCount,
        bool databaseComplete)
    {
        if (status is not ("Stopped" or "Failed"))
        {
            throw new ArgumentException(
                "Érvénytelen lezárási állapot.",
                nameof(status));
        }

        if (localSampleCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(localSampleCount));
        }

        using var connection = Open();

        int updated = Execute(connection, null, """
            UPDATE MonitoringSessions
            SET FinishedAtUtc = @finished,
                Status = @status,
                PersistenceStatus = @persistence,
                LocalSampleCount = @count
            WHERE Id = @session
              AND Status = 'Running';
            """,
            ("@finished", finishedAtUtc.UtcDateTime),
            ("@status", status),
            ("@persistence", databaseComplete ? "Complete" : "Incomplete"),
            ("@count", localSampleCount),
            ("@session", sessionId.ToString("D")));

        if (updated != 1)
        {
            throw new InvalidOperationException(
                "A munkamenet lezárása nem történt meg.");
        }
    }

    private MySqlConnection Open()
    {
        var connection = new MySqlConnection(_connectionString);

        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static int Execute(
        MySqlConnection connection,
        MySqlTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var command = new MySqlCommand(
            sql,
            connection,
            transaction);

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(
                parameter.Name,
                parameter.Value ?? DBNull.Value);
        }

        return command.ExecuteNonQuery();
    }
}