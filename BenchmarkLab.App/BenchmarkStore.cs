using BenchmarkLab.Core;
using MySqlConnector;

namespace BenchmarkLab.App;

public sealed class BenchmarkStore : IBenchmarkStore
{
    private readonly string _connectionString;

    public BenchmarkStore(string connectionString)
    {
        _connectionString = connectionString;

        using var connection = Open();



        using (var createMeasurements = connection.CreateCommand())
{
    createMeasurements.CommandText = """
        CREATE TABLE IF NOT EXISTS Measurements (
            Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
            DeviceId BIGINT NOT NULL,
            TestName VARCHAR(255) NOT NULL,
            MeasuredAtUtc DATETIME(6) NOT NULL,
            ElapsedMilliseconds DOUBLE NOT NULL,
            OperationsPerSecond DOUBLE NOT NULL,
            ThroughputGbPerSec DOUBLE NOT NULL,
            CpuTempBeforeC DOUBLE NULL,
            CpuTempAfterC DOUBLE NULL,

            CONSTRAINT FK_Measurements_Devices
                FOREIGN KEY (DeviceId)
                REFERENCES Devices(Id),

            INDEX IX_Measurements_Device_Time
                (DeviceId, MeasuredAtUtc)
        ) ENGINE=InnoDB;
        """;

    createMeasurements.ExecuteNonQuery();
}

        using (var devices = connection.CreateCommand())
        {
            devices.CommandText = """
                CREATE TABLE IF NOT EXISTS Devices (
                    Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    MachineName VARCHAR(255) NOT NULL,
                    OperatingSystem VARCHAR(40) NOT NULL,
                    CpuModel VARCHAR(255) NOT NULL,
                    TotalMemoryGb DOUBLE NOT NULL,
                    UNIQUE KEY UQ_Devices_Machine_OS
                        (MachineName, OperatingSystem)
                ) ENGINE=InnoDB;
                """;

            devices.ExecuteNonQuery();
        }

        using (var measurements = connection.CreateCommand())
        {
           measurements.CommandText = """
                ALTER TABLE Devices
                    ADD COLUMN IF NOT EXISTS DeviceUid CHAR(36) NULL;

                ALTER TABLE Devices
                    ADD UNIQUE INDEX IF NOT EXISTS UQ_Devices_DeviceUid (DeviceUid);

                ALTER TABLE Devices
                    DROP INDEX IF EXISTS UQ_Devices_Machine_OS;

                CREATE TABLE IF NOT EXISTS BenchmarkRuns (
                    Id CHAR(36) NOT NULL PRIMARY KEY,
                    DeviceId BIGINT NOT NULL,
                    RunType VARCHAR(30) NOT NULL,
                    StartedAtUtc DATETIME(6) NOT NULL,
                    FinishedAtUtc DATETIME(6) NOT NULL,
                    Status VARCHAR(20) NOT NULL,
                    AppVersion VARCHAR(50) NOT NULL,

                CONSTRAINT FK_BenchmarkRuns_Devices
                    FOREIGN KEY (DeviceId) REFERENCES Devices(Id),

                INDEX IX_BenchmarkRuns_Device_Time
                    (DeviceId, StartedAtUtc)
                ) ENGINE=InnoDB;

                ALTER TABLE Measurements
                    ADD COLUMN IF NOT EXISTS RunId CHAR(36) NULL;

                ALTER TABLE Measurements
                    ADD INDEX IF NOT EXISTS IX_Measurements_RunId (RunId);

                ALTER TABLE Measurements
                    ADD FOREIGN KEY IF NOT EXISTS FK_Measurements_BenchmarkRuns
                    (RunId) REFERENCES BenchmarkRuns(Id);
                """;
            measurements.ExecuteNonQuery();
        }
    }

    public void SaveRun(
    DeviceInfo device,
    BenchmarkRun run,
    IReadOnlyList<BenchmarkResult> results)
{
    if (device.DeviceUid == Guid.Empty)
        throw new ArgumentException("Hiányzik az eszközazonosító.");

    if (results.Any(result => result.RunId != run.Id))
        throw new ArgumentException(
            "A mérés futtatásazonosítója nem egyezik.");

    using var connection = Open();
    using var transaction = connection.BeginTransaction();

    // Az eszközt most már a DeviceUid alapján azonosítjuk.
    using (var command = CreateCommand("""
        INSERT INTO Devices
            (DeviceUid, MachineName, OperatingSystem,
             CpuModel, TotalMemoryGb)
        VALUES
            (@uid, @machine, @os, @cpu, @memory)
        ON DUPLICATE KEY UPDATE
            MachineName = @machine,
            OperatingSystem = @os,
            CpuModel = @cpu,
            TotalMemoryGb = @memory;
        """))
    {
        command.Parameters.AddWithValue(
            "@uid", device.DeviceUid.ToString("D"));
        command.Parameters.AddWithValue("@machine", device.MachineName);
        command.Parameters.AddWithValue("@os", device.OperatingSystem);
        command.Parameters.AddWithValue("@cpu", device.CpuModel);
        command.Parameters.AddWithValue("@memory", device.TotalMemoryGb);

        command.ExecuteNonQuery();
    }

    long deviceId;

    using (var command = CreateCommand("""
        SELECT Id FROM Devices WHERE DeviceUid = @uid;
        """))
    {
        command.Parameters.AddWithValue(
            "@uid", device.DeviceUid.ToString("D"));

        deviceId = Convert.ToInt64(
            command.ExecuteScalar()
            ?? throw new InvalidOperationException(
                "Az eszköz nem található."));
    }

    // A közös futtatás mentése.
    using (var command = CreateCommand("""
        INSERT INTO BenchmarkRuns
            (Id, DeviceId, RunType, StartedAtUtc,
             FinishedAtUtc, Status, AppVersion)
        VALUES
            (@id, @device, @type, @started,
             @finished, @status, @version);
        """))
    {
        command.Parameters.AddWithValue("@id", run.Id.ToString("D"));
        command.Parameters.AddWithValue("@device", deviceId);
        command.Parameters.AddWithValue("@type", run.RunType);
        command.Parameters.AddWithValue(
            "@started", run.StartedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue(
            "@finished", run.FinishedAtUtc.UtcDateTime);
        command.Parameters.AddWithValue("@status", run.Status);
        command.Parameters.AddWithValue("@version", run.AppVersion);

        command.ExecuteNonQuery();
    }

    // A futtatáshoz tartozó összes elkészült mérés mentése.
    foreach (BenchmarkResult result in results)
    {
        using var command = CreateCommand("""
            INSERT INTO Measurements
                (DeviceId, RunId, TestName, MeasuredAtUtc,
                 ElapsedMilliseconds, OperationsPerSecond,
                 ThroughputGbPerSec, CpuTempBeforeC, CpuTempAfterC)
            VALUES
                (@device, @run, @name, @time,
                 @elapsed, @ops, @throughput, @before, @after);
            """);

        command.Parameters.AddWithValue("@device", deviceId);
        command.Parameters.AddWithValue("@run", run.Id.ToString("D"));
        command.Parameters.AddWithValue("@name", result.TestName);
        command.Parameters.AddWithValue(
            "@time", result.MeasuredAtUtc.UtcDateTime);
        command.Parameters.AddWithValue(
            "@elapsed", result.ElapsedMilliseconds);
        command.Parameters.AddWithValue(
            "@ops", result.OperationsPerSecond);
        command.Parameters.AddWithValue(
            "@throughput", result.ThroughputGbPerSec);
        command.Parameters.AddWithValue(
            "@before", (object?)result.CpuTempBeforeC ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "@after", (object?)result.CpuTempAfterC ?? DBNull.Value);

        command.ExecuteNonQuery();
    }

    transaction.Commit();

    MySqlCommand CreateCommand(string sql)
    {
        return new MySqlCommand(sql, connection, transaction);
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

    }