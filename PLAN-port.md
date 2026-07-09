# Framework port plan

Garbus is the standalone port of **BigAssCircle** (the osu!lazer ruleset plugin at
`C:\Users\zachd\Code\BAC`) off `osu.Game` onto bare `osu-framework`. Motivation: osu's editor pipeline
assumes custom rulesets auto-generate charts from official-ruleset beatmaps; this game needs
hand-authored charts, and the legacy `.osu` encoder can't roundtrip its objects anyway. Going standalone
also sheds the ruleset/mod/skin/realm machinery we never use.

This file is the tracker — update the checklists as phases land, and record decisions here. The BAC repo
keeps a pointer copy; **this copy is canonical.** The BAC repo stays untouched and buildable as the
osu.Game plugin; if the port works out, development moves here entirely.

**Decisions locked in:**

- **Full port, not osu.Game-as-a-library.** Considered shipping our own exe referencing `ppy.osu.Game`
  (BAC's `VisualTestRunner` proves the shape) — rejected: it drags the whole osu.Game dependency tree
  (realm, online, skinning) into a game that uses none of it.
- **Own repo, own namespaces.** Scaffolded from the `osu-framework-game` template (framework
  2026.629.0 — the same version osu currently builds against). Code is *copied* from BAC into `Garbus.*`
  namespaces, not shared — no dual-targeting.
- **Vendor, don't rewrite, the battle-tested pieces.** osu and osu-framework are both MIT — copy source
  files with attribution headers kept. Vendoring manifest below. Local clones for reference live in
  `C:\Users\zachd\Code\BAC\LocalDependencies`.
- **Own chart format** (JSON-ish serialization of hit objects + timing). This deletes the `.osu` encoder
  roundtrip problem instead of solving it. The legacy decoder is only needed if we later add osu-map
  timing import (nice-to-have, not core).
- **No mods, no difficulty calc, no replays, no skinning in v1.** These exist to satisfy osu's plugin
  contract. Autoplay can return later as a debug input feeder if needed.
- **`Bac*` prefixes are renamed to `Garbus*` during the copy** (BacHitObject → GarbusHitObject, etc.),
  and osu's "beatmap" terminology becomes "chart" (e.g. the vendored FramedBeatmapClock is
  `FramedChartClock`).

## Investigation findings (2026-07, don't re-derive)

### Audio: almost everything latency-critical is already in osu-framework

The whole BASS stack is framework-level (`osu.Framework/Audio/`): `AudioManager` (device management,
mixers, `AudioThread`), `TrackBass`, `SampleBass`. **All the latency tuning lives in
`AudioManager.InitBass()`** — `DeviceBufferLength = 10ms`, `UpdatePeriod = 5ms`, `TruePlayPosition`,
`DeviceNonStop`, plus the experimental low-latency WASAPI mode (`UseExperimentalWasapi`). We get osu's
audio latency for free; do not touch these values without hardware latency data.

The clock smoothing is also framework-level (`osu.Framework/Timing/`): `InterpolatingFramedClock`
(smooths BASS's ~5–10ms position-reporting granularity — this is why scroll motion doesn't jitter) and
`DecouplingFramedClock` (gameplay time can run before the track starts, for lead-ins).

What osu.Game adds on top is small and self-contained (see vendoring manifest):

- `FramedBeatmapClock` — the canonical stack: track → decoupling → interpolating → three offset layers.
  Carries the **platform offset constants: Windows +15ms base, −25ms more under experimental WASAPI.**
  Copy the constants; strip the realm-backed per-beatmap offset and `OsuConfigManager` hooks.
- `OffsetCorrectionClock` — user offset scaled by playback rate so real-time offset stays constant.
- `GameplayClockContainer` / `MasterGameplayClockContainer` — pause/resume/seek/lead-in orchestration.
- Hitsounds: `SkinnableSound` is skin-entangled — we don't vendor it; a thin `DrawableSample` wrapper
  (~50 lines) replaces it since we use one hitsound bank.

### Dependency split (what BAC's ~92 files stand on)

| Subsystem | Lives in | Disposition |
|---|---|---|
| Rendering, DI, bindables, input events, `SmoothPath` | framework | keep |
| BASS audio, mixers, latency config, interpolating/decoupling clocks | framework | keep |
| `KeyBindingContainer` (action bindings) | framework | keep; bindings in our own config file |
| Clock stack (`FramedBeatmapClock` etc.) | osu.Game | **vendor** (~600 lines after trimming) |
| `HitObject` / `DrawableHitObject` / pooling / judgements | osu.Game | **vendor** (~1.5k lines); strip skinning hooks (`ISkinSource`, combo colours) |
| Scroll algorithms (`IScrollAlgorithm`, constant) | osu.Game | **vendor** (~300 lines) — BAC already bypasses `ScrollingHitObjectContainer` with its radial container |
| `ControlPointInfo` + control points | osu.Game | **vendor** (~1.2k lines, zero realm coupling) |
| Legacy `.osu` decoder/encoder | osu.Game | skip for v1 (own format); decoder only if we add osu-map import |
| `RulesetInputManager`, replays | osu.Game | vendor input manager minus replay plumbing |
| `Player`, `ScoreProcessor`, HUD | osu.Game | rewrite minimal (~300-line game loop; `Player` is a 1.3k-line megaclass with realm/online/mod deps) |
| Song select, settings, results | osu.Game | rewrite bespoke (much simpler than osu's) |
| Editor framework (composer, `EditorBeatmap`, timeline, blueprints, `SelectionBox`, beat snap, undo/redo) | osu.Game | **rebuild bespoke — the dominant cost, ~half the port** |
| `Ruleset` / mods / difficulty calc / `RulesetInfo` | osu.Game | drop |
| Test scenes (`EditorTestScene`, `PlayerTestScene`) | osu.Game | rewrite on framework `TestScene` (template's `GarbusTestScene` is the base) |

### Vendoring manifest (source paths in `C:\Users\zachd\Code\BAC\LocalDependencies\osu`)

- `osu.Game/Beatmaps/FramedBeatmapClock.cs` (~284 L)
- `osu.Game/Screens/Play/OffsetCorrectionClock.cs` (~45 L)
- `osu.Game/Screens/Play/GameplayClockContainer.cs` (~206 L)
- `osu.Game/Screens/Play/MasterGameplayClockContainer.cs` (~236 L)
- `osu.Game/Rulesets/Objects/HitObject.cs` (~260 L)
- `osu.Game/Rulesets/Objects/Drawables/DrawableHitObject.cs` (~824 L, minus skinning)
- `osu.Game/Rulesets/Objects/HitObjectLifetimeEntry.cs` (~136 L)
- `osu.Game/Rulesets/Objects/Pooling/PooledDrawableWithLifetimeContainer.cs` (~168 L)
- `osu.Game/Rulesets/UI/Scrolling/Algorithms/*` (interface + constant algorithm)
- `osu.Game/Beatmaps/ControlPoints/*` (~1.2k L)
- Judgement/scoring primitives: `Judgement`, `JudgementResult`, `HitResult`, hit windows (as needed by
  the above)

Editor scaffolding is **not** vendorable — `osu.Game/Screens/Edit` is woven through osu's screen stack,
dialogs, and realm. BAC's own editor logic (`EditorAngleMapping`, wrap-copy polyline rendering,
path-precise selection, snapping math) ports as-is; only the scaffolding is rebuilt.

## Phases

### Phase 1 — walking skeleton (proves audio parity)

- [x] New game project on `ppy.osu.Framework` — this repo, `osu-framework-game` template
      (`Garbus.Game` / `Garbus.Desktop` / `Garbus.Game.Tests` / `Garbus.Resources`)
- [x] Vendor the clock stack (`Garbus.Game/Timing/`: `FramedChartClock`, `OffsetCorrectionClock`,
      `GameplayClockContainer`, `MasterGameplayClockContainer`, `IGameplayClock`) — realm per-beatmap
      offset became the plain `FramedChartClock.ChartOffset` property; mod adjustments dropped;
      `MasterGameplayClockContainer` takes a `Track` directly (no `WorkingBeatmap`)
- [x] Load and play a track through the vendored clock stack — `MainScreen` auto-plays
      `Garbus.Resources/Tracks/sample-track.mp3` with live time/offset/state readouts and
      space/R/arrows controls
- [x] Sanity-check offsets — `GarbusConfigManager` (`garbus.ini`, `GarbusSetting.AudioOffset`) cached
      in `GarbusGameBase`; `TestSceneClockStack` asserts platform offset (+15ms on Windows), user
      offset flow-through, clock advancement and offset-aware seeking (`dotnet test --filter
      TestSceneClockStack`, 5/5 passing)

### Phase 2 — gameplay vertical slice

- [ ] Vendor HitObject/DrawableHitObject/pooling + judgement primitives + scroll algorithm
- [ ] Port `Objects/`, `UI/` (Ring/Lane/playfield — mostly framework-only already), `Input/`
- [ ] Port the gameplay input manager (`KeyBindingContainer` on our action enum, bindings from config)
- [ ] Minimal game loop screen replacing `Player` (~300 lines): clock → update → results → score
- [ ] Hardcoded test chart via `BacTestBeatmapGenerator`; full playthrough with hitsounds
- [ ] Visual test scenes for gameplay in `Garbus.Game.Tests` (replaces `PlayerTestScene`)

### Phase 3 — chart format

- [ ] Define native chart format: hit objects + timing (`ControlPointInfo`) + metadata + audio ref
- [ ] Serializer/deserializer + roundtrip tests
- [ ] Replace `BigAssCircleBeatmapConverter` path: game loads charts directly, no conversion step

### Phase 4 — editor rebuild (the big one)

- [ ] Bespoke scaffolding: editor clock/transport, timeline strip, beat-snap grid, blueprint container,
      drag-box selection, undo/redo (single-game scope — far smaller than osu's generic framework)
- [ ] Port `EditorAngleMapping` + all `Edit/Drawables/` and `Edit/Blueprints/` logic onto it
- [ ] Save/load through the native chart format (finally: persistence)
- [ ] Editor tests on framework `TestScene` (replaces `EditorTestScene`)

### Phase 5 — game chrome

- [ ] Song select (bespoke, simple)
- [ ] Settings screen: audio device, volumes, offset calibration (port osu's suggested-offset idea),
      key bindings
- [ ] Results screen

## Open questions

- Chart format details: JSON vs binary, versioning, how control-point types serialize
- Whether to keep the `Garbus.iOS` template project or delete it until desktop is proven (it doesn't
  build on this machine — no iOS workload — but only the desktop slnf is used)
