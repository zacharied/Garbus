# Judgement-keyed hitsound families — design

## Problem

Today every `GarbusHitObject` seeds a single flat sample list in its constructor
(`Samples = [ new(HIT_NORMAL, BANK_SOFT) ]`), and `DrawableHitObject.PlaySamples()` plays that whole
list on hit, regardless of which judgement was earned. The vendored `HitSampleInfo` machinery is built
to let *individual* objects draw from different sample banks — a flexibility Garbus does not want.

What Garbus wants instead: each **concrete hit object type** owns a **hitsound family** — a set of
sounds, one per judgement the type can earn — and the sound that plays on a hit is chosen by the
earned judgement. Only one sound per type exists today, so an unassigned judgement must fall back to
the next available family member.

## Decisions

- **Granularity:** per concrete object type. CardinalNote, ShoulderNote, CardinalHoldNote,
  ShoulderHoldNote, HoldNoteHead, SliderHead, SliderChild, SlamCentered, and SlamEdge each own a
  distinct family (the Cardinal/Shoulder hold split is explicitly required).
- **Key:** `HitResult` (the ladder the code actually produces: Perfect, Great, Good, Ok, Meh — and
  Miss, which never carries a sound).
- **Population now:** every family defines exactly one member, at the type's **best** judgement key
  (`HitResult.Perfect`, which is `MaxResult` for every current type), pointing at the existing
  `soft-hitnormal` sample. Judgement-specific sounds are added later by filling in more map entries.
- **Fallback:** for an earned judgement `J`, resolve toward *better* first (up the ladder toward
  Perfect), and only if nothing better is defined, toward *worse*. With a single top-keyed entry, every
  earned judgement resolves up to it.
- **Misses are silent:** `Miss` is never a family member, and playback stays gated on
  `ArmedState.Hit`, so a miss never reaches sound resolution.

## Components

### `HitsoundFamily` (new — `Garbus.Game/Gameplay/Audio/HitsoundFamily.cs`)

An immutable value holding a sparse `HitResult → HitSampleInfo` map, constructed from a dictionary
initializer.

- `IEnumerable<HitSampleInfo> AllSamples` — every distinct member, for preloading.
- `HitSampleInfo? Resolve(HitResult earned)` — orders candidate results by
  `HitResultExtensions.GetIndexForOrderedDisplay` (Perfect = 0 … Miss = 5). Scans from `earned`'s index
  toward 0 (better) returning the first defined member; if none, scans from `earned`'s index toward the
  worst returning the first defined member; otherwise returns null (empty family).

This type is the sole authority on the better-first-then-worse fallback. It has no framework
dependencies and is unit-testable in isolation.

### `GarbusHitObject` — declares the family

- Add `public abstract HitsoundFamily Hitsounds { get; }` (or a `protected virtual` factory), returning
  a **shared static** instance per concrete type so no per-object allocation occurs.
- Remove the `Samples = [ ... ]` constructor seeding. Instead, set `Samples = Hitsounds.AllSamples`
  (in the constructor or `ApplyDefaultsToSelf`). This keeps the **vendored preload path untouched**:
  `HitObject.SamplesBindable` still feeds every family member into the `HitSoundContainer` ahead of
  time, so there is no play-time load stutter.
- Each concrete type provides its own family declaration; all currently reference `soft-hitnormal`.

### `HitSoundContainer` — play a chosen member

- Track the originating `HitSampleInfo` alongside each loaded `DrawableSample` (info → sample).
- Add `public void Play(HitSampleInfo? info)` that plays the preloaded channel whose originating info
  matches; a null/unmatched info is a no-op.
- The existing parameterless `Play()` is retained for any wholesale use.
- `PlayCount` remains the test observability seam (incremented on any play that triggers a channel).

### `DrawableGarbusHitObject<T>` — select by judgement

- Override `PlaySamples()` to play the family member resolved from the earned `Result.Type`, guarded on
  `Result?.IsHit == true`.
- Most gameplay drawables derive from `DrawableGarbusHitObject<T>` (notes, hold notes, hold-note heads,
  slider heads, slider bodies, slams), so one override there covers them. The exception is
  `DrawableSliderChild`, which derives from `DrawableHitObject<SliderChild>` directly and needs its own
  override. Both delegate to a shared static helper so the resolution logic lives in one place. The
  vendored `DrawableHitObject.PlaySamples()` is left as written.

## Data flow

1. Object constructed → `Hitsounds.AllSamples` seeds `HitObject.Samples`.
2. Drawable applied → vendored `samplesBindable` binding preloads all family members into the
   `HitSoundContainer`.
3. Hit judged → `UpdateState(ArmedState.Hit)` calls `PlaySamples()`.
4. `PlaySamples()` resolves `Result.Type` through the family (better-first-then-worse) and plays that
   single preloaded member. Miss → no `PlaySamples()` call → silence.

## Testing

- **`HitsoundFamily.Resolve` unit tests:** single-top-entry family resolves every earned judgement to
  that entry; a mid-ladder-only family resolves better-side judgements down to it and worse-side
  judgements up to it (better-first precedence); empty family returns null.
- **Gameplay test:** a non-Perfect hit against a Perfect-only family plays the Perfect member
  (asserted via `HitSoundContainer.PlayCount` and the matched info), and a miss increments no play.

## Out of scope

- Authoring judgement-specific sound files (only `soft-hitnormal` exists; families are structured for
  later expansion).
- Any change to judgement/hit-window behaviour or the `HitResult` ladder itself.
- Authoring judgement-specific slam sounds; the slam drawables already route through the override, so
  they play their family's single member like every other type.
