using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;

namespace Norma.Api.Tests;

/// <summary>
/// Интеграционные тесты среза <c>GET /api/v1/plan</c>: план считается по
/// сохраненному профилю и последнему весу, а недостающие данные перечисляются
/// в ошибке. Арифметика закреплена юнит-тестами ядра, здесь проверяется транспорт.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PlanEndpointTests
{
    private readonly PostgresFixture database;

    public PlanEndpointTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Plan_uses_saved_profile_and_latest_weight()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "plan@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SetProfileAsync(app, user.Token, new
        {
            sex = "Female",
            birthDate = BirthDateForAge(today, 25),
            heightCm = 175.0,
            activity = "Sedentary",
            goal = "Lose",
            kgPerWeek = 0.25,
        });

        // Два замера: план должен взять более поздний (70 кг), а не первый (80 кг).
        await AddWeightAsync(app, user.Token, 80, today.AddDays(-16));
        await AddWeightAsync(app, user.Token, 70, today.AddDays(-7));

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/plan", user.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<PlanPayload>();

        Assert.NotNull(plan);
        // Женщина, 25 лет, 175 см, 70 кг, сидячая: BMR 1507.75, TDEE 1809.3.
        Assert.Equal(1507.75, plan.Bmr, precision: 2);
        Assert.Equal(1809.3, plan.Tdee, precision: 2);
        Assert.Equal(1530, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.False(plan.BelowBmr);
        Assert.NotNull(plan.Macros);
        Assert.Equal(126, plan.Macros.ProteinG);
        Assert.Equal(43, plan.Macros.FatG);
        Assert.Equal(161, plan.Macros.CarbsG);
    }

    [Fact]
    public async Task Plan_defaults_pace_to_quarter_kg()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "default-pace@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SetProfileAsync(app, user.Token, new
        {
            sex = "Female",
            birthDate = BirthDateForAge(today, 25),
            heightCm = 175.0,
            activity = "Sedentary",
            goal = "Lose",
        });

        await AddWeightAsync(app, user.Token, 70, today.AddDays(-1));

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/plan", user.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<PlanPayload>();

        Assert.NotNull(plan);
        Assert.Equal(1530, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
    }

    [Fact]
    public async Task Plan_requires_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.GetAsync("/api/v1/plan");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Plan_reports_missing_profile_fields()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "empty-profile@example.com");

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/plan", user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var keys = await ReadErrorKeysAsync(response);

        Assert.Contains("sex", keys);
        Assert.Contains("birthDate", keys);
        Assert.Contains("heightCm", keys);
        Assert.Contains("activity", keys);
        Assert.Contains("goal", keys);
        Assert.Contains("weight", keys);
    }

    [Fact]
    public async Task Plan_reports_missing_weight()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "no-weight-plan@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SetProfileAsync(app, user.Token, new
        {
            sex = "Male",
            birthDate = BirthDateForAge(today, 30),
            heightCm = 181.0,
            activity = "Light",
            goal = "Maintain",
        });

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/plan", user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var keys = await ReadErrorKeysAsync(response);

        Assert.Contains("weight", keys);
        Assert.DoesNotContain("sex", keys);
    }

    [Fact]
    public async Task Plan_is_isolated_between_users()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-plan@example.com");
        var bob = await RegisterAsync(app, "bob-plan@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SetProfileAsync(app, alice.Token, new
        {
            sex = "Female",
            birthDate = BirthDateForAge(today, 25),
            heightCm = 170.0,
            activity = "Light",
            goal = "Maintain",
        });
        await AddWeightAsync(app, alice.Token, 65, today.AddDays(-1));

        using var aliceResponse = await SendAsync(app, HttpMethod.Get, "/api/v1/plan", alice.Token);
        using var bobResponse = await SendAsync(app, HttpMethod.Get, "/api/v1/plan", bob.Token);

        Assert.Equal(HttpStatusCode.OK, aliceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bobResponse.StatusCode);
    }

    /// <summary>
    /// Дата рождения, при которой на <paramref name="today"/> человеку ровно
    /// <paramref name="years"/> лет. Сдвиг на день назад спасает 29 февраля.
    /// </summary>
    private static DateOnly BirthDateForAge(DateOnly today, int years) =>
        today.AddDays(-1).AddYears(-years);

    private static async Task<HashSet<string>> ReadErrorKeysAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
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

    private static async Task SetProfileAsync(ApiApp app, string token, object profile)
    {
        using var response = await SendAsync(app, HttpMethod.Put, "/api/v1/me/profile", token, profile);
        response.EnsureSuccessStatusCode();
    }

    private static async Task AddWeightAsync(ApiApp app, string token, double kg, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", token, new { date, kg });
        response.EnsureSuccessStatusCode();
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

    /// <summary>Чистые таблицы и строка подключения: тест не зависит от предыдущего.</summary>
    private async Task<string> MigrateAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record PlanPayload(
        double Bmr,
        double Tdee,
        int TargetKcal,
        bool PaceAchievable,
        bool BelowBmr,
        MacroPayload Macros);

    private sealed record MacroPayload(int ProteinG, int FatG, int CarbsG);
}
