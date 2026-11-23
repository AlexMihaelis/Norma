import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ActivityLevel,
  Goal,
  Sex,
  WeightEntry,
  activityTitles,
  goalTitles,
  sexTitles,
} from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { toLocalIso } from '../../core/date';
import { WeightService } from '../../core/weight.service';
import { ProfileService } from './profile.service';

/**
 * Экран профиля: данные, из которых считается цель и БЖУ. Текущий вес здесь
 * только показывается - он ведется замерами на экране «Вес».
 */
@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './profile.page.html',
  styleUrl: './profile.page.scss',
})
export class ProfilePage implements OnInit {
  private readonly profiles = inject(ProfileService);
  private readonly weights = inject(WeightService);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);
  protected readonly onboarding = signal(false);
  protected readonly currentWeight = signal<WeightEntry | null>(null);
  protected readonly weightInput = signal<number | null>(null);
  protected readonly weightBusy = signal(false);
  protected readonly weightError = signal<string | null>(null);

  protected readonly sexTitles = sexTitles;
  protected readonly activityTitles = activityTitles;
  protected readonly goalTitles = goalTitles;

  protected readonly sexes: Sex[] = ['Female', 'Male'];
  protected readonly activities: ActivityLevel[] = [
    'Sedentary',
    'Light',
    'Moderate',
    'Active',
    'VeryActive',
  ];
  protected readonly goals: Goal[] = ['Lose', 'Maintain', 'Gain'];

  protected readonly form = new FormGroup({
    sex: new FormControl<Sex | null>(null),
    birthDate: new FormControl(''),
    heightCm: new FormControl<number | null>(null),
    activity: new FormControl<ActivityLevel | null>(null),
    goal: new FormControl<Goal | null>(null),
    targetWeightKg: new FormControl<number | null>(null),
    kgPerWeek: new FormControl<number | null>(null),
  });

  ngOnInit(): void {
    this.profiles.get().subscribe({
      next: (profile) => {
        if (profile === null) {
          this.onboarding.set(true);
          return;
        }

        this.form.patchValue({
          sex: profile.sex,
          birthDate: profile.birthDate ?? '',
          heightCm: profile.heightCm,
          activity: profile.activity,
          goal: profile.goal,
          targetWeightKg: profile.targetWeightKg,
          kgPerWeek: profile.kgPerWeek,
        });
      },
      error: (error: HttpErrorResponse) => this.error.set(describeProblem(error)),
    });

    this.weights.list().subscribe({
      next: (list) => this.currentWeight.set(list.items[0] ?? null),
      error: () => this.currentWeight.set(null),
    });
  }

  /** Первый замер можно записать прямо здесь: без веса цель не посчитать. */
  protected saveWeight(): void {
    const kg = this.weightInput();

    if (kg === null || kg < 20 || kg > 500) {
      this.weightError.set('Вес должен быть в диапазоне 20-500 кг.');
      return;
    }

    this.weightBusy.set(true);
    this.weightError.set(null);

    this.weights.add(kg, toLocalIso(new Date())).subscribe({
      next: () => {
        this.weightBusy.set(false);
        this.weightInput.set(null);
        this.reloadWeight();
      },
      error: (error: HttpErrorResponse) => {
        this.weightBusy.set(false);
        this.weightError.set(describeProblem(error));
      },
    });
  }

  protected onWeightInput(raw: string): void {
    const value = Number(raw);
    this.weightInput.set(raw.trim() === '' || Number.isNaN(value) ? null : value);
  }

  private reloadWeight(): void {
    this.weights.list().subscribe({
      next: (list) => this.currentWeight.set(list.items[0] ?? null),
      error: () => this.currentWeight.set(null),
    });
  }

  protected submit(): void {
    const value = this.form.getRawValue();

    this.busy.set(true);
    this.saved.set(false);
    this.error.set(null);

    // Пустая дата приходит строкой '', на сервер уходит null - иначе не пройдет
    // разбор DateOnly.
    this.profiles
      .save({
        sex: value.sex,
        birthDate: value.birthDate === '' ? null : value.birthDate,
        heightCm: value.heightCm,
        activity: value.activity,
        goal: value.goal,
        targetWeightKg: value.targetWeightKg,
        kgPerWeek: value.kgPerWeek,
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.saved.set(true);
          this.onboarding.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(describeProblem(error));
        },
      });
  }

  protected format(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : value.toFixed(1).replace('.', ',');
  }
}
