namespace Norma.Core.Energy;

/// <summary>
/// Результат расчёта нормы. Отдаём наружу все промежуточные числа, а не одну
/// итоговую цифру: экран профиля должен объяснять, откуда взялась цель.
/// </summary>
/// <param name="Bmr">Основной обмен, ккал/сут (формула Миффлина — Сан Жеора).</param>
/// <param name="Tdee">Суточный расход с учётом активности, ккал/сут.</param>
/// <param name="TargetKcal">Целевая калорийность, ккал/сут, округлена до 10.</param>
/// <param name="PaceAchievable">
/// <c>false</c> — запрошенный темп требует калорийности ниже безопасного минимума,
/// цель прижата к минимуму (1200 ккал для женщин, 1500 для мужчин). UI обязан
/// показать реальный достижимый темп, а не делать вид, что всё хорошо.
/// </param>
/// <param name="BelowBmr">
/// <c>true</c> — цель ниже основного обмена. Это допустимо, но обычно не
/// рекомендуется без наблюдения врача; UI обязан показать предупреждение.
/// </param>
/// <param name="Macros">Целевые БЖУ, посчитанные от <paramref name="TargetKcal"/>.</param>
public sealed record CaloriePlan(
    double Bmr,
    double Tdee,
    int TargetKcal,
    bool PaceAchievable,
    bool BelowBmr,
    MacroPlan Macros);
