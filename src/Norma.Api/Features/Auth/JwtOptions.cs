namespace Norma.Api.Features.Auth;

/// <summary>Настройки JWT; ключ в продакшене приходит из окружения, а не из репозитория.</summary>
public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "norma";

    public string Audience { get; init; } = "norma-web";

    public string Key { get; init; } = string.Empty;

    public int LifetimeMinutes { get; init; } = 7 * 24 * 60;
}
