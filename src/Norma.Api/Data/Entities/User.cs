namespace Norma.Api.Data.Entities;

/// <summary>Пользователь приложения. Пароль хранится только в виде хэша.</summary>
public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>Email в нижнем регистре: так уникальность не зависит от регистра.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Хэш пароля (PBKDF2 с солью), не сам пароль.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
