using BenchmarkLab.Core;
using MySqlConnector;

namespace BenchmarkLab.App;

public sealed class BenchmarkStore
{
    private readonly string _connectionString;

    public BenchmarkStore(string connectionString)
    {
        _connectionString = connectionString;

        using var connection = Open();

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

            measurements.ExecuteNonQuery();
        }
    }

    public void Save(DeviceInfo device, BenchmarkResult result)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();

        using (var upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;

            upsert.CommandText = """
                INSERT INTO Devices
                    (MachineName, OperatingSystem,
                     CpuModel, TotalMemoryGb)
                VALUES
                    (@machine, @os, @cpu, @memory)
                ON DUPLICATE KEY UPDATE
                    CpuModel = @cpu,
                    TotalMemoryGb = @memory;
                """;

            upsert.Parameters.AddWithValue(
                "@machine", device.MachineName);
            upsert.Parameters.AddWithValue(
                "@os", device.OperatingSystem);
            upsert.Parameters.AddWithValue(
                "@cpu", device.CpuModel);
            upsert.Parameters.AddWithValue(
                "@memory", device.TotalMemoryGb);

            upsert.ExecuteNonQuery();
        }

        long deviceId;

        using (var findDevice = connection.CreateCommand())
        {
            findDevice.Transaction = transaction;

            findDevice.CommandText = """
                SELECT Id
                FROM Devices
                WHERE MachineName = @machine
                  AND OperatingSystem = @os;
                """;

            findDevice.Parameters.AddWithValue(
                "@machine", device.MachineName);
            findDevice.Parameters.AddWithValue(
                "@os", device.OperatingSystem);

            deviceId = Convert.ToInt64(
                findDevice.ExecuteScalar()
                ?? throw new InvalidOperationException(
                    "A gép nem található az adatbázisban."));
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;

            insert.CommandText = """
                INSERT INTO Measurements
                    (DeviceId, TestName, MeasuredAtUtc,
                     ElapsedMilliseconds, OperationsPerSecond,
                     ThroughputGbPerSec, CpuTempBeforeC,
                     CpuTempAfterC)
                VALUES
                    (@deviceId, @name, @time,
                     @elapsed, @ops, @throughput,
                     @before, @after);
                """;

            insert.Parameters.AddWithValue(
                "@deviceId", deviceId);
            insert.Parameters.AddWithValue(
                "@name", result.TestName);

            // A DATETIME nem tárol időzónát.
            // Ebbe az oszlopba mindig UTC időt írunk.
            insert.Parameters.AddWithValue(
                "@time", result.MeasuredAtUtc.UtcDateTime);

            insert.Parameters.AddWithValue(
                "@elapsed", result.ElapsedMilliseconds);
            insert.Parameters.AddWithValue(
                "@ops", result.OperationsPerSecond);
            insert.Parameters.AddWithValue(
                "@throughput", result.ThroughputGbPerSec);
            insert.Parameters.AddWithValue(
                "@before",
                (object?)result.CpuTempBeforeC ?? DBNull.Value);
            insert.Parameters.AddWithValue(
                "@after",
                (object?)result.CpuTempAfterC ?? DBNull.Value);

            insert.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private MySqlConnection Open()
    {
        var connection = new MySqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}