using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Testcontainers.PostgreSql;

namespace Norma.Api.Tests.Activity;

/// <summary>
/// Интеграционные тесты активности: расход по последнему весу, снимок калорий,
/// суммы за день, изоляция и валидация. Арифметика MET закреплена юнит-тестами ядра.
/// </summary>
public sealed class ActivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task Add_activity_uses_latest_weight_and_keeps_kcal_snapshot()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "activity@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await AddWeightAsync(app, user.Token, 80, today.AddDays(-1));

        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/activity", user.Token,
            new { kind = "Walking", minutes = 30, date = today });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var activity = await response.Content.ReadFromJsonAsync<ActivityPayload>();

        Assert.NotNull(activity);
        Assert.Equal("Walking", activity.Kind);
        Assert.Equal(30, activity.Minutes);

        // 4.3 MET × 3.5 / 200 × 80 кг × 30 мин = 180.6 → 181.
        Assert.Equal(181, activity.Kcal);

        // Новый вес не переписывает уже сохраненный расход.
        await AddWeightAsync(app, user.Token, 90, today);

        var day = await GetDayAsync(app, user.Token, today);

        Assert.Single(day.Items);
        Assert.Equal(181, day.Items[0].Kcal);
        Assert.Equal(181, day.TotalKcal);
    }

    [Fact]
    public async Task Day_sums_activity_of_one_user_and_one_day_only()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-activity@example.com");
        var bob = await RegisterAsync(app, "bob-activity@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await AddWeightAsync(app, alice.Token, 80, today);
        await AddWeightAsync(app, bob.Token, 80, today);

        await AddActivityAsync(app, alice.Token, "Walking", 30, today);
        await AddActivityAsync(app, alice.Token, "Yoga", 20, today);
        await AddActivityAsync(app, bob.Token, "Running", 60, today);

        var aliceDay = await GetDayAsync(app, alice.Token, today);
        var bobDay = await GetDayAsync(app, bob.Token, today);
        var emptyDay = await GetDayAsync(app, alice.Token, today.AddDays(-1));

        // 181 + 70 (2.5 MET × 20 мин при 80 кг).
        Assert.Equal(2, aliceDay.Items.Count);
        Assert.Equal(251, aliceDay.TotalKcal);

        Assert.Single(bobDay.Items);
        Assert.Equal(697, bobDay.TotalKcal);

        Assert.Empty(emptyDay.Items);
        Assert.Equal(0, emptyDay.TotalKcal);
    }

    [Fact]
    public async Task Add_activity_requires_weight()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "no-weight-activity@example.com");

        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/activity", user.Token,
            new { kind = "Walking", minutes = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var keys = await ReadErrorKeysAsync(response);

        Assert.Contains("weight", keys);
    }

    [Fact]
    public async Task Add_activity_rejects_invalid_input()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "invalid-activity@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var unknownKind = await SendAsync(
            app, HttpMethod.Post, "/api/v1/activity", user.Token,
            new { kind = "Flying", minutes = 30 });

        using var zeroMinutes = await SendAsync(
            app, HttpMethod.Post, "/api/v1/activity", user.Token,
            new { kind = "Walking", minutes = 0 });

        using var future = await SendAsync(
            app, HttpMethod.Post, "/api/v1/activity", user.Token,
            new { kind = "Walking", minutes = 30, date = today.AddDays(2) });

        Assert.Equal(HttpStatusCode.BadRequest, unknownKind.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, zeroMinutes.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }

    [Fact]
    public async Task Endpoints_require_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var get = await app.Client.GetAsync("/api/v1/activity");
        using var post = await app.Client.PostAsJsonAsync(
            "/api/v1/activity", new { kind = "Walking", minutes = 30 });

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
    }

    private static async Task<HashSet<string>> ReadErrorKeysAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<ActivityPayload> AddActivityAsync(
        ApiApp app, string token, string kind, int minutes, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/activity", token, new { kind, minutes, date });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ActivityPayload>();

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<ActivityDayPayload> GetDayAsync(ApiApp app, string token, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/activity?date={date:yyyy-MM-dd}", token);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ActivityDayPayload>();

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task AddWeightAsync(ApiApp app, string token, double kg, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", token, new { date, kg });
        response.EnsureSuccessStatusCode();
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

    private async Task<string> MigrateAsync()
    {
        var connectionString = database.GetConnectionString();
        var options = new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(connectionString).Options;

        await using var db = new NormaDbContext(options);
        await db.Database.MigrateAsync();

        return connectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record ActivityPayload(Guid Id, DateOnly Date, string Kind, int Minutes, int Kcal);

    private sealed record ActivityDayPayload(
        DateOnly Date,
        IReadOnlyList<ActivityPayload> Items,
        int TotalKcal);
}
