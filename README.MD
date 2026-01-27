# ENG: AutoLoot for Crime Simulator

A BepInEx plugin for Crime Simulator that automatically collects loot items and sends them to the truck.

## Features

- **Auto-collect all nearby loot** — Press `N` to automatically collect all loot items within the configurable radius
- **Smart quota system** — Stops collecting when the required quota is reached (except for skillbooks)
- **Skillbooks priority** — Skillbooks are collected first before other items
- **Heavy items support** — Automatically sends carried heavy items to the truck
- **Skill requirements** — Respects item skill requirements for carrying
- **Value-based sorting** — Items are sorted by value (highest first) for optimal collection

## Installation

1. Install [BepInEx](https://github.com/BepInEx/BepInEx) for Crime Simulator
2. Copy `Plugin.dll` to `BepInEx/plugins/` folder
3. Launch the game

## Usage

- Press **N** in-game to collect all nearby loot items and teleport them to the truck

## Configuration

Configuration file is located at `BepInEx/config/com.ic23.autoloot.cfg`

| Setting | Default | Description |
|---------|---------|-------------|
| `CollectRadius` | 2000 | Item collection radius in meters |

## Technical Details

- Plugin ID: `com.ic23.autoloot`
- Version: `1.0.0`
- Target Framework: .NET Standard 2.1
- Dependencies: BepInEx, Harmony

## How It Works

1. When `N` is pressed, the plugin finds all `Pickupable` items within the configured radius
2. Skillbooks are moved to the front of the collection queue
3. Other items are sorted by value (highest to lowest)
4. Items are collected one by one with small delays to ensure proper inventory handling
5. Each item is picked up and immediately sent to the truck
6. Collection stops when the quota is reached (skillbooks are always collected)

> **Note:** Skillbooks may spawn over time during the heist. It is recommended to press `N` several times after the quota is reached to ensure 100% collection of all skillbooks.

---

# RU: AutoLoot для Crime Simulator

BepInEx плагин для Crime Simulator, который автоматически собирает лут и отправляет его в грузовик.

## Возможности

- **Автосбор всего лута поблизости** — Нажмите `N` для автоматического сбора всех предметов в настраиваемом радиусе
- **Умная система квоты** — Прекращает сбор при достижении требуемой квоты (кроме книг навыков)
- **Приоритет книг навыков** — Книги навыков собираются первыми
- **Поддержка тяжёлых предметов** — Автоматически отправляет переносимые тяжёлые предметы в грузовик
- **Требования навыков** — Учитывает требования навыков для переноса предметов
- **Сортировка по ценности** — Предметы сортируются по стоимости (сначала дорогие) для оптимального сбора

## Установка

1. Установите [BepInEx](https://github.com/BepInEx/BepInEx) для Crime Simulator
2. Скопируйте `Plugin.dll` в папку `BepInEx/plugins/`
3. Запустите игру

## Использование

- Нажмите **N** в игре для сбора всего лута поблизости и телепортации его в грузовик

## Настройка

Файл конфигурации находится по пути `BepInEx/config/com.ic23.autoloot.cfg`

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `CollectRadius` | 2000 | Радиус сбора предметов в метрах |

## Технические детали

- ID плагина: `com.ic23.autoloot`
- Версия: `1.0.0`
- Целевой фреймворк: .NET Standard 2.1
- Зависимости: BepInEx, Harmony

## Как это работает

1. При нажатии `N` плагин находит все объекты `Pickupable` в заданном радиусе
2. Книги навыков перемещаются в начало очереди сбора
3. Остальные предметы сортируются по стоимости (от дорогих к дешёвым)
4. Предметы собираются по одному с небольшими задержками для корректной обработки инвентаря
5. Каждый предмет подбирается и сразу отправляется в грузовик
6. Сбор прекращается при достижении квоты (книги навыков собираются всегда)

> **Примечание:** Книги навыков могут появляться со временем во время ограбления. Рекомендуется нажать `N` несколько раз после достижения квоты, чтобы гарантированно собрать все книги навыков.

## Лицензия / License

MIT