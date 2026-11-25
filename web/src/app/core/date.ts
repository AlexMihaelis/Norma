/**
 * Дата в формате yyyy-MM-dd по локальному часовому поясу.
 *
 * `toISOString()` переводит в UTC: вечером в Москве «сегодня» станет вчерашним,
 * поэтому перед форматированием сдвигаем на смещение пояса.
 */
export function toLocalIso(date: Date): string {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);

  return local.toISOString().slice(0, 10);
}