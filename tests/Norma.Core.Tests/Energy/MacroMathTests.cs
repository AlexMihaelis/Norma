using Norma.Core.Energy;

namespace Norma.Core.Tests.Energy;

/// <summary>
/// Контракт БЖУ: белок от веса с потолком 35 % калорий, жиры 25 % калорий,
/// углеводы — остаток. Цифры посчитаны вручную и закреплены до реализации.
/// </summary>
public sealed class MacroMathTests
{
    private static readonly Profile MihAD =
        new(Sex.Female, AgeYears: 25, HeightCm: 175, WeightKg: 70, ActivityLevel.Sedentary);

    private static readonly Profile OthUs =
        new(Sex.Male, AgeYears: 26, HeightCm: 181, WeightKg: 97, ActivityLevel.Sedentary);

    [Fact]
    public void Macros_for_female_lose_at_1530_kcal()
    {
        // Белок: 1.8×70 = 126 г (не упирается в потолок 133.9 г).
        // Жиры: 1530×0.25 = 382.5 ккал / 9 = 42.5 → 43 г.
        // Углеводы: (1530 − 504 − 382.5) / 4 = 160.875 → 161 г.
        var macros = MacroMath.ForTarget(MihAD, Goal.Lose, targetKcal: 1530);

        Assert.Equal(126, macros.ProteinG);
        Assert.Equal(43, macros.FatG);
        Assert.Equal(161, macros.CarbsG);
    }

    [Fact]
    public void Macros_for_male_lose_at_2100_kcal()
    {
        // Белок: 1.8×97 = 174.6 г (потолок 183.75 не мешает) → 175 г.
        // Жиры: 525 ккал / 9 = 58.33 → 58 г.
        // Углеводы: (2100 − 698.4 − 525) / 4 = 219.15 → 219 г.
        var macros = MacroMath.ForTarget(OthUs, Goal.Lose, targetKcal: 2100);

        Assert.Equal(175, macros.ProteinG);
        Assert.Equal(58, macros.FatG);
        Assert.Equal(219, macros.CarbsG);
    }

    [Fact]
    public void Macros_at_maintenance_use_1_6_g_per_kg()
    {
        // Белок: 1.6×70 = 112 г.
        // Жиры: 1810×0.25 / 9 = 50.28 → 50 г.
        // Углеводы: (1810 − 448 − 452.5) / 4 = 227.375 → 227 г.
        var macros = MacroMath.ForTarget(MihAD, Goal.Maintain, targetKcal: 1810);

        Assert.Equal(112, macros.ProteinG);
        Assert.Equal(50, macros.FatG);
        Assert.Equal(227, macros.CarbsG);
    }

    [Fact]
    public void Protein_cap_keeps_carbs_non_negative_for_very_heavy_profile()
    {
        // 200 кг при цели 1500 ккал: 1.8×200 = 360 г белка съели бы почти всю норму.
        // Потолок 35 % даёт 131.25 → 131 г, углеводы остаются положительными.
        var heavy = new Profile(Sex.Male, AgeYears: 30, HeightCm: 190, WeightKg: 200, ActivityLevel.Sedentary);

        var macros = MacroMath.ForTarget(heavy, Goal.Lose, targetKcal: 1500);

        Assert.Equal(131, macros.ProteinG);
        Assert.Equal(42, macros.FatG);
        Assert.Equal(150, macros.CarbsG);
        Assert.True(macros.CarbsG > 0);
    }

    [Theory]
    [InlineData(1530)]
    [InlineData(1260)]
    [InlineData(1810)]
    public void Macro_calories_match_target_within_rounding(int targetKcal)
    {
        var macros = MacroMath.ForTarget(MihAD, Goal.Lose, targetKcal);
        var macroKcal = (macros.ProteinG * 4) + (macros.FatG * 9) + (macros.CarbsG * 4);

        // Округление каждого БЖУ до целых даёт отклонение до ~10 ккал — это нормально.
        Assert.InRange(macroKcal, targetKcal - 10, targetKcal + 10);
    }
}
