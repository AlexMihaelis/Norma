/** Модели ответов API, нужные первым экранам. */

export interface AuthResponse {
  userId: string;
  email: string;
  token: string;
}

export interface MacroPlan {
  proteinG: number;
  fatG: number;
  carbsG: number;
}

export interface Plan {
  bmr: number;
  tdee: number;
  targetKcal: number;
  paceAchievable: boolean;
  belowBmr: boolean;
  macros: MacroPlan;
}

export type MealKind =
  | 'Breakfast'
  | 'MorningSnack'
  | 'Lunch'
  | 'AfternoonSnack'
  | 'Dinner'
  | 'EveningSnack';

export interface DiaryTotals {
  kcal: number;
  protein: number;
  fat: number;
  carbs: number;
}

export interface DiaryEntry {
  id: string;
  foodId: string;
  foodName: string;
  quantity: number;
  unit: 'Gram' | 'Milliliter';
  kcal: number;
  protein: number;
  fat: number;
  carbs: number;
}

export interface DiaryMeal {
  kind: MealKind;
  items: DiaryEntry[];
  totals: DiaryTotals;
}

export interface DiaryDay {
  date: string;
  meals: DiaryMeal[];
  totals: DiaryTotals;
}

/** Названия приемов для экрана; порядок задает API, здесь только подписи. */
export const mealTitles: Record<MealKind, string> = {
  Breakfast: 'Завтрак',
  MorningSnack: 'Утренний перекус',
  Lunch: 'Обед',
  AfternoonSnack: 'Дневной перекус',
  Dinner: 'Ужин',
  EveningSnack: 'Вечерний перекус',
};
