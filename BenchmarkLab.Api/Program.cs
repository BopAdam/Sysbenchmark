using BenchmarkLab.Api;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);

// Ez az első változat csak a helyi gépen érhető el.
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5080);
});

string? connectionString =
    Environment.GetEnvironmentVariable("SYSBENCHMARK_DB");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "Hiányzik a SYSBENCHMARK_DB környezeti változó.");

    Environment.ExitCode = 1;
    return;
}

string normalizedConnectionString;

try
{
    var settings = new MySqlConnectionStringBuilder(connectionString)
    {
        ConnectionTimeout = 3,
        DefaultCommandTimeout = 5,
        GuidFormat = MySqlGuidFormat.Char36,
        DateTimeKind = MySqlDateTimeKind.Utc
    };

    normalizedConnectionString = settings.ConnectionString;
}
catch (ArgumentException)
{
    Console.Error.WriteLine(
        "Hibás SYSBENCHMARK_DB kapcsolati karakterlánc.");

    Environment.ExitCode = 1;
    return;
}

builder.Services.AddSingleton<MySqlDataSource>(_ =>
    new MySqlDataSource(normalizedConnectionString));

builder.Services.AddSingleton<MonitoringQueries>();

var app = builder.Build();

// Az SQL-hibák részleteit és a kapcsolat adatait
// nem küldjük vissza a böngészőnek.
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (MySqlException ex) when (!context.Response.HasStarted)
    {
        app.Logger.LogWarning(
            "Adatbázis-művelet sikertelen. Hibakód: {ErrorCode}",
            ex.Number);

        context.Response.Clear();

        IResult response = ex.Number == 1146
            ? Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Hiányzó adatbázistábla.",
                detail:
                    "A monitorozás adatbázissémája még nincs létrehozva.")
            : Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Az adatbázis-művelet sikertelen.",
                detail:
                    "Ellenőrizd az adatbázis elérhetőségét és beállításait.");

        await response.ExecuteAsync(context);
    }
});

app.MapGet("/health", () =>
    Results.Ok(new
    {
        Status = "ok",
        Service = "Sysbenchmark API"
    }));

app.MapGet(
    "/api/devices",
    async Task<IResult> (
        MonitoringQueries queries,
        CancellationToken cancellationToken,
        long afterId = 0,
        int limit = 100) =>
    {
        if (afterId < 0 || limit is < 1 or > 100)
        {
            return Results.BadRequest(new
            {
                Error = "afterId >= 0, limit pedig 1–100 lehet."
            });
        }

        var page = await queries.GetDevicesAsync(
            afterId,
            limit,
            cancellationToken);

        return Results.Ok(page);
    });

app.MapGet(
    "/api/sessions",
    async Task<IResult> (
        MonitoringQueries queries,
        CancellationToken cancellationToken,
        long? deviceId = null,
        int offset = 0,
        int limit = 20) =>
    {
        if (deviceId is <= 0 ||
            offset is < 0 or > 1_000_000 ||
            limit is < 1 or > 100)
        {
            return Results.BadRequest(new
            {
                Error =
                    "deviceId pozitív legyen; offset: 0–1000000; " +
                    "limit: 1–100."
            });
        }

        var page = await queries.GetSessionsAsync(
            deviceId,
            offset,
            limit,
            cancellationToken);

        return Results.Ok(page);
    });

app.MapGet(
    "/api/sessions/{sessionId:guid}/samples",
    async Task<IResult> (
        Guid sessionId,
        MonitoringQueries queries,
        CancellationToken cancellationToken,
        long afterSequence = 0,
        int limit = 1000,
        DateTimeOffset? fromUtc = null,
        DateTimeOffset? toUtc = null) =>
    {
        if (afterSequence < 0 || limit is < 1 or > 1000)
        {
            return Results.BadRequest(new
            {
                Error = "afterSequence >= 0, limit: 1–1000."
            });
        }

        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            fromUtc.Value >= toUtc.Value)
        {
            return Results.BadRequest(new
            {
                Error = "fromUtc korábbi legyen, mint toUtc."
            });
        }

        if (!await queries.SessionExistsAsync(
                sessionId,
                cancellationToken))
        {
            return Results.NotFound(new
            {
                Error = "A munkamenet nem található."
            });
        }

        var page = await queries.GetSamplesAsync(
            sessionId,
            afterSequence,
            limit,
            fromUtc,
            toUtc,
            cancellationToken);

        return Results.Ok(page);
    });

app.Run();