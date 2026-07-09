# Garbus — agent onboarding

A standalone rhythm game built directly on **osu-framework** (no osu.Game dependency). It is the port
target for the BigAssCircle osu!lazer ruleset at `C:\Users\zachd\Code\BAC` — a rhythm-based action game
where hit objects spawn at the centre of a circular playfield and travel outward toward the ring, with
judgement at the edge timed to the music.

This is an experimental project and backwards compatibility will NEVER matter until this line is removed. Do not add historical context to documentation, do not add compatibility layers if a schema changes.

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

## Current state (Phase 3 complete)

Phase 3 additions — the native chart format:

- `Garbus.Game/Charts/` — `GarbusChart` (metadata + timing + hit objects; hit windows are fixed —
  `DefaultHitWindows` constants, no per-chart difficulty), `ChartMetadata`, `ChartStore`
  (decodes `.garbus` resources; cached in `GarbusGameBase`), `Charts/Timing/` (vendored
  `ControlPointInfo` stack, timing-only), `Charts/Format/` (versioned JSON DTOs +
  `GarbusChartSerializer`; `"type"` discriminator must stay first per hit object).
- `PlayScreen` loads `Garbus.Resources/Charts/test-chart.garbus` and resolves audio from the chart's
  `audioFile`. The bundled file's source of truth is `GarbusTestChartGenerator` — regenerate via the
  `[Explicit]` test `TestChartFormat.RegenerateBundledTestChart` after changing generator or format.
- 20 headless tests green (`TestChartFormat` adds roundtrip/version/bundled-file coverage).

## Phase 2 state (gameplay vertical slice)

- `Garbus.Game/Timing/` — the vendored gameplay clock stack: `FramedChartClock` (née
  `FramedBeatmapClock`; realm per-beatmap offset → plain `ChartOffset` property),
  `OffsetCorrectionClock`, `GameplayClockContainer`, `MasterGameplayClockContainer` (takes a `Track`
  directly), `IGameplayClock`. Platform offset constants (Windows +15ms, experimental WASAPI −25ms)
  are osu's verbatim — all other latency-critical audio lives in osu-framework itself and is untouched.
- `Garbus.Game/Gameplay/` — the vendored osu.Game gameplay infrastructure: `HitObject`,
  `DrawableHitObject` (skinning stripped; hit sounds via `Gameplay/Audio/HitSoundContainer`, the thin
  `DrawableSample` wrapper replacing `SkinnableSound`), lifetime entries + pooling, `Playfield`,
  `HitObjectContainer`, judgements/`HitResult`/hit windows, constant scroll algorithm +
  `GarbusScrollingInfo`.
- Game domain (ported from BAC, `Bac*` → `Garbus*`): `Objects/` (+drawables), `UI/` (`GarbusPlayfield`
  → `Ring` → `Lane`s, `GarbusScrollingHitObjectContainer` doing the polar time→radius mapping),
  `Input/` (`GarbusAction` enum, `GarbusInputManager` KeyBindingContainer — gamepad + keyboard
  defaults, config rebinding deferred to Phase 5 — and `AnalogInputManager` for stick catchers),
  `Core/`, `Charts/` (`GarbusChart` + `GarbusTestChartGenerator`).
- `Screens/PlayScreen` — the minimal game loop replacing osu's `Player`: clock stack → input →
  playfield, score/combo/accuracy with rewind-revert, inline results overlay. `GarbusGame` boots into
  it; space pauses, R restarts.
- `Garbus.Game/Configuration/` — `GarbusConfigManager` (`IniConfigManager`, `garbus.ini`) with the
  global `AudioOffset` setting; cached in `GarbusGameBase`.
- `MainScreen` — the Phase 1 clock-stack skeleton, still reachable via the test browser.
- Tests (17 headless, all green): `TestSceneClockStack`, `TestSceneGameplay` (manual-clock lifetimes,
  auto-miss, hold/slider judgement, key-press hit via `ManualInputManager`), `TestScenePlayScreen`.
  Gotcha: manual-clock jumps larger than an object's alive window skip judgement entirely — step in
  sub-window increments (`TestSceneGameplay.playThrough`).
- Resources: `Textures/` (square, paddle, arrow from BAC), `Samples/Gameplay/soft-hitnormal.wav`
  (synthesized — osu-resources assets aren't freely reusable), `Tracks/test-track.ogg` (track lookups
  need the full filename — `TrackStore` only probes `.mp3`), `Charts/test-chart.garbus`.

Next: Phase 4 (editor rebuild) per `PLAN-port.md`.
