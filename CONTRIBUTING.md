# Contributing

Issues and pull requests are welcome, in English or Russian.

- Build and test with the .NET 10 SDK: `dotnet test` (or the Docker one-liner in the README).
- Anything that does not need the game goes into `src/Logic/` with a test in `tests/CS2Panel.Tests/`. `src/Plugin.cs` should stay a thin layer over the game API.
- Everything that ends up in a console command must pass a whitelist from `Validation`. Do not build commands from raw config values.
- In-game texts are Russian today. A localisation layer (language files, per-player language) is a welcome contribution; please open an issue first.
- Keep changes focused and describe the reason. By contributing you agree to the [MIT license](LICENSE).

Security problems: use **Security > Report a vulnerability** on GitHub instead of a public issue.
