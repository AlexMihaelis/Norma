using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;

namespace Norma.Api.Tests.Weight;

/// <summary>
/// Интеграционные тесты веса: изоляция данных между пользователями, тренд и
/// валидация. Работают с общим контейнером и чистой базой перед каждым тестом.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WeightTests
{
    private readonly PostgresFixture database;

    public WeightTests(PostgresFixture database) => this.database = database;

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

        // Желаемый вес и рост хранятся в профиле: от них дата цели и ИМТ.
        await SetProfileAsync(app, user.Token, new { targetWeightKg = 77.0, heightCm = 175.0 });

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
            app, HttpMethod.Get, "/api/v1/weight/trend", user.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trend = await response.Content.ReadFromJsonAsync<TrendPayload>();

        Assert.NotNull(trend);
        Assert.NotNull(trend.CurrentEmaKg);

        // EMA отстаёт от реального веса: к 28-му дню она около 78.3 при 77.3.
        Assert.InRange(trend.CurrentEmaKg.Value, 78, 79);
        Assert.NotNull(trend.RateKgPerWeek);
        Assert.True(trend.RateKgPerWeek < 0);
        Assert.Equal(77.0, trend.TargetKg);
        Assert.NotNull(trend.GoalDate);
        Assert.Equal(175.0, trend.HeightCm);
        Assert.Null(trend.Sex);
        Assert.Null(trend.Age);

        // Без даты рождения норма считается по взрослым порогам, полоса Unknown.
        Assert.Equal("Unknown", trend.AgeBand);

        // 77.3 кг при 175 см - ИМТ 25.24, чуть выше верхней границы нормы.
        Assert.NotNull(trend.Bmi);
        Assert.Equal(25.24, trend.Bmi.Value, precision: 2);
        Assert.Equal("High", trend.BmiCategory);

        // Те же 18.5 / 25 / 30 ИМТ, переведенные в килограммы для 175 см.
        Assert.NotNull(trend.NormalWeight);
        Assert.Equal(56.65625, trend.NormalWeight.MinKg, precision: 3);
        Assert.Equal(76.5625, trend.NormalWeight.MaxKg, precision: 3);
        Assert.Equal(18.5, trend.NormalWeight.BmiMin);
        Assert.Equal(25, trend.NormalWeight.BmiMax);
        Assert.Equal(91.875, trend.ObeseFromKg!.Value, precision: 3);
        Assert.Equal(28, trend.Points.Count);
    }

    [Fact]
    public async Task Trend_with_short_history_hides_rate()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "short@example.com");

        await SetProfileAsync(app, user.Token, new { targetWeightKg = 77.0 });

        for (var day = 0; day < 5; day++)
        {
            var date = new DateOnly(2026, 10, 1).AddDays(day);

            using var add = await SendAsync(
                app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date, kg = 80 - (0.1 * day) });
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        }

        using var response = await SendAsync(
            app, HttpMethod.Get, "/api/v1/weight/trend", user.Token);

        var trend = await response.Content.ReadFromJsonAsync<TrendPayload>();

        Assert.NotNull(trend);
        Assert.NotNull(trend.CurrentEmaKg);
        Assert.Equal(77.0, trend.TargetKg);
        Assert.Null(trend.HeightCm);
        Assert.Null(trend.Bmi);
        Assert.Null(trend.NormalWeight);
        Assert.Null(trend.ObeseFromKg);
        Assert.Equal("Unknown", trend.AgeBand);
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

        // Без замеров ИМТ не посчитать, но границы зон от роста уже известны.
        await SetProfileAsync(app, user.Token, new { heightCm = 175.0 });

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/weight/trend", user.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var trend = await response.Content.ReadFromJsonAsync<TrendPayload>();

        Assert.NotNull(trend);
        Assert.Null(trend.CurrentEmaKg);
        Assert.Null(trend.TargetKg);
        Assert.Null(trend.GoalDate);
        Assert.Null(trend.Bmi);
        Assert.Null(trend.BmiCategory);
        Assert.Equal(175.0, trend.HeightCm);
        Assert.Equal("Unknown", trend.AgeBand);
        Assert.NotNull(trend.NormalWeight);
        Assert.Equal(56.65625, trend.NormalWeight.MinKg, precision: 3);
        Assert.Equal(76.5625, trend.NormalWeight.MaxKg, precision: 3);
        Assert.Equal(91.875, trend.ObeseFromKg!.Value, precision: 3);
        Assert.Empty(trend.Points);
    }

    [Fact]
    public async Task Trend_without_target_hides_goal_date()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "no-target@example.com");

        for (var day = 0; day < 28; day++)
        {
            var date = new DateOnly(2026, 10, 1).AddDays(day);
            var kg = 80 - (0.1 * day);

            using var add = await SendAsync(
                app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date, kg });
            Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        }

        using var response = await SendAsync(
            app, HttpMethod.Get, "/api/v1/weight/trend", user.Token);

        var trend = await response.Content.ReadFromJsonAsync<TrendPayload>();

        Assert.NotNull(trend);
        Assert.NotNull(trend.RateKgPerWeek);
        Assert.True(trend.RateKgPerWeek < 0);
        Assert.Null(trend.TargetKg);
        Assert.Null(trend.GoalDate);
        Assert.Null(trend.HeightCm);
        Assert.Null(trend.Bmi);
        Assert.Null(trend.NormalWeight);
        Assert.Equal("Unknown", trend.AgeBand);
    }

    [Fact]
    public async Task Normal_weight_band_uses_older_adult_range()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "older@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SetProfileAsync(app, user.Token, new
        {
            sex = "Female",
            birthDate = today.AddYears(-70),
            heightCm = 175.0,
            targetWeightKg = 77.0,
        });

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date = today, kg = 65.0 });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var trend = await GetTrendAsync(app, user.Token);

        Assert.Equal("Female", trend.Sex);
        Assert.Equal(70, trend.Age);
        Assert.Equal("OlderAdult", trend.AgeBand);

        // 65 кг при 175 см - ИМТ 21.22: для 65+ это уже ниже нормы 22-27.
        Assert.NotNull(trend.Bmi);
        Assert.Equal(21.22, trend.Bmi.Value, precision: 2);
        Assert.Equal("Low", trend.BmiCategory);

        // 22 × 3.0625 = 67.375; 27 × 3.0625 = 82.6875.
        Assert.NotNull(trend.NormalWeight);
        Assert.Equal(67.375, trend.NormalWeight.MinKg, precision: 3);
        Assert.Equal(82.6875, trend.NormalWeight.MaxKg, precision: 3);
        Assert.Equal(22, trend.NormalWeight.BmiMin);
        Assert.Equal(27, trend.NormalWeight.BmiMax);
        Assert.Equal(91.875, trend.ObeseFromKg!.Value, precision: 3);
    }

    [Fact]
    public async Task Normal_weight_band_is_the_same_for_both_sexes()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var female = await RegisterAsync(app, "female-norm@example.com");
        var male = await RegisterAsync(app, "male-norm@example.com");

        await SetProfileAsync(app, female.Token, new
        {
            sex = "Female",
            birthDate = today.AddYears(-40),
            heightCm = 175.0,
        });

        await SetProfileAsync(app, male.Token, new
        {
            sex = "Male",
            birthDate = today.AddYears(-40),
            heightCm = 175.0,
        });

        var femaleTrend = await GetTrendAsync(app, female.Token);
        var maleTrend = await GetTrendAsync(app, male.Token);

        Assert.Equal("Adult", femaleTrend.AgeBand);
        Assert.Equal("Adult", maleTrend.AgeBand);
        Assert.NotNull(femaleTrend.NormalWeight);
        Assert.NotNull(maleTrend.NormalWeight);
        Assert.Equal(femaleTrend.NormalWeight.MinKg, maleTrend.NormalWeight.MinKg);
        Assert.Equal(femaleTrend.NormalWeight.MaxKg, maleTrend.NormalWeight.MaxKg);
    }

    [Fact]
    public async Task Minor_gets_bmi_without_category_or_normal_band()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "minor@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SetProfileAsync(app, user.Token, new
        {
            sex = "Male",
            birthDate = today.AddYears(-15),
            heightCm = 160.0,
        });

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date = today, kg = 55.0 });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var trend = await GetTrendAsync(app, user.Token);

        Assert.Equal(15, trend.Age);
        Assert.Equal("Minor", trend.AgeBand);
        Assert.NotNull(trend.Bmi);
        Assert.Equal(21.48, trend.Bmi.Value, precision: 2);
        Assert.Null(trend.BmiCategory);
        Assert.Null(trend.NormalWeight);
        Assert.Null(trend.ObeseFromKg);
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

    [Fact]
    public async Task Update_fixes_typo_in_weight_and_date()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "edit@example.com");

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date = "2026-11-01", kg = 90 });
        var added = await add.Content.ReadFromJsonAsync<WeightItem>();

        using var update = await SendAsync(
            app, HttpMethod.Put, $"/api/v1/weight/{added!.Id}", user.Token, new { date = "2026-11-02", kg = 79.5 });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<WeightItem>();

        Assert.Equal(79.5, updated!.Kg);
        Assert.Equal(new DateOnly(2026, 11, 2), updated.Date);

        using var list = await SendAsync(app, HttpMethod.Get, "/api/v1/weight", user.Token);
        var items = await list.Content.ReadFromJsonAsync<WeightListPayload>();

        Assert.NotNull(items);
        var single = Assert.Single(items.Items);
        Assert.Equal(79.5, single.Kg);
    }

    [Fact]
    public async Task Update_keeps_date_when_not_passed()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "edit-date@example.com");

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date = "2026-11-01", kg = 80 });
        var added = await add.Content.ReadFromJsonAsync<WeightItem>();

        using var update = await SendAsync(
            app, HttpMethod.Put, $"/api/v1/weight/{added!.Id}", user.Token, new { kg = 81 });

        var updated = await update.Content.ReadFromJsonAsync<WeightItem>();

        Assert.Equal(new DateOnly(2026, 11, 1), updated!.Date);
        Assert.Equal(81, updated.Kg);
    }

    [Fact]
    public async Task Update_rejects_other_users_measurement()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "edit-alice@example.com");
        var bob = await RegisterAsync(app, "edit-bob@example.com");

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", alice.Token, new { date = "2026-11-01", kg = 80 });
        var added = await add.Content.ReadFromJsonAsync<WeightItem>();

        using var update = await SendAsync(
            app, HttpMethod.Put, $"/api/v1/weight/{added!.Id}", bob.Token, new { kg = 60 });

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task Update_rejects_invalid_weight()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "edit-invalid@example.com");

        using var add = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", user.Token, new { date = "2026-11-01", kg = 80 });
        var added = await add.Content.ReadFromJsonAsync<WeightItem>();

        using var update = await SendAsync(
            app, HttpMethod.Put, $"/api/v1/weight/{added!.Id}", user.Token, new { kg = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task Update_requires_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.PutAsJsonAsync(
            $"/api/v1/weight/{Guid.NewGuid()}", new { kg = 80 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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

    private static async Task<TrendPayload> GetTrendAsync(ApiApp app, string token)
    {
        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/weight/trend", token);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TrendPayload>();
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

    /// <summary>Чистые таблицы и строка подключения: тест не зависит от предыдущего.</summary>
    private async Task<string> MigrateAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record WeightListPayload(IReadOnlyList<WeightItem> Items);

    private sealed record WeightItem(Guid Id, DateOnly Date, double Kg);

    private sealed record TrendPayload(
        double? CurrentEmaKg,
        double? RateKgPerWeek,
        double? TargetKg,
        DateOnly? GoalDate,
        double? HeightCm,
        string? Sex,
        int? Age,
        string? AgeBand,
        double? Bmi,
        string? BmiCategory,
        double? ObeseFromKg,
        NormalWeightItem? NormalWeight,
        IReadOnlyList<TrendPointItem> Points);

    private sealed record NormalWeightItem(double MinKg, double MaxKg, double BmiMin, double BmiMax);

    private sealed record TrendPointItem(DateOnly Date, double EmaKg);
}
