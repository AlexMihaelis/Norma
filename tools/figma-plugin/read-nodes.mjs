/*
 * Компактное чтение экспорта Figma.
 *
 * Экспорт с шестью экранами весит около 700 КБ - целиком в чат его не отдать.
 * Этот скрипт печатает только то, что нужно для верстки: список фреймов, дерево
 * одного фрейма, токены или поиск по именам и текстам.
 *
 * Запуск:
 *   node tools/figma-plugin/read-nodes.mjs design/figma-2026-11-25.json
 *   node tools/figma-plugin/read-nodes.mjs design/figma-2026-11-25.json --frame "Вес"
 *   node tools/figma-plugin/read-nodes.mjs design/figma-2026-11-25.json --frame "Вес" --depth 2
 *   node tools/figma-plugin/read-nodes.mjs design/figma-2026-11-25.json --tokens
 *   node tools/figma-plugin/read-nodes.mjs design/figma-2026-11-25.json --search "Талия"
 *
 * Флаги:
 *   --frame <имя>   дерево одного фрейма (без флага - список фреймов)
 *   --depth <N>     глубина дерева, по умолчанию 3
 *   --tokens        переменные и текстовые стили
 *   --usage         сводка частот цветов, радиусов и отступов
 *   --search <txt>  узлы, в имени или тексте которых есть подстрока
 *   --json          печатать данные как JSON, а не строками
 */
import { readFileSync } from 'node:fs';

const args = process.argv.slice(2);
const file = args[0];
if (!file) {
  console.error('Укажи файл экспорта: node read-nodes.mjs design/figma-*.json [--frame "Имя"]');
  process.exit(2);
}

const flag = (name, fallback = null) => {
  const index = args.indexOf(name);
  return index === -1 ? fallback : args[index + 1] ?? fallback;
};
const has = (name) => args.includes(name);

const data = JSON.parse(readFileSync(file, 'utf8'));
const meta = data.meta || {};
const nodes = Array.isArray(data.nodes) ? data.nodes : [];
const tokens = data.tokens || {};

/* ---------- форматирование ---------- */

const shortType = {
  FRAME: 'FRAME',
  GROUP: 'GROUP',
  RECTANGLE: 'RECT',
  ELLIPSE: 'ELLI',
  TEXT: 'TEXT',
  VECTOR: 'VECT',
  LINE: 'LINE',
  COMPONENT: 'COMP',
  INSTANCE: 'INST',
};

const size = (node) => {
  const value = node.size || node.absolute || {};
  return `${Math.round(value.width ?? 0)}x${Math.round(value.height ?? 0)}`;
};

const at = (node) => {
  const value = node.absolute || node.position || {};
  return `${Math.round(value.x ?? 0)},${Math.round(value.y ?? 0)}`;
};

const paint = (node) => {
  const fills = (node.fills || []).filter((item) => item && item.visible !== false);
  if (!fills.length) return '';
  const first = fills[0];
  if (first.type === 'SOLID') {
    const name = first.variable && first.variable.name;
    return `${first.hex}${name ? ' var(--' + name.replace(/\//g, '-') + ')' : ''}`;
  }
  if (first.type === 'GRADIENT_LINEAR' || first.type === 'GRADIENT_RADIAL') {
    return 'gradient';
  }
  return first.type;
};

const text = (node) => {
  const value = node.text || {};
  const characters = value.characters || '';
  const truncated = value.truncated ? ' [обрезано]' : '';
  const font = value.fontSize ? `${value.fontSize}/${value.fontWeight ?? ''}` : '';
  const style = value.textStyle ? ` ${value.textStyle}` : '';
  return `${font}${style} "${characters}"${truncated}`;
};

const line = (node, depth) => {
  const indent = '  '.repeat(depth);
  const type = shortType[node.type] || node.type;
  const name = node.name || '(без имени)';
  const parts = [`${indent}${type} ${name}`, size(node), '@' + at(node)];

  if (node.cornerRadius) parts.push(`r${node.cornerRadius}`);
  if (node.layoutMode && node.layoutMode !== 'NONE') {
    parts.push(node.layoutMode.toLowerCase());
    if (node.itemSpacing) parts.push(`gap ${node.itemSpacing}`);
    if (node.padding) {
      const pad = node.padding;
      const values = [pad.top, pad.right, pad.bottom, pad.left].filter((v) => v !== undefined);
      if (values.length) parts.push(`pad ${values.join('/')}`);
    }
  }
  const fill = paint(node);
  if (fill) parts.push(fill);
  if (node.type === 'TEXT') parts.push(text(node));
  if (node.visible === false) parts.push('скрыт');
  if (node.children && node.children.length) parts.push(`(${node.children.length})`);

  return parts.join('  ');
};

const walk = (node, depth, maxDepth, out) => {
  out.push(line(node, depth));
  if (depth >= maxDepth) return;
  (node.children || []).forEach((child) => walk(child, depth + 1, maxDepth, out));
};

const findNode = (node, name) => {
  if (node.name === name) return node;
  for (const child of node.children || []) {
    const found = findNode(child, name);
    if (found) return found;
  }
  return null;
};

const slug = (name) => name.replace(/\//g, '-');

/* ---------- режимы ---------- */

const output = [];

if (has('--tokens')) {
  const collections = (tokens.variables && tokens.variables.collections) || [];
  const variables = collections.flatMap((collection) =>
    (collection.variables || []).map((variable) => ({
      ...variable,
      collection: collection.name,
      modes: collection.modes || [],
    })),
  );
  output.push(`Переменные: ${variables.length}`);
  variables.forEach((variable) => {
    const values = variable.valuesByMode || {};
    const mode = Object.keys(values)[0];
    const raw = mode ? values[mode] : null;
    const value = raw && typeof raw === 'object' ? JSON.stringify(raw) : raw;
    output.push(`  ${variable.name}  =  ${value}  (${variable.type})`);
  });

  const styles = tokens.styles || {};
  const textStyles = styles.texts || [];
  output.push('');
  output.push(`Текстовые стили: ${textStyles.length}`);
  textStyles.forEach((style) => {
    const font = style.fontName ? `${style.fontName.family} ${style.fontName.style}` : '';
    const lineHeight = style.lineHeight ? JSON.stringify(style.lineHeight) : '';
    output.push(
      `  ${style.name}  ${font} ${style.fontSize ?? ''}/${style.fontWeight ?? ''} lh ${lineHeight}`,
    );
  });
}

if (has('--usage')) {
  const usage = data.usage || {};
  const section = (title, items, format, empty) => {
    output.push('');
    output.push(title);
    if (!items || !items.length) {
      output.push('  ' + (empty || 'пусто'));
      return;
    }
    items.slice(0, 12).forEach((item) => output.push('  ' + format(item)));
  };
  section('Цвета:', usage.colors, (item) =>
    `${item.hex}  x${item.count}${item.variable ? '  var(--' + slug(item.variable) + ')' : ''}`,
  );
  section('Радиусы:', usage.radii, (item) => `${item.value}px  x${item.count}`);
  section(
    'Отступы:',
    usage.spacing,
    (item) => `${item.value}px  x${item.count}`,
    'нет данных: макет собран абсолютными координатами, без авто-разметки',
  );
  section('Типографика:', usage.typography, (item) =>
    `${item.style || ''} ${item.size || ''}px  x${item.count}`,
  );
}

if (has('--search')) {
  const needle = (flag('--search') || '').toLowerCase();
  const found = [];
  const scan = (node) => {
    const characters = (node.text && node.text.characters) || '';
    if ((node.name || '').toLowerCase().includes(needle) || characters.toLowerCase().includes(needle)) {
      found.push(node);
    }
    (node.children || []).forEach(scan);
  };
  nodes.forEach(scan);
  output.push(`Найдено: ${found.length}`);
  found.slice(0, 40).forEach((node) => output.push(line(node, 0)));
}

if (has('--frame')) {
  const name = flag('--frame');
  const frame = nodes.find((node) => node.name === name) || (nodes[0] && findNode(nodes[0], name));
  if (!frame) {
    console.error(`Фрейм «${name}» не найден. Есть: ${nodes.map((n) => n.name).join(', ')}`);
    process.exit(1);
  }
  const depth = Number(flag('--depth', 3));
  const lines = [];
  walk(frame, 0, depth, lines);
  output.push(
    `${meta.fileName || '?'} · ${meta.pageName || '?'} · экспорт ${meta.exportedAt || '?'}`,
  );
  output.push('');
  output.push(...lines);
}

if (!has('--tokens') && !has('--usage') && !has('--search') && !has('--frame')) {
  output.push(
    `${meta.fileName || '?'} · ${meta.pageName || '?'} · экспорт ${meta.exportedAt || '?'}`,
  );
  output.push(`Выделено фреймов: ${nodes.length}`);
  output.push('');
  nodes.forEach((node) => {
    const count = (node.children || []).length;
    output.push(`  ${node.name}  ${size(node)}  @${at(node)}  детей: ${count}`);
  });
  output.push('');
  output.push('Дальше: --frame "Имя" для дерева, --tokens для переменных, --usage для частот.');
}

if (has('--json')) {
  console.log(JSON.stringify({ meta, nodes: nodes.map((n) => ({ name: n.name, size: n.size })) }, null, 2));
} else {
  console.log(output.join('\n'));
}
