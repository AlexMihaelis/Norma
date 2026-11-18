using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Testcontainers.PostgreSql;

namespace Norma.Api.Tests.Diary;

/// <summary>
/// Интеграционные тесты повтора дня: копирование вчерашних позиций в сегодня,
/// добавление в уже существующий прием, копирование одного приема и изоляция.
/// </summary>
public sealed class RepeatDiaryTests : IAsyncLifetime
{
    private static readonly DateOnly Day = new(2026, 11, 18);
    private static readonly DateOnly PreviousDay = Day.AddDays(-1);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgreSqlContainer database = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public Task InitializeAsync() => database.StartAsync();

    public Task DisposeAsync() => database.DisposeAsync().AsTask();

    [Fact]
    public async Task Repeat_copies_previous_day_and_leaves_it_intact()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "repeat@example.com");

        await AddEntryAsync(app, user.Token, "Breakfast", chickenId, 100, PreviousDay);
        await AddEntryAsync(app, user.Token, "Dinner", chickenId, 200, PreviousDay);

        var repeated = await RepeatAsync(app, user.Token, new { fromDate = PreviousDay, date = Day });

        Assert.Equal(2, repeated.CopiedCount);
        Assert.Null(repeated.Hint);
        Assert.Equal(Day, repeated.Date);

        // 100 г (165) + 200 г (330) = 495 ккал.
        Assert.Equal(495, repeated.Totals.Kcal);
        Assert.Equal(MealKind.Breakfast, repeated.Meals[0].Kind);
        Assert.Equal(165, Assert.Single(repeated.Meals[0].Items).Kcal);
        Assert.Equal(330, Assert.Single(repeated.Meals[4].Items).Kcal);

        // Источник не тронут: повтор копирует, а не переносит.
        var source = await GetDayAsync(app, user.Token, PreviousDay);
        Assert.Equal(495, source.Totals.Kcal);
    }

    [Fact]
    public async Task Repeat_without_dates_copies_yesterday_into_today()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "repeat-default@example.com");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await AddEntryAsync(app, user.Token, "Lunch", chickenId, 100, today.AddDays(-1));

        var repeated = await RepeatAsync(app, user.Token, new { });

        Assert.Equal(today, repeated.Date);
        Assert.Equal(1, repeated.CopiedCount);
        Assert.Equal(165, repeated.Totals.Kcal);
    }

    [Fact]
    public async Task Repeat_appends_to_existing_meal_without_creating_another()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "repeat-append@example.com");

        await AddEntryAsync(app, user.Token, "Breakfast", chickenId, 100, PreviousDay);
        await AddEntryAsync(app, user.Token, "Breakfast", chickenId, 50, Day);

        var repeated = await RepeatAsync(app, user.Token, new { fromDate = PreviousDay, date = Day });

        Assert.Equal(1, repeated.CopiedCount);
        Assert.Equal(2, repeated.Meals[0].Items.Count);

        // 165 + 83 (82.5 округляется вверх) = 248.
        Assert.Equal(248, repeated.Totals.Kcal);

        // Второй завтрак не появился: в базе одна строка приема на этот день.
        var options = new DbContextOptionsBuilder<NormaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new NormaDbContext(options);
        var breakfasts = await db.Meals.CountAsync(
            meal => meal.Date == Day && meal.Kind == MealKind.Breakfast);

        Assert.Equal(1, breakfasts);
    }

    [Fact]
    public async Task Repeat_copies_only_requested_meal()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "repeat-one-meal@example.com");

        await AddEntryAsync(app, user.Token, "Breakfast", chickenId, 100, PreviousDay);
        await AddEntryAsync(app, user.Token, "Dinner", chickenId, 200, PreviousDay);

        var repeated = await RepeatAsync(
            app, user.Token, new { fromDate = PreviousDay, date = Day, meal = "Dinner" });

        Assert.Equal(1, repeated.CopiedCount);
        Assert.Empty(repeated.Meals[0].Items);
        Assert.Equal(330, Assert.Single(repeated.Meals[4].Items).Kcal);
        Assert.Equal(330, repeated.Totals.Kcal);
    }

    [Fact]
    public async Task Repeat_of_empty_day_returns_zero_and_hint()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "repeat-empty@example.com");

        var repeated = await RepeatAsync(app, user.Token, new { fromDate = PreviousDay, date = Day });

        Assert.Equal(0, repeated.CopiedCount);
        Assert.NotNull(repeated.Hint);
        Assert.Contains("нечего", repeated.Hint!);
        Assert.Equal(0, repeated.Totals.Kcal);
    }

    [Fact]
    public async Task Repeat_does_not_copy_foreign_day()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-repeat@example.com");
        var bob = await RegisterAsync(app, "bob-repeat@example.com");

        await AddEntryAsync(app, alice.Token, "Breakfast", chickenId, 100, PreviousDay);

        var repeated = await RepeatAsync(app, bob.Token, new { fromDate = PreviousDay, date = Day });

        Assert.Equal(0, repeated.CopiedCount);
        Assert.Equal(0, repeated.Totals.Kcal);
    }

    [Fact]
    public async Task Repeat_rejects_same_day_future_dates_and_bad_meal()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "repeat-invalid@example.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using var sameDay = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary/repeat", user.Token,
            new { fromDate = Day, date = Day });

        using var futureTarget = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary/repeat", user.Token,
            new { date = today.AddDays(3) });

        using var futureSource = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary/repeat", user.Token,
            new { fromDate = today.AddDays(3) });

        using var badMeal = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary/repeat", user.Token,
            new { meal = "Brunch" });

        Assert.Equal(HttpStatusCode.BadRequest, sameDay.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, futureTarget.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, futureSource.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badMeal.StatusCode);
    }

    [Fact]
    public async Task Repeat_requires_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.PostAsJsonAsync("/api/v1/diary/repeat", new { });

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

    private static async Task<RepeatPayload> RepeatAsync(ApiApp app, string token, object body)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary/repeat", token, body);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RepeatPayload>(JsonOptions);

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<DiaryEntryPayload> AddEntryAsync(
        ApiApp app, string token, string meal, Guid foodId, double quantity, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", token,
            new { meal, foodId, quantity, date });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DiaryEntryPayload>(JsonOptions);

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<DiaryDayPayload> GetDayAsync(ApiApp app, string token, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/diary?date={date:yyyy-MM-dd}", token);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DiaryDayPayload>(JsonOptions);

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

    private async Task<string> MigrateAsync()
    {
        var connectionString = database.GetConnectionString();
        var options = new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(connectionString).Options;

        await using var db = new NormaDbContext(options);
        await db.Database.MigrateAsync();

        return connectionString;
    }

    private async Task<Guid> SeedFoodAsync(
        string connectionString,
        string name,
        double kcal,
        double protein,
        double fat,
        double carbs)
    {
        var options = new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(connectionString).Options;

        await using var db = new NormaDbContext(options);

        var food = new Food
        {
            Id = Guid.NewGuid(),
            Source = FoodSource.User,
            State = FoodState.Unknown,
            BaseUnit = FoodUnit.Gram,
            KcalPer100g = kcal,
            ProteinPer100g = protein,
            FatPer100g = fat,
            CarbsPer100g = carbs,
        };

        food.Names.Add(new FoodName
        {
            Id = Guid.NewGuid(),
            Locale = "ru",
            Name = name,
            IsPrimary = true,
        });

        db.Foods.Add(food);
        await db.SaveChangesAsync();

        return food.Id;
    }

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record DiaryEntryPayload(
        Guid Id,
        Guid FoodId,
        string FoodName,
        double Quantity,
        FoodUnit Unit,
        int Kcal,
        double Protein,
        double Fat,
        double Carbs);

    private sealed record DiaryTotalsPayload(int Kcal, double Protein, double Fat, double Carbs);

    private sealed record DiaryMealPayload(
        MealKind Kind,
        IReadOnlyList<DiaryEntryPayload> Items,
        DiaryTotalsPayload Totals);

    private sealed record DiaryDayPayload(
        DateOnly Date,
        IReadOnlyList<DiaryMealPayload> Meals,
        DiaryTotalsPayload Totals);

    private sealed record RepeatPayload(
        DateOnly Date,
        int CopiedCount,
        string? Hint,
        IReadOnlyList<DiaryMealPayload> Meals,
        DiaryTotalsPayload Totals);
}
