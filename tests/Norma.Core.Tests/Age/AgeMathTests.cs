using Norma.Core.Age;

namespace Norma.Core.Tests.Age;

/// <summary>Возраст в полных годах: границы дня рождения и 29 февраля.</summary>
public sealed class AgeMathTests
{
    [Fact]
    public void Birthday_today_counts_full_year()
    {
        Assert.Equal(26, AgeMath.Years(new DateOnly(2000, 5, 10), new DateOnly(2026, 5, 10)));
    }

    [Fact]
    public void Day_before_birthday_is_previous_age()
    {
        Assert.Equal(25, AgeMath.Years(new DateOnly(2000, 5, 10), new DateOnly(2026, 5, 9)));
    }

    [Fact]
    public void Newborn_is_zero()
    {
        var today = new DateOnly(2026, 11, 17);

        Assert.Equal(0, AgeMath.Years(today, today));
    }

    [Fact]
    public void Leap_day_birthday_is_not_counted_before_march_first()
    {
        var bornOnLeapDay = new DateOnly(2000, 2, 29);

        // 29 февраля в невисокосный год не наступает: до 1 марта возраст прежний.
        Assert.Equal(25, AgeMath.Years(bornOnLeapDay, new DateOnly(2026, 2, 28)));
        Assert.Equal(26, AgeMath.Years(bornOnLeapDay, new DateOnly(2026, 3, 1)));
    }
}
