namespace Norma.Api.Data.Entities;

/// <summary>Пользователь приложения; пароль хранится только в виде хэша.</summary>
public sealed class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
