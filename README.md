# Норма (Norma)

Веб-приложение, которое собирает данные о питании, весе и активности в одной
системе и помогает удерживать цель. Ключевая идея — не дневник калорий сам по себе,
а планирование меню под свою норму КБЖУ с отсечением аллергенов и списком покупок.

Полное описание, домен, задачи и критерии приёмки — в [docs/PROJECT.md](docs/PROJECT.md).
Память проекта для ИИ-агентов — в [AGENTS.md](AGENTS.md).

## Стек

- Backend: .NET 9, ASP.NET Core minimal API
- Frontend: Angular + TypeScript (подключается отдельной задачей)
- Хранилище: PostgreSQL, EF Core
- Запуск инфраструктуры: Docker Compose

## Как запустить

```powershell
# база данных (нужна позже, для текущего среза не обязательна)
docker compose up -d db

# API
dotnet run --project src/Norma.Api
```

Проверка:

```powershell
curl http://localhost:5xxx/api/v1/health
# {"status":"ok","version":"1.0.0.0"}
```

Точный порт печатает сам `dotnet run` (см. `src/Norma.Api/Properties/launchSettings.json`).

## Тесты

```powershell
dotnet test
```

## Структура

```
src/Norma.Api/Features/   вертикальные срезы: одна папка = один сценарий
src/Norma.Api/Data/       DbContext, EF-конфигурации, миграции
src/Norma.Api/Jobs/       фоновые задачи (импорт базы продуктов)
src/Norma.Core/           чистые функции расчётов: без EF, без DI, без DateTime.Now
tests/Norma.Core.Tests/   юнит-тесты ядра (миллисекунды)
tests/Norma.Api.Tests/    интеграционные тесты через WebApplicationFactory
```

Правила, которые держат структуру от расползания: срез не вызывает обработчик
другого среза; бизнес-правило живёт в `Core`, а не в эндпоинте; generic-репозиториев
нет — EF Core и есть доступ к данным.

## Статус

Каркас решения. Дальше по плану — расчёт нормы калорий в `Core` (первый контракт
пишется тестами), затем база продуктов, дневник питания, вес, активность.

## Источники данных

- База продуктов: USDA FoodData Central (CC0), русские названия — своя таблица
  локализации.
- Бренды и штрихкоды: Open Food Facts (ODbL), запросы по требованию с кэшем.
  Данные Open Food Facts используются с атрибуцией.
