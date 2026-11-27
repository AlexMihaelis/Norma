/** Потолок размера фото: столько же принимает сервер. */
const MaxBytes = 256 * 1024;

/** Максимальная сторона после сжатия: карточке хватает с запасом. */
const MaxSide = 900;

const AllowedTypes = ['image/png', 'image/jpeg', 'image/webp'];

/**
 * Готовит фото рецепта к отправке: уменьшает до 900 px по большей стороне и
 * сжимает в JPEG, пока data URL не влезет в 256 КБ.
 *
 * Файл читается через `createImageBitmap`, поэтому браузер сам разбирает формат;
 * canvas нужен, чтобы ужать картинку - фотографии с телефона весят по 3-5 МБ.
 */
export async function preparePhoto(file: File): Promise<string> {
  if (!AllowedTypes.includes(file.type)) {
    throw new Error('Формат не поддерживается: png, jpeg или webp.');
  }

  const bitmap = await createImageBitmap(file);
  const scale = Math.min(1, MaxSide / Math.max(bitmap.width, bitmap.height));
  const canvas = document.createElement('canvas');

  canvas.width = Math.max(1, Math.round(bitmap.width * scale));
  canvas.height = Math.max(1, Math.round(bitmap.height * scale));

  const context = canvas.getContext('2d');

  if (!context) {
    throw new Error('Не удалось обработать картинку в этом браузере.');
  }

  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();

  for (const quality of [0.85, 0.7, 0.55, 0.45]) {
    const dataUrl = canvas.toDataURL('image/jpeg', quality);

    if (bytesOf(dataUrl) <= MaxBytes) {
      return dataUrl;
    }
  }

  throw new Error('Не удалось сжать фото до 256 КБ. Возьми картинку поменьше.');
}

function bytesOf(dataUrl: string): number {
  const comma = dataUrl.indexOf(',');

  return comma < 0 ? dataUrl.length : Math.floor(((dataUrl.length - comma - 1) * 3) / 4);
}
