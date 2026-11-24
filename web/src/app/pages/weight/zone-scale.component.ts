import { Component, computed, input } from '@angular/core';

/**
 * Цветная шкала диапазонов: четыре отрезка (недостаток, норма, избыток,
 * ожирение) и маркер там, где сейчас находится значение.
 */
@Component({
  selector: 'app-zone-scale',
  templateUrl: './zone-scale.component.html',
  styleUrl: './zone-scale.component.scss',
})
export class ZoneScaleComponent {
  readonly value = input<number | null>(null);

  /** Три границы: конец синего, конец зеленого, конец оранжевого. */
  readonly boundaries = input.required<readonly [number, number, number]>();

  /** Подписи под шкалой в том же порядке, что и цвета. */
  readonly labels = input.required<readonly [string, string, string, string]>();

  protected readonly colors = ['#60a5fa', '#22c55e', '#f59e0b', '#dc2626'];

  protected readonly geometry = computed(() => {
    const [first, second, third] = this.boundaries();
    const band = second - first;

    // Крайние зоны рисуем такой же ширины, как норма, иначе красный кусок
    // растягивается на пол-шкалы и пугает больше, чем нужно.
    const start = Math.max(0, first - band);
    const end = third + band;
    const span = end - start;

    const x = (value: number) => ((value - start) / span) * 100;

    const segments = [
      { index: 0, x: 0, width: x(first), label: this.labels()[0], color: this.colors[0] },
      { index: 1, x: x(first), width: x(second) - x(first), label: this.labels()[1], color: this.colors[1] },
      { index: 2, x: x(second), width: x(third) - x(second), label: this.labels()[2], color: this.colors[2] },
      { index: 3, x: x(third), width: 100 - x(third), label: this.labels()[3], color: this.colors[3] },
    ];

    const current = this.value();
    const marker = current === null ? null : x(Math.min(end, Math.max(start, current)));
    const active = current === null
      ? -1
      : current < first
        ? 0
        : current < second
          ? 1
          : current < third
            ? 2
            : 3;

    return { segments, marker, active };
  });
}