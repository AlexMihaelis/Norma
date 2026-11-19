using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data.Entities;
using Norma.Core.Age;
using Norma.Core.Energy;
using Testcontainers.PostgreSql;

namespace Norma.Api.Tests.Stats;

/// <summary>
/// Интеграционные тесты статистики: средние только по дням с записями, цель на
/// каждую дату по весу того дня, активность, изоляция и валидация периода.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class StatsTests
{
    private static readonly DateOnly Day = new(2026, 11, 18);
    private static readonly DateOnly PreviousDay = Day.AddDays(-1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresFixture database;

    public StatsTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Averages_use_only_days_with_entries_and_include_activity()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "stats-average@example.com");
        var foodId = await CreateFoodAsync(app, user.Token, "Куриная грудка, отварная", 100, 20, 5, 0);

        await AddWeightAsync(app, user.Token, 80, PreviousDay);
        await AddDiaryAsync(app, user.Token, "Breakfast", foodId, 100, PreviousDay);
        await AddDiaryAsync(app, user.Token, "Breakfast", foodId, 200, Day);
        await AddActivityAsync(app, user.Token, "Walking", 30, Day);

        var stats = await GetStatsAsync(app, user.Token, from: PreviousDay, to: Day);

        // Два дня с едой: 100 и 200 ккал -> среднее 150.
        Assert.Equal(2, stats.Days.Count);
        Assert.Equal(PreviousDay, stats.Days[0].Date);
        Assert.Equal(Day, stats.Days[1].Date);
        Assert.Equal(2, stats.Averages.DaysWithEntries);
        Assert.Equal(150, stats.Averages.Kcal, precision: 3);
        Assert.Equal(30, stats.Averages.Protein, precision: 3);

        // Ходьба 30 минут при 80 кг: 4.3 x 3.5 x 80 / 200 x 30 = 180.6 -> 181.
        Assert.Equal(181, stats.Days[1].ActivityKcal);
        Assert.Equal(181, stats.Averages.ActivityKcal);
    }

    [Fact]
    public async Task Target_is_calculated_for_each_day_from_weight_of_that_day()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "stats-target@example.com");

        await SetProfileAsync(app, user.Token);
        await AddWeightAsync(app, user.Token, 80, PreviousDay);
        await AddWeightAsync(app, user.Token, 70, Day);

        var foodId = await CreateFoodAsync(app, user.Token, "Рис отварной", 116, 2.2, 0.5, 25);
        await AddDiaryAsync(app, user.Token, "Lunch", foodId, 500, PreviousDay);
        await AddDiaryAsync(app, user.Token, "Lunch", foodId, 500, Day);

        var stats = await GetStatsAsync(app, user.Token, from: PreviousDay, to: Day);

        // Цель считается движком плана: на день с весом 80 кг она выше, чем с 70 кг.
        var expectedPrevious = ExpectedTarget(weightKg: 80, onDate: PreviousDay);
        var expectedDay = ExpectedTarget(weightKg: 70, onDate: Day);
        Assert.Equal(expectedPrevious, stats.Days[0].TargetKcal);
        Assert.Equal(expectedDay, stats.Days[1].TargetKcal);
        Assert.True(stats.Days[0].TargetKcal > stats.Days[1].TargetKcal);
        Assert.Equal(stats.Days[0].Kcal - stats.Days[0].TargetKcal, stats.Days[0].DiffKcal);

        // Отклонение - от средней цели, а не от цели последнего дня.
        Assert.NotNull(stats.Deviation);
        Assert.Equal(
            (expectedPrevious + expectedDay) / 2.0,
            (double)stats.Deviation!.TargetKcal,
            precision: 0);
        Assert.Null(stats.TargetHint);
        Assert.Contains("снимку профиля", stats.TargetNote);
    }

    [Fact]
    public async Task Target_uses_profile_snapshot_effective_on_the_day()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "stats-snapshots@example.com");

        // Два дня истории; на каждый день в базе свой снимок профиля.
        var firstDay = new DateOnly(2026, 11, 10);
        var secondDay = new DateOnly(2026, 11, 11);

        await SetProfileAsync(app, user.Token);
        await SeedSnapshotAsync(user.UserId, firstDay, Goal.Maintain, kgPerWeek: 0);
        await SeedSnapshotAsync(user.UserId, secondDay, Goal.Lose, kgPerWeek: 0.5);

        var foodId = await CreateFoodAsync(app, user.Token, "Суп овощной", 60, 3, 2, 6);
        await AddWeightAsync(app, user.Token, 80, firstDay);
        await AddDiaryAsync(app, user.Token, "Lunch", foodId, 500, firstDay);
        await AddDiaryAsync(app, user.Token, "Lunch", foodId, 500, secondDay);

        var stats = await GetStatsAsync(app, user.Token, firstDay, secondDay);

        // Первый день считается по старому снимку (поддержание), второй - по новому
        // (похудение с темпом 0.5): смена цели не переписывает прошлое.
        Assert.Equal(ExpectedTarget(80, firstDay, Goal.Maintain, 0), stats.Days[0].TargetKcal);
        Assert.Equal(ExpectedTarget(80, secondDay, Goal.Lose, 0.5), stats.Days[1].TargetKcal);
        Assert.True(stats.Days[1].TargetKcal < stats.Days[0].TargetKcal);
    }

    [Fact]
    public async Task Missing_profile_hides_target_and_explains_why()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "stats-noprofile@example.com");
        var foodId = await CreateFoodAsync(app, user.Token, "Овсянка", 88, 3, 1.7, 15);

        await AddDiaryAsync(app, user.Token, "Breakfast", foodId, 100, Day);

        var stats = await GetStatsAsync(app, user.Token, from: Day, to: Day);

        Assert.Null(Assert.Single(stats.Days).TargetKcal);
        Assert.Null(stats.Averages.TargetKcal);
        Assert.Null(stats.Deviation);
        Assert.NotNull(stats.TargetHint);
        Assert.Contains("профиль", stats.TargetHint!);
    }

    [Fact]
    public async Task Empty_period_returns_zero_averages_and_default_window()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "stats-empty@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/stats", user.Token);
        response.EnsureSuccessStatusCode();

        var stats = await response.Content.ReadFromJsonAsync<StatsPayload>(JsonOptions);

        Assert.NotNull(stats);
        Assert.Empty(stats.Days);
        Assert.Equal(0, stats.Averages.DaysWithEntries);
        Assert.Equal(today.AddDays(-6), stats.From);
        Assert.Equal(today, stats.To);
        Assert.Null(stats.TargetHint);
    }

    [Fact]
    public async Task Entries_of_other_users_are_not_counted()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-stats@example.com");
        var bob = await RegisterAsync(app, "bob-stats@example.com");

        var foodId = await CreateFoodAsync(app, alice.Token, "Плов", 180, 6, 6, 25);
        await AddDiaryAsync(app, alice.Token, "Lunch", foodId, 300, Day);

        var bobStats = await GetStatsAsync(app, bob.Token, from: Day, to: Day);

        Assert.Empty(bobStats.Days);
        Assert.Equal(0, bobStats.Averages.Kcal, precision: 6);
    }

    [Fact]
    public async Task Period_validation_rejects_inverted_future_and_too_long()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "stats-invalid@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var inverted = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/stats?from={Day:yyyy-MM-dd}&to={PreviousDay:yyyy-MM-dd}", user.Token);

        using var future = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/stats?to={today.AddDays(3):yyyy-MM-dd}", user.Token);

        using var tooLong = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/stats?from={today.AddDays(-400):yyyy-MM-dd}&to={today:yyyy-MM-dd}", user.Token);

        Assert.Equal(HttpStatusCode.BadRequest, inverted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Stats_requires_token()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.GetAsync("/api/v1/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Цель, которую должен посчитать срез для указанного веса, даты и профиля.</summary>
    private static int ExpectedTarget(
        double weightKg,
        DateOnly onDate,
        Goal goal = Goal.Maintain,
        double kgPerWeek = 0)
    {
        var profile = ProfileBuilder.From(
            Sex.Male,
            new DateOnly(1990, 1, 1),
            onDate,
            heightCm: 180,
            weightKg,
            ActivityLevel.Sedentary);

        return EnergyMath.Plan(profile, goal, kgPerWeek).TargetKcal;
    }

    /// <summary>Снимок профиля на прошлую дату: API пишет снимки только на сегодня.</summary>
    private async Task SeedSnapshotAsync(Guid userId, DateOnly effectiveFrom, Goal goal, double kgPerWeek)
    {
        await using var db = database.CreateContext();

        db.UserProfileSnapshots.Add(new UserProfileSnapshot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EffectiveFrom = effectiveFrom,
            // Npgsql для timestamptz принимает только смещение 0 (UTC), поэтому
            // не локальную дату, а явный UTC.
            CreatedAt = new DateTimeOffset(
                effectiveFrom.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            Sex = Sex.Male,
            BirthDate = new DateOnly(1990, 1, 1),
            HeightCm = 180,
            Activity = ActivityLevel.Sedentary,
            Goal = goal,
            KgPerWeek = kgPerWeek,
        });

        await db.SaveChangesAsync();
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

    private static async Task SetProfileAsync(ApiApp app, string token)
    {
        using var response = await SendAsync(app, HttpMethod.Put, "/api/v1/me/profile", token, new
        {
            sex = "Male",
            birthDate = "1990-01-01",
            heightCm = 180,
            activity = "Sedentary",
            goal = "Maintain",
            kgPerWeek = 0,
        });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateFoodAsync(
        ApiApp app, string token, string name, double kcal, double protein, double fat, double carbs)
    {
        using var response = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", token, new
        {
            name,
            kcalPer100 = kcal,
            proteinPer100 = protein,
            fatPer100 = fat,
            carbsPer100 = carbs,
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoodPayload>(JsonOptions);

        Assert.NotNull(payload);

        return payload.Id;
    }

    private static async Task AddWeightAsync(ApiApp app, string token, double kg, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/weight", token, new { kg, date });

        response.EnsureSuccessStatusCode();
    }

    private static async Task AddDiaryAsync(
        ApiApp app, string token, string meal, Guid foodId, double quantity, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", token, new { meal, foodId, quantity, date });

        response.EnsureSuccessStatusCode();
    }

    private static async Task AddActivityAsync(
        ApiApp app, string token, string kind, int minutes, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/activity", token, new { kind, minutes, date });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<StatsPayload> GetStatsAsync(
        ApiApp app, string token, DateOnly from, DateOnly to)
    {
        using var response = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/stats?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", token);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<StatsPayload>(JsonOptions);

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
    private async Task<string> ResetAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record FoodPayload(Guid Id, string Name);

    private sealed record StatsDayPayload(
        DateOnly Date,
        int Kcal,
        double Protein,
        double Fat,
        double Carbs,
        int Entries,
        int ActivityKcal,
        int? TargetKcal,
        int? DiffKcal);

    private sealed record StatsAveragesPayload(
        int DaysWithEntries,
        double Kcal,
        double Protein,
        double Fat,
        double Carbs,
        int ActivityKcal,
        int? TargetKcal);

    private sealed record StatsDeviationPayload(int TargetKcal, int DiffKcal, double DiffPercent);

    private sealed record StatsPayload(
        DateOnly From,
        DateOnly To,
        IReadOnlyList<StatsDayPayload> Days,
        StatsAveragesPayload Averages,
        StatsDeviationPayload? Deviation,
        string? TargetHint,
        string TargetNote);
}
