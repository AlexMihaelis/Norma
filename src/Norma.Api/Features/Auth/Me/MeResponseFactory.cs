using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Auth.Me;

/// <summary>
/// Собирает ответ «кто я». Аватар отдается как data URL: картинки маленькие,
/// а отдельный публичный маршрут за картинкой не нужен.
/// </summary>
internal static class MeResponseFactory
{
    public static MeResponse From(User user) =>
        new(user.Id, user.Email, user.Nickname, AvatarDataUrl(user));

    private static string? AvatarDataUrl(User user)
    {
        if (user.AvatarBytes is null || user.AvatarContentType is null)
        {
            return null;
        }

        return $"data:{user.AvatarContentType};base64,{Convert.ToBase64String(user.AvatarBytes)}";
    }
}