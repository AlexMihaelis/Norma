using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Images;
using Norma.Core.Recipes;

namespace Norma.Api.Features.Recipes;

/// <summary>
/// Разобранное тело рецепта: поля, метки, фото, готовые строки состава с
/// продуктами и ошибки валидации. Одна проверка на создание и на полную замену,
/// чтобы правила не разъезжались.
/// </summary>
internal sealed class RecipeDraft
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 100;
    private const int MaxInstructionsLength = 2000;
    private const int MaxTags = 10;

    // Тридцать строк - уже большой рецепт, а не суп на неделю.
    private const int MaxIngredients = 30;

    // От щепотки специй до килограмма мяса: вне этих границ это опечатка.
    private const double MinGrams = 0.1;
    private const double MaxGrams = 5000;

    private RecipeDraft()
    {
    }

    public Dictionary<string, string[]> Errors { get; } = new(StringComparer.Ordinal);

    public string Name { get; private set; } = string.Empty;

    public string? Instructions { get; private set; }

    public List<RecipeTag> Tags { get; } = [];

    public string? PhotoContentType { get; private set; }

    public byte[]? PhotoBytes { get; private set; }

    /// <summary>Свои КБЖУ на 100 г; <c>null</c>, если человек их не вводил.</summary>
    public double? KcalPer100g { get; private set; }

    public double? ProteinPer100g { get; private set; }

    public double? FatPer100g { get; private set; }

    public double? CarbsPer100g { get; private set; }

    public List<(Food Food, double Grams)> Lines { get; } = [];

    public bool IsValid => Errors.Count == 0;

    public static async Task<RecipeDraft> BuildAsync(
        RecipeRequest? request,
        NormaDbContext db,
        CancellationToken cancellationToken)
    {
        var draft = new RecipeDraft
        {
            Name = request?.Name?.Trim() ?? string.Empty,
            Instructions = string.IsNullOrWhiteSpace(request?.Instructions)
                ? null
                : request.Instructions.Trim(),
        };

        if (draft.Name.Length < MinNameLength || draft.Name.Length > MaxNameLength)
        {
            draft.Errors["name"] =
                [$"Название должно быть длиной от {MinNameLength} до {MaxNameLength} символов."];
        }

        if (draft.Instructions is { Length: > MaxInstructionsLength })
        {
            draft.Errors["instructions"] =
                [$"Инструкция не может быть длиннее {MaxInstructionsLength} символов."];
        }

        draft.ReadTags(request?.Tags);
        draft.ReadPhoto(request?.PhotoDataUrl);
        draft.ReadManualNutrition(request);

        if (!draft.ReadIngredients(request?.Ingredients))
        {
            return draft;
        }

        if (!draft.IsValid)
        {
            return draft;
        }

        var ids = draft.Weights.Select(line => line.FoodId).ToArray();
        var foods = await db.Foods
            .Include(food => food.Names)
            .Where(food => ids.Contains(food.Id))
            .ToListAsync(cancellationToken);

        foreach (var (foodId, grams) in draft.Weights)
        {
            var food = foods.FirstOrDefault(candidate => candidate.Id == foodId);

            if (food is null)
            {
                draft.Errors["ingredients"] = ["Один из продуктов не найден в каталоге."];
                return draft;
            }

            draft.Lines.Add((food, grams));
        }

        return draft;
    }

    /// <summary>Пары «продукт и вес» до загрузки продуктов из каталога.</summary>
    private List<(Guid FoodId, double Grams)> Weights { get; } = [];

    private void ReadTags(IReadOnlyList<string>? raw)
    {
        if (raw is null || raw.Count == 0)
        {
            return;
        }

        if (raw.Count > MaxTags)
        {
            Errors["tags"] = [$"Меток может быть не больше {MaxTags}."];
            return;
        }

        foreach (var code in raw)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var tag = RecipeTagCatalog.ParseManual(code);

            if (tag is null)
            {
                Errors["tags"] = [$"Неизвестная метка: {code}."];
                return;
            }

            if (!Tags.Contains(tag.Value))
            {
                Tags.Add(tag.Value);
            }
        }
    }

    /// <summary>
    /// Свои КБЖУ: либо все четыре поля, либо ни одного. Половина заполненных
    /// значений - ошибка ввода, а не повод считать что-то среднее.
    /// </summary>
    private void ReadManualNutrition(RecipeRequest? request)
    {
        var values = new (string Key, double? Value, double Max)[]
        {
            ("kcalPer100", request?.KcalPer100, 950),
            ("proteinPer100", request?.ProteinPer100, 100),
            ("fatPer100", request?.FatPer100, 100),
            ("carbsPer100", request?.CarbsPer100, 100),
        };

        var given = values.Count(item => item.Value is not null);

        if (given == 0)
        {
            return;
        }

        if (given != values.Length)
        {
            Errors["nutrition"] = ["Заполни все четыре поля КБЖУ или оставь их пустыми - посчитаем из состава."];
            return;
        }

        foreach (var (key, value, max) in values)
        {
            if (value is not double number || double.IsNaN(number) || number < 0 || number > max)
            {
                Errors[key] = [$"Значение должно быть в диапазоне 0-{max}."];
            }
        }

        if (Errors.Count > 0)
        {
            return;
        }

        var macroSum = request!.ProteinPer100!.Value
            + request.FatPer100!.Value
            + request.CarbsPer100!.Value;

        if (macroSum > 100.5)
        {
            Errors["nutrition"] = ["Сумма белков, жиров и углеводов не может быть больше 100 г на 100 г."];
            return;
        }

        KcalPer100g = request.KcalPer100;
        ProteinPer100g = request.ProteinPer100;
        FatPer100g = request.FatPer100;
        CarbsPer100g = request.CarbsPer100;
    }

    private void ReadPhoto(string? dataUrl)
    {
        // Пустое значение - это осознанное «без фото»: замена полная, значит фото снимается.
        if (string.IsNullOrWhiteSpace(dataUrl))
        {
            return;
        }

        if (!PictureData.TryParse(dataUrl, out var mime, out var bytes, out var error))
        {
            Errors["photoDataUrl"] = [error!];
            return;
        }

        PhotoContentType = mime;
        PhotoBytes = bytes;
    }

    /// <summary>Разбирает состав; <c>false</c>, если список не годен целиком.</summary>
    private bool ReadIngredients(IReadOnlyList<RecipeIngredientRequest>? ingredients)
    {
        if (ingredients is null || ingredients.Count == 0)
        {
            Errors["ingredients"] = ["Добавьте хотя бы один продукт."];
            return false;
        }

        if (ingredients.Count > MaxIngredients)
        {
            Errors["ingredients"] = [$"Продуктов в рецепте может быть не больше {MaxIngredients}."];
            return false;
        }

        var seen = new HashSet<Guid>();

        for (var index = 0; index < ingredients.Count; index++)
        {
            var item = ingredients[index];

            if (item?.FoodId is not Guid foodId || foodId == Guid.Empty)
            {
                Errors[$"ingredients[{index}].foodId"] = ["Выберите продукт."];
                continue;
            }

            if (item.Grams is not double grams
                || double.IsNaN(grams)
                || grams < MinGrams
                || grams > MaxGrams)
            {
                Errors[$"ingredients[{index}].grams"] = [$"Вес должен быть от {MinGrams} до {MaxGrams}."];
                continue;
            }

            if (!seen.Add(foodId))
            {
                Errors[$"ingredients[{index}].foodId"] = ["Этот продукт уже добавлен в рецепт."];
                continue;
            }

            Weights.Add((foodId, grams));
        }

        return true;
    }
}
