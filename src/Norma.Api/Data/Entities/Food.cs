namespace Norma.Api.Data.Entities;

/// <summary>Откуда пришёл продукт.</summary>
public enum FoodSource
{
    /// <summary>Импорт USDA FoodData Central (CC0).</summary>
    Usda,

    /// <summary>Поиск в Open Food Facts по штрихкоду (ODbL).</summary>
    OpenFoodFacts,

    /// <summary>Свой продукт пользователя.</summary>
    User,
}

/// <summary>Продукт и его пищевая ценность на 100 г или 100 мл.</summary>
public sealed class Food
{
    public Guid Id { get; set; }

    public FoodSource Source { get; set; }

    /// <summary>Идентификатор в источнике (USDA FDC id, штрихкод OFF); у своих продуктов пусто.</summary>
    public string? SourceId { get; set; }

    public FoodState State { get; set; } = FoodState.Unknown;

    /// <summary>Единица, в которой заданы значения на 100 единиц: граммы или миллилитры.</summary>
    public FoodUnit BaseUnit { get; set; } = FoodUnit.Gram;

    /// <summary>
    /// Кто добавил продукт вручную. Продукт живет в общем каталоге, поэтому это
    /// только авторство: при удалении пользователя поле обнуляется, а продукт
    /// остается - знание о еде не принадлежит аккаунту.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    public double KcalPer100g { get; set; }

    public double ProteinPer100g { get; set; }

    public double FatPer100g { get; set; }

    public double CarbsPer100g { get; set; }

    public List<FoodName> Names { get; set; } = [];

    public List<FoodAlias> Aliases { get; set; } = [];
}
