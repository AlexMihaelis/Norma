using Norma.Core.Energy;

namespace Norma.Core.Tests.Energy;

/// <summary>
/// Контракт расчёта нормы. Числа фиксированы до реализации: их сверяет человек на
/// независимом калькуляторе, и только потом пишется код. Так ошибка в формуле
/// находится в тесте, а не в проде.
/// </summary>
public sealed class EnergyMathTests
{
    // Профиль «MihA_D»: женский, 25 лет, 175 см, 70 кг, сидячая активность.
    private static readonly Profile MihAD =
        new(Sex.Female, AgeYears: 25, HeightCm: 175, WeightKg: 70, ActivityLevel.Sedentary);

    // Профиль «OthUs»: мужской, 26 лет, 181 см, 97 кг, сидячая активность.
    private static readonly Profile OthUs =
        new(Sex.Male, AgeYears: 26, HeightCm: 181, WeightKg: 97, ActivityLevel.Sedentary);

    [Fact]
    public void Bmr_female_uses_mifflin_st_jeor()
    {
        // 10×70 + 6.25×175 − 5×25 − 161 = 1507.75
        var bmr = EnergyMath.BasalMetabolicRate(
            MihAD.Sex, MihAD.AgeYears, MihAD.HeightCm, MihAD.WeightKg);

        Assert.Equal(1507.75, bmr, precision: 2);
    }

    [Fact]
    public void Bmr_male_uses_mifflin_st_jeor()
    {
        // 10×97 + 6.25×181 − 5×26 + 5 = 1976.25
        var bmr = EnergyMath.BasalMetabolicRate(
            OthUs.Sex, OthUs.AgeYears, OthUs.HeightCm, OthUs.WeightKg);

        Assert.Equal(1976.25, bmr, precision: 2);
    }

    [Fact]
    public void Tdee_sedentary_multiplies_bmr_by_1_2()
    {
        // 1507.75 × 1.2 = 1809.3
        var tdee = EnergyMath.TotalDailyEnergyExpenditure(1507.75, ActivityLevel.Sedentary);

        Assert.Equal(1809.3, tdee, precision: 2);
    }

    [Fact]
    public void Tdee_very_active_multiplies_bmr_by_1_9()
    {
        // 1507.75 × 1.9 = 2864.725 — проверяем крайнюю точку таблицы множителей.
        var tdee = EnergyMath.TotalDailyEnergyExpenditure(1507.75, ActivityLevel.VeryActive);

        Assert.Equal(2864.725, tdee, precision: 3);
    }

    [Theory]
    [InlineData(ActivityLevel.Sedentary, 1.2)]
    [InlineData(ActivityLevel.Light, 1.375)]
    [InlineData(ActivityLevel.Moderate, 1.55)]
    [InlineData(ActivityLevel.Active, 1.725)]
    [InlineData(ActivityLevel.VeryActive, 1.9)]
    public void Activity_factor_matches_table(ActivityLevel level, double expected)
    {
        Assert.Equal(expected, EnergyMath.ActivityFactor(level), precision: 3);
    }

    [Fact]
    public void Plan_lose_quarter_kg_per_week_for_female()
    {
        // TDEE 1809.3 − 0.25×7700/7 = 1809.3 − 275 = 1534.3 → 1530 ккал.
        // Выше BMR (1507.75), темп достижим, предупреждения нет.
        var plan = EnergyMath.Plan(MihAD, Goal.Lose, kgPerWeek: 0.25);

        Assert.Equal(1507.75, plan.Bmr, precision: 2);
        Assert.Equal(1809.3, plan.Tdee, precision: 2);
        Assert.Equal(1530, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.False(plan.BelowBmr);
        Assert.Equal(new MacroPlan(126, 43, 161), plan.Macros);
    }

    [Fact]
    public void Plan_lose_half_kg_per_week_for_female_is_below_bmr_but_allowed()
    {
        // 1809.3 − 550 = 1259.3 → 1260 ккал. Выше пола 1200, поэтому темп
        // достижим, но цель ниже BMR 1507.75 — нужен предупреждающий флаг.
        var plan = EnergyMath.Plan(MihAD, Goal.Lose, kgPerWeek: 0.5);

        Assert.Equal(1260, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.True(plan.BelowBmr);
    }

    [Fact]
    public void Plan_lose_quarter_kg_per_week_for_male()
    {
        // TDEE 2371.5 − 275 = 2096.5 → 2100 ккал, выше BMR (1976.25).
        var plan = EnergyMath.Plan(OthUs, Goal.Lose, kgPerWeek: 0.25);

        Assert.Equal(1976.25, plan.Bmr, precision: 2);
        Assert.Equal(2371.5, plan.Tdee, precision: 2);
        Assert.Equal(2100, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.False(plan.BelowBmr);
    }

    [Fact]
    public void Plan_lose_half_kg_per_week_for_male_is_below_bmr_but_allowed()
    {
        // 2371.5 − 550 = 1821.5 → 1820 ккал. Выше пола 1500, но ниже BMR 1976.25.
        var plan = EnergyMath.Plan(OthUs, Goal.Lose, kgPerWeek: 0.5);

        Assert.Equal(1820, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.True(plan.BelowBmr);
    }

    [Fact]
    public void Plan_aggressive_pace_for_female_clamps_to_safety_floor()
    {
        // 1809.3 − 1100 = 709.3 ккал — ниже пола 1200, цель прижата к полу.
        var plan = EnergyMath.Plan(MihAD, Goal.Lose, kgPerWeek: 1.0);

        Assert.Equal(EnergyMath.FemaleSafetyFloorKcal, plan.TargetKcal);
        Assert.False(plan.PaceAchievable);
        Assert.True(plan.BelowBmr);
    }

    [Fact]
    public void Plan_aggressive_pace_for_male_clamps_to_safety_floor()
    {
        // 2371.5 − 1100 = 1271.5 ккал — ниже пола 1500, цель прижата к полу.
        var plan = EnergyMath.Plan(OthUs, Goal.Lose, kgPerWeek: 1.0);

        Assert.Equal(EnergyMath.MaleSafetyFloorKcal, plan.TargetKcal);
        Assert.False(plan.PaceAchievable);
        Assert.True(plan.BelowBmr);
    }

    [Fact]
    public void Plan_maintain_equals_tdee_rounded_to_10()
    {
        // MihA_D: TDEE 1809.3 → 1810.
        var plan = EnergyMath.Plan(MihAD, Goal.Maintain, kgPerWeek: 0);

        Assert.Equal(1810, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.False(plan.BelowBmr);
    }

    [Fact]
    public void Plan_maintain_for_male_equals_tdee_rounded_to_10()
    {
        // TDEE 2371.5 → 2370.
        var plan = EnergyMath.Plan(OthUs, Goal.Maintain, kgPerWeek: 0);

        Assert.Equal(2370, plan.TargetKcal);
    }

    [Fact]
    public void Plan_rejects_negative_pace()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EnergyMath.Plan(MihAD, Goal.Lose, kgPerWeek: -0.25));
    }
}
