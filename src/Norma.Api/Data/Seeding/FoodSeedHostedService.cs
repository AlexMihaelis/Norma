using Microsoft.EntityFrameworkCore;

namespace Norma.Api.Data.Seeding;

/// <summary>
/// Загружает стартовый набор продуктов после старта приложения. Отдельный
/// hosted-сервис, потому что старт приложения синхронный, а работа с базой - нет.
/// </summary>
internal sealed class FoodSeedHostedService(
    IServiceScopeFactory scopes,
    ILogger<FoodSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NormaDbContext>();

            var added = await FoodSeeder.SeedAsync(db, cancellationToken);

            if (added > 0)
            {
                logger.LogInformation("Стартовый набор продуктов загружен: {Count} позиций.", added);
            }
        }
        catch (Exception exception)
        {
            // Без набора продуктов приложение работает, просто искать нечего.
            logger.LogWarning(exception, "Стартовый набор продуктов не загружен.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}