namespace Norma.Api.Data.Entities;

/// <summary>Что именно измеряем сантиметровой лентой.</summary>
public enum BodyMeasurementKind
{
    Chest,
    Waist,
    Hips,
    Thigh,
    Shoulder,
    Calf,
}

/// <summary>
/// Обхват тела на дату. Один замер каждого вида в день: повторная запись
/// перезаписывает значение, а не плодит дубли.
/// </summary>
public sealed class BodyMeasurement
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly Date { get; set; }

    public BodyMeasurementKind Kind { get; set; }

    public double Cm { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}