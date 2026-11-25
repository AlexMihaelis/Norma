using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Norma.Api.Tests.BodyMeasurements;

/// <summary>
/// Обхваты тела: запись, перезапись за тот же день, изменение к прошлому замеру
/// и изоляция между пользователями.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BodyMeasurementTests
{
    private readonly PostgresFixture database;

    public BodyMeasurementTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Measurement_is_saved_and_returned_as_latest()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "body-add@example.com");

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Waist", cm = 74.5, date = "2026-11-20" });

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var latest = await GetLatestAsync(app, user.Token);
        var waist = Assert.Single(latest);

        Assert.Equal("Waist", waist.Kind);
        Assert.Equal(74.5, waist.Cm);
        Assert.Equal(new DateOnly(2026, 11, 20), waist.Date);
        Assert.Null(waist.DeltaCm);
    }

    [Fact]
    public async Task Second_measurement_gives_delta()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "body-delta@example.com");

        await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Waist", cm = 76, date = "2026-11-01" });
        await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Waist", cm = 74, date = "2026-11-10" });

        var latest = await GetLatestAsync(app, user.Token);
        var waist = Assert.Single(latest);

        Assert.Equal(74, waist.Cm);
        Assert.Equal(76, waist.PreviousCm);
        Assert.Equal(-2, waist.DeltaCm);
        Assert.Equal(new DateOnly(2026, 11, 1), waist.PreviousDate);
    }

    [Fact]
    public async Task Same_day_replaces_value_instead_of_adding_row()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "body-upsert@example.com");

        using var first = await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Hips", cm = 98, date = "2026-11-15" });
        using var second = await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Hips", cm = 97, date = "2026-11-15" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var latest = await GetLatestAsync(app, user.Token);
        var hips = Assert.Single(latest);

        Assert.Equal(97, hips.Cm);
        Assert.Null(hips.DeltaCm);
    }

    [Fact]
    public async Task Validation_rejects_bad_kind_cm_and_future_date()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "body-invalid@example.com");

        using var badKind = await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Tail", cm = 70 });
        using var badCm = await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Waist", cm = 5 });
        using var future = await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", user.Token,
            new { kind = "Waist", cm = 70, date = "2999-01-01" });

        Assert.Equal(HttpStatusCode.BadRequest, badKind.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badCm.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }

    [Fact]
    public async Task Measurements_are_isolated_between_users()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "body-alice@example.com");
        var bob = await RegisterAsync(app, "body-bob@example.com");

        await SendAsync(app, HttpMethod.Post, "/api/v1/body-measurements", alice.Token,
            new { kind = "Chest", cm = 92, date = "2026-11-12" });

        var bobLatest = await GetLatestAsync(app, bob.Token);

        Assert.Empty(bobLatest);
    }

    [Fact]
    public async Task Endpoints_require_token()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        using var add = await app.Client.PostAsJsonAsync(
            "/api/v1/body-measurements", new { kind = "Waist", cm = 70 });
        using var latest = await app.Client.GetAsync("/api/v1/body-measurements/latest");

        Assert.Equal(HttpStatusCode.Unauthorized, add.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, latest.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        ApiApp app, HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await app.Client.SendAsync(request);
    }

    private static async Task<IReadOnlyList<LatestPayload>> GetLatestAsync(ApiApp app, string token)
    {
        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/body-measurements/latest", token);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<List<LatestPayload>>();

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<AuthPayload> RegisterAsync(ApiApp app, string email)
    {
        using var response = await app.Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email, password = "correct-horse-battery" });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthPayload>();

        Assert.NotNull(payload);

        return payload;
    }

    private static Task<ApiApp> StartApiAsync(string connectionString) =>
        ApiApp.StartAsync(builder => builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Norma"] = connectionString,
            }));

    private async Task<string> ResetAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record LatestPayload(
        string Kind,
        DateOnly Date,
        double Cm,
        DateOnly? PreviousDate,
        double? PreviousCm,
        double? DeltaCm);
}