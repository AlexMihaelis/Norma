using System.Net;
using System.Net.Http.Json;

namespace Norma.Api.Tests;

/// <summary>
/// Интеграционные тесты среза <c>GET /api/v1/plan</c>: числа — те же, что уже
/// закреплены юнит-тестами ядра, поэтому здесь проверяется транспорт и JSON,
/// а не арифметика.
/// </summary>
public sealed class PlanEndpointTests
{
    [Fact]
    public async Task Plan_returns_targets_for_female_losing_quarter_kg_per_week()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync(
            "/api/v1/plan?sex=female&age=25&heightCm=175&weightKg=70&activity=sedentary&goal=lose&kgPerWeek=0.25");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<PlanPayload>();

        Assert.NotNull(plan);
        Assert.Equal(1507.75, plan.Bmr, precision: 2);
        Assert.Equal(1809.3, plan.Tdee, precision: 2);
        Assert.Equal(1530, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.False(plan.BelowBmr);
        Assert.NotNull(plan.Macros);
        Assert.Equal(126, plan.Macros.ProteinG);
        Assert.Equal(43, plan.Macros.FatG);
        Assert.Equal(161, plan.Macros.CarbsG);
    }

    [Fact]
    public async Task Plan_marks_below_bmr_when_pace_is_half_kg_for_male()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync(
            "/api/v1/plan?sex=male&age=26&heightCm=181&weightKg=97&activity=sedentary&goal=lose&kgPerWeek=0.5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<PlanPayload>();

        Assert.NotNull(plan);
        Assert.Equal(1820, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
        Assert.True(plan.BelowBmr);
    }

    [Fact]
    public async Task Plan_clamps_aggressive_pace_to_safety_floor()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync(
            "/api/v1/plan?sex=female&age=25&heightCm=175&weightKg=70&activity=sedentary&goal=lose&kgPerWeek=1.0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<PlanPayload>();

        Assert.NotNull(plan);
        Assert.Equal(1200, plan.TargetKcal);
        Assert.False(plan.PaceAchievable);
        Assert.True(plan.BelowBmr);
    }

    [Fact]
    public async Task Plan_without_pace_uses_quarter_kg_default()
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync(
            "/api/v1/plan?sex=female&age=25&heightCm=175&weightKg=70&activity=sedentary&goal=lose");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var plan = await response.Content.ReadFromJsonAsync<PlanPayload>();

        Assert.NotNull(plan);
        Assert.Equal(1530, plan.TargetKcal);
        Assert.True(plan.PaceAchievable);
    }

    [Theory]
    [InlineData("/api/v1/plan?sex=mars&age=25&heightCm=175&weightKg=70&activity=sedentary&goal=lose")]
    [InlineData("/api/v1/plan?sex=female&age=25&heightCm=175&weightKg=0&activity=sedentary&goal=lose")]
    [InlineData("/api/v1/plan?sex=female&age=25&heightCm=175&weightKg=70&activity=sedentary&goal=lose&kgPerWeek=3")]
    [InlineData("/api/v1/plan?sex=female&age=25&heightCm=175&weightKg=70&goal=lose")]
    public async Task Plan_rejects_invalid_input(string url)
    {
        await using var app = await ApiApp.StartAsync();

        using var response = await app.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record PlanPayload(
        double Bmr,
        double Tdee,
        int TargetKcal,
        bool PaceAchievable,
        bool BelowBmr,
        MacroPayload Macros);

    private sealed record MacroPayload(int ProteinG, int FatG, int CarbsG);
}
