# design/

Экспорт из Figma: файлы `figma-<дата>.json`, которые отдает плагин
`tools/figma-plugin`. Храним в репозитории - по ним видно, как менялся макет, и из
них перегенерируются стили:

```powershell
node tools/figma-plugin/to-scss.mjs design/figma-<дата>.json web/src/styles/_tokens.scss
```

Сгенерированный `web/src/styles/_tokens.scss` руками не правим: правка макета -
повторный экспорт и повторная генерация.

## Чтение макета

Экспорт с шестью экранами весит около 700 КБ, целиком его не читаем. Скрипт
`tools/figma-plugin/read-nodes.mjs` печатает только нужное:

```powershell
# список экранов
node tools/figma-plugin/read-nodes.mjs design/figma-<дата>.json

# дерево одного экрана
node tools/figma-plugin/read-nodes.mjs design/figma-<дата>.json --frame "Вес" --depth 3

# переменные и текстовые стили
node tools/figma-plugin/read-nodes.mjs design/figma-<дата>.json --tokens

# частоты цветов, радиусов, кеглей
node tools/figma-plugin/read-nodes.mjs design/figma-<дата>.json --usage

# поиск по имени или тексту
node tools/figma-plugin/read-nodes.mjs design/figma-<дата>.json --search "Талия"
```

Готовый промт для верстки по макету - [docs/PROMPT-FIGMA-UI.md](../docs/PROMPT-FIGMA-UI.md).
