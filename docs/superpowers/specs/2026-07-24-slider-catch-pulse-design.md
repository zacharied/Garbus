# Zero-length slider catch pulse — design

## Goal

When a **zero-length slider** (a `SliderBody` with `Duration == 0`) is caught, a white pulse should
sweep from the ring toward the centre of the playfield at the object's angle, as extra catch feedback
(slider-head Perfect is otherwise silent).

Two shapes count as zero-length and both fire the pulse:

- **Head-only** — no control points; one nested `SliderHead`, no children. One node, one angle.
- **Zero-length chord** — control points that all have `TimeOffset == 0` but differing
  `RotationOffset`; a `SliderHead` plus same-time `SliderChild`ren, i.e. several nodes at one time,
  fanned across angles.

The pulse fires **once** per caught zero-length slider, at the **circular mean angle** of its nodes
(head + any same-time children). Head-only is the degenerate case where that mean is just the single
node's angle. This keeps the pulse "down from the ring to the centre" and keeps the pulse drawables
**angle-only** — the mean is computed by the caller (see Chord anchoring).

This pass delivers three tunable pulse **effect drawables** plus one **tuning test scene** to dial in
the look. Wiring the chosen variant to real catches is a deliberate follow-up (see Out of scope).

Relevant domain docs: [docs/agents/gameplay.md](../../agents/gameplay.md) (playfield, ring, the
`JudgementFeedbackDisplay` feedback halo) and [docs/agents/testing.md](../../agents/testing.md)
(Tuning-scene conventions). Per the enforced AGENTS.md rule *"New visual elements ship with a Tuning
test,"* the tuning scene below is required, not optional.

## Background (how zero-length sliders and feedback work today)

- A head-only slider is `SliderBody` with no path control points (`nodeTimes.Length == 1`,
  `Duration == 0`). It carries one nested `SliderHead` and no `SliderChild`ren.
  `DrawableSliderBody` already special-cases it (`rebuildNodes` → a `headContainer`/`headCircle`
  **plus a `headGlow` `GlowPath` disc** travelling centre→ring via `updateHeadCircle`, plus
  `if (nodeTimes.Length >= 2)` guards in `updatePath`/`AngleDegAt`). The empty-path `SliderBody` *is*
  the canonical head-only representation — see `SliderDecomposition.DecomposeIntoHeads`, which turns a
  drawn slider into a run of exactly these. (So a decomposed slider becomes several head-only
  sliders, each of which fires its own pulse; the Ring wiring treats them independently.)
- A zero-length **chord** slider (`nodeTimes.Length >= 2`, all node times equal) is *not* the
  head-circle branch: `DrawableSliderBody.renderBand` draws its nodes as a **co-radial fan** — all
  nodes share the same radius at any instant (equal times → equal `DistanceFromCentreAtTime`) but sit
  at different angles (`AngleDeg + RotationOffset`). No divide-by-zero (`AngleDegAt` guards the
  zero-span link). There is **no** existing chord-centre/centroid/connector for sliders — the cardinal
  `ChordConnectorOverlay` keys strictly on `CardinalNote`/`CardinalHoldNote` `StartTime` and excludes
  sliders. The mean angle this design uses is therefore new caller-side math, not a reused value.
- Each same-time `SliderChild` is judged catch-style at its own angle (zero-duration →
  `ActivationProgress` auto-1, `HeadStyleHit` gated on the catcher pointing at it within the 200 ms
  late window), chained head → child → child. The body applies its unscored `IgnoreHit` only once all
  nested objects are `AllJudged`.
- `DrawableSliderHead` draws nothing; on catch it calls `ApplyMaxResult()`. A caught head's
  Perfect is silenced in `JudgementFeedbackDisplay` (`drawable.HitObject is SliderHead &&
  result.Type == HitResult.Perfect → return false`).
- `JudgementFeedbackDisplay` (on `Ring`) is the canonical "consume `NewResult`/`RevertResult`,
  place a visual at an `IHasAngle` direction" pattern — the model the eventual Ring wiring follows.
- Reusable ring↔centre primitives: `SpikeBlade` (anti-aliased inward triangle blade, in
  `SliderContactSpikes`) — it now takes a `useGlow` ctor flag; the slider-spikes context builds it
  with `useGlow: false` (the old blurred halo was dropped for perf), while `StickCentreSpike` builds
  it with glow on. `StickCentreSpike` calls `Blade.SetGeometry(angleRad, baseRadius, tipRadius,
  halfWidthDeg, opacity)` for a centre→ring wedge. `Arc` (tessellated `SmoothPath` arc/ring, polar
  mapping `positionAt(radians, radius)`). `PlayfieldKeybeam` (additive `CircularProgress` pie slice
  with a transparent-centre→white-ring radial gradient). If the beam/arc variants want a glow, drive
  it explicitly — do not assume `SpikeBlade` brings one.
- Polar convention across the codebase: `x = cos θ · r`, `y = −sin θ · r` (θ=0 → right, CCW).
  Ring radius = `min(DrawWidth, DrawHeight) / 2`.

## Architecture decision

The pulse is fundamentally a **screen effect that fires at catch-time and sweeps the full
ring→centre at the object's angle** — it is not tied to the travelling head circle's current radius.
So it belongs to a `Ring`-level effect layer (sibling to `JudgementFeedbackDisplay`), **not** to a
split-out head-only drawable. Zero-length sliders remain modelled as `SliderBody` + nested
`SliderHead`(+`SliderChild`); no hit-object or drawable surgery.

### Chord anchoring (the pulse angle)

The pulse takes a single angle. For a zero-length slider that angle is the **circular mean** of the
slider's node angles — the head's `AngleDeg` plus each same-time child's `AngleDeg + RotationOffset`:

```
θ̄ = atan2( Σ sin θᵢ , Σ cos θᵢ )
```

Circular mean (sum of unit vectors, then `atan2`), **not** an arithmetic mean of degrees — the latter
breaks across the 0°/360° wrap. If the vector sum is ~zero (nodes cancel out, e.g. diametrically
opposed), fall back to the base `HitObject.AngleDeg`. Head-only collapses to the single node's angle.

This math lives in the **caller** (the deferred Ring display, and — for exercising it — the tuning
scene), so the pulse drawables stay angle-only and identical for both shapes. Concretely: a small
static helper `CircularMeanDeg(IEnumerable<float> anglesDeg, float fallbackDeg)` (returning
`fallbackDeg` when the vector sum degenerates), shipped and unit-tested in this pass so both callers
share it. Extracting a `SliderBody`'s node angles (head + same-time children) is the Ring display's
job in the follow-up.

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
- A **`chord spread` slider + "fire chord" step** exercise the circular-mean helper: the step builds a
  small synthetic fan (base angle ± spread, a few nodes), computes θ̄ with the shared helper, and
  fires one pulse there — so you can eyeball that the pulse lands at the fan's centre. (Since chord
  anchoring reduces to a single mean angle, no chord-specific *drawable* is needed.)
- Defaults mirror each drawable's init-property defaults, with a comment noting to bake the chosen
  combination back into the drawable defaults once picked.

### Testing

- The tuning scene is the visual tuning surface (`[Explicit]`, excluded from headless runs).
- One light **headless smoke test** (a non-`[Explicit]` fixture, e.g. `TestSceneSliderCatchPulse` in
  the Tuning folder) constructs each variant via `SliderCatchPulse.Create`, adds it to a sized
  container, advances a manual clock past its lifetime, and asserts it expires/disposes cleanly —
  pinning the "self-expiring one-shot, fire-and-forget" contract the future Ring display relies on.
- A pure-logic **unit test** for `CircularMeanDeg` (root `*Test.cs` style): single angle → itself;
  a symmetric fan → its centre; a wrap-straddling pair (e.g. 350° & 10° → 0°); and a degenerate
  cancelling set → the fallback.
- **UI-test drawable lookup by `Name`** (enforced AGENTS.md rule): any pulse drawable a test needs to
  reach sets a role-describing `Name` literal in its constructor (e.g. the base sets
  `Name = "slider catch pulse"`), and tests match that literal via `AddUntilStep` + `Single` after
  load — no widened visibility, no container indexing, no matching on colour/label.
- Build and test output stays **warning-clean** (enforced AGENTS.md rule); verification is via the
  headless tests and eyeballing the Tuning scene in the visual browser — the app is not run.

## Out of scope this pass

- The `Ring`-level `SliderCatchPulseDisplay` that fires the chosen variant on a caught zero-length
  slider, at the circular-mean node angle. This is the mechanical follow-up after a variant + params
  are chosen in the tuning scene and baked as the drawable defaults. It follows the
  `JudgementFeedbackDisplay` pattern (bind `NewResult`). Trigger shape to settle then: fire **once**
  per zero-length `SliderBody` when it is caught — cleanest hook is the body's own `IgnoreHit` result
  (applied only once all nested objects are `AllJudged`), gated on `HitObject.Duration == 0`, then
  gather the node angles and call `CircularMeanDeg`. Still open for that pass: whether a partially
  caught chord (some nodes missed) suppresses the pulse or still fires. When the wiring lands, update
  the feedback-halo section of [docs/agents/gameplay.md](../../agents/gameplay.md) per the "update the
  relevant domain doc as work lands" rule.
