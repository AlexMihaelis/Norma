using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Norma.Api.Features.Auth;

/// <summary>
/// Выпуск JWT-токенов. Отдельный сервис в DI: регистрация и вход используют один
/// и тот же код подписи, а тесты могут подменить время через <see cref="TimeProvider"/>.
/// </summary>
public sealed class JwtTokenIssuer(JwtOptions options, TimeProvider timeProvider)
{
    public string Issue(Guid userId, string email)
    {
        var claims = new[]
        {
            // "sub" - стандартный claim идентификатора пользователя.
            new Claim("sub", userId.ToString()),
            new Claim("email", email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            SecurityAlgorithms.HmacSha256);

        var now = timeProvider.GetUtcNow();

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(options.LifetimeMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
