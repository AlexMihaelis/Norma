namespace Norma.Api.Data.Entities;

/// <summary>Один прием воды: к какой дате и сколько миллилитров.</summary>
public sealed class WaterIntake
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly Date { get; set; }

    public int Ml { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
