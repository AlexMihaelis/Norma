using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;

namespace Norma.Api.Tests.Water;
/// <summary>
/// Интеграционные тесты воды: норма от профиля и веса, сумма за день, изоляция
/// между пользователями и подсказки, когда данных для нормы не хватает.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class WaterTests
{
    private static readonly DateOnly Day = new(2026, 11, 17);

    private readonly PostgresFixture database;

    public WaterTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Day_reports_norm_remaining_and_disclaimer()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "water@example.com");

        await SetProfileAsync(app, user.Token, new { sex = "Male", activity = "Moderate" });
        await AddWeightAsync(app, user.Token, 80, Day.AddDays(-16));
        await AddWaterAsync(app, user.Token, 500, Day);

        var day = await GetDayAsync(app, user.Token, Day);

        // 35 × 80 = 2800, плюс 500 за умеренную активность.
        Assert.Equal(2800, day.BaseMl);
        Assert.Equal(500, day.ActivityBonusMl);
        Assert.Equal(3300, day.NormMl);
        Assert.Equal(500, day.DrunkMl);
        Assert.Equal(2800, day.RemainingMl);
        Assert.False(day.ClampedToMin);
        Assert.False(day.ClampedToMax);
        Assert.Null(day.NormHint);
        Assert.Contains("Ориентир, а не медицинская рекомендация", day.Disclaimer);
    }

    [Fact]
    public async Task Day_sums_intakes_of_one_user_and_one_day_only()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-water@example.com");
        var bob = await RegisterAsync(app, "bob-water@example.com");

        await AddWaterAsync(app, alice.Token, 500, Day.AddDays(-1));
        await AddWaterAsync(app, alice.Token, 250, Day.AddDays(-1));
        await AddWaterAsync(app, alice.Token, 300, Day);
        await AddWaterAsync(app, bob.Token, 900, Day.AddDays(-1));

        var aliceYesterday = await GetDayAsync(app, alice.Token, Day.AddDays(-1));
        var aliceToday = await GetDayAsync(app, alice.Token, Day);
        var bobYesterday = await GetDayAsync(app, bob.Token, Day.AddDays(-1));

        Assert.Equal(750, aliceYesterday.DrunkMl);
        Assert.Equal(300, aliceToday.DrunkMl);
        Assert.Equal(900, bobYesterday.DrunkMl);
    }

    [Fact]
    public async Task Norm_is_unavailable_without_sex_or_weight()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var noProfile = await RegisterAsync(app, "noprofile-water@example.com");
        await AddWeightAsync(app, noProfile.Token, 70, Day.AddDays(-1));

        var noWeight = await RegisterAsync(app, "noweight-water@example.com");
        await SetProfileAsync(app, noWeight.Token, new { sex = "Female", activity = "Light" });

        var withoutProfile = await GetDayAsync(app, noProfile.Token, Day);
        var withoutWeight = await GetDayAsync(app, noWeight.Token, Day);

        Assert.Null(withoutProfile.NormMl);
        Assert.Null(withoutProfile.RemainingMl);
        Assert.NotNull(withoutProfile.NormHint);
        Assert.Contains("пол", withoutProfile.NormHint!);

        Assert.Null(withoutWeight.NormMl);
        Assert.NotNull(withoutWeight.NormHint);
        Assert.Contains("вес", withoutWeight.NormHint!);
    }

    [Fact]
    public async Task Missing_activity_keeps_norm_and_explains_why()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "noactivity-water@example.com");

        await SetProfileAsync(app, user.Token, new { sex = "Male" });
        await AddWeightAsync(app, user.Token, 70, Day.AddDays(-1));

        var day = await GetDayAsync(app, user.Token, Day);

        // 35 × 70 = 2450, поправка не применена.
        Assert.Equal(2450, day.NormMl);
        Assert.Equal(0, day.ActivityBonusMl);
        Assert.NotNull(day.NormHint);
        Assert.Contains("активность", day.NormHint!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Norm_is_clamped_to_safety_bounds()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var light = await RegisterAsync(app, "light-water@example.com");
        var heavy = await RegisterAsync(app, "heavy-water@example.com");

        await SetProfileAsync(app, light.Token, new { sex = "Female", activity = "Sedentary" });
        await AddWeightAsync(app, light.Token, 40, Day.AddDays(-1));

        await SetProfileAsync(app, heavy.Token, new { sex = "Male", activity = "VeryActive" });
        await AddWeightAsync(app, heavy.Token, 120, Day.AddDays(-1));

        var low = await GetDayAsync(app, light.Token, Day);
        var high = await GetDayAsync(app, heavy.Token, Day);

        Assert.Equal(1500, low.NormMl);
        Assert.True(low.ClampedToMin);
        Assert.False(low.ClampedToMax);

        Assert.Equal(4000, high.NormMl);
        Assert.True(high.ClampedToMax);
        Assert.False(high.ClampedToMin);
    }

    [Fact]
    public async Task Add_rejects_bad_volume_and_future_date()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "invalid-water@example.com");

        using var tooSmall = await SendAsync(app, HttpMethod.Post, "/api/v1/water", user.Token, new { ml = 10 });
        using var tooBig = await SendAsync(app, HttpMethod.Post, "/api/v1/water", user.Token, new { ml = 5000 });
        using var future = await SendAsync(
            app, HttpMethod.Post, "/api/v1/water", user.Token,
            new { ml = 250, date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2) });

        Assert.Equal(HttpStatusCode.BadRequest, tooSmall.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }

    [Fact]
    public async Task Delete_last_removes_only_the_latest_intake()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "undo-water@example.com");

        await AddWaterAsync(app, user.Token, 250, Day);
        await AddWaterAsync(app, user.Token, 500, Day);

        using var first = await SendAsync(app, HttpMethod.Delete, $"/api/v1/water/last?date={Day}", user.Token);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var afterFirst = await first.Content.ReadFromJsonAsync<WaterDayPayload>();
        Assert.NotNull(afterFirst);

        // Удаляется самый свежий прием - 500 мл, а не первый.
        Assert.Equal(250, afterFirst.DrunkMl);

        using var second = await SendAsync(app, HttpMethod.Delete, $"/api/v1/water/last?date={Day}", user.Token);
        var afterSecond = await second.Content.ReadFromJsonAsync<WaterDayPayload>();
        Assert.NotNull(afterSecond);

        Assert.Equal(0, afterSecond.DrunkMl);

        // Пустой день отменять нечего.
        using var third = await SendAsync(app, HttpMethod.Delete, $"/api/v1/water/last?date={Day}", user.Token);

        Assert.Equal(HttpStatusCode.NotFound, third.StatusCode);
    }

    [Fact]
    public async Task Endpoints_require_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var get = await app.Client.GetAsync("/api/v1/water");
        using var post = await app.Client.PostAsJsonAsync("/api/v1/water", new { ml = 250 });

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
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

    private static async Task AddWaterAsync(ApiApp app, string token, int ml, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/water", token, new { ml, date });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<WaterDayPayload> GetDayAsync(ApiApp app, string token, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/water?date={date:yyyy-MM-dd}", token);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<WaterDayPayload>();

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

    private sealed record WaterDayPayload(
        DateOnly Date,
        int DrunkMl,
        int? NormMl,
        int? BaseMl,
        int? ActivityBonusMl,
        int? RemainingMl,
        bool ClampedToMin,
        bool ClampedToMax,
        string? NormHint,
        string Disclaimer);
}
