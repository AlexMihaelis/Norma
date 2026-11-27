using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Norma.Api.Data;
using Norma.Api.Data.Entities;
using Norma.Api.Features.Auth;
using Norma.Api.Features.Recipes;

namespace Norma.Api.Features.Diary.AddDiaryEntry;

/// <summary>
/// Срез «записать съеденное»: находит или создает прием пищи на дату и кладет в
/// него позицию - продукт из каталога или рецепт. КБЖУ не сохраняется: оно
/// считается из продукта или состава рецепта при чтении.
/// </summary>
/// <remarks>
/// Прием пищи создается лениво: строка <see cref="Meal"/> появляется вместе с
/// первой позицией.
/// </remarks>
public static class AddDiaryEntryEndpoint
{
    /// <summary>Минимум и максимум количества в базовой единице.</summary>
    private const double MinQuantity = 0.1;
    private const double MaxQuantity = 5000;

    public static IEndpointRouteBuilder MapAddDiaryEntry(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/diary", Handle)
            .WithName("AddDiaryEntry")
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Created<DiaryEntryResponse>, ValidationProblem, UnauthorizedHttpResult>> Handle(
        AddDiaryEntryRequest? request,
        ClaimsPrincipal principal,
        NormaDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        if (userId is null)
        {
            return TypedResults.Unauthorized();
        }

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var kind = DiaryParsing.ParseRequiredMeal(request?.Meal, errors);
        var requestedUnit = DiaryParsing.ParseUnit(request?.Unit, errors);

        var foodId = request?.FoodId is Guid value && value != Guid.Empty ? value : (Guid?)null;
        var recipeId = request?.RecipeId is Guid recipeValue && recipeValue != Guid.Empty
            ? recipeValue
            : (Guid?)null;

        // Позиция - либо продукт, либо рецепт: две ссылки сразу или ни одной - ошибка.
        if (foodId is not null && recipeId is not null)
        {
            errors["foodId"] = ["Укажите либо продукт, либо рецепт, но не оба сразу."];
        }
        else if (foodId is null && recipeId is null)
        {
            errors["foodId"] = ["Укажите продукт из справочника или свой рецепт."];
        }

        var quantity = request?.Quantity ?? 0;

        if (quantity < MinQuantity || quantity > MaxQuantity)
        {
            errors["quantity"] = [$"Количество должно быть в диапазоне {MinQuantity}-{MaxQuantity}."];
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var date = request?.Date ?? today;

        // Допуск в сутки: у пользователя может быть уже следующий день по местному времени.
        if (date > today.AddDays(1))
        {
            errors["date"] = ["Дата не может быть в будущем."];
        }

        Food? food = null;
        Recipe? recipe = null;

        if (foodId is Guid foodKey)
        {
            food = await db.Foods
                .Include(candidate => candidate.Names)
                .FirstOrDefaultAsync(candidate => candidate.Id == foodKey, cancellationToken);

            if (food is null)
            {
                errors["foodId"] = ["Продукт не найден. Выберите продукт из поиска."];
            }
        }

        if (recipeId is Guid recipeKey)
        {
            // Фильтр по владельцу в запросе: чужой рецепт неотличим от несуществующего.
            recipe = await db.Recipes
                .Where(candidate => candidate.Id == recipeKey && candidate.UserId == userId)
                .WithComposition()
                .FirstOrDefaultAsync(cancellationToken);

            if (recipe is null)
            {
                errors["recipeId"] = ["Рецепт не найден. Выберите свой рецепт из списка."];
            }
        }

        // Единица записи обязана совпадать с базовой: без плотности граммы и
        // миллилитры не перевести, а подставить одно вместо другого - значит врать
        // в калориях. У рецепта единица всегда граммы.
        var unit = recipe is not null
            ? FoodUnit.Gram
            : requestedUnit ?? food?.BaseUnit ?? FoodUnit.Gram;

        if (food is not null && requestedUnit is not null && requestedUnit != food.BaseUnit)
        {
            errors["unit"] = [$"У продукта базовая единица {food.BaseUnit}; укажите {food.BaseUnit}."];
        }

        if (recipe is not null && requestedUnit is not null && requestedUnit != FoodUnit.Gram)
        {
            errors["unit"] = ["Рецепт записывается в граммах."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        var meal = await DiaryMeals.GetOrCreateAsync(
            db, userId.Value, date, kind!.Value, now, cancellationToken);

        var item = new MealItem
        {
            Id = Guid.NewGuid(),
            MealId = meal.Id,
            FoodId = food?.Id,
            RecipeId = recipe?.Id,
            Quantity = quantity,
            Unit = unit,
            CreatedAt = now,
        };

        db.MealItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        var response = recipe is not null
            ? DiaryMapping.ToRecipeEntry(item.Id, recipe, quantity)
            : DiaryMapping.ToFoodEntry(item.Id, food!, quantity, unit);

        return TypedResults.Created($"/api/v1/diary/{item.Id}", response);
    }
}
