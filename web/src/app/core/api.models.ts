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
  kind: 'Food' | 'Recipe';
  foodId: string | null;
  recipeId: string | null;
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

/** Тело своего продукта: КБЖУ на 100 г или 100 мл. */
export interface CreateFoodRequest {
  name: string;
  state: string;
  baseUnit: 'Gram' | 'Milliliter';
  kcalPer100: number;
  proteinPer100: number;
  fatPer100: number;
  carbsPer100: number;
}

export interface FoodSearchResponse {
  items: FoodSummary[];
}

/** Тело записи позиции: либо продукт, либо рецепт, но не оба сразу. */
export interface AddDiaryEntryRequest {
  meal: MealKind;
  foodId?: string;
  recipeId?: string;
  quantity: number;
  date: string;
}

/** Метка из справочника фильтров: код, подпись, группа и признак «считается сама». */
export interface RecipeTag {
  code: string;
  title: string;
  group: string;
  auto: boolean;
}

export interface RecipeTagListResponse {
  items: RecipeTag[];
}

/** Рецепт в списке: цифры на 100 г, метки и фото. */
export interface RecipeSummary {
  id: string;
  name: string;
  grams: number;
  kcalPer100g: number;
  proteinPer100g: number;
  fatPer100g: number;
  carbsPer100g: number;
  kcal: number;
  ingredientCount: number;
  tags: string[];
  autoTags: string[];
  isFavorite: boolean;
  manualNutrition: boolean;
  cookingMinutes: number | null;
  photoDataUrl: string | null;
  createdAt: string;
}

export interface RecipeIngredient {
  foodId: string;
  name: string;
  grams: number;
  kcal: number;
  kcalPer100g: number;
  proteinPer100g: number;
  fatPer100g: number;
  carbsPer100g: number;
}

/** Рецепт целиком: состав, инструкция и посчитанные цифры. */
export interface Recipe extends RecipeSummary {
  instructions: string | null;
  ingredients: RecipeIngredient[];
}

export interface RecipeListResponse {
  items: RecipeSummary[];
}

/** Тело создания и полной замены рецепта. */
export interface RecipeRequest {
  name: string;
  instructions: string | null;
  tags: string[];
  photoDataUrl: string | null;
  /** Время приготовления в минутах, необязательно. */
  cookingMinutes: number | null;
  /** Свои КБЖУ на 100 г: либо все четыре, либо ни одного. */
  kcalPer100: number | null;
  proteinPer100: number | null;
  fatPer100: number | null;
  carbsPer100: number | null;
  ingredients: { foodId: string; grams: number }[];
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

export const activityKindTitles: Record<ActivityKind, string> = {
  Walking: 'Ходьба',
  Running: 'Бег',
  Cycling: 'Велосипед',
  Swimming: 'Плавание',
  StrengthTraining: 'Силовая',
  Yoga: 'Йога',
  Dancing: 'Танцы',
  Hiking: 'Поход',
  RowingMachine: 'Гребной тренажер',
  DownhillSkiing: 'Горные лыжи',
};

export type ActivityKind =
  | 'Walking'
  | 'Running'
  | 'Cycling'
  | 'Swimming'
  | 'StrengthTraining'
  | 'Yoga'
  | 'Dancing'
  | 'Hiking'
  | 'RowingMachine'
  | 'DownhillSkiing';

export interface ActivityRecord {
  id: string;
  date: string;
  kind: ActivityKind;
  minutes: number;
  kcal: number;
}

export interface ActivityDay {
  date: string;
  items: ActivityRecord[];
  totalKcal: number;
}

export interface WaterDay {
  date: string;
  drunkMl: number;
  normMl: number | null;
  baseMl: number | null;
  activityBonusMl: number | null;
  remainingMl: number | null;
  clampedToMin: boolean;
  clampedToMax: boolean;
  normHint: string | null;
  disclaimer: string;
}

/** Виды обхватов тела; порядок совпадает с перечислением в API. */
export type BodyMeasurementKind = 'Chest' | 'Waist' | 'Hips' | 'Thigh' | 'Shoulder' | 'Calf';

export const bodyMeasurementTitles: Record<BodyMeasurementKind, string> = {
  Chest: 'Грудь',
  Waist: 'Талия',
  Hips: 'Бедра',
  Thigh: 'Бедро',
  Shoulder: 'Плечо',
  Calf: 'Икра',
};

export interface BodyMeasurementLatest {
  kind: BodyMeasurementKind;
  date: string;
  cm: number;
  previousDate: string | null;
  previousCm: number | null;
  deltaCm: number | null;
}

export interface BodyMeasurement {
  id: string;
  date: string;
  kind: BodyMeasurementKind;
  cm: number;
}

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
  weightKg: number | null;
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
