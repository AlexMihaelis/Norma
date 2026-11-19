using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Tests.Diary;

/// <summary>
/// Интеграционные тесты дневника питания: запись позиции в прием, КБЖУ из
/// продукта (пересчет, а не снимок), единицы, итоги дня и изоляция пользователей.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DiaryTests
{
    private static readonly DateOnly Day = new(2026, 11, 18);

    // API отдаёт enum строкой ("Breakfast"): клиенту нужен тот же конвертер.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresFixture database;

    public DiaryTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Entry_is_computed_from_food_and_day_lists_all_six_meals()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "diary@example.com");

        // 150 г по 165 ккал/100 г = 247.5, калории округляются до целого вверх.
        var created = await AddEntryAsync(app, user.Token, "Breakfast", chickenId, 150, null, Day);

        Assert.Equal(248, created.Kcal);
        Assert.Equal(46.5, created.Protein, precision: 3);
        Assert.Equal(5.4, created.Fat, precision: 3);
        Assert.Equal(FoodUnit.Gram, created.Unit);
        Assert.Equal("Куриная грудка, отварная", created.FoodName);

        var day = await GetDayAsync(app, user.Token, Day);

        // Все шесть приемов в хронологическом порядке, пустые тоже приходят.
        Assert.Equal(
            [
                MealKind.Breakfast,
                MealKind.MorningSnack,
                MealKind.Lunch,
                MealKind.AfternoonSnack,
                MealKind.Dinner,
                MealKind.EveningSnack,
            ],
            day.Meals.Select(meal => meal.Kind));

        var breakfast = day.Meals[0];
        var item = Assert.Single(breakfast.Items);
        Assert.Equal(chickenId, item.FoodId);
        Assert.Equal(150, item.Quantity, precision: 3);
        Assert.Equal(248, breakfast.Totals.Kcal);
        Assert.Equal(248, day.Totals.Kcal);
        Assert.Equal(46.5, day.Totals.Protein, precision: 3);
    }

    [Fact]
    public async Task Two_entries_of_one_kind_share_one_meal()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);
        var oatmealId = await SeedFoodAsync(connectionString, "Овсянка, на воде", 88, 3, 1.7, 15);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "one-meal@example.com");

        await AddEntryAsync(app, user.Token, "Breakfast", chickenId, 100, null, Day);
        await AddEntryAsync(app, user.Token, "Breakfast", oatmealId, 80, null, Day);
        await AddEntryAsync(app, user.Token, "MorningSnack", oatmealId, 50, null, Day);

        var day = await GetDayAsync(app, user.Token, Day);

        // Завтрак один: две позиции легли в него, а не в два разных приема.
        Assert.Equal(2, day.Meals[0].Items.Count);
        Assert.Single(day.Meals[1].Items);

        // В базе ровно две строки приема, а не три.
        var options = new DbContextOptionsBuilder<NormaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new NormaDbContext(options);
        var mealCount = await db.Meals.CountAsync(meal => meal.Date == Day);
        Assert.Equal(2, mealCount);
    }

    [Fact]
    public async Task Fixing_food_in_catalog_recalculates_history()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recalc@example.com");

        var created = await AddEntryAsync(app, user.Token, "Lunch", chickenId, 100, null, Day);
        Assert.Equal(165, created.Kcal);

        // Нашли ошибку в каталоге и исправили калорийность продукта.
        var options = new DbContextOptionsBuilder<NormaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using (var db = new NormaDbContext(options))
        {
            var food = await db.Foods.SingleAsync(candidate => candidate.Id == chickenId);
            food.KcalPer100g = 180;
            await db.SaveChangesAsync();
        }

        var day = await GetDayAsync(app, user.Token, Day);

        // Это и есть выбранный пересчет: старый день тоже показывает 180 ккал.
        Assert.Equal(180, Assert.Single(day.Meals[2].Items).Kcal);
        Assert.Equal(180, day.Totals.Kcal);
    }

    [Fact]
    public async Task Unit_defaults_to_food_base_unit_and_mismatch_is_rejected()
    {
        var connectionString = await MigrateAsync();
        var milkId = await SeedFoodAsync(
            connectionString, "Молоко 2,5%", 52, 2.8, 2.5, 4.7, FoodUnit.Milliliter);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "milk@example.com");

        var created = await AddEntryAsync(app, user.Token, "Breakfast", milkId, 200, null, Day);

        Assert.Equal(FoodUnit.Milliliter, created.Unit);
        Assert.Equal(104, created.Kcal);

        // Граммы для молока не принимаем: плотности нет, перевести нельзя.
        using var mismatch = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", user.Token,
            new { meal = "Breakfast", foodId = milkId, quantity = 200, unit = "Gram", date = Day });

        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Contains("Milliliter", await mismatch.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Delete_removes_entry_and_its_empty_meal()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "delete@example.com");

        var created = await AddEntryAsync(app, user.Token, "Dinner", chickenId, 100, null, Day);

        using var deleted = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/diary/{created.Id}", user.Token);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var day = await GetDayAsync(app, user.Token, Day);
        Assert.Equal(0, day.Totals.Kcal);
        Assert.All(day.Meals, meal => Assert.Empty(meal.Items));

        // Прием без позиций тоже удален: он не должен копиться в базе.
        var options = new DbContextOptionsBuilder<NormaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new NormaDbContext(options);
        Assert.False(await db.Meals.AnyAsync(meal => meal.Date == Day));
    }

    [Fact]
    public async Task Delete_keeps_meal_while_it_has_other_items()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);
        var oatmealId = await SeedFoodAsync(connectionString, "Овсянка, на воде", 88, 3, 1.7, 15);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "delete-one@example.com");

        var first = await AddEntryAsync(app, user.Token, "Breakfast", chickenId, 100, null, Day);
        await AddEntryAsync(app, user.Token, "Breakfast", oatmealId, 100, null, Day);

        using var deleted = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/diary/{first.Id}", user.Token);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var day = await GetDayAsync(app, user.Token, Day);
        Assert.Single(day.Meals[0].Items);
    }

    [Fact]
    public async Task Users_do_not_see_or_delete_foreign_entries()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-diary@example.com");
        var bob = await RegisterAsync(app, "bob-diary@example.com");

        var aliceEntry = await AddEntryAsync(app, alice.Token, "Lunch", chickenId, 200, null, Day);

        var bobDay = await GetDayAsync(app, bob.Token, Day);
        Assert.Equal(0, bobDay.Totals.Kcal);

        // Даже зная идентификатор, чужую позицию не удалить.
        using var deleted = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/diary/{aliceEntry.Id}", bob.Token);

        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);

        var aliceDay = await GetDayAsync(app, alice.Token, Day);
        Assert.Single(Assert.Single(aliceDay.Meals.Where(meal => meal.Kind == MealKind.Lunch)).Items);
    }

    [Fact]
    public async Task Add_rejects_bad_quantity_unknown_food_and_future_date()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "invalid-diary@example.com");

        using var zero = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", user.Token,
            new { meal = "Lunch", foodId = chickenId, quantity = 0, date = Day });

        using var unknownFood = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", user.Token,
            new { meal = "Lunch", foodId = Guid.NewGuid(), quantity = 100, date = Day });

        using var badMeal = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", user.Token,
            new { meal = "Brunch", foodId = chickenId, quantity = 100, date = Day });

        using var future = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", user.Token,
            new { meal = "Lunch", foodId = chickenId, quantity = 100, date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2) });

        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownFood.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badMeal.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }

    [Fact]
    public async Task Endpoints_require_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var get = await app.Client.GetAsync("/api/v1/diary");
        using var post = await app.Client.PostAsJsonAsync("/api/v1/diary", new { meal = "Lunch" });
        using var delete = await app.Client.DeleteAsync($"/api/v1/diary/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
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

    private static async Task<DiaryEntryPayload> AddEntryAsync(
        ApiApp app, string token, string meal, Guid foodId, double quantity, string? unit, DateOnly date)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/diary", token,
            new { meal, foodId, quantity, unit, date });

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

    /// <summary>Чистые таблицы и строка подключения: тест не зависит от предыдущего.</summary>
    private async Task<string> MigrateAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
    }

    /// <summary>Кладет продукт в каталог и возвращает его идентификатор.</summary>
    private async Task<Guid> SeedFoodAsync(
        string connectionString,
        string name,
        double kcal,
        double protein,
        double fat,
        double carbs,
        FoodUnit baseUnit = FoodUnit.Gram)
    {
        var options = new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(connectionString).Options;

        await using var db = new NormaDbContext(options);

        var food = new Food
        {
            Id = Guid.NewGuid(),
            Source = FoodSource.User,
            State = FoodState.Unknown,
            BaseUnit = baseUnit,
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
}
