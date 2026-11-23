namespace Norma.Api.Data.Entities;

/// <summary>Пользователь приложения; пароль хранится только в виде хэша.</summary>
public sealed class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Ник для интерфейса; пусто - показываем email.</summary>
    public string? Nickname { get; set; }

    /// <summary>MIME-тип аватара (image/png, image/jpeg, image/webp).</summary>
    public string? AvatarContentType { get; set; }

    /// <summary>Аватар целиком в базе: картинка маленькая, отдельное хранилище не нужно.</summary>
    public byte[]? AvatarBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
