using Norma.Core.Energy;

namespace Norma.Api.Data.Entities;

/// <summary>
/// Профиль пользователя: пол, дата рождения, рост, активность и цель. Поля
/// nullable - профиль можно заполнять постепенно.
/// </summary>
public sealed class UserProfile
{
    public Guid UserId { get; set; }

    public Sex? Sex { get; set; }

    public DateOnly? BirthDate { get; set; }

    public double? HeightCm { get; set; }

    public ActivityLevel? Activity { get; set; }

    public Goal? Goal { get; set; }

    public double? KgPerWeek { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public User? User { get; set; }
}
