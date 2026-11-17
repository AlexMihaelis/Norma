using Microsoft.EntityFrameworkCore;
using Norma.Api.Data.Entities;

namespace Norma.Api.Data;

/// <summary>
/// Контекст базы данных. EF Core и есть доступ к данным: generic-репозиториев
/// нет, срезы работают с <see cref="DbContext"/> напрямую.
/// </summary>
public sealed class NormaDbContext(DbContextOptions<NormaDbContext> options) : DbContext(options)
{
    public DbSet<Food> Foods => Set<Food>();

    public DbSet<FoodName> FoodNames => Set<FoodName>();

    public DbSet<FoodAlias> FoodAliases => Set<FoodAlias>();

    public DbSet<User> Users => Set<User>();

    public DbSet<WeightMeasurement> WeightMeasurements => Set<WeightMeasurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Все IEntityTypeConfiguration из этой сборки: конфигурации лежат рядом с
        // сущностями, а не размазаны по всему проекту.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NormaDbContext).Assembly);
    }
}
