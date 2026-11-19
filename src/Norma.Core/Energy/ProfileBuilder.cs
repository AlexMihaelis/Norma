using Norma.Core.Age;

namespace Norma.Core.Energy;

/// <summary>
/// Сборка профиля для расчёта на конкретную дату. Чистая функция: возраст
/// считается на дату расчёта, а не на сегодня.
/// </summary>
/// <remarks>
/// Нужна статистике: цель за прошлый день должна считаться с тем весом и с тем
/// возрастом, которые были на тот день, а не с сегодняшними.
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
