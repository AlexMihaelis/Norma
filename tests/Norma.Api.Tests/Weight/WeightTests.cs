using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Testcontainers.PostgreSql;

namespace Norma.Api.Tests.Weight;

/// <summary>
/// Интеграционные тесты веса: изоляция данных между пользователями, тренд и
/// валидация. Каждый тест - свой Postgres и чистые данные.
/// </summary>
public sealed class WeightTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task Weight_entries_are_isolated_between_users()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice@example.com");
        var bob = await RegisterAsync(app, "bob@example.com");

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", alice.Token, new { date = "2026-11-01", kg = 80 });

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        using var aliceList = await SendAsync(app, HttpMethod.Get, "/api/v1/weight", alice.Token);
        var aliceItems = await aliceList.Content.ReadFromJsonAsync<WeightListPayload>();

        Assert.NotNull(aliceItems);
        Assert.Single(aliceItems.Items);

        using var bobList = await SendAsync(app, HttpMethod.Get, "/api/v1/weight", bob.Token);
        var bobItems = await bobList.Content.ReadFromJsonAsync<WeightListPayload>();

        Assert.NotNull(bobItems);
        Assert.Empty(bobItems.Items);
    }

    [Fact]
    public async Task Add_weight_requires_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.PostAsJsonAsync("/api/v1/weight", new { kg = 80 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Trend_returns_negative_rate_and_goal_date()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "trend@example.com");

        // Четыре недели истории - минимум, чтобы скорость считалась робастно.
        for (var day = 0; day < 28; day++)
        {
            var date = new DateOnly(2026, 10, 1).AddDays(day);
            var kg = 80 - (0.1 * day);

            using var add = await SendAsync(
                app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date, kg });
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        }

        using var response = await SendAsync(
            app, HttpMethod.Get, "/api/v1/weight/trend?goalKg=77", user.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trend = await response.Content.ReadFromJsonAsync<TrendPayload>();

        Assert.NotNull(trend);
        Assert.NotNull(trend.CurrentEmaKg);

        // EMA отстаёт от реального веса: к 28-му дню она около 78.3 при 77.3.
        Assert.InRange(trend.CurrentEmaKg.Value, 78, 79);
        Assert.NotNull(trend.RateKgPerWeek);
        Assert.True(trend.RateKgPerWeek < 0);
        Assert.NotNull(trend.GoalDate);
        Assert.Equal(28, trend.Points.Count);
    }

    [Fact]
    public async Task Trend_with_short_history_hides_rate()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "short@example.com");

        for (var day = 0; day < 5; day++)
        {
            var date = new DateOnly(2026, 10, 1).AddDays(day);

            using var add = await SendAsync(
                app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date, kg = 80 - (0.1 * day) });
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        }

        using var response = await SendAsync(
            app, HttpMethod.Get, "/api/v1/weight/trend?goalKg=77", user.Token);

        var trend = await response.Content.ReadFromJsonAsync<TrendPayload>();

        Assert.NotNull(trend);
        Assert.NotNull(trend.CurrentEmaKg);
        Assert.Null(trend.RateKgPerWeek);
        Assert.Null(trend.GoalDate);
        Assert.Equal(5, trend.Points.Count);
    }

    [Fact]
    public async Task Trend_without_entries_is_empty()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "empty@example.com");

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/weight/trend", user.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trend = await response.Content.ReadFromJsonAsync<TrendPayload>();

        Assert.NotNull(trend);
        Assert.Null(trend.CurrentEmaKg);
        Assert.Null(trend.GoalDate);
        Assert.Empty(trend.Points);
    }

    [Fact]
    public async Task Add_rejects_invalid_weight()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "invalid@example.com");

        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", user.Token, new { kg = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private sealed record WeightListPayload(IReadOnlyList<WeightItem> Items);

    private sealed record WeightItem(Guid Id, DateOnly Date, double Kg);

    private sealed record TrendPayload(
        double? CurrentEmaKg,
        double? RateKgPerWeek,
        DateOnly? GoalDate,
        IReadOnlyList<TrendPointItem> Points);

    private sealed record TrendPointItem(DateOnly Date, double EmaKg);
}
