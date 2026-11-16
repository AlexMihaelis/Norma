namespace Norma.Core.Energy;

/// <summary>Пол — в формуле Миффлина — Сан Жеора даёт разную константу (+5 / −161).</summary>
public enum Sex
{
    Female,
    Male,
}

/// <summary>Повседневная активность — множитель к основному обмену.</summary>
public enum ActivityLevel
{
    Sedentary,
    Light,
    Moderate,
    Active,
    VeryActive,
}

/// <summary>Цель по весу.</summary>
public enum Goal
{
    Lose,
    Maintain,
    Gain,
}

/// <summary>Данные, из которых считается норма. Хранится в профиле пользователя.</summary>
/// <param name="Sex">Пол.</param>
/// <param name="AgeYears">Возраст в полных годах.</param>
/// <param name="HeightCm">Рост, см.</param>
/// <param name="WeightKg">Вес, кг.</param>
/// <param name="Activity">Повседневная активность.</param>
public sealed record Profile(Sex Sex, int AgeYears, double HeightCm, double WeightKg, ActivityLevel Activity);
