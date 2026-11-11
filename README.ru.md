# cs2-server-admin-plugin

[![CI](https://github.com/sink3x/cs2-server-admin-plugin/actions/workflows/ci.yml/badge.svg)](https://github.com/sink3x/cs2-server-admin-plugin/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/sink3x/cs2-server-admin-plugin)](https://github.com/sink3x/cs2-server-admin-plugin/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

[English](README.md) · Русский

Небольшой плагин [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) для Counter-Strike 2. Это внутриигровая половина проекта
**[cs2-server-admin-panel](https://github.com/sink3x/cs2-server-admin-panel)** (недорогой тренировочный сервер CS2 с веб-панелью), но он работает на любом сервере с CounterStrikeSharp.

- **`!menu`**: меню по центру экрана с разделами (скины, команда, тренировка, раунд и боты, карты, звуки, животные и очистка). Разделы зависят от прав игрока.
- **Звуки**: проиграть звук из библиотеки одному игроку или всем.
- **Оформление команд**: названия, логотипы (коды из файлов игры), флаги, клан-теги и ники из JSON-файла.
- **Обновление скинов**: применить изменение WeaponPaints без перезахода.
- **Очистка карты**: убрать гранаты, дымы, огонь, оружие на полу; попросить клиентов убрать следы пуль.
- **Животные**: курицы (а «свинья» и «корова» это перекрашенная и увеличенная курица).

> Тексты в игре (меню, ответы) пока **на русском**. Переводы приветствуются, см. [CONTRIBUTING.md](CONTRIBUTING.md).

## Установка

Нужен сервер с Metamod:Source и CounterStrikeSharp (API 1.0.379 или новее).

1. Скачайте `CS2Panel.zip` из [последнего релиза](https://github.com/sink3x/cs2-server-admin-plugin/releases/latest) и сверьте с `SHA256SUMS`.
2. Распакуйте так, чтобы файл лежал в `game/csgo/addons/counterstrikesharp/plugins/CS2Panel/CS2Panel.dll`.
3. Перезапустите сервер или выполните `css_plugins load CS2Panel`.

С панелью из cs2-server-admin-panel вручную ничего ставить не нужно: панель сама везёт этот плагин и обновляет его конфиги.

## Команды

| Команда | Чат | Кому | Что делает |
|---|---|---|---|
| `css_menu` | `!menu` | всем (в игре) | Открывает меню. Для всех: скины, команда, тренировка; раунд и боты (`@css/generic`); смена карты (`@css/changemap`); звуки (`@css/generic`); животные и очистка (`@css/cheats`) |
| `css_playsound <имя\|путь> [all\|me\|#userid\|steamid64\|ник]` | | `@css/generic` | Играет звук из библиотеки или по пути. Игрокам не чаще раза в 2 секунды |
| `css_snd [имя] [кому]` | `!snd` | `@css/generic` | Короткая форма; без аргументов показывает список |
| `css_sounds` | `!sounds` | `@css/generic` | Список библиотеки звуков |
| `css_refreshskins [кому]` | | `@css/generic` | Выполняет `css_wp`, чтобы новые скины WeaponPaints применились сразу |
| `css_identity` | | только консоль сервера / RCON | Перечитывает `identity.json` и применяет названия, логотипы, флаги, теги и ники |
| `css_cleanup [grenades\|fire\|weapons\|decals\|all]` | | `@css/cheats` | Очищает карту |
| `css_spawnanimal [chicken\|pig\|cow] [1-30] [auto\|me\|#userid\|ник]` | | `@css/cheats` | Создаёт животных перед живым игроком (на карте не больше 60) |
| `css_killanimals` | | `@css/cheats` | Убирает всех |

Часть пунктов меню вызывает команды других плагинов от имени игрока (`css_skins`, `css_prac`, `css_tp` и др.): нужны
[WeaponPaints](https://github.com/Nereziel/cs2-WeaponPaints), [MatchZy](https://github.com/shobhit-pathak/MatchZy), [ThirdPerson](https://github.com/KKNecmi/ThirdPerson-Revamped). Без нужного плагина пункт ничего не делает.

## Настройка

Оба файла лежат в `game/csgo/addons/counterstrikesharp/configs/plugins/CS2Panel/`; обычно их пишет панель, но можно и вручную. Формат см. в [английском README](README.md#configuration).

Значения проверяются до того, как попадут в игру: название, тег и ник не длиннее 32 символов без кавычек, обратных косых, `;` и управляющих символов; звук это простой путь (`A-Za-z0-9_./-`, до 160 символов);
логотип до 24 букв, цифр или `_`; флаг две буквы. Неверные записи пропускаются.

## Ограничения

- Сервер не может показать свою картинку: логотипы это *коды логотипов из файлов игры*, а звук слышен, только если он уже есть у клиента (встроен в игру или в Workshop-аддоне).
- Следы пуль и спреи рисует клиент; `css_cleanup decals` лишь просит клиентов выполнить `r_cleardecals`.
- Клиенты периодически возвращают свой клан-тег из Steam, поэтому плагин ставит заданные теги заново каждые 10 секунд.
- Раскладку таблицы TAB и значки сторон внизу экрана с сервера изменить нельзя.

## Сборка и тесты

Нужен .NET 10 SDK.

```bash
dotnet test
dotnet publish src/CS2Panel.csproj -c Release -o out
```

Без SDK: `docker run --rm -v "$PWD":/w -w /w mcr.microsoft.com/dotnet/sdk:10.0 dotnet test`.

## Лицензия

[MIT](LICENSE). Counter-Strike является товарным знаком Valve Corporation; проект с Valve не связан.
