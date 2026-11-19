using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Tests.Foods;

/// <summary>
/// Интеграционные тесты своего продукта: продукт попадает в общий каталог и
/// находится поиском, авторство хранится, удаление пользователя продукт не
/// удаляет, а строгая валидация защищает каталог от мусора.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CreateFoodTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PostgresFixture database;

    public CreateFoodTests(PostgresFixture database) => this.database = database;

    [Fact]
    public async Task Created_product_is_found_by_search_and_keeps_author()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "creator@example.com");

        var created = await CreateFoodAsync(app, user.Token, new
        {
            name = "Немолоко, овсяное",
            aliases = new[] { "овсяное молоко" },
            baseUnit = "Milliliter",
            kcalPer100 = 40,
            proteinPer100 = 3,
            fatPer100 = 1.5,
            carbsPer100 = 4.5,
        });

        Assert.Equal("Немолоко, овсяное", created.Name);
        Assert.Equal(FoodUnit.Milliliter, created.BaseUnit);
        Assert.Equal(FoodState.Unknown, created.State);
        Assert.Equal(40, created.KcalPer100g, precision: 3);

        // Продукт общий: находится поиском и по названию, и по синониму.
        var byName = await SearchAsync(app, user.Token, "Немолоко");
        Assert.Equal(created.Id, Assert.Single(byName).Id);

        var byAlias = await SearchAsync(app, user.Token, "овсяное");
        Assert.Equal(created.Id, Assert.Single(byAlias).Id);

        // Авторство сохранено в отдельном поле, но поиск на него не опирается.
        var options = new DbContextOptionsBuilder<NormaDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new NormaDbContext(options);
        var food = await db.Foods.SingleAsync(candidate => candidate.Id == created.Id);
        Assert.Equal(user.UserId, food.CreatedByUserId);
        Assert.Equal(FoodSource.User, food.Source);
    }

    [Fact]
    public async Task Deleting_user_keeps_product_and_clears_author()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var author = await RegisterAsync(app, "food-author@example.com");

        var created = await CreateFoodAsync(app, author.Token, new
        {
            name = "Пирог мамин",
            kcalPer100 = 250,
            proteinPer100 = 4,
            fatPer100 = 12,
            carbsPer100 = 32,
        });

        // Удаляем аккаунт прямо в базе: эндпоинта удаления пользователя пока нет.
        var options = new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(connectionString).Options;

        await using (var db = new NormaDbContext(options))
        {
            var user = await db.Users.SingleAsync(candidate => candidate.Id == author.UserId);
            db.Users.Remove(user);
            await db.SaveChangesAsync();
        }

        // Продукт - общее знание: он остался, автор просто обнулился (SET NULL).
        await using (var db = new NormaDbContext(options))
        {
            var food = await db.Foods.SingleAsync(candidate => candidate.Id == created.Id);
            Assert.Null(food.CreatedByUserId);
            Assert.Equal(FoodSource.User, food.Source);
        }
    }

    [Fact]
    public async Task Duplicate_name_is_rejected()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "food-dup@example.com");

        await CreateFoodAsync(app, user.Token, new
        {
            name = "Молоко кокосовое",
            kcalPer100 = 150,
            proteinPer100 = 1.5,
            fatPer100 = 15,
            carbsPer100 = 3,
        });

        using var duplicate = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", user.Token, new
        {
            name = "молоко кокосовое",
            kcalPer100 = 160,
            proteinPer100 = 1.5,
            fatPer100 = 16,
            carbsPer100 = 3,
        });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("уже есть", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Accepts_energy_dense_product_like_lard()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "food-lard@example.com");

        // Смалец из USDA: 902 ккал и 100 г жира на 100 г. Это не опечатка, и
        // валидация не должна его отклонять.
        var created = await CreateFoodAsync(app, user.Token, new
        {
            name = "Смалец",
            state = "Raw",
            kcalPer100 = 902,
            proteinPer100 = 0,
            fatPer100 = 100,
            carbsPer100 = 0,
        });

        Assert.Equal(902, created.KcalPer100g, precision: 3);
        Assert.Equal(FoodState.Raw, created.State);
    }

    [Fact]
    public async Task Invalid_values_are_rejected()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        var user = await RegisterAsync(app, "food-invalid@example.com");

        using var negative = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", user.Token, new
        {
            name = "Минус калории",
            kcalPer100 = -10,
            proteinPer100 = 0,
            fatPer100 = 0,
            carbsPer100 = 0,
        });

        using var impossible = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", user.Token, new
        {
            name = "Больше, чем может быть в еде",
            kcalPer100 = 1200,
            proteinPer100 = 0,
            fatPer100 = 0,
            carbsPer100 = 0,
        });

        using var macroSum = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", user.Token, new
        {
            name = "Слишком плотный продукт",
            kcalPer100 = 400,
            proteinPer100 = 50,
            fatPer100 = 40,
            carbsPer100 = 30,
        });

        using var shortName = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", user.Token, new
        {
            name = "М",
            kcalPer100 = 40,
        });

        using var badState = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", user.Token, new
        {
            name = "Продукт со странным состоянием",
            state = "Boiled",
            kcalPer100 = 40,
        });

        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, impossible.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, macroSum.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, shortName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badState.StatusCode);
    }

    [Fact]
    public async Task Create_requires_token()
    {
        var connectionString = await MigrateAsync();
        await using var app = await StartApiAsync(connectionString);

        using var response = await app.Client.PostAsJsonAsync("/api/v1/foods", new
        {
            name = "Продукт без токена",
            kcalPer100 = 40,
        });

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

    private static async Task<FoodPayload> CreateFoodAsync(ApiApp app, string token, object body)
    {
        using var response = await SendAsync(app, HttpMethod.Post, "/api/v1/foods", token, body);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoodPayload>(JsonOptions);

        Assert.NotNull(payload);

        return payload;
    }

    private static async Task<IReadOnlyList<FoodPayload>> SearchAsync(ApiApp app, string token, string query)
    {
        var url = $"/api/v1/foods/search?q={Uri.EscapeDataString(query)}";
        using var response = await SendAsync(app, HttpMethod.Get, url, token);

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FoodSearchPayload>(JsonOptions);

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

    private sealed record AuthPayload(Guid UserId, string Email, string Token);

    private sealed record FoodPayload(
        Guid Id,
        string Name,
        FoodState State,
        FoodUnit BaseUnit,
        double KcalPer100g,
        double ProteinPer100g,
        double FatPer100g,
        double CarbsPer100g);

    private sealed record FoodSearchPayload(IReadOnlyList<FoodPayload> Items);
}
