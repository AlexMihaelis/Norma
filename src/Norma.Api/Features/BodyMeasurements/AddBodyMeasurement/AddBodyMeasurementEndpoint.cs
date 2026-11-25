using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;

namespace Norma.Api.Features.BodyMeasurements.AddBodyMeasurement;

/// <summary>
/// Запись обхвата: вид из перечисления, сантиметры 20-300, дата не в будущем.
/// Повторный замер того же вида в тот же день перезаписывает значение.
/// </summary>
public static class AddBodyMeasurementEndpoint
{
    private const double MinCm = 20;
    private const double MaxCm = 300;

    public static IEndpointRouteBuilder MapAddBodyMeasurement(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/body-measurements", Handle)
            .WithName("AddBodyMeasurement")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<BodyMeasurementResponse>, Created<BodyMeasurementResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        BodyMeasurementRequest? request,
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

        if (!Enum.TryParse<BodyMeasurementKind>(request?.Kind, ignoreCase: true, out var kind)
            || !Enum.IsDefined(kind))
        {
            errors["kind"] = [$"Укажи вид замера: {string.Join(", ", Enum.GetNames<BodyMeasurementKind>())}."];
        }

        if (request?.Cm is not double cm || cm < MinCm || cm > MaxCm)
        {
            errors["cm"] = [$"Обхват должен быть в диапазоне {MinCm}-{MaxCm} см."];
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

        var existing = await db.BodyMeasurements.FirstOrDefaultAsync(
            measurement => measurement.UserId == userId.Value
                && measurement.Date == date
                && measurement.Kind == kind,
            cancellationToken);

        if (existing is not null)
        {
            existing.Cm = request!.Cm!.Value;
            await db.SaveChangesAsync(cancellationToken);

            return TypedResults.Ok(new BodyMeasurementResponse(existing.Id, existing.Date, existing.Kind, existing.Cm));
        }

        var entity = new BodyMeasurement
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Date = date,
            Kind = kind,
            Cm = request!.Cm!.Value,
            CreatedAt = clock.GetUtcNow(),
        };

        db.BodyMeasurements.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var response = new BodyMeasurementResponse(entity.Id, entity.Date, entity.Kind, entity.Cm);

        return TypedResults.Created($"/api/v1/body-measurements/{entity.Id}", response);
    }
}