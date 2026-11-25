import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import {
  BmiCategory,
  WeightEntry,
  WeightTrend,
  bmiCategoryTitles,
} from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { WeightService } from '../../core/weight.service';

/** Экран веса: запись замера, тренд, прогноз цели и ИМТ. */
@Component({
  selector: 'app-weight-page',
  imports: [ReactiveFormsModule],
  templateUrl: './weight.page.html',
  styleUrl: './weight.page.scss',
})
export class WeightPage implements OnInit {
  private readonly weights = inject(WeightService);

  protected readonly entries = signal<WeightEntry[]>([]);
  protected readonly trend = signal<WeightTrend | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly busy = signal(false);

  protected readonly bmiTitles = bmiCategoryTitles;
  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly form = new FormGroup({
    kg: new FormControl<number | null>(null),
    date: new FormControl(this.today),
  });

  /** Полилиния тренда: 100x40, по оси Y - диапазон веса, по X - дни. */
  protected readonly chart = computed(() => {
    const points = this.trend()?.points ?? [];

    if (points.length < 2) {
      return null;
    }

    const values = points.map((point) => point.emaKg);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = max - min || 1;

    return points
      .map((point, index) => {
        const x = (index / (points.length - 1)) * 100;
        const y = 40 - ((point.emaKg - min) / span) * 36 - 2;

        return `${x.toFixed(1)},${y.toFixed(1)}`;
      })
      .join(' ');
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    forkJoin({
      list: this.weights.list(),
      trend: this.weights.trend(),
    }).subscribe({
      next: ({ list, trend }) => {
        this.entries.set(list.items);
        this.trend.set(trend);
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });
  }

  protected submit(): void {
    const value = this.form.getRawValue();
    const kg = value.kg;

    if (kg === null || kg < 20 || kg > 500) {
      this.error.set('Вес должен быть в диапазоне 20-500 кг.');
      return;
    }

    this.busy.set(true);
    this.saved.set(false);
    this.error.set(null);

    this.weights.add(kg, value.date ?? this.today).subscribe({
      next: () => {
        this.busy.set(false);
        this.saved.set(true);
        this.form.controls.kg.setValue(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeProblem(error));
      },
    });
  }

  protected format(value: number | null | undefined, digits = 1): string {
    if (value === null || value === undefined) {
      return '—';
    }

    return value.toFixed(digits).replace('.', ',');
  }

  protected formatDate(value: string | null): string {
    if (value === null) {
      return '—';
    }

    return new Date(value).toLocaleDateString('ru-RU', {
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }

  protected bmiClass(category: BmiCategory | null): string {
    return category === null ? 'neutral' : category.toLowerCase();
  }
}
