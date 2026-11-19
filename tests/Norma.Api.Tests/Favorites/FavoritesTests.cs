using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Tests.Favorites;

/// <summary>
/// Интеграционные тесты избранного: добавление продукта, повторное добавление без
/// дубля, порядок списка, удаление и изоляция пользователей.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FavoritesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresFixture database;

    public FavoritesTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Add_returns_product_and_list_shows_it()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "favorites@example.com");

        using var added = await SendAsync(
            app, HttpMethod.Post, "/api/v1/favorites", user.Token, new { foodId = chickenId });

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var items = await GetFavoritesAsync(app, user.Token);

        var item = Assert.Single(items);
        Assert.Equal(chickenId, item.FoodId);
        Assert.Equal("Куриная грудка, отварная", item.Name);
        Assert.Equal(FoodState.Unknown, item.State);
        Assert.Equal(FoodUnit.Gram, item.BaseUnit);
        Assert.Equal(165, item.KcalPer100g, precision: 3);
        Assert.Equal(31, item.ProteinPer100g, precision: 3);
        Assert.NotEqual(default, item.AddedAt);
    }

    [Fact]
    public async Task Add_twice_does_not_create_duplicate()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "favorites-dup@example.com");

        using var first = await SendAsync(
            app, HttpMethod.Post, "/api/v1/favorites", user.Token, new { foodId = chickenId });

        using var second = await SendAsync(
            app, HttpMethod.Post, "/api/v1/favorites", user.Token, new { foodId = chickenId });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        Assert.Single(await GetFavoritesAsync(app, user.Token));

        // В базе тоже одна запись: уникальный индекс (UserId, FoodId).
        var options = new DbContextOptionsBuilder<NormaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new NormaDbContext(options);
        Assert.Equal(1, await db.FavoriteFoods.CountAsync());
    }

    [Fact]
    public async Task List_shows_newest_first()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);
        var oatmealId = await SeedFoodAsync(connectionString, "Овсянка, на воде", 88, 3, 1.7, 15);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "favorites-order@example.com");

        await AddFavoriteAsync(app, user.Token, chickenId);
        await AddFavoriteAsync(app, user.Token, oatmealId);

        var items = await GetFavoritesAsync(app, user.Token);

        // Последний добавленный - наверху: на экране быстрого ввода важны свежие.
        Assert.Equal(2, items.Count);
        Assert.Equal(oatmealId, items[0].FoodId);
        Assert.Equal(chickenId, items[1].FoodId);
    }

    [Fact]
    public async Task Delete_removes_mark_and_second_delete_is_404()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "favorites-delete@example.com");

        await AddFavoriteAsync(app, user.Token, chickenId);

        using var deleted = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/favorites/{chickenId}", user.Token);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await GetFavoritesAsync(app, user.Token));

        using var again = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/favorites/{chickenId}", user.Token);

        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);

        // Продукт из каталога не тронут: избранное - только закладка.
        var options = new DbContextOptionsBuilder<NormaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new NormaDbContext(options);
        Assert.True(await db.Foods.AnyAsync(food => food.Id == chickenId));
    }

    [Fact]
    public async Task Add_rejects_missing_and_unknown_food()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "favorites-invalid@example.com");

        using var withoutFood = await SendAsync(
            app, HttpMethod.Post, "/api/v1/favorites", user.Token, new { });

        using var unknownFood = await SendAsync(
            app, HttpMethod.Post, "/api/v1/favorites", user.Token, new { foodId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, withoutFood.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownFood.StatusCode);
    }

    [Fact]
    public async Task Users_do_not_see_or_delete_foreign_favorites()
    {
        var connectionString = await MigrateAsync();
        var chickenId = await SeedFoodAsync(connectionString, "Куриная грудка, отварная", 165, 31, 3.6, 0);

        await using var app = await StartApiAsync(connectionString);

        var alice = await RegisterAsync(app, "alice-favorites@example.com");
        var bob = await RegisterAsync(app, "bob-favorites@example.com");

        await AddFavoriteAsync(app, alice.Token, chickenId);

        Assert.Empty(await GetFavoritesAsync(app, bob.Token));

        using var deleted = await SendAsync(
            app, HttpMethod.Delete, $"/api/v1/favorites/{chickenId}", bob.Token);

        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
        Assert.Single(await GetFavoritesAsync(app, alice.Token));
    }

    [Fact]
    public async Task Endpoints_require_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var get = await app.Client.GetAsync("/api/v1/favorites");
        using var post = await app.Client.PostAsJsonAsync("/api/v1/favorites", new { foodId = Guid.NewGuid() });
        using var delete = await app.Client.DeleteAsync($"/api/v1/favorites/{Guid.NewGuid()}");

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

    private static async Task AddFavoriteAsync(ApiApp app, string token, Guid foodId)
    {
        using var response = await SendAsync(
            app, HttpMethod.Post, "/api/v1/favorites", token, new { foodId });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<IReadOnlyList<FavoritePayload>> GetFavoritesAsync(ApiApp app, string token)
    {
        using var response = await SendAsync(app, HttpMethod.Get, "/api/v1/favorites", token);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FavoriteListPayload>(JsonOptions);

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

    /// <summary>Чистые таблицы и строка подключения: тест не зависит от предыдущего.</summary>
    private async Task<string> MigrateAsync()
    {
        await database.ResetAsync();

        return database.ConnectionString;
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

    private sealed record FavoritePayload(
        Guid FoodId,
        string Name,
        FoodState State,
        FoodUnit BaseUnit,
        double KcalPer100g,
        double ProteinPer100g,
        double FatPer100g,
        double CarbsPer100g,
        DateTimeOffset AddedAt);

    private sealed record FavoriteListPayload(IReadOnlyList<FavoritePayload> Items);
}
