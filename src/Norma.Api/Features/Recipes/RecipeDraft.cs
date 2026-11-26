using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;

namespace Norma.Api.Features.Recipes;

/// <summary>
/// Разобранное тело рецепта: готовые строки состава с продуктами и ошибки
/// валидации. Одна проверка на создание и на полную замену, чтобы правила не
/// разъезжались.
/// </summary>
internal sealed class RecipeDraft
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 100;
    private const int MaxNoteLength = 500;

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

    public string? Note { get; private set; }

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
            Note = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim(),
        };

        if (draft.Name.Length < MinNameLength || draft.Name.Length > MaxNameLength)
        {
            draft.Errors["name"] =
                [$"Название должно быть длиной от {MinNameLength} до {MaxNameLength} символов."];
        }

        if (draft.Note is { Length: > MaxNoteLength })
        {
            draft.Errors["note"] = [$"Заметка не может быть длиннее {MaxNoteLength} символов."];
        }

        var ingredients = request?.Ingredients;

        if (ingredients is null || ingredients.Count == 0)
        {
            draft.Errors["ingredients"] = ["Добавьте хотя бы один продукт."];
            return draft;
        }

        if (ingredients.Count > MaxIngredients)
        {
            draft.Errors["ingredients"] = [$"Продуктов в рецепте может быть не больше {MaxIngredients}."];
            return draft;
        }

        var weights = new List<(Guid FoodId, double Grams)>();
        var seen = new HashSet<Guid>();

        for (var index = 0; index < ingredients.Count; index++)
        {
            var item = ingredients[index];

            if (item?.FoodId is not Guid foodId || foodId == Guid.Empty)
            {
                draft.Errors[$"ingredients[{index}].foodId"] = ["Выберите продукт."];
                continue;
            }

            if (item.Grams is not double grams
                || double.IsNaN(grams)
                || grams < MinGrams
                || grams > MaxGrams)
            {
                draft.Errors[$"ingredients[{index}].grams"] = [$"Вес должен быть от {MinGrams} до {MaxGrams}."];
                continue;
            }

            if (!seen.Add(foodId))
            {
                draft.Errors[$"ingredients[{index}].foodId"] = ["Этот продукт уже добавлен в рецепт."];
                continue;
            }

            weights.Add((foodId, grams));
        }

        if (!draft.IsValid)
        {
            return draft;
        }

        var ids = weights.Select(line => line.FoodId).ToArray();
        var foods = await db.Foods
            .Include(food => food.Names)
            .Where(food => ids.Contains(food.Id))
            .ToListAsync(cancellationToken);

        foreach (var (foodId, grams) in weights)
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
}
