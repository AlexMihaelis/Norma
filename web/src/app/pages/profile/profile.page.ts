import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import {
  ActivityLevel,
  Goal,
  Sex,
  activityTitles,
  goalTitles,
  sexTitles,
} from '../../core/api.models';
import { describeProblem } from '../../core/problem';
import { ProfileService } from './profile.service';

/** Экран профиля: данные, из которых считается цель и БЖУ. */
@Component({
  selector: 'app-profile-page',
  imports: [ReactiveFormsModule],
  templateUrl: './profile.page.html',
  styleUrl: './profile.page.scss',
})
export class ProfilePage implements OnInit {
  private readonly profiles = inject(ProfileService);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly saved = signal(false);

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
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(describeProblem(error));
        },
      });
  }
}
