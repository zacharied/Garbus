# Judgement feedback halo — design

Date: 2026-07-19
Status: implemented

## Context

Gameplay currently exposes the latest judgement as debug-style HUD text in `PlayScreen`, but gives no
spatial feedback at the playfield itself. A player should be able to associate a result with the object
that produced it without looking away from the ring centre.

This design adds an invisible inner circle around the centre combo display. When a meaningful hit
object result occurs, a short-lived message appears on that circle at the hit object's angle. The
message communicates the judgement rank and, where the result represents input timing, whether the
input was early or late.

## Decisions

- **Ring-local display.** The feedback belongs to the circular playfield, not the screen HUD or each
  individual hit-object drawable. A single display can position, stack, expire, and rewind all messages
  consistently.
- **Every meaningful result.** A result is shown when it is scorable and its drawable has
  `DisplayResult == true`, with a deliberate exception for hold heads. Hold heads are evaluated using
  the same compact feedback as their corresponding cardinal or shoulder notes even though their
  invisible nested drawables otherwise opt out. Other implementation-only results remain excluded.
- **Angle preserves object association.** The message anchor uses the judged object's `IHasAngle`
  angle, following the same polar convention as `GarbusScrollingHitObjectContainer`.
- **Screen-upright text.** Position encodes angle; glyphs never rotate. Text remains immediately
  readable in the lower and side regions of the playfield.
- **Compact qualitative content.** Discrete button objects use a deliberately quiet hierarchy:
  Critical Perfect is silent, Perfect shows only `EARLY` or `LATE` in white, and Near keeps its rank
  plus the timing direction, while Miss shows only its rank. Duration tails follow the same hierarchy
  with their rounded credited-activation percentage replacing timing direction: Critical Perfect is
  silent, Perfect is percentage-only, Bad is rank plus percentage, and Miss is rank-only. Slider-head
  Perfect remains silent. Other surfaced results use a rank line plus an optional detail line.
  Milliseconds are not displayed. An effectively zero timing offset omits the direction line, so an
  exactly-timed button Perfect is silent.
- **Radial collision stack.** Nearby messages coexist briefly. The newest sits nearest the centre and
  pushes older messages outward instead of replacing them or entering a global queue.
- **No visible guide.** The inner circle is layout geometry only; it has no stroke or fill.

## 1. Ownership and event flow

Add a `JudgementFeedbackDisplay` to `Ring`, after the hit-object containers in draw order so feedback
stays legible above emerging objects and the centre combo. The outer ring may remain after it because
the two occupy disjoint radii.

`Ring` owns the subscription:

1. `Ring.NewResult` forwards a drawable and its `JudgementResult` to the display.
2. The display filters non-scorable results, `DisplayResult == false` (except hold heads), and objects
   without `IHasAngle`.
3. An accepted result creates one feedback message associated with that exact `JudgementResult`
   instance.
4. `Ring.RevertResult` removes any live message associated with the reverted result.

The existing nested-playfield forwarding already carries lane and nested-object events into `Ring`, so
the display must not subscribe separately to every lane or drawable. It also respects the playfield's
existing `DisplayJudgements` bindable: disabling judgements hides and clears the feedback layer.

The score processor in `PlayScreen` remains independent. Feedback observes results but never mutates
score, combo, accuracy, or judgement state.

## 2. Layout and angle mapping

The display fills `Ring` and positions messages relative to its geometric centre. The base feedback
radius is approximately 20% of the playfield diameter, with sensible pixel clamps so it remains clear
of the 96 px combo text on small windows and does not drift too far from the centre on large windows.

For angle `a` in radians and radius `r`:

```text
x = centre.X + cos(a) * r
y = centre.Y - sin(a) * r
```

This intentionally matches `GarbusScrollingHitObjectContainer.PositionAtTime`: 0 degrees is right,
90 degrees is up, 180 degrees is left, and 270 degrees is down. Angles are normalised for collision
comparison but are not quantised for placement.

Each message uses `Anchor.Centre` / `Origin.Centre` and zero rotation. Its visual centre sits at the
polar anchor regardless of text width.

## 3. Message content

For discrete button objects (`Note` objects without duration), feedback emphasizes only actionable
timing information:

- Critical Perfect: no message;
- Perfect: only `EARLY` or `LATE`, rendered in white;
- Near: `NEAR` plus `EARLY` or `LATE`;
- Miss: only `MISS`, without a timing direction.

An effectively-zero Perfect has no direction to show and is therefore silent. Non-button results that
opt into display retain the general format below unless they expose duration activation feedback.

The primary line in the general format is the uppercase display name of the result:

- `CRITICAL PERFECT`
- `PERFECT`
- `NEAR`
- `BAD`
- `MISS`

For timing-bearing results, the secondary line comes from the signed `JudgementResult.TimeOffset`:

- negative: `EARLY`;
- positive: `LATE`;
- effectively zero: no secondary line.

Use a very small epsilon only to avoid unstable labels at floating-point zero; do not introduce a
perceptual dead zone that hides real timing information. Automatic expiry misses therefore read
`MISS / LATE`, while an early-miss input reads `MISS / EARLY`.

Duration results do not receive an early/late label. Hold tails and slider children expose the exact
credited activation proportion used by their duration judgement, including opening grace, and the
feedback snapshots it as a whole-number percentage. Their compact presentation is:

- Critical Perfect: no message;
- Perfect: only the activation percentage, rendered in white;
- Bad: `BAD` plus the activation percentage;
- Miss: only `MISS`.

Their application time is a frame-level detail at the segment end, while activation is the actual
quality measurement. Slider heads are catch-timed rather than duration-judged, so their existing
Perfect suppression remains unchanged.

Hold heads are the exception to `DisplayResult == false`: their nested drawables remain invisible and
opted out for general result presentation, but the halo surfaces their timing judgement with the same
compact rules as an ordinary cardinal or shoulder note. The hold parent's later duration judgement
retains duration semantics and remains independently filtered.

## 4. Collision stacking

Messages belong to the same collision cluster when their shortest circular angle difference is within
approximately 15 degrees. On insertion:

- the new message receives stack slot 0 at the base radius;
- existing live messages in the cluster move outward by one slot;
- each slot adds roughly one message height plus a small gap to the radius;
- a cluster holds at most three live messages; exceeding the cap retires the oldest immediately.

Recalculate clusters across all live messages after insertion, expiry, or revert so removing a middle
message closes the gap. Slot changes animate over a short duration instead of snapping. Chords at
distinct cardinal angles do not interfere, while cardinal and shoulder results sharing or nearly
sharing a side remain readable.

## 5. Appearance and lifetime

Recommended initial timings:

- 100 ms fade and scale in from about 0.85 scale;
- 450 ms fully readable;
- 250 ms fade out with a slight outward drift;
- approximately 800 ms total lifetime.

When a message moves to another stack slot, its anchor radius transforms over about 100 ms. Lifetime
continues during the move; stacking does not keep old messages alive indefinitely.

Rank colour is the primary quality cue:

- Critical Perfect: warm white/gold;
- Perfect: cyan;
- Near: yellow;
- Bad: orange;
- Miss: red.

The smaller detail line should be quieter than the rank. Its text carries the timing or activation
meaning, so colour is never the only distinction. Exact colours and font sizes are presentation-tuning
values and may be adjusted in the visual test scene without changing behaviour. Compact Perfect
feedback renders its direction or activation percentage in full-opacity white because that detail is
the entire message.

## 6. Rewind and lifecycle

Every live message retains the `JudgementResult` reference that created it. On `RevertResult`, the
matching message expires immediately; if it has already expired, the operation is a no-op. Replaying
forward can then create a fresh message when the same result object is applied again.

Subscriptions owned by `Ring` must be removed during disposal. Completed transforms must remove and
dispose their message drawables so long sessions do not accumulate expired children. Restarting or
seeking behind several results is handled entirely through the existing reverse-order result reversion
flow.

## 7. Tests

Add headless coverage for:

- discrete-button Critical Perfect silence, white direction-only Perfect, Near rank plus timing, and
  directionless Miss;
- Perfect suppression for hold parents, slider heads, and slider children;
- 0/90/180/270-degree placement matching the gameplay polar convention;
- negative, positive, and effectively-zero timing offsets;
- early-input Miss versus automatic late Miss;
- duration results omitting the timing direction;
- duration Critical Perfect silence, percentage-only Perfect, Bad plus rounded activation percentage,
  and rank-only Miss;
- `IgnoreHit`, `IgnoreMiss`, ordinary `DisplayResult == false`, and non-angle filtering;
- cardinal and shoulder hold heads bypassing their drawable opt-out and using discrete-note feedback;
- nested meaningful results reaching the display through `Ring.NewResult`;
- nearby-angle stacking, newest-first radial order, three-message cap, and circular seam proximity;
- expiry closing stack gaps and disposing messages;
- `RevertResult` removing the exact message and permitting a fresh message after replay;
- `DisplayJudgements` hiding and clearing the layer.

Add a visual test scene with representative grades at all four cardinal angles, a same-angle burst,
near-seam angles, and simultaneous chord results. Animation and colour tuning should be reviewed there;
tests should assert stable layout and state rather than intermediate transform pixels.

## Out of scope

- Displaying numeric millisecond offsets.
- User-configurable feedback radius, colours, duration, or density.
- Sound, vibration, particles, or ring flashes tied to judgement quality.
- Changing which existing drawables opt into `DisplayResult`.
- Replacing the results screen or the score/combo HUD.
