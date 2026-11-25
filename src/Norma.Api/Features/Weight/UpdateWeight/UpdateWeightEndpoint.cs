using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.Weight.UpdateWeight;

/// <summary>
/// Срез «исправить замер»: пользователь правит опечатку в весе или дату. Замер
/// ищется только среди своих - чужой отдает 404.
/// </summary>
public static class UpdateWeightEndpoint
{
    private const double MinKg = 20;
    private const double MaxKg = 500;

    public static IEndpointRouteBuilder MapUpdateWeight(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPut("/weight/{id:guid}", Handle)
            .WithName("UpdateWeight")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<WeightResponse>, NotFound, ValidationProblem, UnauthorizedHttpResult>> Handle(
        Guid id,
        UpdateWeightRequest? request,
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

        if (request?.Date is DateOnly date && date > today)
        {
            errors["date"] = ["Дата не может быть в будущем."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var measurement = await db.WeightMeasurements
            .FirstOrDefaultAsync(
                candidate => candidate.Id == id && candidate.UserId == userId.Value,
                cancellationToken);

        if (measurement is null)
        {
            return TypedResults.NotFound();
        }

        measurement.Kg = request!.Kg!.Value;

        if (request.Date is not null)
        {
            measurement.Date = request.Date.Value;
        }

        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new WeightResponse(measurement.Id, measurement.Date, measurement.Kg));
    }
}

/// <summary>Тело запроса: новый вес и необязательная дата (без даты остается прежняя).</summary>
public sealed record UpdateWeightRequest(double? Kg, DateOnly? Date);