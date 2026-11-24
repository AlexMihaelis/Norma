import { Component, computed, input, signal } from '@angular/core';
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

  protected readonly selected = signal<number | null>(null);

  protected readonly points = computed<ChartPoint[]>(() => {
    const sorted = [...this.entries()].sort(
      (left, right) => left.date.localeCompare(right.date) || left.id.localeCompare(right.id),
    );

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

  protected readonly selectedPoint = computed(() => {
    const index = this.selected();

    return index === null ? null : (this.points()[index] ?? null);
  });

  protected toggle(index: number): void {
    this.selected.set(this.selected() === index ? null : index);
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