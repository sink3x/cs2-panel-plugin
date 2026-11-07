# cs2-panel-plugin

Небольшой плагин CounterStrikeSharp для панели [cs2-yc-sandbox](https://github.com/sink3x/cs2-yc-sandbox).

- `css_playsound <имя|путь> [all|me|#userid|ник]` (чат: `!snd`) — проиграть звук клиентам командой `play`.
- `css_sounds` — список библиотеки (`configs/plugins/CS2Panel/sounds.json`, ею управляет панель).

Права: `@css/generic`. Играются только звуки, которые уже есть у клиента (встроенные в игру или из Workshop-аддона).
Собирается GitHub Actions; релиз — по тегу `v*`.

- `css_spawnanimal [chicken|pig|cow] [1-30] [auto|me|#userid|ник]` (чат: `!spawnanimal`) — создать животных рядом с живым игроком; `css_killanimals` — убрать всех. Права: `@css/cheats`. В CS2 есть только курица, «свинья» и «корова» — она же, перекрашенная и увеличенная.
