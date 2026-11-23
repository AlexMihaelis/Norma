using Norma.Api.Data.Entities;

namespace Norma.Api.Data.Seeding;

/// <summary>Один продукт стартового набора.</summary>
internal sealed record FoodSeedItem(
    string Name,
    FoodState State,
    double Kcal,
    double Protein,
    double Fat,
    double Carbs,
    FoodUnit Unit = FoodUnit.Gram,
    string[]? Aliases = null);

/// <summary>
/// Стартовый набор базовых продуктов: без него дневник бесполезен, потому что
/// поиск находит пару позиций.
/// </summary>
/// <remarks>
/// Значения КБЖУ - типичные справочные, ориентировочные, а не выгрузка USDA:
/// полноценный импорт с кураторскими названиями - отдельная задача. Пока набор
/// дает пользователю привычные продукты и честно помечен источником
/// <see cref="FoodSource.Seed"/>.
/// </remarks>
internal static class FoodSeed
{
    public const string SourceName = "Norma seed";

    public static IReadOnlyList<FoodSeedItem> Items { get; } =
    [
        // Крупы и макароны: сухие и готовые - разные записи, калорийность отличается втрое.
        new("Гречка сухая", FoodState.Raw, 343, 12.6, 3.3, 62, Aliases: ["гречка", "крупа гречневая"]),
        new("Гречка варёная", FoodState.Cooked, 110, 4.2, 1.1, 21.3, Aliases: ["гречка отварная", "каша гречневая"]),
        new("Рис белый сухой", FoodState.Raw, 344, 6.7, 0.7, 78.9, Aliases: ["рис"]),
        new("Рис варёный", FoodState.Cooked, 116, 2.2, 0.5, 25, Aliases: ["рис отварной"]),
        new("Овсяные хлопья", FoodState.Raw, 352, 12.3, 6.1, 59.5, Aliases: ["овсянка", "геркулес"]),
        new("Овсянка на воде", FoodState.Cooked, 88, 3, 1.7, 15, Aliases: ["овсяная каша"]),
        new("Пшено", FoodState.Raw, 348, 11.5, 3.3, 69.3, Aliases: ["пшёнка"]),
        new("Перловка", FoodState.Raw, 352, 9.9, 1.2, 77.7, Aliases: ["перловая крупа"]),
        new("Манная крупа", FoodState.Raw, 333, 10.3, 1, 70.6, Aliases: ["манка"]),
        new("Макароны сухие", FoodState.Raw, 350, 12.5, 1.5, 70.5, Aliases: ["паста", "спагетти"]),
        new("Макароны варёные", FoodState.Cooked, 131, 5, 0.9, 27, Aliases: ["паста варёная"]),

        // Хлеб и выпечка.
        new("Хлеб пшеничный", FoodState.Unknown, 265, 7.9, 3.2, 48.7, Aliases: ["хлеб белый"]),
        new("Хлеб ржаной", FoodState.Unknown, 210, 6.6, 1.2, 41.4, Aliases: ["чёрный хлеб"]),
        new("Батон нарезной", FoodState.Unknown, 260, 7.5, 2.9, 51.4, Aliases: ["батон"]),

        // Мясо, рыба, яйца.
        new("Куриная грудка сырая", FoodState.Raw, 120, 22.5, 2.6, 0, Aliases: ["курица", "грудка"]),
        new("Куриная грудка отварная", FoodState.Cooked, 165, 31, 3.6, 0, Aliases: ["курица отварная"]),
        new("Куриное бедро", FoodState.Raw, 185, 20, 11, 0, Aliases: ["бедро куриное"]),
        new("Филе индейки", FoodState.Raw, 104, 21, 1.5, 0, Aliases: ["индейка"]),
        new("Говядина", FoodState.Raw, 187, 18.6, 12, 0, Aliases: ["мясо говяжье"]),
        new("Свинина", FoodState.Raw, 242, 16.4, 21, 0, Aliases: ["мясо свиное"]),
        new("Фарш говяжий", FoodState.Raw, 250, 17, 20, 0, Aliases: ["фарш"]),
        new("Сосиски", FoodState.Cooked, 260, 12, 23, 2, Aliases: ["сардельки"]),
        new("Лосось", FoodState.Raw, 208, 20, 13, 0, Aliases: ["сёмга", "красная рыба"]),
        new("Треска", FoodState.Raw, 82, 18, 0.7, 0, Aliases: ["белая рыба"]),
        new("Тунец консервированный", FoodState.Cooked, 116, 25, 1, 0, Aliases: ["тунец"]),
        new("Сельдь", FoodState.Raw, 250, 17, 18, 0, Aliases: ["селедка"]),
        new("Креветки", FoodState.Cooked, 99, 24, 0.3, 0, Aliases: ["креветка"]),
        new("Яйцо куриное", FoodState.Cooked, 143, 12.6, 9.5, 0.7, Aliases: ["яйцо", "яйца"]),

        // Молочное.
        new("Молоко 2,5%", FoodState.Unknown, 52, 2.8, 2.5, 4.7, FoodUnit.Milliliter, ["молоко"]),
        new("Кефир 1%", FoodState.Unknown, 40, 3, 1, 4, FoodUnit.Milliliter, ["кефир"]),
        new("Творог 5%", FoodState.Unknown, 121, 17, 5, 3, Aliases: ["творог"]),
        new("Йогурт натуральный", FoodState.Unknown, 66, 5, 3.5, 4.5, Aliases: ["йогурт"]),
        new("Сметана 20%", FoodState.Unknown, 206, 2.8, 20, 3.2, Aliases: ["сметана"]),
        new("Сыр твёрдый", FoodState.Unknown, 364, 25, 29, 2, Aliases: ["сыр"]),
        new("Масло сливочное", FoodState.Unknown, 717, 0.9, 81, 0.4, Aliases: ["масло"]),
        new("Сливки 10%", FoodState.Unknown, 118, 3, 10, 4, FoodUnit.Milliliter, ["сливки"]),

        // Овощи.
        new("Картофель", FoodState.Raw, 77, 2, 0.4, 17, Aliases: ["картошка"]),
        new("Картофель отварной", FoodState.Cooked, 82, 2, 0.4, 17, Aliases: ["картошка варёная"]),
        new("Морковь", FoodState.Raw, 41, 0.9, 0.2, 9.6, Aliases: ["морковка"]),
        new("Лук репчатый", FoodState.Raw, 40, 1.1, 0.1, 9.3, Aliases: ["лук"]),
        new("Помидор", FoodState.Raw, 18, 0.9, 0.2, 3.9, Aliases: ["томат"]),
        new("Огурец", FoodState.Raw, 15, 0.8, 0.1, 2.8, Aliases: ["огурцы"]),
        new("Капуста белая", FoodState.Raw, 28, 1.8, 0.1, 6.6, Aliases: ["капуста"]),
        new("Брокколи", FoodState.Raw, 34, 2.8, 0.4, 6.6, Aliases: ["капуста брокколи"]),
        new("Перец болгарский", FoodState.Raw, 26, 1, 0.3, 5.3, Aliases: ["перец"]),
        new("Кабачок", FoodState.Raw, 17, 0.6, 0.3, 4.6, Aliases: ["кабачки"]),
        new("Баклажан", FoodState.Raw, 25, 1.2, 0.1, 5.5, Aliases: ["баклажаны"]),
        new("Свёкла", FoodState.Raw, 43, 1.6, 0.2, 9.6, Aliases: ["свекла"]),
        new("Шпинат", FoodState.Raw, 23, 2.9, 0.4, 3.6, Aliases: ["салат шпинат"]),
        new("Шампиньоны", FoodState.Raw, 27, 4.3, 1, 0.1, Aliases: ["грибы"]),

        // Фрукты и ягоды.
        new("Яблоко", FoodState.Raw, 52, 0.3, 0.2, 13.8, Aliases: ["яблоки"]),
        new("Банан", FoodState.Raw, 89, 1.1, 0.3, 22.8, Aliases: ["бананы"]),
        new("Апельсин", FoodState.Raw, 47, 0.9, 0.1, 11.8, Aliases: ["апельсины"]),
        new("Груша", FoodState.Raw, 57, 0.4, 0.1, 15.2, Aliases: ["груши"]),
        new("Виноград", FoodState.Raw, 69, 0.7, 0.2, 18.1, Aliases: ["виноград"]),
        new("Клубника", FoodState.Raw, 32, 0.7, 0.3, 7.7, Aliases: ["земляника"]),
        new("Арбуз", FoodState.Raw, 30, 0.6, 0.2, 7.6, Aliases: ["арбуз"]),

        // Бобовые.
        new("Фасоль отварная", FoodState.Cooked, 127, 8.7, 0.5, 22.8, Aliases: ["фасоль"]),
        new("Горох отварной", FoodState.Cooked, 118, 8.3, 0.4, 21, Aliases: ["горох"]),
        new("Чечевица отварная", FoodState.Cooked, 116, 9, 0.4, 20, Aliases: ["чечевица"]),
        new("Нут отварной", FoodState.Cooked, 164, 8.9, 2.6, 27.4, Aliases: ["нут"]),

        // Орехи, масла, сладкое.
        new("Миндаль", FoodState.Raw, 579, 21, 50, 22, Aliases: ["орех миндаль"]),
        new("Грецкий орех", FoodState.Raw, 654, 15, 65, 14, Aliases: ["орехи"]),
        new("Арахис", FoodState.Raw, 567, 26, 49, 16, Aliases: ["орех арахис"]),
        new("Масло оливковое", FoodState.Unknown, 884, 0, 100, 0, Aliases: ["олива"]),
        new("Масло подсолнечное", FoodState.Unknown, 884, 0, 100, 0, Aliases: ["подсолнечное"]),
        new("Сахар", FoodState.Unknown, 399, 0, 0, 99.8, Aliases: ["сахарный песок"]),
        new("Мёд", FoodState.Unknown, 304, 0.3, 0, 82, Aliases: ["мед"]),
        new("Шоколад молочный", FoodState.Unknown, 535, 7.6, 30, 59, Aliases: ["шоколад"]),
        new("Печенье", FoodState.Unknown, 417, 6, 15, 65, Aliases: ["печенье"]),

        // Напитки.
        new("Сок яблочный", FoodState.Unknown, 46, 0.1, 0.1, 11.3, FoodUnit.Milliliter, ["сок"]),
        new("Кофе с молоком", FoodState.Unknown, 35, 1, 1.5, 4, FoodUnit.Milliliter, ["латте", "капучино"]),
        new("Чай без сахара", FoodState.Unknown, 1, 0, 0, 0.2, FoodUnit.Milliliter, ["чай"]),
        new("Вода питьевая", FoodState.Unknown, 0, 0, 0, 0, FoodUnit.Milliliter, ["вода"]),
    ];
}