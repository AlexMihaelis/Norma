namespace Norma.Core.Bmi;

/// <summary>Категория ИМТ по порогам ВОЗ для взрослых.</summary>
public enum BmiCategory
{
    Low,
    Normal,
    High,
    VeryHigh,
}

/// <summary>
/// ИМТ и границы весовых зон для конкретного роста, в килограммах: их удобно
/// рисовать на графике веса, ось которого - килограммы.
/// </summary>
public sealed record BmiResult(
    double Value,
    BmiCategory Category,
    double HealthyWeightMinKg,
    double HealthyWeightMaxKg,
    double ObeseFromKg);

/// <summary>
/// Индекс массы тела: вес (кг) / рост² (м). Пороги ВОЗ для взрослых одинаковы
/// для мужчин и женщин, поэтому пол в формулу не входит: пол влияет на состав
/// тела, а не на границы ИМТ. Дети и подростки требуют перцентилей - вне scope.
/// </summary>
public static class BmiMath
{
    /// <summary>Нижняя граница «низкого» ИМТ: ниже 18.5 - недостаточный вес.</summary>
    public const double LowBelow = 18.5;

    /// <summary>Граница «высокого» ИМТ: от 25 - избыточный вес.</summary>
    public const double HighFrom = 25;

    /// <summary>Граница «очень высокого» ИМТ: от 30 - ожирение.</summary>
    public const double VeryHighFrom = 30;

    /// <summary>ИМТ для пары «вес, рост».</summary>
    public static double Value(double weightKg, double heightCm) =>
        weightKg / HeightSquaredM(heightCm);

    /// <summary>Категория по значению ИМТ.</summary>
    public static BmiCategory CategoryFor(double bmi) => bmi switch
    {
        < LowBelow => BmiCategory.Low,
        < HighFrom => BmiCategory.Normal,
        < VeryHighFrom => BmiCategory.High,
        _ => BmiCategory.VeryHigh,
    };

    /// <summary>ИМТ, категория и границы зон одним вызовом.</summary>
    public static BmiResult ForMeasurement(double weightKg, double heightCm)
    {
        var value = Value(weightKg, heightCm);

        return new BmiResult(
            value,
            CategoryFor(value),
            HealthyWeightMinKg(heightCm),
            HealthyWeightMaxKg(heightCm),
            ObeseFromKg(heightCm));
    }

    /// <summary>Нижняя граница нормы веса: ИМТ 18.5.</summary>
    public static double HealthyWeightMinKg(double heightCm) => LowBelow * HeightSquaredM(heightCm);

    /// <summary>Верхняя граница нормы веса: ИМТ 25 (граница не включается).</summary>
    public static double HealthyWeightMaxKg(double heightCm) => HighFrom * HeightSquaredM(heightCm);

    /// <summary>Вес, с которого начинается «очень высокий»: ИМТ 30.</summary>
    public static double ObeseFromKg(double heightCm) => VeryHighFrom * HeightSquaredM(heightCm);

    private static double HeightSquaredM(double heightCm)
    {
        if (heightCm <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heightCm), heightCm, "Рост должен быть положительным.");
        }

        var heightM = heightCm / 100;

        return heightM * heightM;
    }
}
