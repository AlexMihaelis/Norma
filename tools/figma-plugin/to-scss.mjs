#!/usr/bin/env node
/*
 * Сборка SCSS-токенов из JSON, который отдает плагин tools/figma-plugin.
 *
 * Запуск из корня репозитория:
 *   node tools/figma-plugin/to-scss.mjs design/figma-2026-11-25.json web/src/styles/_tokens.scss
 *
 * Скрипт намеренно ничего не знает про Figma API: на входе обычный JSON,
 * поэтому его легко прогнать на сохраненном файле и перегенерировать стили
 * без открытого макета.
 */

import { mkdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, relative, resolve } from 'node:path';
import process from 'node:process';

const [inputArg, outputArg] = process.argv.slice(2);
const inputPath = resolve(inputArg || 'design/tokens.json');
const outputPath = resolve(outputArg || 'web/src/styles/_tokens.scss');

let data;
try {
  data = JSON.parse(await readFile(inputPath, 'utf8'));
} catch (error) {
  console.error('Не прочитал ' + inputPath);
  console.error('  ' + (error && error.message ? error.message : String(error)));
  console.error('Запуск: node tools/figma-plugin/to-scss.mjs <экспорт.json> <выход.scss>');
  process.exit(1);
}

if (!data.tokens && !data.usage) {
  console.error('В JSON нет ни tokens, ни usage. Собери экспорт плагином с включенными галочками.');
  process.exit(1);
}

/* ---------- утилиты ---------- */

function slug(value) {
  const text = String(value)
    .toLowerCase()
    .replace(/[^a-z0-9а-я]+/gi, '-')
    .replace(/^-+|-+$/g, '');
  return text || 'token';
}

function formatNumber(value) {
  if (typeof value !== 'number' || !Number.isFinite(value)) return '0';
  return String(Math.round(value * 1000) / 1000);
}

function quote(value) {
  return "'" + String(value).replace(/\\/g, '\\\\').replace(/'/g, "\\'") + "'";
}

/** Значение переменной Figma (или сводки) в CSS-цвет. */
function colorToCss(value) {
  if (!value || typeof value !== 'object') return null;
  if (typeof value.hex === 'string') {
    const alpha = typeof value.alpha === 'number' ? value.alpha : 1;
    if (alpha >= 0.999) return value.hex.toLowerCase();
    const r = parseInt(value.hex.slice(1, 3), 16);
    const g = parseInt(value.hex.slice(3, 5), 16);
    const b = parseInt(value.hex.slice(5, 7), 16);
    return 'rgb(' + r + ' ' + g + ' ' + b + ' / ' + formatNumber(alpha) + ')';
  }
  if (value.resolved) return colorToCss(value.resolved);
  return null;
}

function weightOf(style) {
  const text = String(style || 'Regular').toLowerCase().replace(/\s+/g, '');
  if (text.includes('thin') || text.includes('hairline')) return 100;
  if (text.includes('extralight') || text.includes('ultralight')) return 200;
  if (text.includes('light')) return 300;
  if (text.includes('medium')) return 500;
  if (text.includes('semibold') || text.includes('demibold')) return 600;
  if (text.includes('extrabold') || text.includes('ultrabold')) return 800;
  if (text.includes('black') || text.includes('heavy')) return 900;
  if (text.includes('bold')) return 700;
  return 400;
}

function isItalic(style) {
  return String(style || '').toLowerCase().includes('italic');
}

function lineHeightToCss(lineHeight) {
  if (!lineHeight || typeof lineHeight !== 'object') return null;
  if (lineHeight.unit === 'AUTO') return 'normal';
  if (typeof lineHeight.value !== 'number') return null;
  return lineHeight.unit === 'PERCENT'
    ? formatNumber(lineHeight.value) + '%'
    : formatNumber(lineHeight.value) + 'px';
}

function shadowToCss(effects) {
  const parts = [];
  for (const effect of effects || []) {
    if (!effect || typeof effect !== 'object') continue;
    if (effect.type !== 'DROP_SHADOW' && effect.type !== 'INNER_SHADOW') continue;
    const offset = effect.offset || { x: 0, y: 0 };
    const color = effect.color || { hex: '#000000', alpha: 1 };
    const hex = String(color.hex || '#000000').toLowerCase();
    const alpha = typeof color.alpha === 'number' ? color.alpha : 1;
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    const cssColor = 'rgb(' + r + ' ' + g + ' ' + b + ' / ' + formatNumber(alpha) + ')';
    const inset = effect.type === 'INNER_SHADOW' ? 'inset ' : '';
    parts.push(
      inset +
        formatNumber(offset.x) +
        'px ' +
        formatNumber(offset.y) +
        'px ' +
        formatNumber(effect.radius) +
        'px ' +
        formatNumber(effect.spread) +
        'px ' +
        cssColor,
    );
  }
  return parts.length ? parts.join(', ') : null;
}

/* ---------- переменные Figma ---------- */

const collections = (data.tokens && data.tokens.variables && data.tokens.variables.collections) || [];
const variablesNote = (data.tokens && data.tokens.variables && data.tokens.variables.note) || null;

const usedNames = new Set();
const colorTokens = [];
const duplicateNames = [];
const modeOrder = [];

for (const collection of collections) {
  for (const variable of collection.variables || []) {
    if (variable.type !== 'COLOR') continue;
    const values = variable.valuesByMode || {};
    const modes = Object.keys(values);
    if (!modes.length) continue;

    for (const mode of modes) {
      if (!modeOrder.includes(mode)) modeOrder.push(mode);
    }

    const perMode = new Map();
    for (const mode of modes) {
      const css = colorToCss(values[mode]);
      if (css) perMode.set(mode, css);
    }
    if (!perMode.size) continue;

    const name = '--' + slug(variable.name);
    if (usedNames.has(name)) {
      duplicateNames.push(variable.name);
      continue;
    }
    usedNames.add(name);
    colorTokens.push({ name, variable: variable.name, collection: collection.name, perMode });
  }
}

const defaultMode = modeOrder.length ? modeOrder[0] : null;

const rootLines = [];
for (const token of colorTokens) {
  const css = token.perMode.get(defaultMode);
  if (!css) continue;
  rootLines.push('  ' + token.name + ': ' + css + '; // ' + token.collection + ' / ' + token.variable);
}

// Второй и последующие режимы Figma становятся отдельной темой: так их видно
// в CSS, но по умолчанию в проекте ничего не меняется.
const themeBlocks = [];
for (const mode of modeOrder.slice(1)) {
  const lines = [];
  for (const token of colorTokens) {
    const css = token.perMode.get(mode);
    if (!css) continue;
    if (css === token.perMode.get(defaultMode)) continue;
    lines.push('  ' + token.name + ': ' + css + '; // ' + token.variable);
  }
  if (lines.length) themeBlocks.push({ mode, lines });
}

// Числовые, строковые и логические переменные: берем первый режим,
// по-режимные значения в CSS-переменные пока не раскладываются.
const otherLines = [];
const unitless = /opacity|weight|ratio|scale|factor|line|z-index/i;
const radiusName = /radius|corner|round/i;
// Значения, уже занятые переменными Figma: сводка частот их не повторяет,
// иначе в файле будет и --radius/card, и --radius-12 с тем же числом.
const radiusValues = new Set();
const spaceValues = new Set();
for (const collection of collections) {
  for (const variable of collection.variables || []) {
    if (variable.type === 'COLOR') continue;
    const values = variable.valuesByMode || {};
    const modes = Object.keys(values);
    if (!modes.length) continue;
    const value = values[modes[0]];
    const name = '--' + slug(variable.name);
    if (usedNames.has(name)) {
      duplicateNames.push(variable.name);
      continue;
    }
    usedNames.add(name);

    if (variable.type === 'FLOAT' && typeof value === 'number') {
      const suffix = unitless.test(variable.name) ? '' : 'px';
      if (radiusName.test(variable.name)) radiusValues.add(value);
      else spaceValues.add(value);
      otherLines.push('  ' + name + ': ' + formatNumber(value) + suffix + '; // ' + variable.name);
    } else if (variable.type === 'STRING' && value !== null && value !== undefined) {
      otherLines.push('  ' + name + ': ' + quote(value) + '; // ' + variable.name);
    } else if (variable.type === 'BOOLEAN') {
      otherLines.push('  ' + name + ': ' + (value ? '1' : '0') + '; // ' + variable.name);
    }
  }
}

/* ---------- сводка частот: радиусы и отступы ---------- */

const usageRadii = (data.usage && data.usage.radii) || [];
const usageSpacing = (data.usage && data.usage.spacing) || [];
const usageColors = (data.usage && data.usage.colors) || [];

// Если в файле заведена шкала радиусов или отступов, сводка частот ее не
// дополняет: иначе в токены попадут скругления декоративных точек и полосок
// (radius-1, radius-4), которые токенами не являются.
const hasRadiusScale = collections.some((collection) =>
  (collection.variables || []).some((variable) => /^radius\//.test(variable.name)),
);
const hasSpaceScale = collections.some((collection) =>
  (collection.variables || []).some((variable) => /^space\//.test(variable.name)),
);

let radiiEmitted = 0;
for (const item of hasRadiusScale ? [] : usageRadii) {
  if (typeof item.value !== 'number' || item.value <= 0) continue;
  if (radiusValues.has(item.value)) continue;
  radiusValues.add(item.value);
  const name = '--radius-' + formatNumber(item.value).replace('.', '_');
  if (usedNames.has(name)) continue;
  usedNames.add(name);
  radiiEmitted += 1;
  otherLines.push('  ' + name + ': ' + formatNumber(item.value) + 'px; // встретился ' + item.count + ' раз');
}

let spacesEmitted = 0;
for (const item of hasSpaceScale ? [] : usageSpacing) {
  if (typeof item.value !== 'number' || item.value <= 0) continue;
  if (spaceValues.has(item.value)) continue;
  spaceValues.add(item.value);
  const name = '--space-' + formatNumber(item.value).replace('.', '_');
  if (usedNames.has(name)) continue;
  usedNames.add(name);
  spacesEmitted += 1;
  otherLines.push('  ' + name + ': ' + formatNumber(item.value) + 'px; // встретился ' + item.count + ' раз');
}

// Если переменных нет, берем цвета из сводки частот: имена будут числовыми,
// зато палитра из макета не потеряется.
const fallbackColors = [];
if (!colorTokens.length && usageColors.length) {
  usageColors.forEach((item, index) => {
    const css = colorToCss(item);
    if (!css) return;
    const name = '--color-' + (index + 1);
    usedNames.add(name);
    fallbackColors.push('  ' + name + ': ' + css + '; // ' + (item.variable || 'без переменной') + ', встретился ' + item.count + ' раз');
  });
}

/* ---------- типографика, стили и тени ---------- */

const typographyEntries = [];
const seenTypography = new Set();
for (const item of (data.usage && data.usage.typography) || []) {
  if (!item.family || typeof item.size !== 'number') continue;
  const weight = weightOf(item.style);
  const key = slug(item.family) + '-' + weight + '-' + formatNumber(item.size).replace('.', '_');
  if (seenTypography.has(key)) continue;
  seenTypography.add(key);
  typographyEntries.push({ key, item, weight });
}

const textStyleEntries = [];
for (const style of (data.tokens && data.tokens.styles && data.tokens.styles.texts) || []) {
  if (!style.fontName || typeof style.fontSize !== 'number') continue;
  textStyleEntries.push({ key: slug(style.name), style });
}

const paintStyleEntries = [];
for (const style of (data.tokens && data.tokens.styles && data.tokens.styles.paints) || []) {
  const solid = (style.paints || []).find((paint) => paint && paint.type === 'SOLID');
  if (!solid) continue;
  paintStyleEntries.push({ key: slug(style.name), css: colorToCss(solid) });
}

const shadowEntries = [];
for (const style of (data.tokens && data.tokens.styles && data.tokens.styles.effects) || []) {
  const css = shadowToCss(style.effects);
  if (!css) continue;
  shadowEntries.push({ key: slug(style.name), css });
}

/* ---------- сборка файла ---------- */

const fileName = (data.meta && data.meta.fileName) || 'без имени';
const exportedAt = (data.meta && data.meta.exportedAt) || null;
const nodesVisited = (data.meta && data.meta.nodesVisited) || 0;
const inputRelative = relative(process.cwd(), inputPath).replace(/\\/g, '/');
const outputRelative = relative(process.cwd(), outputPath).replace(/\\/g, '/');

const out = [];
out.push('// Файл сгенерирован из экспорта Figma: ' + inputRelative);
out.push('// Макет: «' + fileName + '», узлов: ' + nodesVisited + (exportedAt ? ', выгружен ' + exportedAt : ''));
out.push('// Руками не править: перегенерируй плагином и запусти');
out.push('//   node tools/figma-plugin/to-scss.mjs <экспорт.json> ' + outputRelative);
out.push('//');
out.push("// Подключение в web/src/styles.scss первой строкой (до остальных правил):");
out.push("//   @use 'styles/tokens';");
if (variablesNote) out.push('//');
if (variablesNote) out.push('// Внимание: ' + variablesNote);
if (duplicateNames.length) {
  out.push('//');
  out.push('// Имена, совпавшие после нормализации (в вывод попал первый): ' + duplicateNames.join(', '));
}
if (modeOrder.length > 1) {
  out.push('//');
  out.push('// Режимы Figma: ' + modeOrder.join(', ') + '. Основной режим: ' + defaultMode + '.');
}
out.push('');

if (rootLines.length || otherLines.length || fallbackColors.length) {
  out.push(':root {');
  if (rootLines.length) {
    out.push('  /* Цвета из переменных Figma */');
    out.push(...rootLines);
  }
  if (fallbackColors.length) {
    if (rootLines.length) out.push('');
    out.push('  /* Переменных в файле нет: цвета из сводки частот */');
    out.push(...fallbackColors);
  }
  if (otherLines.length) {
    if (rootLines.length || fallbackColors.length) out.push('');
    out.push('  /* Числа, радиусы и отступы */');
    out.push(...otherLines);
  }
  out.push('}');
  out.push('');
}

for (const theme of themeBlocks) {
  out.push("[data-theme='" + slug(theme.mode) + "'] {");
  out.push('  /* Режим Figma: ' + theme.mode + ' */');
  out.push(...theme.lines);
  out.push('}');
  out.push('');
}

if (typographyEntries.length) {
  out.push('$typography: (');
  for (const entry of typographyEntries) {
    const item = entry.item;
    const lineHeight = item.lineHeight && item.lineHeight !== 'mixed' ? formatNumber(item.lineHeight) + 'px' : null;
    out.push('  ' + quote(entry.key) + ': (');
    out.push('    family: ' + quote(item.family) + ',');
    out.push('    weight: ' + entry.weight + ',');
    if (isItalic(item.style)) out.push('    style: italic,');
    out.push('    size: ' + formatNumber(item.size) + 'px,');
    if (lineHeight) out.push('    line-height: ' + lineHeight + ',');
    out.push('    usage: ' + item.count + ',');
    out.push('  ),');
  }
  out.push(');');
  out.push('');
}

if (textStyleEntries.length) {
  out.push('$text-styles: (');
  for (const entry of textStyleEntries) {
    const style = entry.style;
    const lineHeight = lineHeightToCss(style.lineHeight);
    out.push('  ' + quote(entry.key) + ': (');
    out.push('    family: ' + quote(style.fontName.family) + ',');
    out.push('    weight: ' + weightOf(style.fontName.style) + ',');
    if (isItalic(style.fontName.style)) out.push('    style: italic,');
    out.push('    size: ' + formatNumber(style.fontSize) + 'px,');
    if (lineHeight) out.push('    line-height: ' + lineHeight + ',');
    if (style.letterSpacing && typeof style.letterSpacing.value === 'number') {
      out.push('    letter-spacing: ' + formatNumber(style.letterSpacing.value) + 'px,');
    }
    out.push('  ),');
  }
  out.push(');');
  out.push('');
}

if (paintStyleEntries.length) {
  out.push('$color-styles: (');
  for (const entry of paintStyleEntries) out.push('  ' + quote(entry.key) + ': ' + entry.css + ',');
  out.push(');');
  out.push('');
}

if (shadowEntries.length) {
  out.push('$shadows: (');
  for (const entry of shadowEntries) out.push('  ' + quote(entry.key) + ': (' + entry.css + '),');
  out.push(');');
  out.push('');
}

await mkdir(dirname(outputPath), { recursive: true });
await writeFile(outputPath, out.join('\n'), 'utf8');

/* ---------- отчет ---------- */

const colorsCount = colorTokens.length || fallbackColors.length;
const parts = [
  colorsCount + ' цветов',
  (radiiEmitted ? radiiEmitted + ' радиусов' : null),
  (spacesEmitted ? spacesEmitted + ' отступов' : null),
  (typographyEntries.length ? typographyEntries.length + ' начертаний' : null),
  (paintStyleEntries.length ? paintStyleEntries.length + ' стилей заливки' : null),
  (shadowEntries.length ? shadowEntries.length + ' теней' : null),
].filter(Boolean);

console.log('Готово: ' + outputRelative);
console.log('  ' + parts.join(', '));
if (!colorTokens.length) {
  console.log('  Переменных Figma в экспорте нет: цвета получили имена --color-1, --color-2 и т.д.');
}
if (themeBlocks.length) {
  console.log('  Режимы в темах: ' + themeBlocks.map((theme) => slug(theme.mode)).join(', '));
}
