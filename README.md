# Decklist Checker

A small C#/.NET tool for parsing and validating card-game decklists.

## Layout

```
src/DecklistChecker          Console app (`decklist-checker <file>`)
  Decklist.cs                 Decklist model, parser, and validator
  Program.cs                  Command-line entry point
tests/DecklistChecker.Tests   xUnit tests
samples/                      Example decklists
```

## Getting started

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```sh
dotnet build
dotnet test
dotnet run --project src/DecklistChecker -- samples/burn.txt
```

## Decklist format

```
// comments start with // or #
4 Lightning Bolt
20 Mountain

Sideboard
2 Pyroblast
```

A `Sideboard` line or the first blank line after main-deck cards starts the sideboard.
Default rules (see `Decklist.Validate`): 60+ card main deck, up to 15 sideboard cards,
max 4 copies of any card except basic lands.
