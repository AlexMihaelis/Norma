namespace Norma.Api.Features.Auth;

/// <summary>Ответ регистрации и входа: токен и данные пользователя.</summary>
public sealed record AuthResponse(Guid UserId, string Email, string Token);

/// <summary>Ответ защищённого эндпоинта «кто я».</summary>
public sealed record MeResponse(Guid UserId, string Email);
