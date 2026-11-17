using System.Net.Mail;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Auth.Register;

/// <summary>Регистрация по email и паролю; сразу выдаёт JWT.</summary>
public static class RegisterEndpoint
{
    private const int MinPasswordLength = 8;

    public static IEndpointRouteBuilder MapRegister(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/auth/register", Handle)
            .WithName("Register")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<Results<Created<AuthResponse>, ValidationProblem, Conflict<ProblemDetails>>> Handle(
        RegisterRequest? request,
        NormaDbContext db,
        IPasswordHasher<User> passwordHasher,
        JwtTokenIssuer tokens,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var email = request?.Email?.Trim().ToLowerInvariant();
        var password = request?.Password;

        if (string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email, out _))
        {
            errors["email"] = ["Нужен корректный email."];
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            errors["password"] = [$"Пароль должен быть не короче {MinPasswordLength} символов."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "Пользователь с таким email уже существует.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var entity = new User
        {
            Id = Guid.NewGuid(),
            Email = email!,
            CreatedAt = clock.GetUtcNow(),
        };

        entity.PasswordHash = passwordHasher.HashPassword(entity, password!);

        db.Users.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        var token = tokens.Issue(entity.Id, entity.Email);

        return TypedResults.Created("/api/v1/me", new AuthResponse(entity.Id, entity.Email, token));
    }
}

/// <summary>Тело запроса регистрации.</summary>
public sealed record RegisterRequest(string? Email, string? Password);
