using Norma.Api.Data.Entities;
using Norma.Api.Features.Images;

namespace Norma.Api.Features.Auth.Me;

/// <summary>
/// Собирает ответ «кто я». Аватар отдается как data URL: картинки маленькие,
/// а отдельный публичный маршрут за картинкой не нужен.
/// </summary>
internal static class MeResponseFactory
{
    public static MeResponse From(User user) =>
        new(user.Id, user.Email, user.Nickname, PictureData.ToDataUrl(user.AvatarContentType, user.AvatarBytes));
}
