using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;
using Norma.Core.Activity;

namespace Norma.Api.Features.Activity.AddActivity;

/// <summary>
/// Срез «записать активность»: калории считаются от последнего веса и
/// сохраняются снимком, чтобы история не менялась от новых замеров.
/// </summary>
public static class AddActivityEndpoint
{
    private const int MinMinutes = 1;
    private const int MaxMinutes = 1440;

    public static IEndpointRouteBuilder MapAddActivity(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/activity", Handle)
            .WithName("AddActivity")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<ActivityResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        AddActivityRequest? request,
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
        var kind = ParseKind(request?.Kind, errors);

        if (request?.Minutes is not int minutes || minutes < MinMinutes || minutes > MaxMinutes)
        {
            errors["minutes"] = [$"Длительность должна быть в диапазоне {MinMinutes}-{MaxMinutes} минут."];
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

        var weightKg = await db.WeightMeasurements
            .Where(measurement => measurement.UserId == userId)
            .OrderByDescending(measurement => measurement.Date)
            .ThenByDescending(measurement => measurement.CreatedAt)
            .Select(measurement => (double?)measurement.Kg)
            .FirstOrDefaultAsync(cancellationToken);

        // Без веса расход не посчитать: формула MET умножает на вес.
        if (weightKg is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["weight"] = ["Запишите вес - расход считается от последнего замера."],
            });
        }

        var kcal = (int)Math.Round(
            ActivityMath.Calories(kind!.Value, weightKg.Value, request!.Minutes!.Value),
            MidpointRounding.AwayFromZero);

        var record = new ActivityRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            Date = date,
            Kind = kind.Value,
            Minutes = request.Minutes.Value,
            Kcal = kcal,
            CreatedAt = clock.GetUtcNow(),
        };

        db.ActivityRecords.Add(record);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/v1/activity/{record.Id}",
            new ActivityResponse(record.Id, record.Date, record.Kind, record.Minutes, record.Kcal));
    }

    private static ActivityKind? ParseKind(string? raw, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors["kind"] = [$"Укажите вид активности: {string.Join(", ", Enum.GetNames<ActivityKind>())}."];
            return null;
        }

        if (!Enum.TryParse<ActivityKind>(raw, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            errors["kind"] = [$"Недопустимый вид: ожидается {string.Join(", ", Enum.GetNames<ActivityKind>())}."];
            return null;
        }

        return kind;
    }
}
