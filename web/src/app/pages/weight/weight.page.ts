import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { catchError, forkJoin, of } from 'rxjs';
import {
  BmiCategory,
  BodyMeasurementKind,
  BodyMeasurementLatest,
  WeightEntry,
  WeightTrend,
  bmiCategoryTitles,
  bodyMeasurementTitles,
} from '../../core/api.models';
import { WeightService } from '../../core/weight.service';
import { describeProblem } from '../../core/problem';
import { WeightChartComponent } from './weight-chart.component';
import { ZoneScaleComponent } from './zone-scale.component';

/** Экран веса: динамика, итоги, обхваты, история и зоны ИМТ. */
@Component({
  selector: 'app-weight-page',
  imports: [ReactiveFormsModule, WeightChartComponent, ZoneScaleComponent],
  templateUrl: './weight.page.html',
  styleUrl: './weight.page.scss',
})
export class WeightPage implements OnInit {
  private readonly weights = inject(WeightService);

  protected readonly entries = signal<WeightEntry[]>([]);
  protected readonly trend = signal<WeightTrend | null>(null);
  protected readonly bodyLatest = signal<BodyMeasurementLatest[]>([]);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly busy = signal(false);
  protected readonly weightFormOpen = signal(false);

  protected readonly Math = Math;

  protected readonly bmiTitles = bmiCategoryTitles;
  protected readonly bodyTitles = bodyMeasurementTitles;
  protected readonly today = new Date().toISOString().slice(0, 10);

  protected readonly weightZoneLabels = [
    'Недостаток веса',
    'Нормальный',
    'Избыток веса',
    'Ожирение',
  ] as const;
  protected readonly bmiZoneLabels = ['Низкий', 'Нормальный', 'Высокий', 'Очень высокий'] as const;

  protected readonly form = new FormGroup({
    kg: new FormControl<number | null>(null),
    date: new FormControl(this.today),
  });

  protected readonly bodyKinds: BodyMeasurementKind[] = [
    'Chest',
    'Waist',
    'Hips',
    'Thigh',
    'Shoulder',
    'Calf',
  ];

  protected readonly bodyOpen = signal(false);
  protected readonly bodyKind = signal<BodyMeasurementKind>('Waist');
  protected readonly bodyCm = signal<number | null>(null);
  protected readonly bodyDate = signal(this.today);
  protected readonly bodyBusy = signal(false);
  protected readonly bodyError = signal<string | null>(null);

  protected readonly editingId = signal<string | null>(null);
  protected readonly editKg = signal<number | null>(null);
  protected readonly editDate = signal('');
  protected readonly editBusy = signal(false);
  protected readonly editError = signal<string | null>(null);

  protected readonly currentKg = computed(() => this.entries()[0]?.kg ?? null);
  protected readonly targetKg = computed(() => this.trend()?.targetKg ?? null);
  protected readonly remainingKg = computed(() => {
    const current = this.currentKg();
    const target = this.targetKg();

    return current === null || target === null ? null : Math.round((current - target) * 10) / 10;
  });

  /** Изменение веса: скорость из тренда, иначе разница первого и последнего замера. */
  protected readonly change = computed<{ value: number; label: string } | null>(() => {
    const rate = this.trend()?.rateKgPerWeek;

    if (rate !== null && rate !== undefined) {
      return { value: rate, label: 'за неделю' };
    }

    const entries = [...this.entries()].sort((left, right) => left.date.localeCompare(right.date));

    if (entries.length < 2) {
      return null;
    }

    const first = entries[0];
    const last = entries[entries.length - 1];
    const days = Math.max(
      1,
      Math.round(
        (new Date(last.date).getTime() - new Date(first.date).getTime()) / (24 * 60 * 60 * 1000),
      ),
    );

    return { value: last.kg - first.kg, label: `за ${days} дн.` };
  });

  protected readonly weightBoundaries = computed<readonly [number, number, number] | null>(() => {
    const trend = this.trend();
    const normal = trend?.normalWeight;

    if (!trend || !normal) {
      return null;
    }

    return [normal.minKg, normal.maxKg, trend.obeseFromKg ?? normal.maxKg * 1.2];
  });

  protected readonly bmiBoundaries = computed<readonly [number, number, number] | null>(() => {
    const normal = this.trend()?.normalWeight;

    return normal === null || normal === undefined ? null : [normal.bmiMin, normal.bmiMax, 30];
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    forkJoin({
      list: this.weights.list(),
      trend: this.weights.trend(),
      // Обхваты необязательны: старый API без них не должен запирать весь экран.
      body: this.weights.latestBodyMeasurements().pipe(catchError(() => of([]))),
    }).subscribe({
      next: ({ list, trend, body }) => {
        this.entries.set(list.items);
        this.trend.set(trend);
        this.bodyLatest.set(body);
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

  protected bodyFor(kind: BodyMeasurementKind): BodyMeasurementLatest | null {
    return this.bodyLatest().find((item) => item.kind === kind) ?? null;
  }

  protected toggleWeightForm(): void {
    this.weightFormOpen.update((open) => !open);
    this.saved.set(false);
  }

  protected openBody(): void {
    this.bodyOpen.update((open) => !open);
    this.bodyError.set(null);
  }

  protected onBodyCm(raw: string): void {
    const normalized = raw.replace(',', '.').trim();
    const value = Number(normalized);

    this.bodyCm.set(normalized === '' || Number.isNaN(value) ? null : value);
  }

  protected saveBody(): void {
    const cm = this.bodyCm();

    if (cm === null || cm < 20 || cm > 300) {
      this.bodyError.set('Обхват должен быть в диапазоне 20-300 см.');
      return;
    }

    this.bodyBusy.set(true);
    this.bodyError.set(null);

    this.weights.addBodyMeasurement(this.bodyKind(), cm, this.bodyDate()).subscribe({
      next: () => {
        this.bodyBusy.set(false);
        this.bodyOpen.set(false);
        this.bodyCm.set(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.bodyBusy.set(false);
        this.bodyError.set(describeProblem(error));
      },
    });
  }

  protected startEdit(entry: WeightEntry): void {
    this.editingId.set(entry.id);
    this.editKg.set(entry.kg);
    this.editDate.set(entry.date);
    this.editError.set(null);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
    this.editError.set(null);
  }

  protected onEditKg(raw: string): void {
    const normalized = raw.replace(',', '.').trim();
    const value = Number(normalized);

    this.editKg.set(normalized === '' || Number.isNaN(value) ? null : value);
  }

  protected saveEdit(): void {
    const id = this.editingId();
    const kg = this.editKg();

    if (id === null) {
      return;
    }

    if (kg === null || kg < 20 || kg > 500) {
      this.editError.set('Вес должен быть в диапазоне 20-500 кг.');
      return;
    }

    this.editBusy.set(true);
    this.editError.set(null);

    this.weights.update(id, kg, this.editDate()).subscribe({
      next: () => {
        this.editBusy.set(false);
        this.editingId.set(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.editBusy.set(false);
        this.editError.set(describeProblem(error));
      },
    });
  }

  /** Изменение к следующему (более старому) замеру, как в макете. */
  protected delta(index: number): number | null {
    const all = this.entries();
    const next = all[index + 1];

    return next === undefined ? null : Math.round((all[index].kg - next.kg) * 10) / 10;
  }

  protected format(value: number | null | undefined, digits = 1): string {
    return value === null || value === undefined ? '—' : value.toFixed(digits).replace('.', ',');
  }

  protected formatSigned(value: number): string {
    return `${value > 0 ? '+' : ''}${this.format(value)}`;
  }

  protected formatDate(value: string | null): string {
    if (value === null) {
      return '—';
    }

    return new Date(value).toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' });
  }

  /** «12 ноя · -1 см»: дата и изменение для карточки обхватов. */
  protected bodyFootnote(item: BodyMeasurementLatest): string {
    const date = new Date(item.date).toLocaleDateString('ru-RU', { day: 'numeric', month: 'short' });
    const delta = item.deltaCm;

    return delta === null || delta === 0 ? `${date} · без изменений` : `${date} · ${this.formatSigned(delta)} см`;
  }

  protected formatNumber(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : value.toLocaleString('ru-RU');
  }
}
