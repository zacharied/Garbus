# Gameplay

## Purpose & scope

The runtime that turns a `PlayableChart` into a played game: the hit-object/drawable/pooling
infrastructure, the circular playfield and its polar time→radius scrolling, the ordered hit policy,
and the judgement + judgement-feedback systems. The **rules** (exact windows, note-lock, slam/slider
grading) are specified in [`docs/rules-specs/Judgement.md`](../rules-specs/Judgement.md) and
[`docs/rules-specs/Inputs.md`](../rules-specs/Inputs.md) — this doc summarises the implementation and
points there for authority. The data model is in [charts.md](charts.md); actions/keys are in
[input.md](input.md); the clock that drives it all is in [timing-audio.md](timing-audio.md).

## Layout: playfield → ring → lanes

Objects spawn at the **centre** of a circular playfield and travel **outward** to the ring, where
they are judged in time with the music.

- `UI/JacketBackground.cs` — the static jacket background under the playfield (hosted by `PlayScreen`, outside the gameplay-clock subtree): the song jacket circle-clipped to the ring's disc (sharing `GarbusPlayfield.SCREEN_PADDING` for alignment) plus a one-shot cached downscale+blur color wash behind it. Tuned in `Tuning/TestSceneJacketBackgroundTuning`.
- `UI/GarbusPlayfield.cs` — the top-level playfield (a `ScrollingPlayfield`).
- `UI/Ring.cs` — the judgement ring at the playfield edge. It owns the hit-object layers and, above
  them, the `JudgementFeedbackDisplay` (below). It also hosts the keybeams (`PlayfieldKeybeam`),
  radial lines (`PlayfieldRadialLines`), stick indicator (`StickIndicator`/`StickCentreSpike`), and
  warning indicators (`WarningIndicatorDisplay`). The warning glow's blur/mask `BufferedContainer`s
  cache their framebuffers and are force-redrawn only when a side's revealed angle changes — an
  uncached buffer re-blurs the whole playfield every frame, which cripples integrated GPUs (see the
  gotcha in [osu-framework.md](osu-framework.md)).
- `UI/Lane.cs` — the directional lanes objects travel along.
- `UI/GarbusScrollingHitObjectContainer.cs` — the polar container. Instead of osu's linear
  time→position scroll, it maps each object's remaining time-to-judgement to a **radius** (centre =
  far future, ring = now) at the object's angle, using the visible `timeRange` and the constant
  scroll algorithm from `GarbusScrollingInfo`. It keeps a `layoutComputed` set and re-lays-out only
  what changed.

## Objects and drawables

Two parallel hierarchies:

- **Model:** `Objects/GarbusHitObject.cs` and its concrete types — `CardinalNote`, `CardinalHoldNote`,
  `ShoulderNote`, `ShoulderHoldNote`, `SliderBody` (+ nested `SliderHead`/`SliderChild`),
  `GarbusSlamCentered`, `GarbusSlamEdge`, plus `HoldNoteHead`. Angle/side/direction are exposed
  through `IHasAngle`, `IHasSide`, `IHasCardinalDirection`, `IHasMutableAngle`. `BarLine` +
  `BarLineGenerator` produce metronome bar lines.
- **Gameplay drawables:** `Objects/Drawables/Drawable*` — the polar in-game sprites
  (`DrawableCardinalNote`, `DrawableHoldNote`, `DrawableSliderBody`/`Child`/`Head`, slams, etc.),
  distinct from the editor's simplified drawables (see [editor.md](editor.md)). `GlowPath`,
  `SliderContactSpikes`, `ShoulderNoteGeometry`, and `GarbusHitSoundPlayback` support them.
  `IHasActivationProgress`/`IHittableNote`/`ISelfPosition` are the drawable-side contracts.

**Slider path pooling:** every `Path` draws through a buffered draw node (its own framebuffer) and
allocates a 9000-quad GPU vertex batch on first draw, while slider-body drawables are constructed up
front for the whole chart — so `DrawableSliderBody` rents its `SmoothPath`s and `GlowPath` twins from
`SliderPathPool`, a shared free-list `[Cached]` on `GarbusPlayfield` (resolved `CanBeNull`; bare test
scenes without one construct per body). Bodies rent lazily in `updatePath` and hand everything back in
`OnKilled`/`OnFree` — detaching each path from the body's own containers first — capping live `Path`
instances at what is on screen and making the cycle safe under rewind, where a killed body revives and
re-rents. Buckets key on `PathRadius` (crisp) / `GlowPath.Profile` (glow twins) so a body never
receives a path shaped for a different look. Pin: `TestSceneGameplay.TestSliderPathsReturnToSharedPoolOnKill`;
the editor Mini preview cycles are pinned in `TestSceneMiniPreview` (see [editor.md](editor.md)).

Chording/coincidence helpers (`Objects/ChordHighlighter.cs`, `ChordColours`, `ChordIndex`,
`UI/SlamCoincidenceIndex.cs`, `UI/ChordConnectorOverlay.cs`) group simultaneous objects for visuals
and slam-coincidence judgement floors.

## Auto-hit (presentation-only)

`DrawableHitObject` carries a general **`autoHit`** capability (`AutoHit { get; init; }`, inherited by
nested drawables through `AutoHitActive`): the drawable plays its hit animation as a **pure function of
clock time**, never produces a `JudgementResult`, never scores, and lets the scrolling container own its
lifetime — it **swallows drawable-side `LifetimeEnd` writes** (from `Expire`/`UpdateState`) so a scrub or
rewind can't pin lifetime to a path-dependent moment. `AutoHitEngaged`
(`AutoHitActive && Time.Current >= StartTime`) is the time-derived seam that durationed drawables read to
present continuous **held / caught** state with no input — hold notes' `Holding` and the slider body's
leading-edge catch both consult it. An optional forward-crossing hitsound (`AutoHitPlaysSamples`) fires
once as the clock passes the hit time going forward (left off for the silent editor preview). Auto-hit is
presentation-only in every context; it is what powers the editor Mini preview (see [editor.md](editor.md)),
where the real gameplay drawables are reused verbatim over the editor's live hit objects.

## Vendored infrastructure

`Gameplay/` holds the osu.Game infrastructure, vendored and trimmed:

- `Gameplay/Objects/` — `HitObject`, `HitObjectLifetimeEntry`, `Pooling/` (pooled drawable-with-
  lifetime container). Gameplay drawables **are pooled** here (unlike the editor's manual composer).
- `Gameplay/UI/` — `Playfield`, `HitObjectContainer`, and `UI/Scrolling/` (the `IScrollAlgorithm`
  interface + `ConstantScrollAlgorithm`, `GarbusScrollingInfo` with `TimeRange`,
  `ScrollingHitObjectContainer`/`ScrollingPlayfield`).
- `Gameplay/Judgements/` (`Judgement`, `JudgementResult`), `Gameplay/Scoring/` (`HitResult`,
  `HitWindows`), `Gameplay/Audio/` (`HitSoundContainer` + the `DrawableSample` wrapper; `HitsoundFamily`).

Trims from osu: skinning, combo colours, mods, and cursor are stripped from `DrawableHitObject`/
`Playfield`; `HitResult` drops `LegacyComboIncrease`/`SliderTailHit`; author-configurable
`HitSampleInfo` is replaced by fixed per-object hitsound families (`Objects/HitsoundFamilies.cs`).
Read the originals in `docs/code-reference/osu` before editing.

## Judgement (summary — see the spec)

`HitResult` is the Garbus ladder (`Miss < Bad < Near < Perfect < CriticalPerfect`, plus the Ignore
pair). Windows are asymmetric `(Early, Late)` ranges (`HitWindowRange`) with an early-only Miss
window; hittability keys off `HitWindows.LateEligibilityEdge`. Notes carry `CardinalNoteHitWindows` /
`ShoulderNoteHitWindows` (hold heads share the parent's instance). Note-lock is the spec's
oldest-eligible-containing rule with no force-missing (`UI/GarbusOrderedHitPolicy.cs`).
`Objects/Judgement/DurationJudgement.cs` applies hold/slider grace, short-duration, end-floor, and
ending-grace rules. Slider heads use a late-only 200 ms catch window (`SliderCatchHitWindows`);
children use hold-family segment proportions plus a catch-style pseudo-head chain with
same-time/same-side slam floors. Slams use timestamped early-permissive `SlamHitWindows`. **The spec
is authoritative — change behavior there and here together.**

## Judgement feedback halo

`UI/JudgementFeedbackDisplay.cs` (owned by `Ring`, above the hit-object layers) consumes the ring's
forwarded `NewResult`/`RevertResult` stream and places upright rank + early/late messages
(`JudgementFeedbackMessage`) at each object's angle, stacking nearby results radially across the
angle seam (three-message cap). `DrawableHitObject.DisplayResult` gates whether a result surfaces;
`DisplayTimingOffset` is false for hold tails and slider children (their frame-time offset is not a
timing grade). Discrete buttons keep high-accuracy feedback quiet (Critical Perfect silent, Perfect
white-text-only, Near rank+direction, Miss rank-only); duration tails substitute a rounded
credited-activation percentage for early/late. Rewind removal keys on the exact `JudgementResult`
reference so replay/rewind can safely reuse a reset result object.
`UI/HoldActivationDebugDisplay.cs` shows a live cardinal-hold activation bar.

## osu-framework background

Drawable pooling and lifetime entries (see [osu-framework.md](osu-framework.md)); the scrolling
algorithm interface; `IFrameBasedClock` (the playfield runs on the gameplay clock — see below).

## Gotchas

- **The playfield must run on the gameplay clock, not wall-time.** Garbus dropped osu's
  `DrawableRuleset`/`FrameStabilityContainer`, so nothing re-applies the gameplay clock to the
  playfield subtree automatically — `GameplayClockContainer` sets `Content.Clock = GameplayClock`
  to compensate. Without it, object lifetimes compare against app-session time and nothing appears
  once the app has been open longer than the chart. Full story in
  [timing-audio.md](timing-audio.md).
- **Manual-clock test jumps that exceed an object's alive window skip judgement entirely** — the
  entry never becomes alive/dead, so it is never judged. Step in sub-window increments. See
  [testing.md](testing.md) (`TestSceneGameplay.playThrough`).
- **Removed non-pooled drawables must be disposed** — a cross-cutting trap that bites hardest in the
  editor composer; see [osu-framework.md](osu-framework.md) and [editor.md](editor.md).
- **The hold head-pop is skipped under auto-hit.** Auto-hit force-schedules the Hit exit at *apply*
  time (before the head is reached); `DrawableHoldNote`'s `OnHeadHit` fires later at `StartTime` and its
  head-sprite transform prunes the already-scheduled exit chain on the same sprite, leaving the head
  static instead of playing its fade/spin/scale exit. Real gameplay is immune — there the exit is
  scheduled at `EndTime`, after the pop. A seek/jump also hides it (the pop then fires *after* the exit
  starts), so it only reproduces under real forward playback. Pin:
  `TestPreviewHoldExitUnderRealPlayback`.
