#!/usr/bin/env node
/*
 * Проверка генератора без Figma.
 *
 * Запускает code.js в песочнице node с подставным объектом figma и проверяет,
 * что плагин создал переменные, текстовые стили, страницу и шесть фреймов,
 * а в панель ушло сообщение об успехе. Ловит опечатки в обращениях к API до
 * запуска в редакторе.
 *
 * Запуск из корня репозитория:
 *   node tools/figma-generator/check.mjs
 */

import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import process from 'node:process';
import vm from 'node:vm';

const failures = [];
const messages = [];
const notifications = [];
const pages = [];
const variables = [];
const textStyles = [];

function check(condition, message) {
  if (!condition) failures.push(message);
}

/* ---------- модели узлов ---------- */

class FakeNode {
  constructor(type) {
    this.type = type;
    this.id = type + ':' + Math.random().toString(36).slice(2, 8);
    this.name = '';
    this.children = [];
    this.parent = null;
    this.width = 0;
    this.height = 0;
    this.x = 0;
    this.y = 0;
    this.rotation = 0;
    this.opacity = 1;
    this.fills = [];
    this.strokes = [];
    this.cornerRadius = 0;
    this.strokeWeight = 0;
    this.strokeAlign = 'INSIDE';
    this.clipsContent = false;
    this.arcData = null;
    this.removed = false;
  }

  resize(width, height) {
    this.width = width;
    this.height = height;
  }

  appendChild(child) {
    this.children.push(child);
    child.parent = this;
    return child;
  }

  setBoundVariable() {}

  remove() {
    this.removed = true;
    if (this.parent) {
      this.parent.children = this.parent.children.filter((item) => item !== this);
    }
  }
}

class FakeText extends FakeNode {
  constructor() {
    super('TEXT');
    this._characters = '';
    this.fontSize = 12;
    this.fontName = { family: 'Inter', style: 'Regular' };
    this.textStyleId = null;
  }

  get characters() {
    return this._characters;
  }

  set characters(value) {
    this._characters = String(value);
    this.width = Math.max(1, this._characters.length * this.fontSize * 0.55);
    this.height = this.fontSize * 1.35;
  }
}

/* ---------- подставной figma ---------- */

const currentPageRef = { page: null };

const figma = {
  root: {
    name: 'Norma',
    children: pages,
  },
  get currentPage() {
    return currentPageRef.page;
  },
  set currentPage(page) {
    currentPageRef.page = page;
  },
  setCurrentPageAsync: async (page) => {
    currentPageRef.page = page;
  },
  loadFontAsync: async () => undefined,
  notify: (message) => {
    notifications.push(message);
  },
  showUI: () => undefined,
  closePlugin: () => undefined,
  getLocalVariableCollectionsAsync: async () => [],
  getLocalVariablesAsync: async () => [],
  getLocalTextStylesAsync: async () => [],
  createPage: () => {
    const page = new FakeNode('PAGE');
    page.name = 'Page';
    pages.push(page);
    return page;
  },
  createFrame: () => new FakeNode('FRAME'),
  createRectangle: () => new FakeNode('RECTANGLE'),
  createEllipse: () => new FakeNode('ELLIPSE'),
  createText: () => new FakeText(),
  createTextStyle: () => {
    const style = { id: 'style:' + textStyles.length, name: '', fontName: null, fontSize: 0, lineHeight: null };
    textStyles.push(style);
    return style;
  },
  variables: {
    createVariableCollection: (name) => ({
      id: 'collection:' + name,
      name,
      modes: [{ modeId: 'mode:1', name: 'Mode 1' }],
      variableIds: [],
    }),
    createVariable: (name, collection, type) => {
      const variable = {
        id: 'variable:' + name,
        name,
        resolvedType: type,
        valuesByMode: {},
        setValueForMode(modeId, value) {
          this.valuesByMode[modeId] = value;
        },
      };
      collection.variableIds.push(variable.id);
      variables.push(variable);
      return variable;
    },
    setBoundVariableForPaint: (paint, field, variable) => ({
      ...paint,
      boundVariables: { ...(paint.boundVariables || {}), [field]: { type: 'VARIABLE_ALIAS', id: variable.id } },
    }),
  },
  ui: {
    onmessage: null,
    postMessage(message) {
      messages.push(message);
    },
  },
};

/* ---------- запуск ---------- */

const code = await readFile(resolve(import.meta.dirname, 'code.js'), 'utf8');
const context = vm.createContext({ figma, __html__: '<html></html>', console });
vm.runInContext(code, context, { filename: 'code.js' });

check(typeof figma.ui.onmessage === 'function', 'onmessage не назначен: плагин не увидит сообщения панели');

await figma.ui.onmessage({ type: 'generate' });

const done = messages.find((message) => message.type === 'done');
const error = messages.find((message) => message.type === 'error');

check(!error, 'генерация упала: ' + (error ? error.message : ''));
check(Boolean(done), 'плагин не прислал сообщение done');

check(variables.length >= 20, 'переменных слишком мало: ' + variables.length);
check(
  variables.some((variable) => variable.resolvedType === 'COLOR'),
  'не создано ни одной цветовой переменной',
);
check(
  variables.some((variable) => variable.resolvedType === 'FLOAT'),
  'не создано ни одной числовой переменной',
);
check(textStyles.length >= 5, 'текстовых стилей слишком мало: ' + textStyles.length);
check(textStyles.every((style) => style.name && style.fontName), 'у текстового стиля нет имени или шрифта');

check(pages.length === 1, 'ожидалась одна новая страница, создано: ' + pages.length);
const page = pages[0];

if (page) {
  check(page.name === 'Норма · макеты', 'страница называется не так: ' + page.name);
  check(page.children.length === 6, 'ожидалось шесть фреймов, создано: ' + page.children.length);

  const names = page.children.map((frame) => frame.name);
  const expected = ['Главная', 'Дневник', 'Вес', 'Статистика', 'Профиль', 'Форма замера'];
  expected.forEach((name) => {
    check(names.indexOf(name) !== -1, 'нет фрейма «' + name + '»');
  });

  page.children.forEach((frame) => {
    check(frame.children.length > 0, 'фрейм «' + frame.name + '» пустой');
    check(frame.width > 0 && frame.height > 0, 'у фрейма «' + frame.name + '» нулевой размер');
    check(frame.fills.length > 0, 'у фрейма «' + frame.name + '» нет заливки');
  });
}

if (done) {
  const result = done.result || {};
  check(result.screens === 6, 'в результате не шесть экранов: ' + result.screens);
  check(result.variables === variables.length, 'счетчик переменных расходится с созданными');
  check(result.textStyles === textStyles.length, 'счетчик стилей расходится с созданными');
}

check(notifications.length > 0, 'плагин не показал уведомление об успехе');

if (failures.length) {
  console.error('Проверка генератора не прошла:');
  for (const failure of failures) console.error('  - ' + failure);
  process.exit(1);
}

const totalNodes = pages[0]
  ? pages[0].children.reduce((sum, frame) => sum + frame.children.length, 0)
  : 0;

console.log('Генератор прошел проверку на заглушке Figma.');
console.log('  переменных: ' + variables.length + ', текстовых стилей: ' + textStyles.length);
console.log('  фреймов: ' + (pages[0] ? pages[0].children.length : 0) + ', узлов верхнего уровня: ' + totalNodes);
