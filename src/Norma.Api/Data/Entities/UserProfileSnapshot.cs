using Norma.Core.Energy;

namespace Norma.Api.Data.Entities;

/// <summary>
/// Снимок профиля на дату. Профиль хранится одной текущей записью, поэтому без
/// снимков нельзя честно сказать, какая цель была у пользователя в прошлом.
/// </summary>
/// <remarks>
/// Снимок пишется при каждом сохранении профиля и действует с даты сохранения:
/// статистика берет последний снимок не позже дня, за который считает.
/// </remarks>
public sealed class UserProfileSnapshot
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>С какой даты действует этот снимок.</summary>
    public DateOnly EffectiveFrom { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Sex? Sex { get; set; }

    public DateOnly? BirthDate { get; set; }

    public double? HeightCm { get; set; }

    public ActivityLevel? Activity { get; set; }

    public Goal? Goal { get; set; }

    public double? TargetWeightKg { get; set; }

    public double? KgPerWeek { get; set; }

    public User? User { get; set; }
}
