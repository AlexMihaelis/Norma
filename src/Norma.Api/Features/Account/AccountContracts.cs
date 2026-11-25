namespace Norma.Api.Features.Account;

/// <summary>Тело изменения ника. Пустая строка стирает ник (fallback - email).</summary>
public sealed record UpdateNicknameRequest(string? Nickname);

/// <summary>Тело загрузки аватара: картинка как data URL.</summary>
public sealed record UpdateAvatarRequest(string? DataUrl);