# Testing

## Purpose & scope

How Garbus is tested, the conventions to follow, and the timing/harness traps that cost debugging
cycles. This doc owns testing *practice*; the domain docs own their individual pinning tests. The
enforced behavioral test rules live in [AGENTS.md](../../AGENTS.md)'s Rules section — this doc is the
how-to they point at.

## Two test modes

- **Headless (CI):** `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`. Runs the NUnit suite
  headlessly — both pure-logic tests (e.g. `HitWindowsTest`, `DurationJudgementTest`,
  `GarbusSliderPathTest`, `TestChartFormat`) and the visual `TestScene`s driven headlessly.
- **Visual browser:** run `Garbus.Game.Tests` directly for the interactive test browser
  (`GarbusTestBrowser`) — step through scenes, watch draw counters, eyeball visuals.

## Layout

- `Garbus.Game.Tests/Visual/` — `TestScene`-based scenes; `GarbusTestScene` is the base (it hosts a
  `GarbusGameBase` test runner so DI-cached game dependencies resolve).
- `Garbus.Game.Tests/Editor/` — editor scenes (compose, timeline, timing, integration).
- `Garbus.Game.Tests/Charts/`, `Input/` — domain tests.
- Root `*Test.cs` — pure-logic unit tests (judgement, hit windows, slider path, geometry).
- `Garbus.Game.Tests/Tuning/` and `Profiling/` — visual tuning / profiling scenes (below).

## Conventions

- Visual/headless scenes declare steps with `AddStep` / `AddAssert` / `AddUntilStep`; drive input with
  `ManualInputManager`; drive time with a manual clock.
- **Test-value rules (enforced — see** [AGENTS.md](../../AGENTS.md) **Rules):**
  - **Every pinned constant needs a source of truth.** Asserting an exact value is allowed only when
    it traces to a spec (`docs/rules-specs/`, `docs/presentation-specs/`) or is an explicitly
    commented calibration anchor (pattern: `VolumeCurveTest` — "position 30% ⇒ 3% gain" with the
    comment saying so). Never assert bare styling: colours, alphas, pixel offsets, layout
    coordinates, or user-facing copy strings that no spec owns. Pin the *relation* instead —
    non-overlap, monotonicity, distinctness, "centred on X" — relations survive restyling; raw
    values break on every tune.
  - **Expected values are derived independently of the implementation.** Hand-compute goldens
    (pattern: `SliderSweepTest.SmoothHermiteMatchesGolden` — 41.25 worked out by hand). A test whose
    expectation calls the same function or reuses the same constant as the code under test is a
    mirror, not a check — it changes in lockstep with the bug it should catch.
  - **No strict-subset tests.** If every assertion in a test is a setup precondition of a sibling
    test, delete it — it can never fail alone.
- **New visual elements ship with a Tuning test (enforced — see** [AGENTS.md](../../AGENTS.md) **Rules).**
  When you add or reshape a visual element, add a scene under `Tuning/` (pattern:
  `TestSceneSliderGlowTuning`; `Profiling/` holds throughput scenes like
  `TestSceneHeadOnlySliderStream`) that isolates it and exposes **every configurable parameter as a
  live test control** — `AddSliderStep` for numbers, `AddToggleStep`/checkboxes for booleans,
  dropdowns for enums — so the look can be tuned by hand in the visual test browser and regressions
  caught.
- **Keep build and test output warning-clean (enforced — see** [AGENTS.md](../../AGENTS.md) **Rules).**
  Don't introduce compiler or analyzer warnings in production or test code; fix any you add before
  calling the work done.
- Every gotcha fix in the domain docs is pinned by a named test — when you change that behavior,
  update the test in the same commit.

## Timing & harness traps

- **A manual-clock jump larger than an object's alive window skips judgement entirely.** The lifetime
  entry never becomes alive/dead, so the object is never judged (no `entryBecameAlive`/`Dead`). Step in
  sub-window increments. See `TestSceneGameplay.playThrough`.
- **Wire the composer subtree's `Clock` to the `EditorClock` in editor harnesses** (as `ComposeTab`
  does). Otherwise the playfield maps time↔position against the ambient wall clock and
  editor-time-relative behaviour — e.g. the compose hit zone at time 0 mapping to negative times —
  can't be reproduced. See [editor.md](editor.md).
- **Placement auto-seeks.** `HitObjectPlacementBlueprint.EndPlacement` seeks the clock to the placed
  object; wait for the seek (`AddUntilStep`) before asserting screen positions.
- **Bare-constructed input managers fall back to default bindings** — `GarbusInputManager` resolves
  its `KeyBindingStore` as `canBeNull: true`, so a test without a cached store uses `DefaultKeyBindings`.
  Provide a store when testing rebinds.

## osu-framework background

`TestScene`, `TestScene.Steps`, `ManualInputManager`, manual/framed clocks, and `ITestSceneTestRunner`
from osu-framework (`docs/code-reference/osu-framework`). Garbus's `GarbusTestScene` wraps these with a
game-base runner so cached dependencies (config, chart store, clocks) are available.

## Gotchas

- **Step manual clocks in sub-window increments** (see above) — the single most common cause of a
  "why isn't this object judged?" test failure.
- **In the UI, don't write ordering-dependent tests.** If a test depends on ordering, resolve the order
  dynamically rather than assuming it — order-dependent tests flake when UI elements are moved around.
- Use `[Explicit]` for tests that are user-initiated such as profiling and tuning scenes — **no
  exceptions**. An eyeball scene (no assertion that can meaningfully fail) without `[Explicit]` runs
  in CI as noise. The only sanctioned unmarked no-assert scenes are the `Visual/HitObjects/*Stream`
  family, whose implicit constructor pass is the per-hit-object-type PlayScreen load smoke.