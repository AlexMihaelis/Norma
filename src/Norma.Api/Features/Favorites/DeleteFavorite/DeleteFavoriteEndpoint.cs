using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Favorites.DeleteFavorite;

/// <summary>
/// Срез «убрать из избранного»: удаляет только отметку, каталог и дневник не трогает.
/// </summary>
public static class DeleteFavoriteEndpoint
{
    public static IEndpointRouteBuilder MapDeleteFavorite(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapDelete("/favorites/{foodId:guid}", Handle)
            .WithName("DeleteFavorite")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> Handle(
        Guid foodId,
        ClaimsPrincipal principal,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        // Условие по пользователю в запросе: чужую отметку не удалить даже по id.
        var favorite = await db.FavoriteFoods
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.FoodId == foodId,
                cancellationToken);

        if (favorite is null)
        {
            return TypedResults.NotFound();
        }

        db.FavoriteFoods.Remove(favorite);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}
