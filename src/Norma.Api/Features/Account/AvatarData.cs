namespace Norma.Api.Features.Account;

/// <summary>
/// Разбор аватара из data URL. Аватар хранится в базе целиком, поэтому формат и
/// размер проверяются до сохранения.
/// </summary>
internal static class AvatarData
{
    /// <summary>Потолок размера картинки: больше держать в строке таблицы незачем.</summary>
    public const int MaxBytes = 256 * 1024;

    private static readonly string[] AllowedMimeTypes = ["image/png", "image/jpeg", "image/webp"];

    public static bool TryParse(string? dataUrl, out string mime, out byte[] bytes, out string? error)
    {
        mime = string.Empty;
        bytes = [];
        error = null;

        if (string.IsNullOrWhiteSpace(dataUrl)
            || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            error = "Нужно изображение в формате data URL (data:image/png;base64,...).";
            return false;
        }

        var separator = dataUrl.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);

        if (separator < "data:".Length + 1)
        {
            error = "Не удалось разобрать data URL: ожидается MIME-тип и ;base64,.";
            return false;
        }

        mime = dataUrl["data:".Length..separator];

        if (!AllowedMimeTypes.Contains(mime, StringComparer.OrdinalIgnoreCase))
        {
            error = "Формат не поддерживается: png, jpeg или webp.";
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(dataUrl[(separator + ";base64,".Length)..]);
        }
        catch (FormatException)
        {
            error = "Содержимое не похоже на base64.";
            return false;
        }

        if (bytes.Length == 0)
        {
            error = "Пустая картинка.";
            return false;
        }

        if (bytes.Length > MaxBytes)
        {
            error = $"Картинка должна быть не больше {MaxBytes / 1024} КБ.";
            return false;
        }

        return true;
    }
}