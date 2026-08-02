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

## Master release CI

`.github/workflows/release-master.yml` runs the headless suite and cross-publishes self-contained
`win-x64`, `linux-x64`, and `osx-arm64` archives on Ubuntu. The macOS publish then passes through a
dedicated `macos-latest` job that only signs and verifies the Mach-O files; it does not rebuild the
game. Ubuntu assembles the signed macOS archive with the Windows and Linux archives and generates
`SHA256SUMS.txt` from those final bytes.

The macOS archive uses an ad-hoc signature (`codesign --sign -`) so Apple Silicon accepts the
cross-published executable and native libraries. It is not Developer ID signed or notarized. Pull
requests that change the workflow exercise build, signing, and assembly with read-only permissions;
only a successful push to `master` can publish the commit-specific prerelease.

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
- **Locate drawables by `Name` (enforced — see** [AGENTS.md](../../AGENTS.md) **Rules).** The element
  names itself in its constructor; the test matches that name as a bare string:

  ```csharp
  // Garbus.Game/… — where the drawable is built
  Name = "tap-bpm-plus1";

  // Garbus.Game.Tests/Editor/TestSceneTimingTab.cs
  editor.ChildrenOfType<RepeatNudgeButton>().Single(b => b.Name == "tap-bpm-plus1");
  ```

  `Drawable.Name` is a plain identification field — the framework reads it only in `ToString()` (and
  so the draw visualiser), so it costs nothing and couples to nothing. It is the only locator that
  survives the churn breaking the alternatives: moving an element between containers breaks index
  and position lookups, restyling breaks glyph/colour matching, copy edits break label matching, and
  type lookups pressure production types into `internal` for the test's benefit. As a bonus the name
  labels the element in the draw visualiser. Rules:
  - **No name constants.** Write the literal on both sides. A `public const string` on the production
    type is a test-only member cluttering real code, and it does not buy what it looks like it buys —
    changing the const's *value* breaks the test exactly as hard as changing a literal. The only
    thing it catches is a typo at the test site, which `Single` already turns into an immediate,
    unmissable failure. Grep is how you find a name's other end.
  - **Name the role, not the look or the location.** `"settings header action"`, not
    `"top-right gear"` — a name describing where the element sits is as brittle as the index was.
  - **Names are unique within the subtree searched, and lookups use `Single`.** Scope the search to
    the owning component rather than the whole scene. `Single` fails loudly when a second instance
    appears; `First` silently picks one and hides the ambiguity.
  - **Generic components repeated many times take their discriminator as the name** — `SettingsSection`
    is named for its title, `SettingsSlider` for its label. That is the one case where the name is
    user-facing copy, so it moves when the copy does; the test passes the same string it constructed
    the scene with.
  - **A name is a locator, not an API.** If the test wants to assert *state* rather than reach an
    element, expose a bindable or property instead of poking at a named drawable.
  - **Look up after load.** `ChildrenOfType` over a still-loading subtree returns nothing — wrap the
    lookup in `AddUntilStep`, not a fixed `AddWaitStep`.
- **New visual elements ship with a Tuning test (enforced — see** [AGENTS.md](../../AGENTS.md) **Rules).**
  When you add or reshape a visual element, add a scene under `Tuning/` (pattern:
  `TestSceneSliderGlowTuning`; `Profiling/` holds throughput scenes like
  `TestSceneHeadOnlySliderStream`) that isolates it and exposes **every configurable parameter as a
  live test control** — `AddSliderStep` for numbers, `AddToggleStep`/checkboxes for booleans,
  dropdowns for enums — so the look can be tuned by hand in the visual test browser and regressions
  caught. **Run the scene you just added before calling it done** (below) — a plain `dotnet test`
  never touches it.
- **Keep build and test output warning-clean (enforced — see** [AGENTS.md](../../AGENTS.md) **Rules).**
  Don't introduce compiler or analyzer warnings in production or test code; fix any you add before
  calling the work done.
- Every gotcha fix in the domain docs is pinned by a named test — when you change that behavior,
  update the test in the same commit.

## Running the `[Explicit]` scenes

A plain `dotnet test` **silently excludes every `[Explicit]` fixture** — they are not discovered, so
they do not even appear in the `Skipped` count, and a green "all tests passed" says nothing about
them. Since every tuning and profiling scene is `[Explicit]`, a broken one ships unnoticed. Select
them by namespace to actually run them:

```
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~Garbus.Game.Tests.Tuning"
```

Each fixture's generated `TestConstructor` runs its constructor steps followed by its `[SetUpSteps]`,
which is exactly the order the visual browser uses on load — so that one run reproduces a
crash-on-load without opening the browser. Do this whenever you add or touch a scene under `Tuning/`
or `Profiling/`.

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
  To reach a specific element, name it (see the `Name` convention above) rather than indexing.
  For same-StartTime hit objects, capture references at add time instead of indexing `HitObjects` —
  the list re-sorts with an unstable sort on updates, so ties can swap.
- **Asserting a UI element is absent: reach the shown state first, then transition.** "Assert hidden"
  after a few `AddWaitStep` frames false-passes when the element rebuilds on a delay (e.g. the
  inspector's 250ms roll). Instead drive a state where the element IS shown (`AddUntilStep` shown),
  change to the state under test, then `AddUntilStep` hidden — the shown→hidden transition is
  race-free. See `TestSceneComposeSelection.TestMergeButtonHiddenForSingleSlider`.
- **Steps added in a tuning scene's constructor run BEFORE its `[SetUpSteps]`.** The visual browser
  runs the whole step list in order, and constructor steps (`AddSliderStep` and friends) were queued
  first — so a slider callback fires while the scene it configures does not exist yet. Reaching for
  the subject with `Single()`/`First()` there throws "Sequence contains no elements" the moment the
  scene loads. Callbacks must tolerate a not-yet-built scene (store the value, `SingleOrDefault`,
  bail), with a setup step re-applying them once it is built. The same ordering means **every step
  auto-runs on load**, so a step that toggles state blindly leaves the scene in that state before it
  has been looked at — drive that kind of interaction by pointing at the element instead.
- Use `[Explicit]` for tests that are user-initiated such as profiling and tuning scenes — **no
  exceptions**. An eyeball scene (no assertion that can meaningfully fail) without `[Explicit]` runs
  in CI as noise. The only sanctioned unmarked no-assert scenes are the `Visual/HitObjects/*Stream`
  family, whose implicit constructor pass is the per-hit-object-type PlayScreen load smoke.
