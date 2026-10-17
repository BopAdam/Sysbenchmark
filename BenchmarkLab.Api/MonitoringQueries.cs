using MySqlConnector;

namespace BenchmarkLab.Api;

public sealed class MonitoringQueries
{
    private readonly MySqlDataSource _dataSource;

    public MonitoringQueries(MySqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public Task<PageDto<DeviceDto>> GetDevicesAsync(
        long afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        return ReadPageAsync(
            """
            SELECT
                Id,
                DeviceUid,
                MachineName,
                OperatingSystem,
                CpuModel,
                TotalMemoryGb
            FROM Devices
            WHERE Id > @after
            ORDER BY Id
            LIMIT @fetch;
            """,
            reader => new DeviceDto(
                Id: reader.GetInt64(0),
                DeviceUid: reader.IsDBNull(1)
                    ? null
                    : reader.GetGuid(1),
                MachineName: reader.GetString(2),
                OperatingSystem: reader.GetString(3),
                CpuModel: reader.GetString(4),
                TotalMemoryGiB: reader.GetDouble(5)),
            limit,
            cancellationToken,
            ("@after", afterId));
    }

    public Task<PageDto<SessionDto>> GetSessionsAsync(
        long? deviceId,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        return ReadPageAsync(
            """
            SELECT
                s.Id,
                s.DeviceId,
                d.MachineName,
                s.StartedAtUtc,
                s.FinishedAtUtc,
                s.SampleIntervalMilliseconds,
                s.Status,
                s.PersistenceStatus,
                s.LocalSampleCount
            FROM MonitoringSessions s
            JOIN Devices d ON d.Id = s.DeviceId
            WHERE (@device IS NULL OR s.DeviceId = @device)
            ORDER BY s.StartedAtUtc DESC, s.Id DESC
            LIMIT @fetch OFFSET @offset;
            """,
            reader => new SessionDto(
                Id: reader.GetGuid(0),
                DeviceId: reader.GetInt64(1),
                MachineName: reader.GetString(2),
                StartedAtUtc: ReadUtc(reader, 3),
                FinishedAtUtc: reader.IsDBNull(4)
                    ? null
                    : ReadUtc(reader, 4),
                SampleIntervalMilliseconds: reader.GetInt32(5),
                Status: reader.GetString(6),
                PersistenceStatus: reader.GetString(7),
                LocalSampleCount: reader.IsDBNull(8)
                    ? null
                    : reader.GetInt64(8)),
            limit,
            cancellationToken,
            ("@device", deviceId),
            ("@offset", offset));
    }

    public async Task<bool> SessionExistsAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        await using var connection = _dataSource.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM MonitoringSessions
                WHERE Id = @id
            );
            """;

        command.Parameters.AddWithValue(
            "@id",
            sessionId.ToString("D"));

        object? result = await command.ExecuteScalarAsync(
            cancellationToken);

        return Convert.ToInt32(result) == 1;
    }

    public Task<PageDto<SampleDto>> GetSamplesAsync(
        Guid sessionId,
        long afterSequence,
        int limit,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        return ReadPageAsync(
            """
            SELECT
                SequenceNumber,
                MeasuredAtUtc,
                CpuUsagePercent,
                TotalMemoryGiB,
                AvailableMemoryGiB,
                UsedMemoryGiB,
                CpuTemperatureC
            FROM MonitoringSamples
            WHERE SessionId = @session
              AND SequenceNumber > @after
              AND (@from IS NULL OR MeasuredAtUtc >= @from)
              AND (@to IS NULL OR MeasuredAtUtc < @to)
            ORDER BY SequenceNumber
            LIMIT @fetch;
            """,
            reader => new SampleDto(
                Sequence: reader.GetInt64(0),
                MeasuredAtUtc: ReadUtc(reader, 1),
                CpuUsagePercent: reader.GetDouble(2),
                TotalMemoryGiB: reader.GetDouble(3),
                AvailableMemoryGiB: reader.GetDouble(4),
                UsedMemoryGiB: reader.GetDouble(5),
                CpuTemperatureC: reader.IsDBNull(6)
                    ? null
                    : reader.GetDouble(6)),
            limit,
            cancellationToken,
            ("@session", sessionId.ToString("D")),
            ("@after", afterSequence),
            ("@from", fromUtc?.UtcDateTime),
            ("@to", toUtc?.UtcDateTime));
    }

    private async Task<PageDto<T>> ReadPageAsync<T>(
        string sql,
        Func<MySqlDataReader, T> map,
        int limit,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = _dataSource.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = sql;

        // Egy plusz találatból látjuk, van-e következő oldal.
        command.Parameters.AddWithValue("@fetch", limit + 1);

        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(
                parameter.Name,
                parameter.Value ?? DBNull.Value);
        }

        var items = new List<T>(limit + 1);

        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }

        bool hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new PageDto<T>(items, hasMore);
    }

    private static DateTimeOffset ReadUtc(
        MySqlDataReader reader,
        int ordinal)
    {
        // A meglévő adatbázis DATETIME mezőibe UTC-t írunk.
        DateTime value = DateTime.SpecifyKind(
            reader.GetDateTime(ordinal),
            DateTimeKind.Utc);

        return new DateTimeOffset(value);
    }
}