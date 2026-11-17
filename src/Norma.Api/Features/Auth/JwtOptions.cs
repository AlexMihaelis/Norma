namespace Norma.Api.Features.Auth;

/// <summary>Настройки выпуска и проверки JWT. Читаются из секции <c>Jwt</c>.</summary>
public sealed record JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "norma";

    public string Audience { get; init; } = "norma-web";

    /// <summary>
    /// Ключ подписи. В разработке - заглушка, в продакшене обязан приходить из
    /// переменной окружения, а не из репозитория.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public int LifetimeMinutes { get; init; } = 7 * 24 * 60;
}
