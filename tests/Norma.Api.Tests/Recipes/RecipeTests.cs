using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace Norma.Api.Tests.Recipes;

/// <summary>
/// Рецепты: КБЖУ считается из состава, замена меняет состав целиком, рецепты
/// изолированы по пользователю и не удаляются, если попали в дневник.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RecipeTests
{
    private readonly PostgresFixture database;

    public RecipeTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Recipe_values_are_computed_from_composition()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-compute@example.com");
        var chicken = await CreateFoodAsync(app, user.Token, "Куриная грудка", 165, 31, 3.6, 0);
        var rice = await CreateFoodAsync(app, user.Token, "Рис отварной", 116, 2.2, 0.5, 23);

        var recipe = await CreateRecipeAsync(
            app,
            user.Token,
            "Курица с рисом",
            [(chicken, 200), (rice, 150)]);

        // 200 г курицы и 150 г риса: 330 + 174 = 504 ккал на 350 г.
        Assert.Equal(350, recipe.Grams, 3);
        Assert.Equal(504, recipe.Kcal, 3);
        Assert.Equal(144, recipe.KcalPer100g, 1);
        Assert.Equal(2, recipe.Ingredients.Count);
    }

    [Fact]
    public async Task Update_replaces_composition_and_recomputes()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-update@example.com");
        var rice = await CreateFoodAsync(app, user.Token, "Рис", 343, 7, 0.6, 78);
        var milk = await CreateFoodAsync(app, user.Token, "Молоко", 60, 3, 3.2, 4.7);

        var created = await CreateRecipeAsync(app, user.Token, "Каша", [(rice, 80)]);

        using var update = await SendAsync(
            app,
            HttpMethod.Put,
            $"/api/v1/recipes/{created.Id}",
            user.Token,
            new
            {
                name = "Каша на молоке",
                ingredients = new[]
                {
                    new { foodId = rice, grams = 80 },
                    new { foodId = milk, grams = 200 },
                },
            });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<RecipePayload>();
        Assert.NotNull(updated);

        Assert.Equal("Каша на молоке", updated.Name);
        Assert.Equal(280, updated.Grams, 3);
        Assert.Equal(2, updated.Ingredients.Count);

        // 80 г риса (274.4) + 200 г молока (120) = 394.4 ккал на 280 г = 140.9.
        Assert.Equal(140.9, updated.KcalPer100g, 1);
    }

    [Fact]
    public async Task Recipes_are_isolated_between_users()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "recipe-alice@example.com");
        var bob = await RegisterAsync(app, "recipe-bob@example.com");
        var food = await CreateFoodAsync(app, alice.Token, "Гречка", 110, 4, 1, 20);

        var recipe = await CreateRecipeAsync(app, alice.Token, "Гречка с маслом", [(food, 100)]);

        var bobList = await ListAsync(app, bob.Token);

        Assert.Empty(bobList);

        using var bobGet = await SendAsync(app, HttpMethod.Get, $"/api/v1/recipes/{recipe.Id}", bob.Token);

        Assert.Equal(HttpStatusCode.NotFound, bobGet.StatusCode);

        using var bobDelete = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}", bob.Token);

        Assert.Equal(HttpStatusCode.NotFound, bobDelete.StatusCode);
    }

    [Fact]
    public async Task Validation_rejects_bad_composition()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-invalid@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Овсянка", 352, 12, 6, 62);

        using var empty = await SendAsync(
            app, HttpMethod.Post, "/api/v1/recipes", user.Token, new { name = "Пусто", ingredients = Array.Empty<object>() });

        using var unknownFood = await SendAsync(
            app, HttpMethod.Post, "/api/v1/recipes", user.Token,
            new { name = "Неизвестный", ingredients = new[] { new { foodId = Guid.NewGuid(), grams = 100 } } });

        using var duplicate = await SendAsync(
            app, HttpMethod.Post, "/api/v1/recipes", user.Token,
            new { name = "Дубль", ingredients = new[] { new { foodId = food, grams = 100 }, new { foodId = food, grams = 50 } } });

        using var shortName = await SendAsync(
            app, HttpMethod.Post, "/api/v1/recipes", user.Token,
            new { name = "Я", ingredients = new[] { new { foodId = food, grams = 100 } } });

        using var hugeWeight = await SendAsync(
            app, HttpMethod.Post, "/api/v1/recipes", user.Token,
            new { name = "Килограмм", ingredients = new[] { new { foodId = food, grams = 9000 } } });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownFood.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, hugeWeight.StatusCode);
    }

    [Fact]
    public async Task List_filters_by_name()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-search@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Свекла", 43, 1.5, 0.2, 9);

        await CreateRecipeAsync(app, user.Token, "Борщ", [(food, 300)]);
        await CreateRecipeAsync(app, user.Token, "Салат из свеклы", [(food, 150)]);

        var all = await ListAsync(app, user.Token);
        var found = await ListAsync(app, user.Token, "борщ");

        Assert.Equal(2, all.Count);
        var single = Assert.Single(found);
        Assert.Equal("Борщ", single.Name);
    }

    [Fact]
    public async Task Delete_removes_recipe_and_returns_404_after()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-delete@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Морковь", 35, 1.3, 0.1, 7);

        var recipe = await CreateRecipeAsync(app, user.Token, "Морковь по-корейски", [(food, 120)]);

        using var delete = await SendAsync(app, HttpMethod.Delete, $"/api/v1/recipes/{recipe.Id}", user.Token);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var get = await SendAsync(app, HttpMethod.Get, $"/api/v1/recipes/{recipe.Id}", user.Token);

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Empty(await ListAsync(app, user.Token));
    }

    [Fact]
    public async Task Endpoints_require_token()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        using var list = await app.Client.GetAsync("/api/v1/recipes");
        using var create = await app.Client.PostAsJsonAsync("/api/v1/recipes", new { name = "Тест" });

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    [Fact]
    public async Task Tags_favorite_and_instructions_round_trip()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-tags@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Кабачок", 24, 0.6, 0.3, 4.6);

        using var create = await SendAsync(app, HttpMethod.Post, "/api/v1/recipes", user.Token, new
        {
            name = "Кабачок в духовке",
            instructions = "Нарезать, посолить, запечь 25 минут.",
            tags = new[] { "Dinner", "Oven" },
            ingredients = new[] { new { foodId = food, grams = 300 } },
        });

        create.EnsureSuccessStatusCode();

        var created = await create.Content.ReadFromJsonAsync<RecipePayload>();
        Assert.NotNull(created);

        Assert.Equal("Нарезать, посолить, запечь 25 минут.", created.Instructions);
        Assert.Contains("Dinner", created.Tags);
        Assert.Contains("Oven", created.Tags);
        Assert.False(created.IsFavorite);

        using var favorite = await SendAsync(
            app, HttpMethod.Put, $"/api/v1/recipes/{created.Id}/favorite", user.Token, new { isFavorite = true });

        Assert.Equal(HttpStatusCode.NoContent, favorite.StatusCode);

        var favoriteList = await ListAsync(app, user.Token, favoriteOnly: true);
        Assert.Single(favoriteList);

        // Метка в списке тоже фильтрует.
        var dinnerList = await ListAsync(app, user.Token, tag: "Dinner");
        Assert.Single(dinnerList);

        var breakfastList = await ListAsync(app, user.Token, tag: "Breakfast");
        Assert.Empty(breakfastList);
    }

    [Fact]
    public async Task Auto_tags_are_computed_from_composition()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-auto@example.com");
        var chicken = await CreateFoodAsync(app, user.Token, "Куриная грудка", 165, 31, 3.6, 0);

        var recipe = await CreateRecipeAsync(app, user.Token, "Грудка на пару", [(chicken, 200)]);

        Assert.Contains("Chicken", recipe.AutoTags);
        Assert.Contains("HighProtein", recipe.AutoTags);
        Assert.Empty(recipe.Tags);
    }

    [Fact]
    public async Task Unknown_tag_and_broken_photo_are_rejected()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-bad-tag@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Огурец", 15, 0.8, 0.1, 2.8);

        using var unknownTag = await SendAsync(app, HttpMethod.Post, "/api/v1/recipes", user.Token, new
        {
            name = "Огурец",
            tags = new[] { "Космос" },
            ingredients = new[] { new { foodId = food, grams = 100 } },
        });

        using var brokenPhoto = await SendAsync(app, HttpMethod.Post, "/api/v1/recipes", user.Token, new
        {
            name = "Огурец с фото",
            photoDataUrl = "data:text/plain;base64,AAAA",
            ingredients = new[] { new { foodId = food, grams = 100 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, unknownTag.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, brokenPhoto.StatusCode);
    }

    [Fact]
    public async Task Tag_catalog_is_available()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-catalog@example.com");

        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/recipes/tags", user.Token);

        response.EnsureSuccessStatusCode();

        var catalog = await response.Content.ReadFromJsonAsync<TagCatalogPayload>();
        Assert.NotNull(catalog);

        Assert.Contains(catalog.Items, item => item.Code == "Breakfast" && item.Title == "завтрак");
        Assert.Contains(catalog.Items, item => item.Code == "HighProtein");
    }

    [Fact]
    public async Task Manual_nutrition_overrides_composition()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-manual@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Кабачок", 24, 0.6, 0.3, 4.6);

        using var create = await SendAsync(app, HttpMethod.Post, "/api/v1/recipes", user.Token, new
        {
            name = "Кабачок по-своему",
            kcalPer100 = 250,
            proteinPer100 = 20,
            fatPer100 = 10,
            carbsPer100 = 15,
            ingredients = new[] { new { foodId = food, grams = 200 } },
        });

        create.EnsureSuccessStatusCode();

        var created = await create.Content.ReadFromJsonAsync<RecipePayload>();
        Assert.NotNull(created);

        Assert.True(created.ManualNutrition);
        Assert.Equal(250, created.KcalPer100g, 3);
        Assert.Equal(20, created.ProteinPer100g, 3);
        // 250 ккал на 100 г × 200 г = 500.
        Assert.Equal(500, created.Kcal, 3);

        // Замена без своих цифр возвращает расчет из состава.
        using var update = await SendAsync(
            app, HttpMethod.Put, $"/api/v1/recipes/{created.Id}", user.Token, new
            {
                name = "Кабачок по-своему",
                ingredients = new[] { new { foodId = food, grams = 200 } },
            });

        update.EnsureSuccessStatusCode();

        var updated = await update.Content.ReadFromJsonAsync<RecipePayload>();
        Assert.NotNull(updated);

        Assert.False(updated.ManualNutrition);
        Assert.Equal(24, updated.KcalPer100g, 3);
    }

    [Fact]
    public async Task Partial_manual_nutrition_is_rejected()
    {
        var connectionString = await ResetAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "recipe-half@example.com");
        var food = await CreateFoodAsync(app, user.Token, "Огурец", 15, 0.8, 0.1, 2.8);

        using var response = await SendAsync(app, HttpMethod.Post, "/api/v1/recipes", user.Token, new
        {
            name = "Огурец",
            kcalPer100 = 100,
            ingredients = new[] { new { foodId = food, grams = 100 } },
        });

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

    private static async Task<IReadOnlyList<RecipeSummaryPayload>> ListAsync(
        ApiApp app,
        string token,
        string? query = null,
        string? tag = null,
        bool favoriteOnly = false)
    {
        var parts = new List<string>();

        if (query is not null)
        {
            parts.Add($"q={Uri.EscapeDataString(query)}");
        }

        if (tag is not null)
        {
            parts.Add($"tag={Uri.EscapeDataString(tag)}");
        }

        if (favoriteOnly)
        {
            parts.Add("favorite=true");
        }

        var url = parts.Count == 0
            ? "/api/v1/recipes"
            : $"/api/v1/recipes?{string.Join("&", parts)}";

        using var response = await SendAsync(app, HttpMethod.Get, url, token);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RecipeListPayload>();

        Assert.NotNull(payload);

        return payload.Items;
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

    private sealed record RecipeIngredientPayload(Guid FoodId, string Name, double Grams, double Kcal);

    private sealed record RecipePayload(
        Guid Id,
        string Name,
        string? Instructions,
        double Grams,
        double KcalPer100g,
        double ProteinPer100g,
        double FatPer100g,
        double CarbsPer100g,
        double Kcal,
        List<string> Tags,
        List<string> AutoTags,
        bool IsFavorite,
        bool ManualNutrition,
        string? PhotoDataUrl,
        List<RecipeIngredientPayload> Ingredients);

    private sealed record RecipeSummaryPayload(
        Guid Id,
        string Name,
        double Grams,
        double KcalPer100g,
        double Kcal,
        int IngredientCount,
        List<string> Tags,
        List<string> AutoTags,
        bool IsFavorite);

    private sealed record RecipeListPayload(List<RecipeSummaryPayload> Items);

    private sealed record TagPayload(string Code, string Title, string Group);

    private sealed record TagCatalogPayload(List<TagPayload> Items);
}
