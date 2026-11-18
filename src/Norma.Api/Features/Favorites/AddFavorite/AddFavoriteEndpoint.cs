using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Favorites.AddFavorite;

/// <summary>
/// Срез «добавить в избранное»: отметка ставится вручную и только для своего
/// списка. Повторное добавление не создает вторую запись.
/// </summary>
/// <remarks>
/// Ответы: 201 - отметка создана, 200 - продукт уже был в избранном (idempotent),
/// 404 - такого продукта нет в каталоге.
/// </remarks>
public static class AddFavoriteEndpoint
{
    public static IEndpointRouteBuilder MapAddFavorite(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/favorites", Handle)
            .WithName("AddFavorite")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<FavoriteResponse>, Ok<FavoriteResponse>, NotFound, ValidationProblem, UnauthorizedHttpResult>> Handle(
        AddFavoriteRequest? request,
        ClaimsPrincipal principal,
        NormaDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (request?.FoodId is null || request.FoodId == Guid.Empty)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["foodId"] = ["Укажите продукт из справочника."],
            });
        }

        var foodId = request.FoodId.Value;

        var food = await db.Foods
            .AsNoTracking()
            .Include(candidate => candidate.Names)
            .FirstOrDefaultAsync(candidate => candidate.Id == foodId, cancellationToken);

        if (food is null)
        {
            return TypedResults.NotFound();
        }

        var existing = await db.FavoriteFoods
            .AsNoTracking()
            .FirstOrDefaultAsync(
                favorite => favorite.UserId == userId && favorite.FoodId == foodId,
                cancellationToken);

        if (existing is not null)
        {
            return TypedResults.Ok(FavoriteMapping.ToResponse(existing, food));
        }

        var favorite = new FavoriteFood
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            FoodId = foodId,
            CreatedAt = clock.GetUtcNow(),
        };

        db.FavoriteFoods.Add(favorite);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Два одновременных «в избранное»: второй запрос не создает дубль,
            // а возвращает уже существующую отметку.
            db.Entry(favorite).State = EntityState.Detached;

            var raced = await db.FavoriteFoods
                .AsNoTracking()
                .FirstAsync(
                    candidate => candidate.UserId == userId && candidate.FoodId == foodId,
                    cancellationToken);

            return TypedResults.Ok(FavoriteMapping.ToResponse(raced, food));
        }

        return TypedResults.Created(
            $"/api/v1/favorites/{foodId}",
            FavoriteMapping.ToResponse(favorite, food));
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
