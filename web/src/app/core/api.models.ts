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

/** Продукт из поиска и избранного: КБЖУ на 100 г или 100 мл. */
export interface FoodSummary {
  id: string;
  name: string;
  state: string;
  baseUnit: 'Gram' | 'Milliliter';
  kcalPer100g: number;
  proteinPer100g: number;
  fatPer100g: number;
  carbsPer100g: number;
}

export interface FoodSearchResponse {
  items: FoodSummary[];
}

/** Тело записи позиции: единица не передается, API берет базовую у продукта. */
export interface AddDiaryEntryRequest {
  meal: MealKind;
  foodId: string;
  quantity: number;
  date: string;
}

export type Sex = 'Female' | 'Male';

export type ActivityLevel = 'Sedentary' | 'Light' | 'Moderate' | 'Active' | 'VeryActive';

export type Goal = 'Lose' | 'Maintain' | 'Gain';

/** Профиль: все поля необязательны, план считается, когда заполнены ключевые. */
export interface Profile {
  sex: Sex | null;
  birthDate: string | null;
  heightCm: number | null;
  activity: ActivityLevel | null;
  goal: Goal | null;
  targetWeightKg: number | null;
  kgPerWeek: number | null;
  updatedAt: string;
}

/** Тело сохранения профиля: PUT - полная замена, пустые поля стираются. */
export interface UpdateProfileRequest {
  sex: Sex | null;
  birthDate: string | null;
  heightCm: number | null;
  activity: ActivityLevel | null;
  goal: Goal | null;
  targetWeightKg: number | null;
  kgPerWeek: number | null;
}

export const sexTitles: Record<Sex, string> = {
  Female: 'Женский',
  Male: 'Мужской',
};

export const activityTitles: Record<ActivityLevel, string> = {
  Sedentary: 'Сидячий образ жизни',
  Light: 'Небольшая активность',
  Moderate: 'Умеренная активность',
  Active: 'Высокая активность',
  VeryActive: 'Очень высокая активность',
};

export const goalTitles: Record<Goal, string> = {
  Lose: 'Похудение',
  Maintain: 'Поддержание',
  Gain: 'Набор массы',
};

export interface WeightEntry {
  id: string;
  date: string;
  kg: number;
}

export interface WeightList {
  items: WeightEntry[];
}

export type WeightAgeBand = 'Unknown' | 'Adult' | 'OlderAdult' | 'Minor';

export type BmiCategory = 'Low' | 'Normal' | 'High' | 'VeryHigh';

/** Норма веса для роста и возраста: границы в кг и в ИМТ. */
export interface NormalWeight {
  minKg: number;
  maxKg: number;
  bmiMin: number;
  bmiMax: number;
}

export interface TrendPoint {
  date: string;
  emaKg: number;
}

/** Тренд веса: сглаженная кривая, скорость, прогноз цели, ИМТ и норма. */
export interface WeightTrend {
  currentEmaKg: number | null;
  rateKgPerWeek: number | null;
  targetKg: number | null;
  goalDate: string | null;
  heightCm: number | null;
  sex: Sex | null;
  age: number | null;
  ageBand: WeightAgeBand;
  bmi: number | null;
  bmiCategory: BmiCategory | null;
  obeseFromKg: number | null;
  normalWeight: NormalWeight | null;
  points: TrendPoint[];
}

export const bmiCategoryTitles: Record<BmiCategory, string> = {
  Low: 'ниже нормы',
  Normal: 'норма',
  High: 'выше нормы',
  VeryHigh: 'ожирение',
};

/** Аккаунт: кто вошел, ник и аватар. */
export interface Me {
  userId: string;
  email: string;
  nickname: string | null;
  avatarDataUrl: string | null;
}

/** День статистики: съеденное, активность и цель на эту дату. */
export interface StatsDay {
  date: string;
  kcal: number;
  protein: number;
  fat: number;
  carbs: number;
  entries: number;
  activityKcal: number;
  targetKcal: number | null;
  diffKcal: number | null;
}

/** Средние по дням с записями: пустые дни в среднее не входят. */
export interface StatsAverages {
  daysWithEntries: number;
  kcal: number;
  protein: number;
  fat: number;
  carbs: number;
  activityKcal: number;
  targetKcal: number | null;
}

/** Отклонение среднего факта от средней цели. */
export interface StatsDeviation {
  targetKcal: number;
  diffKcal: number;
  diffPercent: number;
}

/** Статистика за период. */
export interface Stats {
  from: string;
  to: string;
  days: StatsDay[];
  averages: StatsAverages;
  deviation: StatsDeviation | null;
  targetHint: string | null;
  targetNote: string;
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
