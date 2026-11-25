#!/usr/bin/env node
/*
 * Проверка плагина без Figma.
 *
 * Запускает code.js в песочнице node с подставным объектом figma: фрейм,
 * текст с разными стилями в абзаце, инстанс компонента, переменные и стили.
 * Ловит опечатки в обращениях к API и падение на "mixed" значениях - то, что
 * иначе всплыло бы только при запуске плагина в редакторе.
 *
 * Запуск из корня репозитория:
 *   node tools/figma-plugin/check.mjs
 */

import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import process from 'node:process';
import vm from 'node:vm';

const MIXED = Symbol('figma.mixed');
const messages = [];
const failures = [];

function check(condition, message) {
  if (!condition) failures.push(message);
}

const solid = (hex, alpha = 1) => ({
  type: 'SOLID',
  visible: true,
  opacity: 1,
  color: {
    r: parseInt(hex.slice(1, 3), 16) / 255,
    g: parseInt(hex.slice(3, 5), 16) / 255,
    b: parseInt(hex.slice(5, 7), 16) / 255,
    a: alpha,
  },
});

const titleText = {
  id: '1:3',
  type: 'TEXT',
  name: 'Заголовок',
  visible: true,
  x: 16,
  y: 24,
  width: 320,
  height: 32,
  rotation: 0,
  opacity: 1,
  characters: 'Сегодня',
  fontName: { family: 'Inter', style: 'Semi Bold' },
  fontSize: 24,
  fontWeight: 600,
  lineHeight: { value: 32, unit: 'PIXELS' },
  letterSpacing: { value: 0, unit: 'PIXELS' },
  textAlignHorizontal: 'LEFT',
  textAlignVertical: 'TOP',
  textCase: null,
  textDecoration: 'NONE',
  fills: [solid('#2f855a')],
  strokes: [],
  effects: [],
  fillStyleId: '',
  strokeStyleId: '',
  effectStyleId: '',
  textStyleId: '',
};

const mixedText = {
  id: '1:5',
  type: 'TEXT',
  name: 'Абзац с разными стилями',
  visible: true,
  x: 16,
  y: 200,
  width: 320,
  height: 48,
  rotation: 0,
  opacity: 1,
  characters: 'Съешь еще этих мягких булок',
  fontName: MIXED,
  fontSize: MIXED,
  fontWeight: MIXED,
  lineHeight: MIXED,
  letterSpacing: MIXED,
  textAlignHorizontal: 'LEFT',
  textCase: MIXED,
  textDecoration: MIXED,
  fills: MIXED,
  strokes: [],
  effects: [],
  getStyledTextSegments: () => [
    {
      characters: 'Съешь',
      fontName: { family: 'Inter', style: 'Bold' },
      fontSize: 16,
      lineHeight: { value: 24, unit: 'PIXELS' },
      letterSpacing: { value: 0, unit: 'PIXELS' },
      fills: [solid('#111827')],
      textStyleId: '',
      textCase: null,
      textDecoration: 'NONE',
    },
    {
      characters: ' еще',
      fontName: { family: 'Inter', style: 'Italic' },
      fontSize: 14,
      lineHeight: { value: 20, unit: 'PIXELS' },
      letterSpacing: { value: 0, unit: 'PIXELS' },
      fills: [solid('#6b7280')],
      textStyleId: 'S:2',
      textCase: null,
      textDecoration: 'NONE',
    },
  ],
};

const buttonInstance = {
  id: '1:6',
  type: 'INSTANCE',
  name: 'Кнопка',
  visible: true,
  x: 16,
  y: 260,
  width: 120,
  height: 40,
  rotation: 0,
  opacity: 1,
  cornerRadius: 8,
  clipsContent: false,
  fills: [solid('#2f855a')],
  strokes: [],
  effects: [],
  fillStyleId: 'S:1',
  strokeStyleId: '',
  effectStyleId: 'S:3',
  textStyleId: '',
  getMainComponentAsync: async () => ({ id: 'C:1', name: 'Button/Primary' }),
};

const cardFrame = {
  id: '1:4',
  type: 'FRAME',
  name: 'Карточка цели',
  visible: true,
  x: 16,
  y: 72,
  width: 358,
  height: 180,
  rotation: 0,
  opacity: 1,
  cornerRadius: MIXED,
  topLeftRadius: 12,
  topRightRadius: 12,
  bottomRightRadius: 0,
  bottomLeftRadius: 0,
  clipsContent: true,
  layoutMode: 'VERTICAL',
  layoutWrap: 'NO_WRAP',
  itemSpacing: 16,
  paddingTop: 16,
  paddingRight: 16,
  paddingBottom: 16,
  paddingLeft: 16,
  primaryAxisAlignItems: 'MIN',
  counterAxisAlignItems: 'CENTER',
  layoutSizingHorizontal: 'FIXED',
  layoutSizingVertical: 'AUTO',
  layoutGrow: 0,
  strokeWeight: 0,
  strokeAlign: 'INSIDE',
  fills: [solid('#ffffff')],
  strokes: [],
  effects: [{ type: 'DROP_SHADOW', visible: true, radius: 3, spread: 0, offset: { x: 0, y: 1 }, color: { r: 0, g: 0, b: 0, a: 0.08 } }],
  fillStyleId: 'S:1',
  strokeStyleId: '',
  effectStyleId: 'S:3',
  textStyleId: '',
  children: [mixedText, buttonInstance],
};

const rootFrame = {
  id: '1:2',
  type: 'FRAME',
  name: 'День',
  visible: true,
  x: 0,
  y: 0,
  width: 390,
  height: 844,
  rotation: 0,
  opacity: 1,
  absoluteBoundingBox: { x: 0, y: 0, width: 390, height: 844 },
  clipsContent: true,
  layoutMode: 'VERTICAL',
  itemSpacing: 16,
  paddingTop: 24,
  paddingRight: 16,
  paddingBottom: 24,
  paddingLeft: 16,
  primaryAxisAlignItems: 'MIN',
  counterAxisAlignItems: 'CENTER',
  fills: [solid('#f8fafc')],
  strokes: [],
  effects: [],
  fillStyleId: '',
  strokeStyleId: '',
  effectStyleId: '',
  textStyleId: '',
  children: [titleText, cardFrame],
};

const selection = [rootFrame];

const figma = {
  mixed: MIXED,
  root: { name: 'Norma' },
  currentPage: { name: 'Экраны', selection },
  ui: {
    onmessage: null,
    postMessage(message) {
      messages.push(message);
    },
  },
  showUI() {},
  closePlugin() {},
  getLocalPaintStylesAsync: async () => [{ id: 'S:1', name: 'surface/card', paints: [solid('#ffffff')] }],
  getLocalTextStylesAsync: async () => [
    {
      id: 'S:2',
      name: 'title/large',
      fontName: { family: 'Inter', style: 'Semi Bold' },
      fontSize: 24,
      lineHeight: { value: 32, unit: 'PIXELS' },
      letterSpacing: { value: 0, unit: 'PIXELS' },
      paragraphSpacing: 0,
      textCase: null,
    },
  ],
  getLocalEffectStylesAsync: async () => [
    {
      id: 'S:3',
      name: 'shadow/card',
      effects: [{ type: 'DROP_SHADOW', visible: true, radius: 3, spread: 0, offset: { x: 0, y: 1 }, color: { r: 0, g: 0, b: 0, a: 0.08 } }],
    },
  ],
  getLocalGridStylesAsync: async () => [],
  variables: {
    getLocalVariableCollectionsAsync: async () => [
      { id: 'VC:1', name: 'Тема', modes: [{ modeId: 'M:1', name: 'Светлая' }] },
    ],
    getLocalVariablesAsync: async () => [
      {
        id: 'V:1',
        name: 'color/text/primary',
        resolvedType: 'COLOR',
        description: '',
        variableCollectionId: 'VC:1',
        valuesByMode: { 'M:1': { r: 0.18, g: 0.52, b: 0.35, a: 1 } },
      },
      {
        id: 'V:2',
        name: 'radius/card',
        resolvedType: 'FLOAT',
        description: '',
        variableCollectionId: 'VC:1',
        valuesByMode: { 'M:1': 12 },
      },
    ],
    getVariableByIdAsync: async () => null,
  },
};

const code = await readFile(resolve(import.meta.dirname, 'code.js'), 'utf8');
const context = vm.createContext({ figma, __html__: '<html></html>', console });
vm.runInContext(code, context, { filename: 'code.js' });

check(typeof figma.ui.onmessage === 'function', 'onmessage не назначен: плагин не увидит сообщения панели');

await figma.ui.onmessage({ type: 'export', options: { tree: true, tokens: true, usage: true } });

const resultMessage = messages.find((message) => message.type === 'result');
const errorMessage = messages.find((message) => message.type === 'error');
check(!errorMessage, 'экспорт упал: ' + (errorMessage ? errorMessage.message : ''));
check(Boolean(resultMessage), 'плагин не прислал result');

if (resultMessage) {
  const data = JSON.parse(resultMessage.json);

  check(data.meta.selection.length === 1, 'в meta.selection должен быть один фрейм');
  check(data.meta.nodesVisited === 5, 'ожидалось 5 узлов, обойдено: ' + data.meta.nodesVisited);
  check(data.nodes && data.nodes.length === 1, 'дерево выделения не собрано');

  const root = data.nodes[0];
  check(root.children.length === 2, 'у корневого фрейма должно быть два ребенка');

  const title = root.children[0];
  check(title.text && title.text.characters === 'Сегодня', 'текст заголовка потерялся');

  const card = root.children[1];
  check(card.cornerRadius && card.cornerRadius.topLeft === 12, 'разные радиусы углов не разложены по углам');
  check(card.layout && card.layout.padding.top === 16, 'отступы авторазметки потерялись');
  check(card.styles && card.styles.fill && card.styles.fill.name === 'surface/card', 'привязка к стилю заливки не найдена');

  const mixed = card.children[0];
  check(mixed.text && mixed.text.fontName === 'mixed', 'mixed-шрифт не помечен');
  check(Array.isArray(mixed.text.segments) && mixed.text.segments.length === 2, 'фрагменты текста не собраны');

  const button = card.children[1];
  check(button.component && button.component.mainComponent && button.component.mainComponent.name === 'Button/Primary', 'имя главного компонента не найдено');

  check(data.tokens.variables.collections.length === 1, 'коллекция переменных не прочитана');
  check(data.tokens.variables.collections[0].variables.length === 2, 'переменные не прочитаны');
  check(data.tokens.styles.texts.length === 1, 'текстовые стили не прочитаны');

  check(data.usage.colors.length >= 3, 'сводка цветов пустая');
  check(data.usage.typography.some((item) => item.family === 'Inter'), 'сводка типографики пустая');
  check(data.usage.spacing.some((item) => item.value === 16), 'сводка отступов пустая');
}

// Второй сценарий: ничего не выделено - плагин обязан сказать об этом внятно.
figma.currentPage.selection = [];
messages.length = 0;
await figma.ui.onmessage({ type: 'export', options: {} });
const emptyError = messages.find((message) => message.type === 'error');
check(Boolean(emptyError), 'на пустом выделении нет сообщения об ошибке');
check(
  Boolean(emptyError) && emptyError.message.includes('Ничего не выделено'),
  'сообщение на пустом выделении непонятное: ' + (emptyError ? emptyError.message : ''),
);

if (failures.length) {
  console.error('Проверка не прошла:');
  for (const failure of failures) console.error('  - ' + failure);
  process.exit(1);
}

console.log('Плагин прошел проверку на заглушке Figma.');
console.log('  сценарии: экспорт выделения, пустое выделение');
