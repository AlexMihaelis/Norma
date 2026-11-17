using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Auth.Login;

/// <summary>Вход по email и паролю; выдаёт тот же JWT, что и регистрация.</summary>
public static class LoginEndpoint
{
    public static IEndpointRouteBuilder MapLogin(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/auth/login", Handle)
            .WithName("Login")
            .AllowAnonymous();

        return endpoints;
    }

    private static async Task<Results<Ok<AuthResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        LoginRequest? request,
        NormaDbContext db,
        IPasswordHasher<User> passwordHasher,
        JwtTokenIssuer tokens,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            errors["email"] = ["Укажи email."];
        }

        if (string.IsNullOrWhiteSpace(request?.Password))
        {
            errors["password"] = ["Укажи пароль."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var email = request!.Email!.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Email == email, cancellationToken);

        // Одинаковый 401 для «нет пользователя» и «неверный пароль»: не даём
        // перебором выяснять, какие адреса зарегистрированы.
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);

        if (verification == PasswordVerificationResult.Failed)
        {
            return TypedResults.Unauthorized();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(new AuthResponse(user.Id, user.Email, tokens.Issue(user.Id, user.Email)));
    }
}

/// <summary>Тело запроса входа.</summary>
public sealed record LoginRequest(string? Email, string? Password);
