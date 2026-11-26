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

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<UserProfileSnapshot> UserProfileSnapshots => Set<UserProfileSnapshot>();

    public DbSet<WaterIntake> WaterIntakes => Set<WaterIntake>();

    public DbSet<ActivityRecord> ActivityRecords => Set<ActivityRecord>();

    public DbSet<Meal> Meals => Set<Meal>();

    public DbSet<MealItem> MealItems => Set<MealItem>();

    public DbSet<FavoriteFood> FavoriteFoods => Set<FavoriteFood>();

    public DbSet<BodyMeasurement> BodyMeasurements => Set<BodyMeasurement>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Все IEntityTypeConfiguration из этой сборки: конфигурации лежат рядом с
        // сущностями, а не размазаны по всему проекту.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NormaDbContext).Assembly);
    }
}
