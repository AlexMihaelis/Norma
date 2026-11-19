using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Tests.Foods;

/// <summary>
/// Интеграционные тесты поиска продуктов. Работают с общим контейнером Postgres
/// и чистой базой перед каждым тестом: не зависят от локальной базы и порядка
/// запуска.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SearchFoodsTests
{
    private readonly PostgresFixture database;

    public SearchFoodsTests(PostgresFixture database) => this.database = database;

    // API отдаёт enum строкой ("Cooked"). У HttpClient по умолчанию enum читается
    // числом, поэтому клиентской стороне нужен тот же конвертер, что и серверу.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Search_finds_food_by_alias_and_returns_macros()
    {
        var connectionString = await SeedAsync();

        await using var app = await StartApiAgainstAsync(connectionString);

        var query = Uri.EscapeDataString("гречка");
        using var response = await app.Client.GetAsync($"/api/v1/foods/search?q={query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FoodSearchPayload>(JsonOptions);

        Assert.NotNull(payload);

        // «гречка» - синоним, а не название: проверяем, что поиск идёт и по
        // FoodAlias, и отдаёт основное название продукта.
        var item = Assert.Single(payload.Items);
        Assert.Equal("Крупа гречневая, варёная", item.Name);
        Assert.Equal(FoodState.Cooked, item.State);
        Assert.Equal(110, item.KcalPer100g, precision: 3);
        Assert.Equal(4.2, item.ProteinPer100g, precision: 3);
    }

    [Fact]
    public async Task Search_returns_empty_list_when_nothing_matches()
    {
        var connectionString = await SeedAsync();

        await using var app = await StartApiAgainstAsync(connectionString);

        var query = Uri.EscapeDataString("ананас");
        using var response = await app.Client.GetAsync($"/api/v1/foods/search?q={query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<FoodSearchPayload>(JsonOptions);

        Assert.NotNull(payload);
        Assert.Empty(payload.Items);
    }

    [Fact]
    public async Task Search_rejects_too_short_query()
    {
        var connectionString = await SeedAsync();

        await using var app = await StartApiAgainstAsync(connectionString);

        using var response = await app.Client.GetAsync("/api/v1/foods/search?q=я");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Task<ApiApp> StartApiAgainstAsync(string connectionString) =>
        ApiApp.StartAsync(builder => builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Norma"] = connectionString,
            }));

    /// <summary>Чистые таблицы и минимальный набор продуктов для поиска.</summary>
    private async Task<string> SeedAsync()
    {
        var connectionString = database.ConnectionString;
        var options = new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(connectionString).Options;

        await database.ResetAsync();

        await using var db = new NormaDbContext(options);

        var buckwheat = new Food
        {
            Id = Guid.NewGuid(),
            Source = FoodSource.Usda,
            SourceId = "170286",
            State = FoodState.Cooked,
            KcalPer100g = 110,
            ProteinPer100g = 4.2,
            FatPer100g = 1.1,
            CarbsPer100g = 21.3,
        };
        buckwheat.Names.Add(new FoodName
        {
            Id = Guid.NewGuid(),
            Locale = "ru",
            Name = "Крупа гречневая, варёная",
            IsPrimary = true,
        });
        buckwheat.Aliases.Add(new FoodAlias { Id = Guid.NewGuid(), Alias = "гречка" });

        var chicken = new Food
        {
            Id = Guid.NewGuid(),
            Source = FoodSource.Usda,
            SourceId = "171477",
            State = FoodState.Raw,
            KcalPer100g = 120,
            ProteinPer100g = 22.5,
            FatPer100g = 2.6,
            CarbsPer100g = 0,
        };
        chicken.Names.Add(new FoodName
        {
            Id = Guid.NewGuid(),
            Locale = "ru",
            Name = "Куриная грудка, сырая",
            IsPrimary = true,
        });

        db.Foods.AddRange(buckwheat, chicken);
        await db.SaveChangesAsync();

        return connectionString;
    }

    private sealed record FoodSearchPayload(IReadOnlyList<FoodSearchItemDto> Items);

    private sealed record FoodSearchItemDto(
        Guid Id,
        string Name,
        FoodState State,
        double KcalPer100g,
        double ProteinPer100g,
        double FatPer100g,
        double CarbsPer100g);
}
