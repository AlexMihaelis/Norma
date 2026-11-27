import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { Recipe, RecipeSummary, RecipeTag } from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { RecipeDetailComponent } from './recipe-detail.component';
import { RecipeFormComponent } from './recipe-form.component';
import { RecipePhotoComponent } from './recipe-photo.component';
import { RecipesService } from './recipes.service';

/**
 * Экран рецептов: поиск, фильтры-чипсы, список карточек и модалки формы и
 * карточки рецепта. Автоматические метки приходят с сервера, ручные ставит
 * человек в форме.
 */
@Component({
  selector: 'app-recipes-page',
  imports: [DecimalPipe, RecipeDetailComponent, RecipeFormComponent, RecipePhotoComponent],
  templateUrl: './recipes.page.html',
  styleUrl: './recipes.page.scss',
})
export class RecipesPage implements OnInit {
  private readonly recipes = inject(RecipesService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly catalog = signal<RecipeTag[]>([]);
  protected readonly items = signal<RecipeSummary[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  protected readonly query = signal('');
  protected readonly favoritesOnly = signal(false);
  protected readonly activeFilter = signal<RecipeTag | null>(null);

  protected readonly formOpen = signal(false);
  protected readonly editing = signal<Recipe | null>(null);
  protected readonly detail = signal<Recipe | null>(null);
  protected readonly detailOpen = signal(false);
  protected readonly detailBusy = signal(false);

  protected readonly titles = computed<Record<string, string>>(() =>
    Object.fromEntries(this.catalog().map((tag) => [tag.code, tag.title])),
  );

  private readonly queries = new Subject<string>();

  ngOnInit(): void {
    this.recipes.tags().subscribe({
      next: (response) => this.catalog.set(response.items),
      error: () => this.catalog.set([]),
    });

    this.queries
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load());

    this.load();
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.queries.next(value.trim());
  }

  protected toggleFavorites(): void {
    this.favoritesOnly.update((value) => !value);
    this.load();
  }

  protected toggleFilter(tag: RecipeTag): void {
    this.activeFilter.update((current) => (current?.code === tag.code ? null : tag));
    this.load();
  }

  protected load(): void {
    this.recipes.list(this.filter()).subscribe({
      next: (response) => {
        this.items.set(response.items);
        this.loading.set(false);
        this.error.set(null);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected openDetail(item: RecipeSummary): void {
    this.detailBusy.set(true);

    this.recipes.get(item.id).subscribe({
      next: (recipe) => {
        this.detailBusy.set(false);
        this.detail.set(recipe);
        this.detailOpen.set(true);
      },
      error: (error: HttpErrorResponse) => {
        this.detailBusy.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected toggleFavorite(item: RecipeSummary, event: Event): void {
    event.stopPropagation();

    this.recipes.setFavorite(item.id, !item.isFavorite).subscribe({
      next: () => {
        this.items.update((list) =>
          list.map((recipe) =>
            recipe.id === item.id ? { ...recipe, isFavorite: !item.isFavorite } : recipe,
          ),
        );

        const open = this.detail();

        if (open?.id === item.id) {
          this.detail.set({ ...open, isFavorite: !item.isFavorite });
        }
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected openCreate(): void {
    this.editing.set(null);
    this.formOpen.set(true);
  }

  protected openEdit(recipe: Recipe): void {
    this.editing.set(recipe);
    this.detailOpen.set(false);
    this.formOpen.set(true);
  }

  protected onSaved(recipe: Recipe): void {
    this.formOpen.set(false);
    this.editing.set(null);
    this.notice.set(`Рецепт «${recipe.name}» сохранен.`);
    this.detail.set(recipe);
    this.detailOpen.set(true);
    this.load();
  }

  protected closeForm(): void {
    this.formOpen.set(false);
    this.editing.set(null);
  }

  protected closeDetail(): void {
    this.detailOpen.set(false);
    this.detail.set(null);
  }

  protected onDetailChanged(): void {
    this.load();
  }

  protected onRecipeAdded(): void {
    this.notice.set('Блюдо записано в дневник.');
  }

  protected tagTitles(recipe: RecipeSummary): string {
    const titles = this.titles();

    return [...recipe.autoTags, ...recipe.tags]
      .map((code) => titles[code])
      .filter((title): title is string => Boolean(title))
      .join(' · ');
  }

  private filter(): {
    q?: string;
    favorite?: boolean;
    tag?: string;
    autoTag?: string;
  } {
    const active = this.activeFilter();

    return {
      q: this.query().trim() || undefined,
      favorite: this.favoritesOnly() || undefined,
      tag: active && !active.auto ? active.code : undefined,
      autoTag: active?.auto ? active.code : undefined,
    };
  }
}