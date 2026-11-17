namespace Norma.Core.Bmi;

/// <summary>Возрастная полоса, по которой считается норма веса.</summary>
public enum WeightAgeBand
{
    /// <summary>Дата рождения не заполнена: берется взрослая норма 18.5-25.</summary>
    Unknown,

    /// <summary>18-64 года: пороги ВОЗ 18.5-25.</summary>
    Adult,

    /// <summary>65 лет и старше: ориентир 22-27.</summary>
    OlderAdult,

    /// <summary>До 18 лет: норма по перцентилям, в scope не входит.</summary>
    Minor,
}

/// <summary>Норма веса: границы в килограммах и в ИМТ для роста и возраста.</summary>
public sealed record NormalWeightRange(
    double MinKg,
    double MaxKg,
    double BmiMin,
    double BmiMax,
    WeightAgeBand AgeBand);

/// <summary>
/// Норма веса по ИМТ с поправкой на возраст. Для 18-64 лет - пороги ВОЗ
/// 18.5-25; для 65+ - ориентир 22-27, потому что пожилым вреден и недостаток,
/// и избыток веса. Пол на границы не влияет: пороги ВОЗ для взрослых одинаковы.
/// Дети и подростки измеряются перцентилями по возрасту и в scope не входят.
/// </summary>
public static class NormalWeightMath
{
    /// <summary>С этого возраста начинается взрослая норма.</summary>
    public const int AdultFromAge = 18;

    /// <summary>С этого возраста норма сдвигается вверх.</summary>
    public const int OlderAdultFromAge = 65;

    /// <summary>Нижняя граница нормы для 18-64 лет.</summary>
    public const double AdultBmiMin = BmiMath.LowBelow;

    /// <summary>Верхняя граница нормы для 18-64 лет.</summary>
    public const double AdultBmiMax = BmiMath.HighFrom;

    /// <summary>Нижняя граница нормы для 65+.</summary>
    public const double OlderAdultBmiMin = 22;

    /// <summary>Верхняя граница нормы для 65+.</summary>
    public const double OlderAdultBmiMax = 27;

    /// <summary>Возрастная полоса по возрасту в полных годах.</summary>
    public static WeightAgeBand BandFor(int? age) => age switch
    {
        null => WeightAgeBand.Unknown,
        < AdultFromAge => WeightAgeBand.Minor,
        < OlderAdultFromAge => WeightAgeBand.Adult,
        _ => WeightAgeBand.OlderAdult,
    };

    /// <summary>
    /// Границы нормы веса для роста и возраста. Для детей и подростков норма не
    /// считается (перцентили): возвращается <c>null</c>.
    /// </summary>
    public static NormalWeightRange? For(int? age, double heightCm)
    {
        var band = BandFor(age);

        if (band == WeightAgeBand.Minor)
        {
            return null;
        }

        var (bmiMin, bmiMax) = band == WeightAgeBand.OlderAdult
            ? (OlderAdultBmiMin, OlderAdultBmiMax)
            : (AdultBmiMin, AdultBmiMax);

        return new NormalWeightRange(
            BmiMath.WeightKgFor(bmiMin, heightCm),
            BmiMath.WeightKgFor(bmiMax, heightCm),
            bmiMin,
            bmiMax,
            band);
    }

    /// <summary>
    /// Категория ИМТ относительно нормы возраста: ниже min - Low, от max до
    /// порога ожирения 30 - High, от 30 - VeryHigh.
    /// </summary>
    public static BmiCategory CategoryFor(double bmi, NormalWeightRange range)
    {
        if (bmi < range.BmiMin)
        {
            return BmiCategory.Low;
        }

        if (bmi < range.BmiMax)
        {
            return BmiCategory.Normal;
        }

        return bmi < BmiMath.VeryHighFrom ? BmiCategory.High : BmiCategory.VeryHigh;
    }
}
