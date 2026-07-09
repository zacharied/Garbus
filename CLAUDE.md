# Garbus — agent onboarding

A standalone rhythm game built directly on **osu-framework** (no osu.Game dependency). It is the port
target for the BigAssCircle osu!lazer ruleset at `C:\Users\zachd\Code\BAC` — a rhythm-based action game
where hit objects spawn at the centre of a circular playfield and travel outward toward the ring, with
judgement at the edge timed to the music.

**Read `PLAN-port.md` first** — it is the canonical port plan and progress tracker: locked-in decisions,
the osu.Game→osu-framework dependency-split findings, the vendoring manifest, and per-phase checklists.
Update it as work lands. The BAC repo holds the gameplay/editor source being ported and the reference
clones of `osu` / `osu-framework` in `BAC\LocalDependencies` (both MIT — vendoring is fine with
attribution headers kept).

## Build / run / test

- **Build:** `dotnet build Garbus.Desktop.slnf` (the iOS slnf needs a workload not installed here)
- **Run:** `dotnet run --project Garbus.Desktop`
- **Tests:** `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` (headless NUnit over visual test
  scenes), or run `Garbus.Game.Tests` directly for the visual test browser
- **Runtime logs:** `%APPDATA%\Garbus\logs\*.runtime.log` (config lives at `%APPDATA%\Garbus\garbus.ini`)

## Conventions

- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- **Vendored osu.Game files** keep the ppy MIT attribution header plus an "Adapted for Garbus:" line
  summarising the trims. Vendor faithfully; deviate minimally and note why.
- **Terminology:** osu's "beatmap" is "chart" here; `Bac*` class prefixes from the source repo become
  `Garbus*` when ported.
- No mods, difficulty calculation, replays, skinning, or realm — deliberately dropped (see plan).

## Current state (Phase 1 complete)

- `Garbus.Game/Timing/` — the vendored gameplay clock stack: `FramedChartClock` (née
  `FramedBeatmapClock`; realm per-beatmap offset → plain `ChartOffset` property),
  `OffsetCorrectionClock`, `GameplayClockContainer`, `MasterGameplayClockContainer` (takes a `Track`
  directly), `IGameplayClock`. Platform offset constants (Windows +15ms, experimental WASAPI −25ms)
  are osu's verbatim — all other latency-critical audio lives in osu-framework itself and is untouched.
- `Garbus.Game/Configuration/` — `GarbusConfigManager` (`IniConfigManager`, `garbus.ini`) with the
  global `AudioOffset` setting; cached in `GarbusGameBase`.
- `MainScreen` — walking skeleton: auto-plays `Garbus.Resources/Tracks/sample-track.mp3` through the
  stack with live time/offset readouts (space play/pause, R reset, ←/→ seek, ↑/↓ user offset).
- `TestSceneClockStack` — headless verification of platform/user offsets, clock advance, seeking.

Next: Phase 2 (gameplay vertical slice) per `PLAN-port.md`.
