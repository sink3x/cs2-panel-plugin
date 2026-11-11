# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning: [SemVer](https://semver.org/).

## [Unreleased]

### Changed
- Repository renamed from `cs2-panel-plugin` (old GitHub URLs redirect).

## [0.6.0] - 2026-11-10

First public release of the plugin (the version used by cs2-server-admin-panel).

### Added
- `!menu`: centre-screen menu with sections that depend on admin flags.
- Team names, logos and flags; clan tags and nicknames from `identity.json` (`css_identity`).
- `css_playsound`, `css_snd`, `css_sounds`: sound library from `sounds.json`.
- `css_refreshskins`: apply WeaponPaints changes without a reconnect.
- `css_cleanup`: grenades, fire, dropped weapons, decals.
- `css_spawnanimal`, `css_killanimals`.
- Unit tests for the pure logic (validation, target parsing, config reading, animal limits); CI and release workflows.
