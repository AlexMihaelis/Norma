import { Component, computed, effect, input, output, signal } from '@angular/core';
import { WeightEntry } from '../../core/api.models';

interface ChartPoint {
  index: number;
  x: number;
  y: number;
  entry: WeightEntry;
  deltaKg: number | null;
}

/**
 * График веса по фактическим замерам: линия идет вверх и вниз вслед за весом.
 * Точки кликабельны - показывают дату, вес и разницу с прошлым замером.
 */
@Component({
  selector: 'app-weight-chart',
  templateUrl: './weight-chart.component.html',
  styleUrl: './weight-chart.component.scss',
})
export class WeightChartComponent {
  /** Замеры в любом порядке: компонент сам сортирует по дате. */
  readonly entries = input.required<WeightEntry[]>();

  /** Выбранная точка: родитель переключает на нее показатели тренда. */
  readonly picked = output<WeightEntry | null>();

  protected readonly selected = signal<number | null>(null);

  constructor() {
    // После нового замера список меняется - сбрасываем выбор и возвращаем
    // показатели к текущему весу.
    effect(() => {
      this.entries();
      this.selected.set(null);
      this.picked.emit(null);
    });
  }

  protected readonly points = computed<ChartPoint[]>(() => {
    // Окно графика - последние 30 замеров, как подписано в макете.
    const sorted = [...this.entries()]
      .sort((left, right) => left.date.localeCompare(right.date) || left.id.localeCompare(right.id))
      .slice(-30);

    if (sorted.length === 0) {
      return [];
    }

    const values = sorted.map((entry) => entry.kg);
    const min = Math.min(...values);
    const max = Math.max(...values);

    // По полкило отступа, чтобы линия не липла к краям.
    const low = min - Math.max(0.5, (max - min) * 0.15);
    const high = max + Math.max(0.5, (max - min) * 0.15);
    const span = high - low;

    return sorted.map((entry, index) => {
      const x = sorted.length === 1 ? 50 : (index / (sorted.length - 1)) * 100;
      const y = 100 - ((entry.kg - low) / span) * 90 - 5;
      const previous = index === 0 ? null : sorted[index - 1];

      return {
        index,
        x,
        y,
        entry,
        deltaKg: previous === null ? null : Math.round((entry.kg - previous.kg) * 10) / 10,
      };
    });
  });

  /** Ломаная по точкам: именно она «прыгает» вверх и вниз вместе с весом. */
  protected readonly polyline = computed(() =>
    this.points()
      .map((point) => `${point.x.toFixed(1)},${point.y.toFixed(1)}`)
      .join(' '),
  );

  /** Заливка под линией, как в макете: от первой точки до последней. */
  protected readonly area = computed(() => {
    const points = this.points();

    if (points.length < 2) {
      return '';
    }

    const body = points.map((point) => `${point.x.toFixed(1)},${point.y.toFixed(1)}`).join(' L ');

    return `M ${points[0].x.toFixed(1)},100 L ${body} L ${points[points.length - 1].x.toFixed(1)},100 Z`;
  });

  protected readonly selectedPoint = computed(() => {
    const index = this.selected();

    return index === null ? null : (this.points()[index] ?? null);
  });

  protected toggle(index: number): void {
    const next = this.selected() === index ? null : index;
    this.selected.set(next);
    this.picked.emit(next === null ? null : this.points()[next].entry);
  }

  protected formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' });
  }

  protected formatDateLong(iso: string): string {
    return new Date(iso).toLocaleDateString('ru-RU', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }

  protected formatKg(value: number): string {
    return value.toFixed(1).replace('.', ',');
  }

  protected deltaLabel(delta: number | null): string {
    if (delta === null) {
      return 'первый замер';
    }

    const sign = delta > 0 ? '+' : '';

    return `${sign}${this.formatKg(delta)} кг к прошлому`;
  }
}