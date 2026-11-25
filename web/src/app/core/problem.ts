import { HttpErrorResponse } from '@angular/common/http';

interface ProblemBody {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/**
 * Достаёт понятный текст из ошибки API. Сервер отвечает RFC 9457: для валидации
 * там `errors` по полям, для остальных случаев - `title`/`detail`.
 */
export function describeProblem(error: HttpErrorResponse): string {
  const body = error.error as ProblemBody | string | null;

  if (body !== null && typeof body === 'object') {
    if (body.errors) {
      const messages = Object.values(body.errors).flat().filter(Boolean);

      if (messages.length > 0) {
        return messages.join(' ');
      }
    }

    if (body.detail) {
      return body.detail;
    }

    if (body.title) {
      return body.title;
    }
  }

  if (typeof body === 'string' && body.length > 0) {
    return body;
  }

  // status 0 - запрос не дошел до сервера: API выключен или браузер отклонил
  // адрес по CORS. Без этой подсказки пользователь видит только "(0)".
  if (error.status === 0) {
    return 'Сервер не ответил. Проверьте, что API запущен (http://localhost:5150/api/v1/health), и откройте сайт по адресу http://localhost:4200.';
  }

  return `Не удалось выполнить запрос (${error.status}).`;
}
