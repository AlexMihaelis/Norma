using Norma.Core.Age;

namespace Norma.Core.Energy;

/// <summary>
/// Сборка профиля на конкретную дату: возраст считается на дату расчёта, а не на
/// сегодня.
/// </summary>
/// <remarks>
/// Нужна статистике: цель за прошлый день считается с весом и возрастом того дня.
/// </remarks>
public static class ProfileBuilder
{
    public static Profile From(
        Sex sex,
        DateOnly birthDate,
        DateOnly onDate,
        double heightCm,
        double weightKg,
        ActivityLevel activity) =>
        new(sex, AgeMath.Years(birthDate, onDate), heightCm, weightKg, activity);
}
