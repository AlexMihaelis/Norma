import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { CreateFoodRequest, FoodSummary, Recipe, RecipeRequest, RecipeTag } from '../../core/api.models';
import { preparePhoto } from '../../core/image-file';
import { describeProblem } from '../../core/problem';
import { RecipePhotoComponent } from './recipe-photo.component';
import { RecipesService } from './recipes.service';

interface IngredientRow {
  food: FoodSummary;
  grams: number;
}

interface ManualNutrition {
  kcal: string;
  protein: string;
  fat: string;
  carbs: string;
}

/**
 * Форма рецепта: фото слева, справа название, состав с добавлением через «+»,
 * способ приготовления. Ниже метки и свои КБЖУ: если их не заполнить, цифры
 * считаются из состава.
 */
@Component({
  selector: 'app-recipe-form',
  imports: [DecimalPipe, RecipePhotoComponent],
  templateUrl: './recipe-form.component.html',
  styleUrl: './recipe-form.component.scss',
})
export class RecipeFormComponent implements OnInit {
  private readonly recipes = inject(RecipesService);
  private readonly destroyRef = inject(DestroyRef);

  /** Рецепт для правки; <c>null</c> - создаем новый. */
  readonly recipe = input<Recipe | null>(null);
  readonly catalog = input.required<RecipeTag[]>();

  readonly saved = output<Recipe>();
  readonly closed = output<void>();

  protected readonly name = signal('');
  protected readonly instructions = signal('');
  protected readonly photo = signal<string | null>(null);
  protected readonly tags = signal<string[]>([]);
  protected readonly rows = signal<IngredientRow[]>([]);
  protected readonly busy = signal(false);
  protected readonly photoBusy = signal(false);
  protected readonly error = signal<string | null>(null);

  /** Ввод продукта: поиск, выбранный продукт и граммовка до нажатия «+». */
  protected readonly query = signal('');
  protected readonly results = signal<FoodSummary[]>([]);
  protected readonly selected = signal<FoodSummary | null>(null);
  protected readonly gramsInput = signal('100');

  /** Быстрое создание продукта, которого нет в каталоге. */
  protected readonly quickOpen = signal(false);
  protected readonly quickName = signal('');
  protected readonly quickUnit = signal<'Gram' | 'Milliliter'>('Gram');
  protected readonly quickKcal = signal('');
  protected readonly quickProtein = signal('');
  protected readonly quickFat = signal('');
  protected readonly quickCarbs = signal('');
  protected readonly quickBusy = signal(false);

  /** Свои КБЖУ на 100 г: пустые поля означают «посчитай из состава». */
  protected readonly manual = signal<ManualNutrition>({
    kcal: '',
    protein: '',
    fat: '',
    carbs: '',
  });

  private readonly queries = new Subject<string>();

  /** Ручные метки по группам: приемы пищи, тип блюда, способ приготовления. */
  protected readonly groups = computed(() => {
    const groups = new Map<string, RecipeTag[]>();

    for (const tag of this.catalog()) {
      if (tag.auto) {
        continue;
      }

      const list = groups.get(tag.group) ?? [];
      list.push(tag);
      groups.set(tag.group, list);
    }

    return [...groups.entries()].map(([title, items]) => ({ title, items }));
  });

  /** Автоматические цифры из состава - то, что подставится, если свои не введены. */
  protected readonly computedTotals = computed(() => {
    let grams = 0;
    let kcal = 0;
    let protein = 0;
    let fat = 0;
    let carbs = 0;

    for (const row of this.rows()) {
      const factor = row.grams / 100;

      grams += row.grams;
      kcal += row.food.kcalPer100g * factor;
      protein += row.food.proteinPer100g * factor;
      fat += row.food.fatPer100g * factor;
      carbs += row.food.carbsPer100g * factor;
    }

    return {
      grams,
      kcalPer100: grams > 0 ? (kcal / grams) * 100 : 0,
      proteinPer100: grams > 0 ? (protein / grams) * 100 : 0,
      fatPer100: grams > 0 ? (fat / grams) * 100 : 0,
      carbsPer100: grams > 0 ? (carbs / grams) * 100 : 0,
    };
  });

  /** Заполнено ли хоть одно поле своих КБЖУ. */
  protected readonly manualActive = computed(() => {
    const manual = this.manual();

    return [manual.kcal, manual.protein, manual.fat, manual.carbs].some(
      (value) => value.trim() !== '',
    );
  });

  /** Действующие цифры на 100 г: свои, если введены, иначе из состава. */
  protected readonly effectivePer100 = computed(() => {
    const computed = this.computedTotals();

    if (!this.manualActive()) {
      return computed;
    }

    const manual = this.manual();

    return {
      grams: computed.grams,
      kcalPer100: number(manual.kcal),
      proteinPer100: number(manual.protein),
      fatPer100: number(manual.fat),
      carbsPer100: number(manual.carbs),
    };
  });

  protected readonly unit = computed(() =>
    this.selected()?.baseUnit === 'Milliliter' ? 'мл' : 'г',
  );

  ngOnInit(): void {
    const current = this.recipe();

    if (current) {
      this.name.set(current.name);
      this.instructions.set(current.instructions ?? '');
      this.photo.set(current.photoDataUrl);
      this.tags.set([...current.tags]);
      this.rows.set(
        current.ingredients.map((ingredient) => ({
          grams: ingredient.grams,
          food: {
            id: ingredient.foodId,
            name: ingredient.name,
            state: 'Unknown',
            baseUnit: 'Gram',
            kcalPer100g: ingredient.kcalPer100g,
            proteinPer100g: ingredient.proteinPer100g,
            fatPer100g: ingredient.fatPer100g,
            carbsPer100g: ingredient.carbsPer100g,
          },
        })),
      );

      if (current.manualNutrition) {
        this.manual.set({
          kcal: String(Math.round(current.kcalPer100g)),
          protein: String(Math.round(current.proteinPer100g * 10) / 10),
          fat: String(Math.round(current.fatPer100g * 10) / 10),
          carbs: String(Math.round(current.carbsPer100g * 10) / 10),
        });
      }
    }

    this.queries
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((query) => {
          const trimmed = query.trim();

          return trimmed.length < 2
            ? of<FoodSummary[]>([])
            : this.recipes.searchFoods(trimmed).pipe(map((response) => response.items));
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (items) => this.results.set(items),
        error: () => this.results.set([]),
      });
  }

  protected onName(value: string): void {
    this.name.set(value);
  }

  protected onInstructions(value: string): void {
    this.instructions.set(value);
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.selected.set(null);
    this.queries.next(value);
  }

  protected pick(food: FoodSummary): void {
    this.selected.set(food);
    this.query.set(food.name);
    this.results.set([]);
    this.error.set(null);
  }

  protected onGramsInput(raw: string): void {
    this.gramsInput.set(raw);
  }

  /**
   * «+» рядом с продуктом: выбранный продукт уходит в состав, а если продукт
   * только набран руками и его нет в каталоге - открывается быстрая форма.
   */
  protected addIngredient(): void {
    const food = this.selected();

    if (food) {
      this.addFood(food, number(this.gramsInput()) || 100);
      return;
    }

    const typed = this.query().trim();

    if (typed.length < 2) {
      this.error.set('Введи название продукта или выбери его из списка.');
      return;
    }

    this.quickName.set(typed);
    this.quickUnit.set('Gram');
    this.quickKcal.set('');
    this.quickProtein.set('');
    this.quickFat.set('');
    this.quickCarbs.set('');
    this.quickOpen.set(true);
    this.error.set(null);
  }

  protected onQuickName(value: string): void {
    this.quickName.set(value);
  }

  protected onQuickUnit(value: string): void {
    this.quickUnit.set(value === 'Milliliter' ? 'Milliliter' : 'Gram');
  }

  protected onQuickField(field: 'kcal' | 'protein' | 'fat' | 'carbs', value: string): void {
    switch (field) {
      case 'kcal':
        this.quickKcal.set(value);
        break;
      case 'protein':
        this.quickProtein.set(value);
        break;
      case 'fat':
        this.quickFat.set(value);
        break;
      default:
        this.quickCarbs.set(value);
    }
  }

  /** Сохраняет новый продукт в каталог и сразу кладет его в состав. */
  protected saveQuickFood(): void {
    const name = this.quickName().trim();

    if (name.length < 2) {
      this.error.set('Название продукта должно быть хотя бы из двух символов.');
      return;
    }

    const request: CreateFoodRequest = {
      name,
      state: 'Unknown',
      baseUnit: this.quickUnit(),
      kcalPer100: number(this.quickKcal()),
      proteinPer100: number(this.quickProtein()),
      fatPer100: number(this.quickFat()),
      carbsPer100: number(this.quickCarbs()),
    };

    this.quickBusy.set(true);
    this.error.set(null);

    this.recipes.createFood(request).subscribe({
      next: (food) => {
        this.quickBusy.set(false);
        this.quickOpen.set(false);
        this.query.set('');
        this.addFood(food, number(this.gramsInput()) || 100);
      },
      error: (error: HttpErrorResponse) => {
        this.quickBusy.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected closeQuick(): void {
    this.quickOpen.set(false);
  }

  protected onGrams(index: number, raw: string): void {
    const grams = number(raw);

    this.rows.update((rows) =>
      rows.map((row, position) => (position === index ? { ...row, grams } : row)),
    );
  }

  protected removeRow(index: number): void {
    this.rows.update((rows) => rows.filter((_, position) => position !== index));
  }

  protected toggleTag(code: string): void {
    this.tags.update((list) =>
      list.includes(code) ? list.filter((item) => item !== code) : [...list, code],
    );
  }

  protected onManual(field: keyof ManualNutrition, value: string): void {
    this.manual.update((manual) => ({ ...manual, [field]: value }));
  }

  protected onPhoto(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    input.value = '';

    if (!file) {
      return;
    }

    this.photoBusy.set(true);
    this.error.set(null);

    preparePhoto(file)
      .then((dataUrl) => this.photo.set(dataUrl))
      .catch((reason: Error) => this.error.set(reason.message))
      .finally(() => this.photoBusy.set(false));
  }

  protected clearPhoto(): void {
    this.photo.set(null);
  }

  protected save(): void {
    const manual = this.manual();
    const filled = [manual.kcal, manual.protein, manual.fat, manual.carbs].filter(
      (value) => value.trim() !== '',
    ).length;

    if (filled > 0 && filled < 4) {
      this.error.set('Заполни все четыре поля КБЖУ или оставь их пустыми - посчитаем из состава.');
      return;
    }

    const request: RecipeRequest = {
      name: this.name().trim(),
      instructions: this.instructions().trim() || null,
      tags: this.tags(),
      photoDataUrl: this.photo(),
      kcalPer100: filled === 4 ? number(manual.kcal) : null,
      proteinPer100: filled === 4 ? number(manual.protein) : null,
      fatPer100: filled === 4 ? number(manual.fat) : null,
      carbsPer100: filled === 4 ? number(manual.carbs) : null,
      ingredients: this.rows().map((row) => ({ foodId: row.food.id, grams: row.grams })),
    };

    if (request.name.length < 2) {
      this.error.set('Название должно быть хотя бы из двух символов.');
      return;
    }

    if (request.ingredients.length === 0) {
      this.error.set('Добавь хотя бы один продукт в состав.');
      return;
    }

    if (request.ingredients.some((ingredient) => ingredient.grams < 1)) {
      this.error.set('Вес каждого продукта должен быть не меньше грамма.');
      return;
    }

    this.busy.set(true);
    this.error.set(null);

    const current = this.recipe();
    const call = current
      ? this.recipes.update(current.id, request)
      : this.recipes.create(request);

    call.subscribe({
      next: (recipe) => {
        this.busy.set(false);
        this.saved.emit(recipe);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected close(): void {
    this.closed.emit();
  }

  private addFood(food: FoodSummary, grams: number): void {
    if (this.rows().some((row) => row.food.id === food.id)) {
      this.error.set('Этот продукт уже есть в составе.');
      return;
    }

    this.rows.update((rows) => [...rows, { food, grams: Math.max(1, grams) }]);
    this.query.set('');
    this.selected.set(null);
    this.results.set([]);
    this.gramsInput.set('100');
    this.error.set(null);
  }
}

/** Число из строки с запятой; пустое или мусор - ноль. */
function number(raw: string): number {
  const parsed = Number(raw.replace(',', '.').trim());

  return Number.isFinite(parsed) ? Math.max(0, parsed) : 0;
}