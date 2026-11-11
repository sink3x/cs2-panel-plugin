# cs2-server-admin-plugin

[![CI](https://github.com/sink3x/cs2-server-admin-plugin/actions/workflows/ci.yml/badge.svg)](https://github.com/sink3x/cs2-server-admin-plugin/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/sink3x/cs2-server-admin-plugin)](https://github.com/sink3x/cs2-server-admin-plugin/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

English · [Русский](README.ru.md)

A small [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) plugin for Counter-Strike 2. It is the in-game half of
**[cs2-server-admin-panel](https://github.com/sink3x/cs2-server-admin-panel)** (a cheap CS2 practice server with a web admin panel), but it works on any server that runs CounterStrikeSharp.

- **`!menu`**: a centre-screen menu with sections (skins, team, practice, round and bots, maps, sounds, animals and cleanup). Sections depend on the player's admin flags.
- **Sounds**: play a sound from a library on one player or on everybody.
- **Team identity**: team names, logos (codes from the game files), flags, clan tags and nicknames from a JSON file.
- **Skins refresh**: apply a WeaponPaints change without a reconnect.
- **Map cleanup**: remove grenades, smokes, fire, dropped weapons, ask clients to clear decals.
- **Animals**: spawn chickens (and a "pig" and a "cow", which are recoloured, scaled chickens).

> In-game texts (menu, replies) are currently **in Russian**. Translations are welcome, see [CONTRIBUTING.md](CONTRIBUTING.md).

## Install

Requires a server with Metamod:Source and CounterStrikeSharp (API 1.0.379 or newer).

1. Download `CS2Panel.zip` from the [latest release](https://github.com/sink3x/cs2-server-admin-plugin/releases/latest) and check it against `SHA256SUMS`.
2. Unpack so that the file ends up at `game/csgo/addons/counterstrikesharp/plugins/CS2Panel/CS2Panel.dll`.
3. Restart the server or run `css_plugins load CS2Panel`.

With the admin panel from cs2-server-admin-panel you do not install anything by hand: the panel ships this plugin and keeps its config files up to date.

## Commands

| Command | Chat | Who | What |
|---|---|---|---|
| `css_menu` | `!menu` | everyone (in game) | Opens the menu. Sections: Skins and look, Team, Practice for everybody; Round and bots (`@css/generic`); Change map (`@css/changemap`); Sounds (`@css/generic`); Animals and cleanup (`@css/cheats`) |
| `css_playsound <name\|path> [all\|me\|#userid\|steamid64\|nick]` | | `@css/generic` | Plays a library sound or a raw sound path on the targets. Rate-limited to one call per 2 seconds for players |
| `css_snd [name] [target]` | `!snd` | `@css/generic` | Short form; without arguments lists the library |
| `css_sounds` | `!sounds` | `@css/generic` | Lists the sound library |
| `css_refreshskins [target]` | | `@css/generic` | Runs `css_wp` for the targets so new WeaponPaints skins apply now |
| `css_identity` | | server console / RCON only | Re-reads `identity.json` and applies team names, logos, flags, clan tags and nicknames |
| `css_cleanup [grenades\|fire\|weapons\|decals\|all]` | | `@css/cheats` | Cleans the map |
| `css_spawnanimal [chicken\|pig\|cow] [1-30] [auto\|me\|#userid\|nick]` | | `@css/cheats` | Spawns animals in front of a living player (at most 60 on the map) |
| `css_killanimals` | | `@css/cheats` | Removes all of them |

The menu forwards some entries to other plugins as the player (`css_skins`, `css_knife`, `css_gloves`, `css_agents`, `css_prac`, `css_bot`, `css_tp`, ...). They need
[WeaponPaints](https://github.com/Nereziel/cs2-WeaponPaints), [MatchZy](https://github.com/shobhit-pathak/MatchZy) or [ThirdPerson](https://github.com/KKNecmi/ThirdPerson-Revamped); an entry does nothing when its plugin is missing.

## Configuration

Both files live in `game/csgo/addons/counterstrikesharp/configs/plugins/CS2Panel/` and are written by the panel. You can write them yourself.

`sounds.json`: library name to what the client should `play`:

```json
{ "ding": { "play": "UI/buttonclick", "title": "Ding" } }
```

`identity.json`: teams are `ct` and `t`; players are keyed by SteamID64:

```json
{
  "teams": {
    "ct": { "name": "NAVI", "logo": "navi", "flag": "UA" },
    "t":  { "name": "FaZe", "logo": "faze", "flag": "" }
  },
  "players": { "76561198000000000": { "tag": "TAG", "name": "Nick" } }
}
```

Values are validated before they reach the game: a team name, tag or nickname is at most 32 characters without quotes, backslashes, `;` or control characters; a sound is a plain path
(`A-Za-z0-9_./-`, at most 160 characters); a logo is at most 24 letters, digits or `_`; a flag is two letters. Invalid entries are skipped.

## Limits

- A server cannot show a custom picture: team logos are *codes of logos that already exist in the game files*, and a sound plays only if the client already has it (built into the game or in a Workshop addon).
- Bullet holes and sprays are drawn by the client. `css_cleanup decals` only asks clients to run `r_cleardecals`.
- Clients periodically take their Steam clan tag back, so the plugin re-applies the configured ones every 10 seconds.
- The TAB scoreboard layout and the side icons at the bottom of the screen cannot be changed from a server.

## Build and test

Needs the .NET 10 SDK.

```bash
dotnet test                                   # unit tests of the pure logic (no game server needed)
dotnet publish src/CS2Panel.csproj -c Release -o out
```

Without a local SDK: `docker run --rm -v "$PWD":/w -w /w mcr.microsoft.com/dotnet/sdk:10.0 dotnet test`.

The code that talks to the game lives in `src/Plugin.cs`. Everything that can be tested without a server (validation, target parsing, config reading, animal limits) lives in `src/Logic/` and is compiled directly into the test project.

## Releases

Push a tag `vX.Y.Z` that matches `ModuleVersion` in `src/Plugin.cs`. The workflow tests, builds, publishes `CS2Panel.zip` with `SHA256SUMS` and a provenance attestation.

## License

[MIT](LICENSE). Counter-Strike is a trademark of Valve Corporation; this project is not affiliated with Valve.
