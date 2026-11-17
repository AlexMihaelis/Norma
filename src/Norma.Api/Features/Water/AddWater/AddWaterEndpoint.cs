using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Water.AddWater;

/// <summary>Срез «записать воду»: прием сохраняется за текущим пользователем.</summary>
public static class AddWaterEndpoint
{
    private const int MinMl = 50;
    private const int MaxMl = 3000;

    public static IEndpointRouteBuilder MapAddWater(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/water", Handle)
            .WithName("AddWater")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<WaterIntakeResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        AddWaterRequest? request,
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

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request?.Ml is not int ml || ml < MinMl || ml > MaxMl)
        {
            errors["ml"] = [$"Объем за прием должен быть в диапазоне {MinMl}-{MaxMl} мл."];
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var date = request?.Date ?? today;

        if (date > today)
        {
            errors["date"] = ["Дата не может быть в будущем."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new WaterIntake
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Date = date,
            Ml = request!.Ml!.Value,
            CreatedAt = clock.GetUtcNow(),
        };

        db.WaterIntakes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/water/{entity.Id}",
            new WaterIntakeResponse(entity.Id, entity.Date, entity.Ml));
    }
}
