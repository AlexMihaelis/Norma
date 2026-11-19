/** Настройки окружения: адрес API и ключи хранения токена. */
export const environment = {
  /** Базовый адрес API. Порт совпадает с launchSettings профиля http. */
  apiUrl: 'http://localhost:5150/api/v1',
} as const;

/** Ключи localStorage, вынесены сюда, чтобы сервис и interceptor не разъезжались. */
export const storageKeys = {
  token: 'norma.token',
  email: 'norma.email',
} as const;
