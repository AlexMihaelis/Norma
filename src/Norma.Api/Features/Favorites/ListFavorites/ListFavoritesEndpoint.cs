using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Favorites.ListFavorites;

/// <summary>
/// Срез «список избранного»: свои продукты, сначала недавно добавленные - на
/// экране быстрого ввода нужны последние.
/// </summary>
public static class ListFavoritesEndpoint
{
    /// <summary>Потолок списка: избранное - быстрый доступ, а не каталог.</summary>
    private const int MaxItems = 200;

    public static IEndpointRouteBuilder MapListFavorites(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/favorites", Handle)
            .WithName("ListFavorites")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<FavoriteListResponse>, UnauthorizedHttpResult>> Handle(
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        // Один запрос: избранное вместе с продуктом и названием. Продукт читается
        // без отслеживания - он только показывается.
        var items = await db.FavoriteFoods
            .AsNoTracking()
            .Where(favorite => favorite.UserId == userId)
            .Include(favorite => favorite.Food)
            .ThenInclude(food => food!.Names)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new FavoriteListResponse(
            items
                .Where(favorite => favorite.Food is not null)
                .Select(favorite => FavoriteMapping.ToResponse(favorite, favorite.Food!))
                .ToList()));
    }
}
