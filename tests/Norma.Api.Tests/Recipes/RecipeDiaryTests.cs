using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Norma.Api.Tests.Recipes;

/// <summary>
/// Рецепт как позиция дневника: граммовка, КБЖУ из состава, итоги дня,
/// статистика, повтор дня и запрет удалять рецепт, который уже съеден.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RecipeDiaryTests
{
    private readonly PostgresFixture database;

    public RecipeDiaryTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Recipe_entry_counts_with_its_own_nutrition()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "diary-recipe@example.com");
        var chicken = await CreateFoodAsync(app, user.Token, "Куриная грудка", 165, 31, 3.6, 0);
        var recipe = await CreateRecipeAsync(app, user.Token, "Грудка с гарниром", [(chicken, 200)]);

        using var add = await SendAsync(app, HttpMethod.Post, "/api/v1/diary", user.Token, new
        {
            meal = "Lunch",
            recipeId = recipe.Id,
            quantity = 150,
        });

        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var entry = await add.Content.ReadFromJsonAsync<DiaryEntryPayload>();
        Assert.NotNull(entry);

        Assert.Equal("Recipe", entry.Kind);
        Assert.Equal(recipe.Id, entry.RecipeId);
        Assert.Null(entry.FoodId);
        Assert.Equal("Грудка с гарниром", entry.FoodName);
        Assert.Equal("Gram", entry.Unit);
        // 165 ккал на 100 г × 150 г = 247.5, округление вверх.
        Assert.Equal(248, entry.Kcal);

        var day = await GetDayAsync(app, user.Token, Today());

        Assert.Equal(248, day.Totals.Kcal);
    }

    [Fact]
    public async Task Entry_needs_exactly_one_source()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "diary-source@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Рис", 116, 2.2, 0.5, 23);
        var recipe = await CreateRecipeAsync(app, user.Token, "Рис отварной", [(food, 100)]);

        using var both = await SendAsync(app, HttpMethod.Post, "/api/v1/diary", user.Token, new
        {
            meal = "Lunch",
            foodId = food,
            recipeId = recipe.Id,
            quantity = 100,
        });

        using var none = await SendAsync(app, HttpMethod.Post, "/api/v1/diary", user.Token, new
        {
            meal = "Lunch",
            quantity = 100,
        });

        Assert.Equal(HttpStatusCode.BadRequest, both.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
    }

    [Fact]
    public async Task Foreign_recipe_cannot_be_added()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "diary-alice@example.com");
        var bob = await RegisterAsync(app, "diary-bob@example.com");
        var food = await CreateFoodAsync(app, alice.Token, "Гречка", 110, 4, 1, 20);
        var recipe = await CreateRecipeAsync(app, alice.Token, "Гречка", [(food, 100)]);

        using var add = await SendAsync(app, HttpMethod.Post, "/api/v1/diary", bob.Token, new
        {
            meal = "Lunch",
            recipeId = recipe.Id,
            quantity = 100,
        });

        Assert.Equal(HttpStatusCode.BadRequest, add.StatusCode);
    }

    [Fact]
    public async Task Recipe_used_in_diary_cannot_be_deleted()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "diary-conflict@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Овсянка", 352, 12, 6, 62);
        var recipe = await CreateRecipeAsync(app, user.Token, "Каша", [(food, 80)]);

        await SendAsync(app, HttpMethod.Post, "/api/v1/diary", user.Token, new
        {
            meal = "Breakfast",
            recipeId = recipe.Id,
            quantity = 80,
        });

        using var delete = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}", user.Token);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task Stats_and_repeat_see_recipe_entries()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "diary-stats@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Морковь", 35, 1.3, 0.1, 7);
        var recipe = await CreateRecipeAsync(app, user.Token, "Морковь по-корейски", [(food, 100)]);

        var today = Today();

        await SendAsync(app, HttpMethod.Post, "/api/v1/diary", user.Token, new
        {
            meal = "Dinner",
            recipeId = recipe.Id,
            quantity = 200,
            date = today,
        });

        // 35 ккал на 100 г × 200 г = 70.
        using var stats = await SendAsync(
            app, HttpMethod.Get, $"/api/v1/stats?from={today}&to={today}", user.Token);

        stats.EnsureSuccessStatusCode();

        var statsPayload = await stats.Content.ReadFromJsonAsync<StatsPayload>();
        Assert.NotNull(statsPayload);

        var day = Assert.Single(statsPayload.Days);
        Assert.Equal(70, day.Kcal);
        Assert.Equal(1, day.Entries);

        // Повтор дня копирует и рецептные позиции.
        using var repeat = await SendAsync(app, HttpMethod.Post, "/api/v1/diary/repeat", user.Token, new
        {
            fromDate = today,
            date = today,
        });

        Assert.Equal(HttpStatusCode.BadRequest, repeat.StatusCode);

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        await SendAsync(app, HttpMethod.Post, "/api/v1/diary", user.Token, new
        {
            meal = "Lunch",
            recipeId = recipe.Id,
            quantity = 100,
            date = yesterday,
        });

        using var repeatOk = await SendAsync(app, HttpMethod.Post, "/api/v1/diary/repeat", user.Token, new
        {
            fromDate = yesterday,
            date = today,
        });

        repeatOk.EnsureSuccessStatusCode();

        var repeated = await repeatOk.Content.ReadFromJsonAsync<RepeatPayload>();
        Assert.NotNull(repeated);

        Assert.Equal(1, repeated.CopiedCount);

        var dayAfter = await GetDayAsync(app, user.Token, today);

        // 70 ккал за обед и 35 за скопированную позицию.
        Assert.Equal(105, dayAfter.Totals.Kcal);
    }

    private static string Today() => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

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

    private static async Task<DiaryDayPayload> GetDayAsync(ApiApp app, string token, string date)
    {
        using var response = await SendAsync(app, HttpMethod.Get, $"/api/v1/diary?date={date}", token);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DiaryDayPayload>();

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<Guid> CreateFoodAsync(
        ApiApp app, string token, string name, double kcal, double protein, double fat, double carbs)
    {
        using var response = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", token, new
        {
            name = $"{name} {Guid.NewGuid():N}",
            kcalPer100 = kcal,
            proteinPer100 = protein,
            fatPer100 = fat,
            carbsPer100 = carbs,
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoodPayload>();

        Assert.NotNull(payload);

        return payload.Id;
    }

    private static async Task<RecipePayload> CreateRecipeAsync(
        ApiApp app, string token, string name, IReadOnlyList<(Guid FoodId, double Grams)> composition)
    {
        using var response = await SendAsync(app, HttpMethod.Post, "/api/v1/recipes", token, new
        {
            name,
            ingredients = composition.Select(line => new { foodId = line.FoodId, grams = line.Grams }),
        });

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RecipePayload>();

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

    private sealed record FoodPayload(Guid Id, string Name);

    private sealed record RecipePayload(Guid Id, string Name);

    private sealed record DiaryEntryPayload(
        Guid Id,
        string Kind,
        Guid? FoodId,
        Guid? RecipeId,
        string FoodName,
        double Quantity,
        string Unit,
        int Kcal,
        double Protein,
        double Fat,
        double Carbs);

    private sealed record DiaryTotalsPayload(int Kcal, double Protein, double Fat, double Carbs);

    private sealed record DiaryDayPayload(
        string Date, List<object> Meals, DiaryTotalsPayload Totals);

    private sealed record StatsDayPayload(string Date, int Kcal, int Entries);

    private sealed record StatsPayload(List<StatsDayPayload> Days);

    private sealed record RepeatPayload(string Date, int CopiedCount);
}
