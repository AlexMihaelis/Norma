namespace Norma.Core.Age;

/// <summary>
/// Возраст в полных годах. Чистая функция: «сегодня» приходит параметром, чтобы
/// результат не зависел от часов машины и его можно было проверить тестом.
/// </summary>
public static class AgeMath
{
    /// <summary>
    /// Сколько полных лет исполнилось на дату <paramref name="today"/>.
    /// День рождения, который ещё не наступил в этом году, не засчитывается.
    /// </summary>
    public static int Years(DateOnly birthDate, DateOnly today)
    {
        var age = today.Year - birthDate.Year;

        // Год рождения вычтен, но день рождения в этом году мог ещё не наступить:
        // тогда полных лет на один меньше.
        return birthDate > today.AddYears(-age) ? age - 1 : age;
    }
}
