/*
 * Генератор макетов «Нормы» в Figma.
 *
 * Создает на отдельной странице шесть фреймов по утвержденным черновикам и
 * заводит дизайн-систему: переменные (цвета, радиусы, отступы) и текстовые
 * стили. После генерации запускается плагин экспорта (tools/figma-plugin),
 * и токены уезжают в _tokens.scss.
 *
 * Плагин работает без сети. Сборка не нужна - обычный JS, Figma API 1.0.0.
 */

const PAGE_NAME = 'Норма · макеты';
const FONT_FAMILY = 'Inter';
const FONT_WEIGHTS = { regular: 'Regular', medium: 'Medium', semibold: 'Semi Bold', bold: 'Bold' };
const NAV = ['Дневник', 'Вес', 'Статистика', 'Профиль'];
const CONTENT_X = 360;
const CONTENT_WIDTH = 720;
const TOPBAR_HEIGHT = 64;
const FRAME_GAP = 120;

/* Палитра: те же значения, что в черновиках на Python. */
const COLORS = {
  'color/bg': '#f8fafc',
  'color/surface': '#ffffff',
  'color/border': '#e5e7eb',
  'color/text': '#111827',
  'color/text-muted': '#6b7280',
  'color/text-inverse': '#ffffff',
  'color/accent': '#2f855a',
  'color/accent-soft': '#e6f4ec',
  'color/accent-border': '#cde8d9',
  'color/track': '#f0f2f5',
  'color/water': '#2563eb',
  'color/water-soft': '#e8effd',
  'color/protein': '#8ecfae',
  'color/fat-pastel': '#f0c191',
  'color/carbs-pastel': '#a3bde8',
  'color/overlay': '#0f172a',
};

/* Радиусы и отступы. */
const FLOATS = {
  'radius/card': 12,
  'radius/field': 10,
  'radius/button': 10,
  'radius/modal': 16,
  'radius/chip': 13,
  'space/4': 4,
  'space/8': 8,
  'space/12': 12,
  'space/16': 16,
  'space/20': 20,
  'space/24': 24,
  'space/32': 32,
};

/* Текстовые стили: family, weight, size, line-height. */
const TEXT_STYLES = [
  { name: 'Heading/H1', size: 28, weight: 'bold', lineHeight: 36 },
  { name: 'Heading/H2', size: 26, weight: 'bold', lineHeight: 34 },
  { name: 'Heading/H3', size: 20, weight: 'bold', lineHeight: 28 },
  { name: 'Heading/H4', size: 18, weight: 'bold', lineHeight: 26 },
  { name: 'Heading/H5', size: 15, weight: 'bold', lineHeight: 22 },
  { name: 'Body/Large', size: 14, weight: 'regular', lineHeight: 20 },
  { name: 'Body/Medium', size: 13, weight: 'regular', lineHeight: 18 },
  { name: 'Body/MediumBold', size: 13, weight: 'bold', lineHeight: 18 },
  { name: 'Body/Small', size: 12, weight: 'regular', lineHeight: 16 },
  { name: 'Body/SmallBold', size: 12, weight: 'bold', lineHeight: 16 },
  { name: 'Caption', size: 11, weight: 'regular', lineHeight: 15 },
  { name: 'CaptionBold', size: 11, weight: 'bold', lineHeight: 15 },
  { name: 'Micro', size: 10, weight: 'regular', lineHeight: 14 },
];

const state = {
  variables: {},
  textStyles: {},
  page: null,
};

/* ---------- утилиты ---------- */

function hexToRgb(hex) {
  const value = String(hex).replace('#', '');
  return {
    r: parseInt(value.slice(0, 2), 16) / 255,
    g: parseInt(value.slice(2, 4), 16) / 255,
    b: parseInt(value.slice(4, 6), 16) / 255,
  };
}

function colorOf(key) {
  const value = COLORS[key];
  if (!value) {
    throw new Error('Неизвестный цвет: ' + key);
  }
  return value;
}

function paint(key) {
  const base = { type: 'SOLID', color: hexToRgb(colorOf(key)), opacity: 1 };
  const variable = state.variables[key];
  if (variable && typeof figma.variables.setBoundVariableForPaint === 'function') {
    try {
      return figma.variables.setBoundVariableForPaint(base, 'color', variable);
    } catch (error) {
      return base;
    }
  }
  return base;
}

/* ---------- примитивы ---------- */

function addText(parent, spec) {
  const node = figma.createText();
  node.fontName = { family: FONT_FAMILY, style: FONT_WEIGHTS[spec.weight || 'regular'] };
  node.fontSize = spec.size || 14;
  node.characters = spec.text || '';
  node.fills = [paint(spec.color || 'color/text')];
  parent.appendChild(node);

  const align = spec.align || 'left';
  if (align === 'right') {
    node.x = (spec.x || 0) - node.width;
  } else if (align === 'center') {
    node.x = (spec.x || 0) - node.width / 2;
  } else {
    node.x = spec.x || 0;
  }
  node.y = spec.y || 0;
  return node;
}

function addRect(parent, spec) {
  const node = figma.createRectangle();
  node.name = spec.name || 'Прямоугольник';
  node.resize(spec.width, spec.height);
  node.x = spec.x || 0;
  node.y = spec.y || 0;
  if (spec.radius) {
    node.cornerRadius = spec.radius;
  }
  node.fills = spec.fill ? [paint(spec.fill)] : [];
  if (spec.stroke) {
    node.strokes = [paint(spec.stroke)];
    node.strokeWeight = 1;
    node.strokeAlign = 'INSIDE';
  }
  if (spec.opacity !== undefined) {
    node.opacity = spec.opacity;
  }
  parent.appendChild(node);
  return node;
}

function addCircle(parent, spec) {
  const node = figma.createEllipse();
  node.name = spec.name || 'Круг';
  const diameter = spec.diameter || 12;
  node.resize(diameter, diameter);
  node.x = (spec.x || 0) - diameter / 2;
  node.y = (spec.y || 0) - diameter / 2;
  node.fills = spec.fill ? [paint(spec.fill)] : [];
  if (spec.stroke) {
    node.strokes = [paint(spec.stroke)];
    node.strokeWeight = spec.strokeWeight || 1;
    node.strokeAlign = 'INSIDE';
  }
  parent.appendChild(node);
  return node;
}

function addLine(parent, spec) {
  const dx = spec.x2 - spec.x1;
  const dy = spec.y2 - spec.y1;
  const length = Math.sqrt(dx * dx + dy * dy) || 1;
  const thickness = spec.thickness || 1;
  const cx = (spec.x1 + spec.x2) / 2;
  const cy = (spec.y1 + spec.y2) / 2;

  const node = figma.createRectangle();
  node.name = 'Линия';
  node.resize(length, thickness);
  node.x = cx - length / 2;
  node.y = cy - thickness / 2;
  node.rotation = (Math.atan2(dy, dx) * 180) / Math.PI;
  node.fills = [paint(spec.color || 'color/border')];
  parent.appendChild(node);
  return node;
}

function addCard(parent, spec) {
  return addRect(parent, {
    name: spec.name || 'Карточка',
    x: spec.x,
    y: spec.y,
    width: spec.width,
    height: spec.height,
    radius: spec.radius || FLOATS['radius/card'],
    fill: spec.fill || 'color/surface',
    stroke: spec.stroke || 'color/border',
  });
}

function addButton(parent, spec) {
  const ghost = spec.kind === 'ghost';
  addRect(parent, {
    name: spec.name || 'Кнопка',
    x: spec.x,
    y: spec.y,
    width: spec.width,
    height: spec.height,
    radius: spec.radius || FLOATS['radius/button'],
    fill: ghost ? 'color/surface' : 'color/accent',
    stroke: ghost ? 'color/accent-border' : null,
  });
  addText(parent, {
    text: spec.text,
    size: spec.size || 14,
    weight: 'bold',
    color: ghost ? 'color/accent' : 'color/text-inverse',
    x: spec.x + spec.width / 2,
    y: spec.y + (spec.height - (spec.size || 14) * 1.4) / 2,
    align: 'center',
  });
}

function addBar(parent, spec) {
  const radius = spec.height / 2;
  addRect(parent, {
    name: 'Дорожка',
    x: spec.x,
    y: spec.y,
    width: spec.width,
    height: spec.height,
    radius,
    fill: spec.track || 'color/track',
  });
  const filled = Math.max(spec.height, spec.width * Math.max(0, Math.min(1, spec.value)));
  addRect(parent, {
    name: 'Заполнение',
    x: spec.x,
    y: spec.y,
    width: filled,
    height: spec.height,
    radius,
    fill: spec.color || 'color/accent',
  });
}

function addPlusButton(parent, spec) {
  const size = spec.size || 28;
  addCircle(parent, { x: spec.x + size / 2, y: spec.y + size / 2, diameter: size, fill: 'color/accent' });
  addLine(parent, {
    x1: spec.x + size * 0.28,
    y1: spec.y + size / 2,
    x2: spec.x + size * 0.72,
    y2: spec.y + size / 2,
    color: 'color/text-inverse',
    thickness: 2,
  });
  addLine(parent, {
    x1: spec.x + size / 2,
    y1: spec.y + size * 0.28,
    x2: spec.x + size / 2,
    y2: spec.y + size * 0.72,
    color: 'color/text-inverse',
    thickness: 2,
  });
}

function addInfoButton(parent, spec) {
  const size = spec.size || 15;
  addCircle(parent, {
    x: spec.x + size / 2,
    y: spec.y + size / 2,
    diameter: size,
    fill: 'color/surface',
    stroke: 'color/border',
  });
  addText(parent, {
    text: '?',
    size: Math.round(size * 0.7),
    weight: 'bold',
    color: 'color/text-muted',
    x: spec.x + size / 2,
    y: spec.y + size / 2 - size * 0.42,
    align: 'center',
  });
}

function addTopbar(parent, active) {
  addRect(parent, { name: 'Топбар', x: 0, y: 0, width: 1440, height: TOPBAR_HEIGHT, fill: 'color/text' });
  addRect(parent, { name: 'Знак', x: 24, y: 16, width: 32, height: 32, radius: 9, fill: 'color/accent' });
  addText(parent, { text: 'N', size: 19, weight: 'bold', color: 'color/text-inverse', x: 40, y: 21, align: 'center' });
  const leaf = figma.createEllipse();
  leaf.name = 'Лист';
  leaf.resize(13, 7);
  leaf.x = 46;
  leaf.y = 17;
  leaf.rotation = -38;
  leaf.fills = [paint('color/text-inverse')];
  parent.appendChild(leaf);
  addText(parent, { text: 'Norma', size: 18, weight: 'bold', color: 'color/text-inverse', x: 66, y: 21 });

  let x = 280;
  NAV.forEach((label) => {
    const isActive = label === active;
    const node = addText(parent, {
      text: label,
      size: 14,
      weight: isActive ? 'bold' : 'regular',
      color: isActive ? 'color/text-inverse' : 'color/text-muted',
      x,
      y: 23,
    });
    if (isActive) {
      addRect(parent, { name: 'Индикатор', x, y: 56, width: node.width, height: 2, radius: 1, fill: 'color/accent' });
    }
    x += node.width + 28;
  });

  addText(parent, { text: 'Элина', size: 14, color: 'color/text-muted', x: 1356, y: 24, align: 'right' });
  addCircle(parent, { x: 1382, y: 32, diameter: 32, fill: 'color/accent', stroke: 'color/text-muted', strokeWeight: 1 });
  addText(parent, { text: 'Э', size: 14, weight: 'bold', color: 'color/text-inverse', x: 1382, y: 25, align: 'center' });
}

function addDonut(parent, spec) {
  const outer = spec.diameter || 108;
  const thickness = spec.thickness || 14;
  const inner = 1 - thickness / outer;

  const track = figma.createEllipse();
  track.name = 'Дорожка кольца';
  track.resize(outer, outer);
  track.x = spec.x - outer / 2;
  track.y = spec.y - outer / 2;
  track.arcData = { startingAngle: 0, endingAngle: Math.PI * 2 - 0.001, innerRadius: inner };
  track.fills = [paint('color/track')];
  parent.appendChild(track);

  let angle = -Math.PI / 2;
  spec.segments.forEach((segment) => {
    const sweep = Math.PI * 2 * segment.value;
    const node = figma.createEllipse();
    node.name = 'Сегмент кольца';
    node.resize(outer, outer);
    node.x = spec.x - outer / 2;
    node.y = spec.y - outer / 2;
    node.arcData = {
      startingAngle: angle,
      endingAngle: angle + sweep - 0.035,
      innerRadius: inner,
    };
    node.fills = [paint(segment.color)];
    parent.appendChild(node);
    angle += sweep;
  });
}

function addChip(parent, spec) {
  addRect(parent, {
    name: 'Чип',
    x: spec.x,
    y: spec.y,
    width: spec.width,
    height: spec.height || 26,
    radius: FLOATS['radius/chip'],
    fill: spec.fill || 'color/accent-soft',
  });
  addText(parent, {
    text: spec.text,
    size: spec.size || 12,
    weight: 'bold',
    color: spec.color || 'color/accent',
    x: spec.x + 12,
    y: spec.y + 5,
  });
}

/* ---------- экраны ---------- */

function buildMain(frame) {
  const CO = CONTENT_X;
  const CW = CONTENT_WIDTH;

  addTopbar(frame, null);

  addText(frame, { text: 'Добрый день, Элина', size: 26, weight: 'bold', x: CO, y: 86 });
  addText(frame, { text: 'Среда, 25 ноября', size: 14, color: 'color/text-muted', x: CO + CW, y: 94, align: 'right' });

  addCard(frame, { name: 'Вес', x: CO, y: 132, width: CW, height: 200 });
  addText(frame, { text: 'Вес', size: 13, color: 'color/text-muted', x: CO + 24, y: 152 });
  const weightValue = addText(frame, { text: '72,4', size: 34, weight: 'bold', x: CO + 24, y: 172 });
  addText(frame, { text: 'кг', size: 16, color: 'color/text-muted', x: CO + 24 + weightValue.width + 8, y: 192 });
  addText(frame, { text: 'цель 68 кг', size: 13, color: 'color/text-muted', x: CO + 24 + weightValue.width + 40, y: 192 });
  addText(frame, { text: '-0,3 кг за неделю', size: 12, weight: 'bold', color: 'color/accent', x: CO + 24, y: 224 });
  addButton(frame, { kind: 'ghost', text: 'Внести текущий вес', size: 13, x: CO + 24, y: 250, width: 208, height: 38 });

  addText(frame, { text: '14 дней', size: 11, color: 'color/text-muted', x: CO + 696, y: 152, align: 'right' });
  [28, 56, 84].forEach((offset) => {
    addLine(frame, { x1: CO + 300, y1: 180 + offset, x2: CO + 696, y2: 180 + offset, color: 'color/track' });
  });

  const weights = [74.1, 74.0, 73.6, 73.8, 73.4, 73.1, 73.3, 72.9, 72.7, 72.8, 72.5, 72.6, 72.3, 72.4];
  const low = Math.min.apply(null, weights);
  const high = Math.max.apply(null, weights);
  const span = high - low || 1;
  const chartPoints = weights.map((weight, index) => ({
    x: CO + 300 + (396 * index) / (weights.length - 1),
    y: 296 - (108 * (weight - low)) / span,
  }));
  chartPoints.forEach((point, index) => {
    if (index === 0) return;
    const previous = chartPoints[index - 1];
    addLine(frame, { x1: previous.x, y1: previous.y, x2: point.x, y2: point.y, color: 'color/accent', thickness: 2 });
  });
  const lastPoint = chartPoints[chartPoints.length - 1];
  addCircle(frame, { x: lastPoint.x, y: lastPoint.y, diameter: 8, fill: 'color/accent', stroke: 'color/surface', strokeWeight: 2 });
  addText(frame, { text: 'сегодня 72,4 кг', size: 11, color: 'color/text-muted', x: CO + 696, y: 304, align: 'right' });

  addCard(frame, { name: 'Питание за день', x: CO, y: 352, width: CW, height: 200 });
  addText(frame, { text: 'Питание за день', size: 13, color: 'color/text-muted', x: CO + 24, y: 372 });
  addText(frame, { text: 'цель 2 100 ккал', size: 12, color: 'color/text-muted', x: CO + 696, y: 372, align: 'right' });

  addDonut(frame, {
    x: CO + 94,
    y: 462,
    diameter: 108,
    thickness: 14,
    segments: [
      { value: 60 / 157, color: 'color/protein' },
      { value: 34 / 157, color: 'color/fat-pastel' },
      { value: 63 / 157, color: 'color/carbs-pastel' },
    ],
  });
  addText(frame, { text: '798', size: 20, weight: 'bold', x: CO + 94, y: 444, align: 'center' });
  addText(frame, { text: 'ккал', size: 11, color: 'color/text-muted', x: CO + 94, y: 468, align: 'center' });

  const legend = [
    { label: 'Белки', value: '60 / 140 г', color: 'color/protein' },
    { label: 'Жиры', value: '34 / 60 г', color: 'color/fat-pastel' },
    { label: 'Углеводы', value: '63 / 210 г', color: 'color/carbs-pastel' },
  ];
  let maxLabelWidth = 0;
  legend.forEach((row, index) => {
    const y = 416 + index * 30;
    addCircle(frame, { x: CO + 184, y: y + 10, diameter: 12, fill: row.color });
    const label = addText(frame, { text: row.label, size: 13, x: CO + 196, y });
    maxLabelWidth = Math.max(maxLabelWidth, label.width);
  });
  legend.forEach((row, index) => {
    addText(frame, { text: row.value, size: 13, weight: 'bold', x: CO + 196 + maxLabelWidth + 14, y: 416 + index * 30 });
  });

  addButton(frame, { text: '+  Добавить продукт', size: 14, x: CO + 404, y: 408, width: 292, height: 42 });
  addText(frame, { text: 'осталось 1 302 ккал', size: 12, color: 'color/text-muted', x: CO + 404, y: 462 });
  addText(frame, { text: 'белки 80 г · жиры 26 г · углеводы 147 г', size: 12, color: 'color/text-muted', x: CO + 404, y: 482 });

  addCard(frame, { name: 'Вода', x: CO, y: 572, width: 348, height: 140 });
  addText(frame, { text: 'Вода', size: 13, color: 'color/text-muted', x: CO + 20, y: 590 });
  const water = addText(frame, { text: '1 400', size: 26, weight: 'bold', x: CO + 20, y: 610 });
  addText(frame, { text: 'из 2 300 мл', size: 13, color: 'color/text-muted', x: CO + 20 + water.width + 6, y: 622 });
  addBar(frame, { x: CO + 20, y: 658, width: 300, height: 8, value: 0.61, color: 'color/water', track: 'color/water-soft' });
  addText(frame, { text: 'осталось 900 мл', size: 12, color: 'color/text-muted', x: CO + 20, y: 676 });
  addPlusButton(frame, { x: CO + 300, y: 588, size: 28 });

  addCard(frame, { name: 'Активность', x: CO + 372, y: 572, width: 348, height: 140 });
  addText(frame, { text: 'Активность', size: 13, color: 'color/text-muted', x: CO + 392, y: 590 });
  const activity = addText(frame, { text: '190', size: 26, weight: 'bold', x: CO + 392, y: 610 });
  addText(frame, { text: 'ккал примерно', size: 13, color: 'color/text-muted', x: CO + 392 + activity.width + 6, y: 622 });
  addText(frame, { text: 'Ходьба 40 мин', size: 13, x: CO + 392, y: 658 });
  addText(frame, { text: 'записи придут из мобильного приложения', size: 11, color: 'color/text-muted', x: CO + 392, y: 684 });
}

function buildDiary(frame) {
  const CO = CONTENT_X;
  const CW = CONTENT_WIDTH;

  addTopbar(frame, 'Дневник');
  addText(frame, { text: 'Дневник', size: 28, weight: 'bold', x: CO, y: 92 });
  addText(frame, { text: 'Среда, 25 ноября', size: 14, color: 'color/text-muted', x: CO + 300, y: 100 });

  const meals = [
    { name: 'Завтрак', kcal: '540 ккал', items: [['Овсянка с ягодами', '250 г', '320 ккал'], ['Кофе с молоком', '200 мл', '60 ккал'], ['Банан', '1 шт', '160 ккал']] },
    { name: 'Перекус', kcal: '120 ккал', items: [['Яблоко', '1 шт', '120 ккал']] },
    { name: 'Обед', kcal: '620 ккал', items: [['Куриная грудка', '150 г', '250 ккал'], ['Рис', '200 г', '260 ккал'], ['Салат', '150 г', '110 ккал']] },
    { name: 'Полдник', kcal: '150 ккал', items: [['Творог', '100 г', '150 ккал']] },
    { name: 'Ужин', kcal: '480 ккал', items: [['Лосось', '150 г', '280 ккал'], ['Овощи на пару', '200 г', '200 ккал']] },
    { name: 'Вечерний перекус', kcal: '90 ккал', items: [['Кефир', '200 мл', '90 ккал']] },
  ];

  let y = 152;
  meals.forEach((meal) => {
    const height = 44 + meal.items.length * 32;
    addCard(frame, { name: meal.name, x: CO, y, width: CW, height });
    addText(frame, { text: meal.name, size: 15, weight: 'bold', x: CO + 20, y: y + 14 });
    addText(frame, { text: meal.kcal, size: 13, color: 'color/text-muted', x: CO + 648, y: y + 16, align: 'right' });
    addPlusButton(frame, { x: CO + 620, y: y + 12, size: 28 });
    meal.items.forEach((item, index) => {
      addText(frame, { text: item[0], size: 14, x: CO + 20, y: y + 44 + index * 32 });
      addText(frame, { text: item[1], size: 13, color: 'color/text-muted', x: CO + 320, y: y + 45 + index * 32 });
      addText(frame, { text: item[2], size: 13, color: 'color/text-muted', x: CO + 648, y: y + 45 + index * 32, align: 'right' });
    });
    y += height + 12;
  });
}

function buildWeight(frame) {
  const CO = CONTENT_X;
  const CW = CONTENT_WIDTH;

  addTopbar(frame, 'Вес');
  addText(frame, { text: 'Вес', size: 28, weight: 'bold', x: CO, y: 92 });

  addCard(frame, { name: 'Динамика веса', x: CO, y: 132, width: CW, height: 260 });
  addText(frame, { text: 'Динамика веса', size: 15, weight: 'bold', x: CO + 20, y: 152 });
  addText(frame, { text: '30 дней', size: 12, color: 'color/text-muted', x: CO + 696, y: 155, align: 'right' });

  const weights = [74.2, 74.0, 73.9, 73.7, 73.8, 73.5, 73.4, 73.2, 73.3, 73.0, 72.9, 72.7, 72.8, 72.6, 72.4];
  const low = Math.min.apply(null, weights);
  const high = Math.max.apply(null, weights);
  const span = high - low || 1;
  const chartX = CO + 64;
  const chartY = 186;
  const chartW = 600;
  const chartH = 120;

  [0, 0.5, 1].forEach((ratio) => {
    addLine(frame, { x1: chartX, y1: chartY + chartH * ratio, x2: chartX + chartW, y2: chartY + chartH * ratio, color: 'color/track' });
  });

  const points = weights.map((weight, index) => ({
    x: chartX + (chartW * index) / (weights.length - 1),
    y: chartY + chartH - (chartH * (weight - low)) / span,
  }));
  points.forEach((point, index) => {
    if (index === 0) return;
    const previous = points[index - 1];
    addLine(frame, { x1: previous.x, y1: previous.y, x2: point.x, y2: point.y, color: 'color/accent', thickness: 2 });
  });
  points.forEach((point, index) => {
    if (index % 2 !== 0) return;
    addCircle(frame, { x: point.x, y: point.y, diameter: 6, fill: 'color/surface', stroke: 'color/accent', strokeWeight: 2 });
  });

  addCard(frame, { name: 'Итоги', x: CO, y: 412, width: CW, height: 96 });
  const facts = [
    { label: 'Сейчас', value: '72,4', unit: 'кг', note: 'последний замер' },
    { label: 'Изменение', value: '-0,3', unit: 'кг', note: 'за неделю' },
    { label: 'Цель', value: '68', unit: 'кг', note: 'осталось 4,4 кг' },
  ];
  facts.forEach((fact, index) => {
    const x = CO + 20 + index * 232;
    addText(frame, { text: fact.label, size: 12, color: 'color/text-muted', x, y: 430 });
    const value = addText(frame, { text: fact.value, size: 22, weight: 'bold', x, y: 450 });
    addText(frame, { text: fact.unit, size: 12, color: 'color/text-muted', x: x + value.width + 6, y: 458 });
    addText(frame, { text: fact.note, size: 11, color: 'color/text-muted', x, y: 486 });
  });

  addCard(frame, { name: 'Замеры тела', x: CO, y: 528, width: CW, height: 196 });
  addText(frame, { text: 'Замеры тела', size: 15, weight: 'bold', x: CO + 20, y: 546 });
  addText(frame, { text: '12 ноября', size: 12, color: 'color/text-muted', x: CO + 696, y: 549, align: 'right' });

  const body = [
    { label: 'Грудь', value: '92', delta: '-1' },
    { label: 'Талия', value: '71', delta: '-2' },
    { label: 'Бедра', value: '98', delta: '0' },
    { label: 'Бедро', value: '56', delta: '-1' },
    { label: 'Плечо', value: '29', delta: '+1' },
    { label: 'Икра', value: '36', delta: '0' },
  ];
  body.forEach((row, index) => {
    const x = CO + 20 + index * 112;
    const label = addText(frame, { text: row.label, size: 11, color: 'color/text-muted', x, y: 580 });
    addInfoButton(frame, { x: x + label.width + 6, y: 579, size: 13 });
    const value = addText(frame, { text: row.value, size: 20, weight: 'bold', x, y: 598 });
    addText(frame, { text: 'см', size: 12, color: 'color/text-muted', x: x + value.width + 4, y: 606 });
    addText(frame, {
      text: '12 ноя · ' + row.delta + ' см',
      size: 10,
      color: row.delta.indexOf('-') === 0 ? 'color/accent' : 'color/text-muted',
      x,
      y: 628,
    });
  });

  addLine(frame, { x1: CO + 20, y1: 656, x2: CO + 700, y2: 656, color: 'color/track' });
  addButton(frame, { kind: 'ghost', text: '+  Внести замер', size: 13, x: CO + 456, y: 668, width: 220, height: 40 });

  addCard(frame, { name: 'История веса', x: CO, y: 748, width: CW, height: 240 });
  addText(frame, { text: 'История веса', size: 15, weight: 'bold', x: CO + 20, y: 766 });
  addText(frame, { text: 'все замеры', size: 12, color: 'color/accent', x: CO + 696, y: 769, align: 'right' });

  const history = [
    { date: '25 ноября', weight: '72,4', delta: '-0,3' },
    { date: '23 ноября', weight: '72,6', delta: '+0,2' },
    { date: '21 ноября', weight: '72,7', delta: '-0,1' },
    { date: '19 ноября', weight: '72,9', delta: '-0,4' },
    { date: '17 ноября', weight: '73,1', delta: '-0,2' },
  ];
  history.forEach((row, index) => {
    const y = 800 + index * 34;
    addText(frame, { text: row.date, size: 13, color: 'color/text-muted', x: CO + 20, y });
    addText(frame, { text: row.weight, size: 14, weight: 'bold', x: CO + 180, y });
    addText(frame, { text: 'кг', size: 12, color: 'color/text-muted', x: CO + 220, y: y + 2 });
    addText(frame, {
      text: row.delta,
      size: 12,
      color: row.delta.indexOf('-') === 0 ? 'color/accent' : 'color/text-muted',
      x: CO + 270,
      y: y + 2,
    });
    addText(frame, { text: 'изменить', size: 12, color: 'color/text-muted', x: CO + 696, y: y + 2, align: 'right' });
  });
}

function buildStats(frame) {
  const CO = CONTENT_X;
  const CW = CONTENT_WIDTH;

  addTopbar(frame, 'Статистика');
  addText(frame, { text: 'Статистика', size: 28, weight: 'bold', x: CO, y: 92 });

  const periods = ['7 дней', '30 дней', 'Год'];
  periods.forEach((label, index) => {
    const x = CO + 440 + index * 88;
    const active = index === 1;
    addRect(frame, {
      name: 'Период',
      x,
      y: 96,
      width: 80,
      height: 32,
      radius: 16,
      fill: active ? 'color/accent' : 'color/track',
    });
    addText(frame, {
      text: label,
      size: 12,
      weight: active ? 'bold' : 'regular',
      color: active ? 'color/text-inverse' : 'color/text-muted',
      x: x + 40,
      y: 105,
      align: 'center',
    });
  });

  addCard(frame, { name: 'Сводка', x: CO, y: 148, width: CW, height: 120 });
  const summary = [
    { label: 'Средние калории', value: '2 050', note: 'из 2 100' },
    { label: 'Средние белки', value: '118 г', note: 'из 140 г' },
    { label: 'Средние жиры', value: '64 г', note: 'из 60 г' },
    { label: 'Средние углеводы', value: '212 г', note: 'из 210 г' },
  ];
  summary.forEach((item, index) => {
    const x = CO + 20 + index * 172;
    addText(frame, { text: item.label, size: 11, color: 'color/text-muted', x, y: 168 });
    addText(frame, { text: item.value, size: 22, weight: 'bold', x, y: 188 });
    addText(frame, { text: item.note, size: 11, color: 'color/text-muted', x, y: 224 });
  });

  addCard(frame, { name: 'Калории по дням', x: CO, y: 288, width: CW, height: 280 });
  addText(frame, { text: 'Калории по дням', size: 15, weight: 'bold', x: CO + 20, y: 306 });
  addText(frame, { text: 'цель 2 100', size: 12, color: 'color/text-muted', x: CO + 696, y: 309, align: 'right' });

  const days = [1900, 2150, 1800, 2050, 2250, 2000, 2100];
  const labels = ['пн', 'вт', 'ср', 'чт', 'пт', 'сб', 'вс'];
  const barWidth = 56;
  const gap = 40;
  const baseY = 528;
  const maxValue = 2400;

  addLine(frame, { x1: CO + 20, y1: baseY, x2: CO + 700, y2: baseY, color: 'color/border' });
  days.forEach((value, index) => {
    const x = CO + 40 + index * (barWidth + gap);
    const height = (value / maxValue) * 180;
    const over = value > 2100;
    addRect(frame, {
      name: 'Столбец',
      x,
      y: baseY - height,
      width: barWidth,
      height,
      radius: 6,
      fill: over ? 'color/fat-pastel' : 'color/protein',
    });
    addText(frame, { text: labels[index], size: 11, color: 'color/text-muted', x: x + barWidth / 2, y: baseY + 10, align: 'center' });
    addText(frame, { text: String(value), size: 10, color: 'color/text-muted', x: x + barWidth / 2, y: baseY - height - 16, align: 'center' });
  });

  addCard(frame, { name: 'Дни', x: CO, y: 588, width: CW, height: 300 });
  addText(frame, { text: 'Дни', size: 15, weight: 'bold', x: CO + 20, y: 606 });

  const columns = [
    { label: 'Дата', x: CO + 20 },
    { label: 'Вес', x: CO + 200 },
    { label: 'Калории', x: CO + 320 },
    { label: 'БЖУ', x: CO + 460 },
  ];
  columns.forEach((column) => {
    addText(frame, { text: column.label, size: 11, color: 'color/text-muted', x: column.x, y: 638 });
  });

  const rows = [
    { date: '25 ноя', weight: '72,4', kcal: '2 050', macros: '120 / 64 / 210' },
    { date: '24 ноя', weight: '72,6', kcal: '2 250', macros: '130 / 70 / 230' },
    { date: '23 ноя', weight: '72,7', kcal: '1 800', macros: '110 / 55 / 190' },
    { date: '22 ноя', weight: '72,9', kcal: '2 150', macros: '125 / 68 / 215' },
    { date: '21 ноя', weight: '73,1', kcal: '1 900', macros: '115 / 60 / 200' },
    { date: '20 ноя', weight: '73,2', kcal: '2 100', macros: '120 / 65 / 205' },
  ];
  rows.forEach((row, index) => {
    const y = 668 + index * 34;
    addText(frame, { text: row.date, size: 13, x: CO + 20, y });
    addText(frame, { text: row.weight, size: 13, x: CO + 200, y });
    addText(frame, { text: row.kcal, size: 13, x: CO + 320, y });
    addText(frame, { text: row.macros, size: 12, color: 'color/text-muted', x: CO + 460, y: y + 1 });
  });
}

function buildProfile(frame) {
  const CO = CONTENT_X;
  const CW = CONTENT_WIDTH;

  addTopbar(frame, 'Профиль');
  addText(frame, { text: 'Профиль', size: 28, weight: 'bold', x: CO, y: 92 });
  addButton(frame, { text: 'Сохранить', size: 13, x: CO + 576, y: 92, width: 144, height: 40 });

  addCard(frame, { name: 'Личные данные', x: CO, y: 148, width: CW, height: 300 });
  addText(frame, { text: 'Личные данные', size: 15, weight: 'bold', x: CO + 20, y: 166 });

  const fields = [
    { label: 'Имя', value: 'Элина', x: CO + 20, y: 200, width: 300 },
    { label: 'Дата рождения', value: '14 марта 1994', x: CO + 340, y: 200, width: 340 },
    { label: 'Рост', value: '168 см', x: CO + 20, y: 272, width: 300 },
    { label: 'Целевой вес', value: '68 кг', x: CO + 340, y: 272, width: 340 },
    { label: 'Активность', value: 'Умеренная', x: CO + 20, y: 344, width: 300 },
    { label: 'Норма воды', value: '2 300 мл', x: CO + 340, y: 344, width: 340 },
  ];
  fields.forEach((field) => {
    addText(frame, { text: field.label, size: 12, color: 'color/text-muted', x: field.x, y: field.y });
    addRect(frame, {
      name: 'Поле',
      x: field.x,
      y: field.y + 18,
      width: field.width,
      height: 40,
      radius: FLOATS['radius/field'],
      fill: 'color/surface',
      stroke: 'color/border',
    });
    addText(frame, { text: field.value, size: 13, x: field.x + 12, y: field.y + 30 });
  });

  addCard(frame, { name: 'Замеры тела', x: CO, y: 468, width: CW, height: 276 });
  addText(frame, { text: 'Замеры тела', size: 15, weight: 'bold', x: CO + 20, y: 486 });
  addChip(frame, { text: 'необязательно', x: CO + 148, y: 484, width: 130, height: 26, size: 11, fill: 'color/track', color: 'color/text-muted' });
  addText(frame, { text: 'обновлено 12 ноября', size: 11, color: 'color/text-muted', x: CO + 696, y: 490, align: 'right' });

  const body = [
    { label: 'Грудь', value: '92' },
    { label: 'Талия', value: '71' },
    { label: 'Бедра', value: '98' },
    { label: 'Бедро', value: '56' },
    { label: 'Плечо', value: '29' },
    { label: 'Икра', value: '36' },
  ];
  body.forEach((row, index) => {
    const column = index % 3;
    const rowIndex = Math.floor(index / 3);
    const x = CO + 20 + column * 224;
    const y = 526 + rowIndex * 88;
    const label = addText(frame, { text: row.label, size: 12, color: 'color/text-muted', x, y });
    addInfoButton(frame, { x: x + label.width + 6, y: y - 2, size: 13 });
    addRect(frame, {
      name: 'Поле замера',
      x,
      y: y + 18,
      width: 208,
      height: 40,
      radius: FLOATS['radius/field'],
      fill: 'color/surface',
      stroke: 'color/border',
    });
    addText(frame, { text: row.value, size: 13, x: x + 12, y: y + 30 });
    addText(frame, { text: 'см', size: 12, color: 'color/text-muted', x: x + 196, y: y + 31, align: 'right' });
    addText(frame, { text: 'обновлено 12 ноября', size: 10, color: 'color/text-muted', x, y: y + 64 });
  });
}

function buildMeasureModal(frame) {
  addRect(frame, { name: 'Затемнение', x: 0, y: 0, width: 1440, height: 1190, fill: 'color/overlay', opacity: 0.45 });

  const MX = 420;
  const MY = 331;
  const MW = 600;
  const MH = 488;

  addCard(frame, { name: 'Форма замера', x: MX, y: MY, width: MW, height: MH, radius: FLOATS['radius/modal'] });
  addText(frame, { text: 'Внести замер', size: 18, weight: 'bold', x: MX + 24, y: MY + 22 });
  addText(frame, { text: '×', size: 20, color: 'color/text-muted', x: MX + MW - 24, y: MY + 22, align: 'right' });
  addText(frame, { text: 'Все поля необязательны - заполни то, что удалось измерить', size: 12, color: 'color/text-muted', x: MX + 24, y: MY + 54 });

  addText(frame, { text: 'Дата', size: 12, color: 'color/text-muted', x: MX + 24, y: MY + 88 });
  addRect(frame, {
    name: 'Поле даты',
    x: MX + 24,
    y: MY + 106,
    width: 240,
    height: 40,
    radius: FLOATS['radius/field'],
    fill: 'color/surface',
    stroke: 'color/border',
  });
  addText(frame, { text: '25 ноября 2026', size: 13, x: MX + 38, y: MY + 116 });

  const body = [
    { label: 'Грудь', value: '92' },
    { label: 'Талия', value: '71' },
    { label: 'Бедра', value: '98' },
    { label: 'Бедро', value: '56' },
    { label: 'Плечо', value: '29' },
    { label: 'Икра', value: '36' },
  ];
  body.forEach((row, index) => {
    const column = index % 2;
    const rowIndex = Math.floor(index / 2);
    const x = MX + 24 + column * 284;
    const y = MY + 160 + rowIndex * 72;
    addText(frame, { text: row.label, size: 12, color: 'color/text-muted', x, y });
    addRect(frame, {
      name: 'Поле замера',
      x,
      y: y + 18,
      width: 268,
      height: 44,
      radius: FLOATS['radius/field'],
      fill: 'color/surface',
      stroke: 'color/border',
    });
    addText(frame, { text: row.value, size: 14, x: x + 14, y: y + 30 });
    addText(frame, { text: 'см', size: 12, color: 'color/text-muted', x: x + 254, y: y + 31, align: 'right' });
  });

  addText(frame, { text: 'Прошлые значения подставлены - можно поправить', size: 11, color: 'color/text-muted', x: MX + 24, y: MY + 388 });
  addButton(frame, { kind: 'ghost', text: 'Отмена', size: 13, x: MX + 24, y: MY + 420, width: 180, height: 44 });
  addButton(frame, { text: 'Сохранить', size: 13, x: MX + 296, y: MY + 420, width: 280, height: 44 });
}

/* ---------- дизайн-система и сборка ---------- */

const SCREENS = [
  { name: 'Главная', width: 1440, height: 780, build: buildMain },
  { name: 'Дневник', width: 1440, height: 920, build: buildDiary },
  { name: 'Вес', width: 1440, height: 1020, build: buildWeight },
  { name: 'Статистика', width: 1440, height: 920, build: buildStats },
  { name: 'Профиль', width: 1440, height: 760, build: buildProfile },
  { name: 'Форма замера', width: 1440, height: 1190, build: buildMeasureModal },
];

async function loadFonts() {
  const styles = Object.keys(FONT_WEIGHTS).map((key) => FONT_WEIGHTS[key]);
  for (const style of styles) {
    await figma.loadFontAsync({ family: FONT_FAMILY, style });
  }
}

async function createVariables() {
  let collection = null;
  try {
    const collections = await figma.variables.getLocalVariableCollectionsAsync();
    collection = collections.find((item) => item.name === 'Norma') || null;
  } catch (error) {
    collection = null;
  }
  if (!collection) {
    collection = figma.variables.createVariableCollection('Norma');
  }
  const modeId = collection.modes[0].modeId;

  let existing = [];
  try {
    existing = await figma.variables.getLocalVariablesAsync();
  } catch (error) {
    existing = [];
  }
  const byName = new Map(existing.map((variable) => [variable.name, variable]));

  const ensure = (key, type, value) => {
    const variable = byName.get(key) || figma.variables.createVariable(key, collection, type);
    variable.setValueForMode(modeId, value);
    state.variables[key] = variable;
  };

  Object.keys(COLORS).forEach((key) => ensure(key, 'COLOR', hexToRgb(COLORS[key])));
  Object.keys(FLOATS).forEach((key) => ensure(key, 'FLOAT', FLOATS[key]));

  return collection;
}

async function createTextStyles() {
  let existing = [];
  try {
    existing = await figma.getLocalTextStylesAsync();
  } catch (error) {
    existing = [];
  }
  const byName = new Map(existing.map((style) => [style.name, style]));

  TEXT_STYLES.forEach((definition) => {
    const textStyle = byName.get(definition.name) || figma.createTextStyle();
    textStyle.name = definition.name;
    textStyle.fontName = { family: FONT_FAMILY, style: FONT_WEIGHTS[definition.weight] };
    textStyle.fontSize = definition.size;
    textStyle.lineHeight = { unit: 'PIXELS', value: definition.lineHeight };
    state.textStyles[definition.name] = textStyle;
  });
}

async function createPage() {
  let page = null;
  try {
    const pages = figma.root.children.filter((child) => child.type === 'PAGE');
    page = pages.find((child) => child.name === PAGE_NAME) || null;
  } catch (error) {
    page = null;
  }

  if (page) {
    try {
      if (typeof page.loadAsync === 'function') {
        await page.loadAsync();
      }
      [...page.children].forEach((child) => child.remove());
    } catch (error) {
      page = null;
    }
  }

  if (!page) {
    page = figma.createPage();
    page.name = PAGE_NAME;
  }

  if (typeof figma.setCurrentPageAsync === 'function') {
    await figma.setCurrentPageAsync(page);
  } else {
    figma.currentPage = page;
  }
  state.page = page;
  return page;
}

function buildScreens(page) {
  let x = 0;
  SCREENS.forEach((screen) => {
    const frame = figma.createFrame();
    frame.name = screen.name;
    frame.resize(screen.width, screen.height);
    frame.x = x;
    frame.y = 0;
    frame.fills = [paint('color/bg')];
    frame.clipsContent = true;
    page.appendChild(frame);
    screen.build(frame);
    x += screen.width + FRAME_GAP;
  });
}

async function generate() {
  await loadFonts();
  const collection = await createVariables();
  await createTextStyles();
  const page = await createPage();
  buildScreens(page);

  return {
    collection: collection.name,
    variables: Object.keys(state.variables).length,
    textStyles: Object.keys(state.textStyles).length,
    screens: SCREENS.length,
  };
}

/* ---------- запуск ---------- */

figma.showUI(__html__, { width: 360, height: 420, themeColors: true });

figma.ui.onmessage = async (message) => {
  if (!message || message.type !== 'generate') return;
  try {
    const result = await generate();
    figma.ui.postMessage({ type: 'done', result });
    figma.notify('Макеты «Нормы» собраны');
  } catch (error) {
    figma.ui.postMessage({
      type: 'error',
      message: String(error && error.message ? error.message : error),
    });
  }
};
