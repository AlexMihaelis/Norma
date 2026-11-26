using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Recipes.ListRecipeTags;

/// <summary>
/// Срез «справочник меток»: панель фильтров и форма рецепта берут список отсюда,
/// поэтому подписи и коды не расходятся между экранами.
/// </summary>
public static class ListRecipeTagsEndpoint
{
    public static IEndpointRouteBuilder MapListRecipeTags(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/recipes/tags", Handle)
            .WithName("ListRecipeTags")
            .RequireAuthorization();

        return endpoints;
    }

    private static Results<Ok<RecipeTagListResponse>, UnauthorizedHttpResult> Handle(ClaimsPrincipal principal) =>
        principal.GetUserId() is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new RecipeTagListResponse(RecipeTagCatalog.Build()));
}
