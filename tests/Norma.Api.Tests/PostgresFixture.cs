using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Testcontainers.PostgreSql;

namespace Norma.Api.Tests;

/// <summary>
/// Один контейнер Postgres на все интеграционные тесты. Раньше каждый тест
/// поднимал свой: Docker Desktop не успевал, и падала готовность контейнера
/// (TimeoutException при exec). Теперь контейнер и миграции - один раз на прогон,
/// а между тестами данные очищаются.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();

    /// <summary>
    /// Чистые таблицы перед тестом. CASCADE снимает внешние ключи, поэтому
    /// порядок таблиц в списке не важен; идентификаторы задаем в коде, sequence
    /// не нужны.
    /// </summary>
    public async Task ResetAsync()
    {
        await using var db = CreateContext();

        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE users, user_profiles, weight_measurements, water_intakes, "
            + "activity_records, meals, meal_items, favorite_foods, recipes, recipe_ingredients, "
            + "foods, food_names, food_aliases "
            + "CASCADE");
    }

    public NormaDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<NormaDbContext>().UseNpgsql(ConnectionString).Options);
}

/// <summary>
/// Общая коллекция: все классы с этим атрибутом работают с одним контейнером и
/// выполняются последовательно - иначе тесты мешали бы друг другу в одной базе.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
