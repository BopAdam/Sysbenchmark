using MySqlConnector;

namespace BenchmarkLab.IntegrationTests;

internal sealed class TestDatabase : IDisposable
{
    private readonly string _databaseName =
        $"sysbench_test_{Guid.NewGuid():N}";

    private bool _disposed;

    public string ConnectionString { get; }

    public TestDatabase()
    {
        var builder = CreateServerSettings();
        builder.Database = _databaseName;

        ConnectionString = builder.ConnectionString;

        ExecuteOnServer(
            $"CREATE DATABASE `{_databaseName}` " +
            "CHARACTER SET utf8mb4;");
    }

    public void Execute(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var connection = OpenConnection();
        using var command = new MySqlCommand(sql, connection);

        AddParameters(command, parameters);
        command.ExecuteNonQuery();
    }

    public object? Scalar(
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        using var connection = OpenConnection();
        using var command = new MySqlCommand(sql, connection);

        AddParameters(command, parameters);

        object? value = command.ExecuteScalar();

        return value is DBNull ? null : value;
    }

    public long Count(string sql)
    {
        return Convert.ToInt64(Scalar(sql));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Csak a saját, generált nevű tesztadatbázist töröljük.
        ExecuteOnServer(
            $"DROP DATABASE IF EXISTS `{_databaseName}`;");

        _disposed = true;
    }

    private MySqlConnection OpenConnection()
    {
        var connection = new MySqlConnection(ConnectionString);

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

    private static MySqlConnectionStringBuilder CreateServerSettings()
    {
        return new MySqlConnectionStringBuilder
        {
            Server = "127.0.0.1",
            Port = 3308,
            UserID = "root",
            Password = "sysbenchmark-integration-only",
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 15
        };
    }

    private static void ExecuteOnServer(string sql)
    {
        using var connection = new MySqlConnection(
            CreateServerSettings().ConnectionString);

        connection.Open();

        using var command = new MySqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }

    private static void AddParameters(
        MySqlCommand command,
        IEnumerable<(string Name, object? Value)> parameters)
    {
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(
                parameter.Name,
                parameter.Value ?? DBNull.Value);
        }
    }
}