import { Component, computed, input } from '@angular/core';

interface RingArc {
  color: string;
  dash: string;
  offset: number;
}

/**
 * Кольцо БЖУ. Окружность делят три макроса по доле калорий съеденного за день
 * (белок 4 ккал/г, жир 9, углеводы 4), а не по проценту выполнения цели: иначе
 * в день, когда съедено мало, все сектора превращаются в тонкие штрихи и белка
 * на кольце просто не видно.
 */
@Component({
  selector: 'app-nutrition-ring',
  templateUrl: './nutrition-ring.component.html',
  styleUrl: './nutrition-ring.component.scss',
})
export class NutritionRingComponent {
  readonly kcal = input.required<number>();
  readonly protein = input.required<number>();
  readonly fat = input.required<number>();
  readonly carbs = input.required<number>();

  /** Кольцо 108x108, обводка 12: радиус 46 оставляет по 10 пикселей с каждой стороны. */
  protected readonly radius = 46;

  private readonly circumference = 2 * Math.PI * 46;

  /** Зазор между секторами: цвета не сливаются в один бублик. */
  private readonly gap = 6;

  protected readonly arcs = computed<RingArc[]>(() => {
    const parts = [
      { color: 'var(--color-protein)', kcal: this.protein() * 4 },
      { color: 'var(--color-fat-pastel)', kcal: this.fat() * 9 },
      { color: 'var(--color-carbs-pastel)', kcal: this.carbs() * 4 },
    ].filter((part) => part.kcal > 0);

    const total = parts.reduce((sum, part) => sum + part.kcal, 0);

    if (total <= 0) {
      return [];
    }

    const usable = this.circumference - this.gap * parts.length;
    let sent = 0;

    return parts.map((part) => {
      const length = (part.kcal / total) * usable;
      const arc = {
        color: part.color,
        dash: `${length.toFixed(2)} ${(this.circumference - length).toFixed(2)}`,
        offset: -sent,
      };

      sent += length + this.gap;

      return arc;
    });
  });

  protected readonly kcalText = computed(() => Math.round(this.kcal()).toLocaleString('ru-RU'));
}
