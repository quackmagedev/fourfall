# Fourfall

Rogue-lite puzzle game: Connect Four gravity + cascading match clears. Pure C#
core logic (.NET 8.0) with a Godot 4.6 (C#/mono) front end.

## Layout

- `src/Fourfall.Core/` — the engine library
  - `Board.cs` — 7x6 matrix (row 0 = bottom), gravity landing, column splice (`CollapseColumn` / `ApplyGravity`)
  - `Token.cs` — base `Token` + variants: `StandardToken`, `IronToken` (deletes the token beneath on drop), `GlassToken` (x3 score, self-deletes at end of turn if uncleared), `DrainToken` (deletes the bottom token, pulls the column down)
  - `MatchScanner.cs` — 4-in-a-row detection in all four directions; overlapping runs union into clusters
  - `GameEngine.cs` — turn orchestration: drop effect → settle → recursive cascade resolution with compounding multiplier (x1, x2, x4… per wave, `CascadeGrowth` configurable) → end-of-turn glass shatter (which can chain further cascades on the same multiplier)
  - `TurnResult.cs` — per-turn score, wave count, peak multiplier, event log
  - `TurnStep.cs` — structured playback timeline (`TurnResult.Steps`): every removal, gravity slide, and settle in order, so a renderer can animate the turn step by step instead of diffing final board state
- `tests/Fourfall.Sim/` — headless verification console: 12 deterministic rule scenarios + 100 randomized drops with structural invariant checks (no floating tokens, no surviving flagged glass, no exceptions) and a step-replay check (applying each turn's `Steps` to a mirror grid must reproduce the engine board exactly)
- `game/` — Godot 4.6 project (Phase 2)
  - `scenes/Main.tscn` — root: board instance + HUD (score, last-turn summary, next-token preview)
  - `scenes/Board.tscn` / `scripts/BoardView.cs` — visual 7x6 grid mapped onto the core matrix (board row 0 = bottom), column-selection input (mouse hover/click, keyboard/controller left/right + accept), and sequential cascade playback: clears vanish, columns splice down, next wave starts only after the previous finishes; input is locked while a turn animates
  - `scenes/TokenView.tscn` / `scripts/TokenView.cs` — self-drawn token visuals (no textures); kind accents: iron ring, glass translucency, drain arrow
  - `scripts/Main.cs` — owns the `GameEngine`, rolls the weighted token queue, bridges input → engine turn → playback
  - `scripts/Hud.cs` — score/summary labels + next-token preview

## Run verification

```
dotnet run --project tests/Fourfall.Sim          # default seed
dotnet run --project tests/Fourfall.Sim -- 777   # explicit seed
```

Exit code 0 = all checks passed.

## Build / run the game

```
dotnet build game/Fourfall.Game.sln
"C:\Program Files\Godot\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --path game
```
