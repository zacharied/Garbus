# Head-only slider catch pulse — design

## Goal

When a **head-only slider** (a `SliderBody` with zero control points → zero duration, one nested
`SliderHead`) is caught, a white pulse should sweep from the ring toward the centre of the playfield
at the object's angle, as extra catch feedback (slider-head Perfect is otherwise silent).

This pass delivers three tunable pulse **effect drawables** plus one **tuning test scene** to dial in
the look. Wiring the chosen variant to real catches is a deliberate follow-up (see Out of scope).

Relevant domain docs: [docs/agents/gameplay.md](../../agents/gameplay.md) (playfield, ring, the
`JudgementFeedbackDisplay` feedback halo) and [docs/agents/testing.md](../../agents/testing.md)
(Tuning-scene conventions). Per the enforced AGENTS.md rule *"New visual elements ship with a Tuning
test,"* the tuning scene below is required, not optional.

## Background (how head-only sliders and feedback work today)

- A head-only slider is `SliderBody` with no path control points (`nodeTimes.Length == 1`,
  `Duration == 0`). It carries one nested `SliderHead` and no `SliderChild`ren.
  `DrawableSliderBody` already special-cases it (a `headContainer`/`headCircle` travelling
  centre→ring, plus `if (nodeTimes.Length >= 2)` guards in `updatePath`/`AngleDegAt`).
- `DrawableSliderHead` draws nothing; on catch it calls `ApplyMaxResult()`. A caught head's
  Perfect is silenced in `JudgementFeedbackDisplay` (`drawable.HitObject is SliderHead &&
  result.Type == HitResult.Perfect → return false`).
- `JudgementFeedbackDisplay` (on `Ring`) is the canonical "consume `NewResult`/`RevertResult`,
  place a visual at an `IHasAngle` direction" pattern — the model the eventual Ring wiring follows.
- Reusable ring↔centre primitives: `SpikeBlade` (anti-aliased inward triangle blade, in
  `SliderContactSpikes`), `StickCentreSpike`/`Blade.SetGeometry(angle, inner, outer)` (centre→ring
  wedge), `Arc` (tessellated `SmoothPath` arc/ring, `positionAt` polar mapping), `PlayfieldKeybeam`
  (additive `CircularProgress` pie slice with a transparent-centre→white-ring radial gradient).
- Polar convention across the codebase: `x = cos θ · r`, `y = −sin θ · r` (θ=0 → right, CCW).
  Ring radius = `min(DrawWidth, DrawHeight) / 2`.

## Architecture decision

The pulse is fundamentally a **screen effect that fires at catch-time and sweeps the full
ring→centre at the object's angle** — it is not tied to the travelling head circle's current radius.
So it belongs to a `Ring`-level effect layer (sibling to `JudgementFeedbackDisplay`), **not** to a
split-out head-only drawable. Head-only sliders remain modelled as `SliderBody` + nested
`SliderHead`; no hit-object or drawable surgery.

## Components

### 1. `Garbus.Game/UI/CatchPulse/` — three one-shot effect drawables + shared base

- **`SliderCatchPulse : CompositeDrawable`** (abstract base). Constructed with an **angle (degrees)**
  plus its tunable params (captured at construction, like the glow scene's baked values). Resolves
  the ring radius from its parent's draw size (`min(w,h)/2`). On `LoadComplete` it builds its
  geometry, runs its animation via transforms, and `Expire()`s itself when the animation ends
  (self-managed lifetime — fire-and-forget). White, additive blending by default (matching
  `PlayfieldKeybeam`). Uses the codebase polar convention above.
- **`SliderCatchPulseType { RadialBeam, Travelling, ArcSweep }`** enum + a
  **`SliderCatchPulse.Create(type, angleDeg, …)`** factory, so the tuning scene and the future Ring
  display construct all three uniformly.
- Three concrete subclasses:

  - **`RadialBeamPulse`** — a straight wedge at the angle spanning ring→centre (a `SpikeBlade`-style
    anti-aliased triangle or a thin radial gradient box) that flashes **in place**: fade/scale in,
    brief hold, fade out. Params: `Width`, `InnerReach` (0 = reaches centre … 1 = stops short),
    `PeakAlpha`, `FadeInMs`, `HoldMs`, `FadeOutMs`.
  - **`TravellingPulse`** — a short bright radial segment that spawns at the ring and moves inward
    to the centre along the angle, trailing/fading as it goes. Params: `SegmentLength` (px along
    radius), `TravelMs` (ring→centre), `Thickness`, `TrailFade`, `PeakAlpha`.
  - **`ArcSweepPulse`** — a curved band (a slice of the ring via `Arc`/`CircularProgress`) centred
    on the angle that contracts inward and fades. Params: `SpanDeg`, `BandThickness`,
    `ContractDistance` (how far inward it sweeps), `LifetimeMs`, `PeakAlpha`.

### 2. `Garbus.Game.Tests/Tuning/TestSceneSliderCatchPulseTuning.cs`

`[TestFixture] [Explicit]`, modelled on `TestSceneSliderGlowTuning`:

- A centred host container drawing a ring-boundary reference (`Arc(0, 2π)`) plus a small centre dot,
  so pulses are seen in the same space they'll fire in-game.
- A looping `ManualClock` (survives across fires so the view doesn't jump).
- A **variant selector**: one `AddStep` button per `SliderCatchPulseType` ("fire: radial beam", etc.)
  that sets the active variant (there is no `AddEnumStep` helper in the test project; `AddStep`
  buttons are the available idiom). Each button also fires that variant once immediately, so a
  single click both selects and shows it.
- Per-variant `AddSliderStep`s for every param above, name-prefixed by variant (e.g. `beam: width`,
  `travel: length`, `arc: span`). All sliders are always present; only the active variant's matter,
  matching the glow scene's always-present-sliders style.
- On each loop tick, fires the selected variant at a **rotating angle** (so the look is seen at
  several directions), spawning a fresh pulse instance with the current slider values and adding it
  to the host — no rebuild; the pulse self-expires.
- Defaults mirror each drawable's init-property defaults, with a comment noting to bake the chosen
  combination back into the drawable defaults once picked.

### Testing

- The tuning scene is the visual tuning surface (`[Explicit]`, excluded from headless runs).
- One light **headless smoke test** (a non-`[Explicit]` fixture, e.g. `TestSceneSliderCatchPulse` in
  the Tuning folder) constructs each variant via `SliderCatchPulse.Create`, adds it to a sized
  container, advances a manual clock past its lifetime, and asserts it expires/disposes cleanly —
  pinning the "self-expiring one-shot, fire-and-forget" contract the future Ring display relies on.
- Build and test output stays **warning-clean** (enforced AGENTS.md rule); verification is via the
  headless test and eyeballing the Tuning scene in the visual browser — the app is not run.

## Out of scope this pass

- The `Ring`-level `SliderCatchPulseDisplay` that consumes `NewResult`, filters for a caught
  head-only `SliderHead`, and fires the chosen variant at its angle. This is the mechanical
  follow-up after a variant + params are chosen in the tuning scene and baked as the drawable
  defaults. It follows the `JudgementFeedbackDisplay` pattern (bind `NewResult`; place at the
  `IHasAngle` direction; the head-only filter is `HitObject is SliderHead && Parent path has no
  control points`). When that wiring lands, update the feedback-halo section of
  [docs/agents/gameplay.md](../../agents/gameplay.md) per the "update the relevant domain doc as work
  lands" rule.
