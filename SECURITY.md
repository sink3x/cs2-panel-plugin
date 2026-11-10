# Security policy

Please report vulnerabilities privately via **Security > Report a vulnerability** on this repository, not in a public issue.

The plugin reads two JSON files written by the admin panel and turns some values into console commands (`mp_teamname_*`, client `play ...`). Every such value goes through a whitelist (`src/Logic/Validation.cs`)
and invalid entries are skipped. Permissions of every command are enforced by CounterStrikeSharp (`@css/generic`, `@css/cheats`, `@css/changemap`). Reports about a bypass of either are especially welcome.

Supported: the latest release.
