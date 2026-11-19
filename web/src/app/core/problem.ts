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

  return `Не удалось выполнить запрос (${error.status}).`;
}
