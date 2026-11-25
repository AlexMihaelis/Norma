using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Weight.AddWeight;

/// <summary>Срез «записать вес»: замер сохраняется за текущим пользователем.</summary>
public static class AddWeightEndpoint
{
    private const double MinKg = 20;
    private const double MaxKg = 500;

    public static IEndpointRouteBuilder MapAddWeight(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/weight", Handle)
            .WithName("AddWeight")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<WeightResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        AddWeightRequest? request,
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

        if (request?.Kg is not double kg || kg < MinKg || kg > MaxKg)
        {
            errors["kg"] = [$"Вес должен быть в диапазоне {MinKg}-{MaxKg} кг."];
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var date = request?.Date ?? today;

        // Допуск в сутки: у пользователя может быть уже следующий день по местному времени.
        if (date > today.AddDays(1))
        {
            errors["date"] = ["Дата не может быть в будущем."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var entity = new WeightMeasurement
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Date = date,
            Kg = request!.Kg!.Value,
            CreatedAt = clock.GetUtcNow(),
        };

        db.WeightMeasurements.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/weight/{entity.Id}",
            new WeightResponse(entity.Id, entity.Date, entity.Kg));
    }
}

/// <summary>Тело запроса: дата (по умолчанию сегодня) и вес.</summary>
public sealed record AddWeightRequest(DateOnly? Date, double? Kg);
