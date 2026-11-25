/*
 * Norma · экспорт дизайна из Figma.
 *
 * Плагин ничего не отправляет в сеть (networkAccess: none). Он читает
 * выделение, переменные и локальные стили файла и отдает один JSON в UI.
 * Дальше JSON кладется в репозиторий, а из него собираются стили.
 *
 * Почему все чтения через safe(): свойства узлов в Figma бывают "mixed"
 * (у текста разные шрифты в абзаце, у рамки разные радиусы углов), а у
 * листовых узлов некоторых свойств просто нет. Прямое чтение бросает
 * исключение, поэтому каждое необязательное поле читается через обертку.
 */

const PLUGIN_VERSION = '1.0.0';

const LIMITS = {
  maxNodes: 4000, // потолок на весь обход: защита от зависания на гигантских фреймах
  maxDepth: 60,
  maxTextChars: 300,
  maxSegmentChars: 80,
  maxSegments: 20,
  maxUsageEntries: 40,
};

figma.showUI(__html__, { width: 400, height: 600, themeColors: true, title: 'Norma · экспорт дизайна' });

// Индексы заполняются перед обходом дерева, чтобы имя переменной или стиля
// можно было подставить в узел, не делая лишних запросов.
const varById = new Map();
const varMeta = new Map();
const styleNameById = {
  paint: new Map(),
  text: new Map(),
  effect: new Map(),
  grid: new Map(),
};

// Сводка частот: собирается попутно с обходом, нужна для генерации токенов.
let usage;

function resetState() {
  varById.clear();
  varMeta.clear();
  styleNameById.paint.clear();
  styleNameById.text.clear();
  styleNameById.effect.clear();
  styleNameById.grid.clear();
  usage = {
    colors: new Map(),
    typography: new Map(),
    radii: new Map(),
    spacing: new Map(),
  };
}

figma.ui.onmessage = async (message) => {
  if (!message || typeof message !== 'object') return;

  if (message.type === 'export') {
    try {
      const result = await buildExport(message.options || {});
      const json = JSON.stringify(result, null, 2);
      figma.ui.postMessage({
        type: 'result',
        json,
        stats: {
          nodes: result.meta.nodesVisited,
          selection: result.meta.selection.length,
          variables: countVariables(result),
          styles: countStyles(result),
          colors: result.usage ? result.usage.colors.length : 0,
          truncated: result.meta.truncated,
          fileName: result.meta.fileName,
        },
      });
    } catch (error) {
      figma.ui.postMessage({ type: 'error', message: errorText(error) });
    }
  }

  if (message.type === 'close') figma.closePlugin();
};

async function buildExport(options) {
  resetState();

  const selection = figma.currentPage.selection || [];
  if (!selection.length) {
    throw new Error('Ничего не выделено. Выдели один или несколько фреймов на холсте и нажми кнопку снова.');
  }

  const wantTree = options.tree !== false;
  const wantTokens = options.tokens !== false;
  const wantUsage = options.usage !== false;

  // Токены читаются первыми: в них лежит словарь переменных, без него
  // узлы не смогут показать, к какой переменной привязан цвет.
  const tokens = wantTokens ? await collectTokens() : null;

  const ctx = { count: 0, truncated: false, reason: null };
  const nodes = [];
  for (const node of selection) {
    nodes.push(await serializeNode(node, 0, ctx));
  }

  const meta = {
    plugin: 'norma-design-export',
    version: PLUGIN_VERSION,
    exportedAt: new Date().toISOString(),
    fileName: safe(() => figma.root.name, null),
    fileKey: safe(() => figma.fileKey, null),
    pageName: safe(() => figma.currentPage.name, null),
    selection: selection.map((node) => ({
      id: node.id,
      name: node.name,
      type: node.type,
      width: round(safe(() => node.width, 0), 2),
      height: round(safe(() => node.height, 0), 2),
    })),
    nodesVisited: ctx.count,
    truncated: ctx.truncated,
    truncatedReason: ctx.reason,
    options: { tree: wantTree, tokens: wantTokens, usage: wantUsage },
  };

  const result = { meta };
  if (wantTokens) result.tokens = tokens;
  if (wantTree) result.nodes = nodes;
  if (wantUsage) result.usage = usageOutput();
  return result;
}

/* ---------- токены: переменные и локальные стили ---------- */

async function collectTokens() {
  const variables = { collections: [], note: null };

  if (!figma.variables) {
    variables.note = 'API переменных недоступно в этой версии Figma: обнови клиент, если нужны токены из панели Variables';
  } else {
    try {
      const collections = await figma.variables.getLocalVariableCollectionsAsync();
      const allVariables = await figma.variables.getLocalVariablesAsync();

      const collectionById = new Map();
      for (const collection of collections) collectionById.set(collection.id, collection);

      for (const variable of allVariables) {
        varById.set(variable.id, variable);
        varMeta.set(variable.id, { name: variable.name, collection: null, type: variable.resolvedType });
      }
      for (const variable of allVariables) {
        const collection = collectionById.get(variable.variableCollectionId);
        const meta = varMeta.get(variable.id);
        if (collection && meta) meta.collection = collection.name;
      }

      for (const collection of collections) {
        const block = {
          id: collection.id,
          name: collection.name,
          modes: collection.modes.map((mode) => ({ id: mode.modeId, name: mode.name })),
          variables: [],
        };
        for (const variable of allVariables) {
          if (variable.variableCollectionId !== collection.id) continue;
          const entry = {
            id: variable.id,
            name: variable.name,
            type: variable.resolvedType,
            description: variable.description || null,
            valuesByMode: {},
          };
          for (const mode of collection.modes) {
            entry.valuesByMode[mode.name] = await resolveVariableValue(variable.valuesByMode[mode.modeId], 0);
          }
          block.variables.push(entry);
        }
        variables.collections.push(block);
      }

      if (!variables.collections.length) variables.note = 'В файле нет локальных переменных: собирать нечего, цвета ниже из сводки частот';
    } catch (error) {
      variables.note = 'Не удалось прочитать переменные: ' + errorText(error);
    }
  }

  return { variables, styles: await collectStyles() };
}

/**
 * Значение переменной в режиме (mode) может быть ссылкой на другую переменную
 * (VARIABLE_ALIAS). Разворачиваем ссылку, чтобы в JSON лежало и имя, и цвет.
 * Глубина ограничена: в файлах встречаются цепочки алиасов.
 */
async function resolveVariableValue(value, depth) {
  if (value === null || value === undefined) return null;
  if (depth > 4) return { unresolved: true };

  if (typeof value === 'object' && value.type === 'VARIABLE_ALIAS') {
    let target = varById.get(value.id);
    if (!target) {
      try {
        target = await figma.variables.getVariableByIdAsync(value.id);
      } catch (error) {
        target = null;
      }
    }
    if (!target) return { alias: { id: value.id, name: null }, resolved: null };

    const modeIds = Object.keys(target.valuesByMode || {});
    const resolved = modeIds.length ? await resolveVariableValue(target.valuesByMode[modeIds[0]], depth + 1) : null;
    return { alias: { id: target.id, name: target.name }, resolved };
  }

  if (typeof value === 'object' && typeof value.r === 'number') {
    return { hex: toHex(value), alpha: round(typeof value.a === 'number' ? value.a : 1, 3) };
  }

  return value;
}

async function collectStyles() {
  const out = { paints: [], texts: [], effects: [], grids: [], note: null };

  try {
    const paintStyles = await figma.getLocalPaintStylesAsync();
    for (const style of paintStyles) {
      styleNameById.paint.set(style.id, style.name);
      out.paints.push({
        id: style.id,
        name: style.name,
        // collect=false: цвет из определения стиля не должен попадать в сводку
        // частот, иначе один и тот же цвет считается дважды.
        paints: (style.paints || []).map((paint) => serializePaint(paint, false)),
      });
    }
  } catch (error) {
    out.note = joinNote(out.note, 'стили заливок: ' + errorText(error));
  }

  try {
    const textStyles = await figma.getLocalTextStylesAsync();
    for (const style of textStyles) {
      styleNameById.text.set(style.id, style.name);
      out.texts.push({
        id: style.id,
        name: style.name,
        fontName: style.fontName || null,
        fontSize: style.fontSize,
        lineHeight: serializeLineHeight(style.lineHeight),
        letterSpacing: serializeLetterSpacing(style.letterSpacing),
        paragraphSpacing: style.paragraphSpacing,
        textCase: style.textCase || null,
      });
    }
  } catch (error) {
    out.note = joinNote(out.note, 'текстовые стили: ' + errorText(error));
  }

  try {
    const effectStyles = await figma.getLocalEffectStylesAsync();
    for (const style of effectStyles) {
      styleNameById.effect.set(style.id, style.name);
      out.effects.push({
        id: style.id,
        name: style.name,
        effects: (style.effects || []).map(serializeEffect),
      });
    }
  } catch (error) {
    out.note = joinNote(out.note, 'стили эффектов: ' + errorText(error));
  }

  try {
    const gridStyles = await figma.getLocalGridStylesAsync();
    for (const style of gridStyles) {
      styleNameById.grid.set(style.id, style.name);
      out.grids.push({ id: style.id, name: style.name, grids: style.layoutGrids || [] });
    }
  } catch (error) {
    out.note = joinNote(out.note, 'сетки: ' + errorText(error));
  }

  return out;
}

/* ---------- обход дерева ---------- */

async function serializeNode(node, depth, ctx) {
  if (!node || typeof node !== 'object') return null;

  if (ctx.count >= LIMITS.maxNodes) {
    ctx.truncated = true;
    ctx.reason = 'node-limit';
    return { id: node.id, type: node.type, name: node.name, skipped: 'node-limit' };
  }
  if (depth > LIMITS.maxDepth) {
    ctx.truncated = true;
    ctx.reason = 'depth-limit';
    return { id: node.id, type: node.type, name: node.name, skipped: 'depth-limit' };
  }
  ctx.count += 1;

  const out = {
    id: node.id,
    type: node.type,
    name: node.name,
    visible: safe(() => node.visible !== false, true),
    size: { width: round(safe(() => node.width, 0), 2), height: round(safe(() => node.height, 0), 2) },
    position: { x: round(safe(() => node.x, 0), 2), y: round(safe(() => node.y, 0), 2) },
    rotation: round(safe(() => node.rotation, 0), 2),
    opacity: round(safe(() => node.opacity, 1), 3),
  };

  const absolute = safe(() => node.absoluteBoundingBox, null);
  if (absolute) {
    out.absolute = {
      x: round(absolute.x, 2),
      y: round(absolute.y, 2),
      width: round(absolute.width, 2),
      height: round(absolute.height, 2),
    };
  }

  const radius = serializeRadius(node);
  if (radius !== null) out.cornerRadius = radius;

  const fills = serializePaints(node, 'fill');
  if (fills) out.fills = fills;

  const strokes = serializePaints(node, 'stroke');
  if (strokes) out.strokes = strokes;

  // У любого узла в Figma есть strokeWeight (по умолчанию 1), даже когда обводки
  // нет. Пишем толщину и выравнивание только при реальной обводке, иначе JSON шумит.
  const hasStroke = Array.isArray(strokes) && strokes.some((paint) => paint && paint.visible !== false);
  if (hasStroke) {
    const strokeWeight = safe(() => node.strokeWeight, null);
    out.strokeWeight = strokeWeight === 'mixed' ? 'mixed' : round(strokeWeight, 2);
    out.strokeAlign = safe(() => node.strokeAlign, null);
  }

  const layout = serializeLayout(node);
  if (layout) out.layout = layout;

  const text = serializeText(node);
  if (text) out.text = text;

  const effects = serializeEffects(node);
  if (effects.length) out.effects = effects;

  const styleRefs = serializeStyleRefs(node);
  if (styleRefs) out.styles = styleRefs;

  const component = await serializeComponent(node);
  if (component) out.component = component;

  if (node.type === 'FRAME') out.clipsContent = safe(() => node.clipsContent, null);

  const children = safe(() => node.children, null);
  if (Array.isArray(children) && children.length) {
    out.children = [];
    for (const child of children) {
      out.children.push(await serializeNode(child, depth + 1, ctx));
    }
  }

  return out;
}

function serializePaints(node, kind) {
  let paints;
  try {
    paints = kind === 'fill' ? node.fills : node.strokes;
  } catch (error) {
    return null;
  }
  if (paints === figma.mixed) return { mixed: true };
  if (!Array.isArray(paints) || !paints.length) return null;

  return paints.map((paint) => serializePaint(paint, true, kind));
}

function serializePaint(paint, collectUsage, kind) {
  if (!paint || typeof paint !== 'object') return null;

  const out = { type: paint.type, visible: paint.visible !== false };

  if (paint.type === 'SOLID') {
    out.hex = toHex(paint.color);
    const baseAlpha = paint.color && typeof paint.color.a === 'number' ? paint.color.a : 1;
    out.alpha = round((paint.opacity === null || paint.opacity === undefined ? 1 : paint.opacity) * baseAlpha, 3);
    if (collectUsage) addColorUsage(out.hex, out.alpha, kind === 'stroke' ? 'stroke' : 'fill', describeAlias(boundColor(paint)));
  } else if (typeof paint.type === 'string' && paint.type.indexOf('GRADIENT') === 0) {
    out.stops = (paint.gradientStops || []).map((stop) => ({
      position: round(stop.position, 3),
      hex: toHex(stop.color),
      alpha: round(stop.color && typeof stop.color.a === 'number' ? stop.color.a : 1, 3),
    }));
    out.alpha = round(paint.opacity === null || paint.opacity === undefined ? 1 : paint.opacity, 3);
  } else if (paint.type === 'IMAGE') {
    out.scaleMode = paint.scaleMode || null;
    out.imageHash = paint.imageHash ? String(paint.imageHash).slice(0, 8) : null;
  }

  const alias = describeAlias(boundColor(paint));
  if (alias) out.variable = alias;

  return out;
}

function boundColor(paint) {
  try {
    return paint.boundVariables && paint.boundVariables.color ? paint.boundVariables.color : null;
  } catch (error) {
    return null;
  }
}

function describeAlias(alias) {
  if (!alias || typeof alias !== 'object' || !alias.id) return null;
  const meta = varMeta.get(alias.id);
  return meta
    ? { id: alias.id, name: meta.name, collection: meta.collection || null }
    : { id: alias.id, name: null, collection: null };
}

function serializeRadius(node) {
  try {
    if (!('cornerRadius' in node)) return null;
    const radius = node.cornerRadius;
    if (radius === figma.mixed) {
      return {
        topLeft: safe(() => node.topLeftRadius, null),
        topRight: safe(() => node.topRightRadius, null),
        bottomRight: safe(() => node.bottomRightRadius, null),
        bottomLeft: safe(() => node.bottomLeftRadius, null),
      };
    }
    if (typeof radius === 'number') {
      if (radius > 0) addUsage('radii', String(round(radius, 2)));
      return radius;
    }
    return null;
  } catch (error) {
    return null;
  }
}

function serializeLayout(node) {
  const mode = safe(() => node.layoutMode, null);
  if (!mode || mode === 'NONE') return null;

  const padding = {
    top: round(safe(() => node.paddingTop, 0), 2),
    right: round(safe(() => node.paddingRight, 0), 2),
    bottom: round(safe(() => node.paddingBottom, 0), 2),
    left: round(safe(() => node.paddingLeft, 0), 2),
  };
  const gap = round(safe(() => node.itemSpacing, 0), 2);

  for (const value of [gap, padding.top, padding.right, padding.bottom, padding.left]) {
    if (typeof value === 'number' && value > 0) addUsage('spacing', String(value));
  }

  return {
    mode,
    wrap: safe(() => node.layoutWrap, null),
    gap,
    padding,
    align: {
      primary: safe(() => node.primaryAxisAlignItems, null),
      counter: safe(() => node.counterAxisAlignItems, null),
    },
    sizing: {
      horizontal: safe(() => node.layoutSizingHorizontal, null),
      vertical: safe(() => node.layoutSizingVertical, null),
    },
    grow: safe(() => node.layoutGrow, 0),
  };
}

function serializeText(node) {
  if (node.type !== 'TEXT') return null;

  const raw = String(safe(() => node.characters, ''));
  const text = {
    characters: raw.length > LIMITS.maxTextChars ? raw.slice(0, LIMITS.maxTextChars) + '...' : raw,
    truncated: raw.length > LIMITS.maxTextChars,
    fontName: safe(() => node.fontName, null),
    fontSize: safe(() => node.fontSize, null),
    fontWeight: safe(() => node.fontWeight, null),
    lineHeight: serializeLineHeight(safe(() => node.lineHeight, null)),
    letterSpacing: serializeLetterSpacing(safe(() => node.letterSpacing, null)),
    textAlignHorizontal: safe(() => node.textAlignHorizontal, null),
    textAlignVertical: safe(() => node.textAlignVertical, null),
    textCase: safe(() => node.textCase, null),
    textDecoration: safe(() => node.textDecoration, null),
  };

  if (text.fontName && typeof text.fontName === 'object') {
    addTypography(text.fontName.family, text.fontName.style, text.fontSize, text.lineHeight);
  }

  const fillsMixed = (() => {
    try {
      return node.fills === figma.mixed;
    } catch (error) {
      return false;
    }
  })();

  if (text.fontName === 'mixed' || text.fontSize === 'mixed' || fillsMixed) {
    text.segments = collectTextSegments(node);
  }

  return text;
}

function collectTextSegments(node) {
  try {
    const segments = node.getStyledTextSegments([
      'fontName',
      'fontSize',
      'lineHeight',
      'letterSpacing',
      'fills',
      'textStyleId',
      'textCase',
      'textDecoration',
    ]);
    return segments.slice(0, LIMITS.maxSegments).map((segment) => {
      const characters = String(segment.characters || '');
      const limit = LIMITS.maxSegmentChars;
      return {
        characters: characters.length > limit ? characters.slice(0, limit) + '...' : characters,
        fontName: segment.fontName || null,
        fontSize: segment.fontSize,
        lineHeight: serializeLineHeight(segment.lineHeight),
        letterSpacing: serializeLetterSpacing(segment.letterSpacing),
        fills: (segment.fills || []).map((paint) => serializePaint(paint, true, 'text')),
        textStyleId: segment.textStyleId || null,
        textCase: segment.textCase || null,
        textDecoration: segment.textDecoration || null,
      };
    });
  } catch (error) {
    return null;
  }
}

function serializeEffects(node) {
  let effects;
  try {
    effects = node.effects;
  } catch (error) {
    return [];
  }
  if (effects === figma.mixed) return [];
  if (!Array.isArray(effects) || !effects.length) return [];

  return effects.map((effect) => {
    const out = serializeEffect(effect);
    if (effect.color) addColorUsage(toHex(effect.color), effect.color.a === undefined ? 1 : effect.color.a, 'effect', null);
    return out;
  });
}

function serializeEffect(effect) {
  if (!effect) return null;
  return {
    type: effect.type,
    visible: effect.visible !== false,
    radius: round(effect.radius || 0, 2),
    spread: round(effect.spread || 0, 2),
    offset: effect.offset ? { x: round(effect.offset.x, 2), y: round(effect.offset.y, 2) } : null,
    color: effect.color
      ? { hex: toHex(effect.color), alpha: round(typeof effect.color.a === 'number' ? effect.color.a : 1, 3) }
      : null,
  };
}

function serializeStyleRefs(node) {
  const out = {};
  const fill = safe(() => node.fillStyleId, null);
  const stroke = safe(() => node.strokeStyleId, null);
  const text = safe(() => node.textStyleId, null);
  const effect = safe(() => node.effectStyleId, null);

  if (fill && fill !== 'mixed') out.fill = { id: fill, name: styleNameById.paint.get(fill) || null };
  if (stroke && stroke !== 'mixed') out.stroke = { id: stroke, name: styleNameById.paint.get(stroke) || null };
  if (text && text !== 'mixed') out.text = { id: text, name: styleNameById.text.get(text) || null };
  if (effect && effect !== 'mixed') out.effect = { id: effect, name: styleNameById.effect.get(effect) || null };

  return Object.keys(out).length ? out : null;
}

async function serializeComponent(node) {
  try {
    if (node.type === 'INSTANCE') {
      let main = null;
      if (typeof node.getMainComponentAsync === 'function') {
        main = await node.getMainComponentAsync();
      } else {
        main = node.mainComponent;
      }
      return { type: 'INSTANCE', mainComponent: main ? { id: main.id, name: main.name } : null };
    }
    if (node.type === 'COMPONENT' || node.type === 'COMPONENT_SET') return { type: node.type };
  } catch (error) {
    return null;
  }
  return null;
}

/* ---------- сводка частот ---------- */

function addColorUsage(hex, alpha, source, variable) {
  if (!hex || !usage) return;
  const a = typeof alpha === 'number' ? alpha : 1;
  const key = String(hex).toLowerCase() + '|' + round(a, 2);
  const item = usage.colors.get(key) || {
    hex: String(hex).toLowerCase(),
    alpha: round(a, 3),
    count: 0,
    fill: 0,
    stroke: 0,
    effect: 0,
    text: 0,
    variable: null,
  };
  item.count += 1;
  const slot = source === 'stroke' || source === 'effect' || source === 'text' ? source : 'fill';
  item[slot] += 1;
  if (variable && variable.name && !item.variable) item.variable = variable.name;
  usage.colors.set(key, item);
}

function addTypography(family, style, size, lineHeight) {
  if (!usage || !family || typeof size !== 'number') return;
  const key = family + '|' + (style || 'Regular') + '|' + round(size, 1);
  const item = usage.typography.get(key) || {
    family,
    style: style || 'Regular',
    size: round(size, 1),
    lineHeight: null,
    count: 0,
  };
  item.count += 1;
  const pixels = lineHeight && typeof lineHeight === 'object' && lineHeight.unit === 'PIXELS' ? round(lineHeight.value, 1) : null;
  if (pixels !== null) {
    if (item.lineHeight === null) item.lineHeight = pixels;
    else if (item.lineHeight !== pixels) item.lineHeight = 'mixed';
  }
  usage.typography.set(key, item);
}

function addUsage(kind, key) {
  if (!usage) return;
  const map = usage[kind];
  if (!map) return;
  map.set(key, (map.get(key) || 0) + 1);
}

function usageOutput() {
  const byCount = (a, b) => b.count - a.count || String(a.hex || a.family || '').localeCompare(String(b.hex || b.family || ''));

  const colors = Array.from(usage.colors.values()).sort(byCount).slice(0, LIMITS.maxUsageEntries);
  const typography = Array.from(usage.typography.values()).sort(byCount).slice(0, LIMITS.maxUsageEntries);

  const numeric = (map) =>
    Array.from(map.entries())
      .map(([value, count]) => ({ value: Number(value), count }))
      .sort((a, b) => b.count - a.count || a.value - b.value)
      .slice(0, LIMITS.maxUsageEntries);

  return {
    colors,
    typography,
    radii: numeric(usage.radii),
    spacing: numeric(usage.spacing),
  };
}

/* ---------- утилиты ---------- */

function safe(getter, fallback) {
  try {
    const value = getter();
    if (value === undefined || value === null) return fallback;
    if (value === figma.mixed) return 'mixed';
    return value;
  } catch (error) {
    return fallback;
  }
}

function round(value, digits) {
  if (typeof value !== 'number' || !isFinite(value)) return value === undefined ? null : value;
  const factor = Math.pow(10, digits === undefined ? 2 : digits);
  return Math.round(value * factor) / factor;
}

function clamp01(value) {
  if (typeof value !== 'number' || !isFinite(value)) return 0;
  return Math.min(1, Math.max(0, value));
}

function toHex(color) {
  if (!color) return null;
  const channel = (value) => Math.round(clamp01(value) * 255).toString(16).padStart(2, '0');
  return '#' + channel(color.r) + channel(color.g) + channel(color.b);
}

function serializeLineHeight(lineHeight) {
  if (!lineHeight || typeof lineHeight !== 'object') return lineHeight === 'mixed' ? 'mixed' : lineHeight || null;
  if (lineHeight.unit === 'AUTO') return { unit: 'AUTO' };
  return { value: round(lineHeight.value, 2), unit: lineHeight.unit };
}

function serializeLetterSpacing(spacing) {
  if (!spacing || typeof spacing !== 'object') return spacing === 'mixed' ? 'mixed' : spacing || null;
  return { value: round(spacing.value, 2), unit: spacing.unit };
}

function errorText(error) {
  if (!error) return 'неизвестная ошибка';
  if (typeof error === 'string') return error;
  return error.message ? error.message : String(error);
}

function joinNote(note, part) {
  return note ? note + '; ' + part : part;
}

function countVariables(result) {
  if (!result.tokens || !result.tokens.variables) return 0;
  return result.tokens.variables.collections.reduce((sum, collection) => sum + collection.variables.length, 0);
}

function countStyles(result) {
  if (!result.tokens || !result.tokens.styles) return 0;
  const styles = result.tokens.styles;
  return styles.paints.length + styles.texts.length + styles.effects.length + styles.grids.length;
}
