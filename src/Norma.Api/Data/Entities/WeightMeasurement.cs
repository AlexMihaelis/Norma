namespace Norma.Api.Data.Entities;

/// <summary>Один замер веса пользователя: к какой дате и сколько килограммов.</summary>
public sealed class WeightMeasurement
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateOnly Date { get; set; }

    public double Kg { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public User? User { get; set; }
}
