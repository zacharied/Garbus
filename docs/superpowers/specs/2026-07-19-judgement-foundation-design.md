# Judgement foundation — design

Date: 2026-07-19
Status: approved

## Context

`docs/rules-specs/Judgement.md` is the canonical judgement spec, and the implementation currently
deviates from it everywhere: the code still runs on osu's vendored `HitResult` ladder and symmetric
`DefaultHitWindows`, note-lock uses mania semantics, and the duration/catch rules for holds, sliders,
and slams are unimplemented or first-cut stubs.

Alignment is split into cycles. **This design covers the foundation cycle only**: the native result
enum, asymmetric per-type hit windows, the eligibility/auto-miss edge, and the note-lock rewrite.
Later cycles (each with its own design): hold tails, slider head + children, slams.

## Decisions already made

- **Native enum, not a mapping.** The vendored files are owned copies (no osu.Game package
  reference), so `HitResult` is rewritten to spec-native grades rather than mapping spec grades onto
  osu names. Vendored files keep their ppy attribution header plus an "Adapted for Garbus:" note.
- **Single shared grade ladder.** The spec's three families (note / hold / early-permissive) share
  `Perfect` and `Miss` and never cross-compare intermediates, so each family is a subset of one
  ordinal ladder. No per-family enums.
- **Foundation first.** Holds/sliders/slams get mechanical re-mappings so they compile and behave
  approximately as before; their full spec alignment is deferred to their own cycles.
- **Slam↔node coincidence = same StartTime and same Side** (recorded for the slider cycle, which
  implements the rule; `Judgement.md` gets this clarification then).
- **Windows API**: rework the vendored `HitWindows` class in place with per-result (early, late)
  extents, rather than replacing it with a standalone table type.
- **Note-lock**: keep the per-drawable input flow and `CheckHittable` delegate; rewrite the
  predicate. No lane-centralised input dispatch.

## 1. `HitResult` enum + extensions

New enum, ordinal order (replaces osu's wholesale):

```
None < Miss < Bad < Near < Perfect < CriticalPerfect < IgnoreMiss < IgnoreHit
```

- `[Description]`s: "Miss", "Bad", "Near", "Perfect", "Critical Perfect". `[Order]` display
  attributes kept (best-first: CriticalPerfect, Perfect, Near, Bad, Miss; None/Ignores last) —
  `HitsoundFamily.Resolve` and the results breakdown sort by them.
- Deleted members: `Meh`, `Ok`, `Good`, `Great`, all ticks, both bonuses, `ComboBreak`.
- Family subsets: note = {Miss, Near, Perfect, CriticalPerfect}; hold = {Miss, Bad, Perfect,
  CriticalPerfect}; early-permissive = {Miss, Near, Perfect}; catch heads = {Miss, Perfect}.
- Extensions trim to: `IsHit` (Bad…CriticalPerfect, plus `IgnoreHit`), `IsMiss` (`Miss`,
  `IgnoreMiss`), `AffectsCombo` = basic range Miss…CriticalPerfect, `IsScorable` (ordinal
  `>= Miss && < IgnoreMiss`), `AffectsAccuracy`/`IsBasic` collapse to `IsScorable`,
  `IsValidHitResult` keeps its ordinal range check. `ValidateHitResultPair` keeps: max must be a
  hit, min must not be, `IgnoreHit` → `IgnoreMiss` pairing, basic max → min must be `Miss`.
  `IsTick`/`IsBonus` deleted.
- Combo semantics (not covered by the judgement spec; obvious default): every non-Miss basic result
  increases combo, `Miss` breaks it.

## 2. `Judgement`

- `MaxResult => CriticalPerfect` default.
- `MinResult`: `IgnoreHit → IgnoreMiss`, else `Miss`.
- Health table trimmed to the five real members (health is not wired to anything yet).
- Family-specific maxima (`MaxResult => Perfect` for slams and catch-timed slider parts) come from
  per-object `CreateJudgement` overrides (§6).

## 3. `HitWindows` — asymmetric rework

- `WindowFor(result)` returns a small readonly struct `(double Early, double Late)`. A symmetric row
  is `(x, x)`; the spec's early-only Miss row is `(200, 0)` — a **zero side means no window on that
  side**.
- `ResultFor(timeOffset)` is sign-aware: iterates best→worst among allowed results; a negative
  offset checks `-t <= Early`, a non-negative one `t <= Late`; first containing window wins
  (windows are nested, so first = innermost). Returns `None` if nothing contains the offset — a
  late press past Near's edge is ignored, matching the spec's "no late Miss window".
- New `LateEligibilityEdge` property = late extent of the **latest non-Miss** allowed window
  (110 cardinal / 150 shoulder). `CanBeHit(t)` → `t <= LateEligibilityEdge`. Auto-miss and drawable
  lifetimes key off this value.
- The `ensureValidHitWindows` debug hook grows a nesting assert: walking worst→best, each side's
  extent is non-increasing; a zero side is "absent" and exempt.
- `DefaultHitWindows` is deleted. Concrete tables (constants live here — the one place to tune; the
  spec marks the early-miss extents provisional):

| Windows class | CriticalPerfect | Perfect | Near | Miss |
|---|---|---|---|---|
| `CardinalNoteHitWindows` | (32, 32) | (64, 64) | (110, 110) | (200, 0) |
| `ShoulderNoteHitWindows` | (40, 40) | (80, 80) | (150, 150) | (200, 0) |

Both disallow `Bad` via `IsHitResultAllowed`.

- `HitObject.CreateHitWindows` default becomes `HitWindows.Empty` (objects without a timed button
  input get no phantom note windows). `HitObject.MaximumJudgementOffset` becomes
  `HitWindows?.LateEligibilityEdge ?? 0`; `GarbusSlamCentered`/`GarbusSlamEdge` override it to 200
  so their drawables' existing first-cut window logic keeps its lifetime headroom.

## 4. Window wiring

- `CardinalNote` and `ShoulderNote` override `CreateHitWindows` with their table. The empty override
  in `Note` is removed — each concrete type declares its windows.
- `CardinalHoldNote` / `ShoulderHoldNote` extend `Note` directly, so they carry the same overrides
  (their windows matter for lane eligibility).
- `HoldNoteHead<TParent>.CreateHitWindows => Parent.HitWindows`. The parent's windows instance
  exists by the time nested defaults run (`ApplyDefaultsToSelf` precedes `CreateNestedHitObjects`),
  and window classes are stateless, so instance sharing is fine. A shoulder hold's head thereby
  uses shoulder windows automatically.

## 5. Note-lock rewrite (`GarbusOrderedHitPolicy`)

Spec rule: an input resolves against the oldest eligible object in its lane whose window contains
it; eligibility ends at judgement or when the object's latest non-Miss window elapses; newer
objects are unaffected; inputs contained by no window are ignored.

- `IsHittable(hitObject, time)` returns **false iff an older eligible object's window contains the
  press**: any alive, unjudged object in the lane with strictly smaller StartTime whose
  `ResultFor(time − its StartTime) != None`.
- The candidate's own containment is not checked here: `DrawableNote.CheckForResult` already
  declines to consume when `ResultFor` is `None`, so the press flows to the next drawable in the
  queue. Net effect: exactly the oldest containing object accepts any press, independent of
  input-queue order.
- `HandleHit` and the `Lane.NewResult → HandleHit` subscription are deleted — no force-missing.
  Earlier objects die only by their own window elapsing (auto-miss at `LateEligibilityEdge`) or
  their own judgement.
- Accepted edge: two same-lane objects at the identical StartTime have no "older" ordering; the
  first in queue wins. Degenerate chart data — a candidate for an editor Verify check later, not a
  judgement concern.

## 6. Interim mappings (out-of-scope objects keep compiling; no behavioural redesign)

- **Hold tails** (`DrawableHoldNote`): `resultFor` re-keyed to the spec's hold proportions (since
  the old grades no longer exist): CriticalPerfect ≥ 100%, Perfect ≥ 95%, Bad ≥ 60%, else Miss.
  `headCarries` compares duration against `Head`'s `LateEligibilityEdge` instead of the deleted
  `WindowFor(Miss)`. Grace periods, floors, and the min-vs-best fix remain for the hold cycle.
- **Slams**: `CreateJudgement` override with `MaxResult => Perfect` + the
  `MaximumJudgementOffset => 200` override. Drawable logic untouched (Near grade comes in the slam
  cycle).
- **Slider head**: `CreateJudgement` → `MaxResult => Perfect`. Still the always-Perfect stub
  otherwise (real catch judgement in the slider cycle).
- **Slider child**: `CreateJudgement` → `MaxResult => Perfect`; binary catch logic untouched until
  the slider cycle.
- **`HitsoundFamilies`**: member keys re-mapped mechanically (best-grade keys move to each type's
  actual max; intermediates to the nearest new grade).
- **`PlayScreen`**: `scoreFor` → CriticalPerfect 320, Perfect 300, Near 200, Bad 100, Miss/Ignores
  0. Placeholder values, out of the judgement spec's scope. The results breakdown picks up the new
  `[Description]` names automatically.

## 7. Tests

- Update existing assertions in `TestSceneGameplay` / `TestScenePlayScreen` that reference osu
  grades.
- New headless pinning tests:
  - window-table boundaries per note type, including ±1 ms edges;
  - asymmetry: press at −150 on a cardinal → immediate Miss; press at +120 → ignored, then
    auto-miss at elapse;
  - auto-miss occurs at the Near late edge (not osu's 136/173);
  - note-lock oldest-first: a press inside two overlapping windows judges the older object;
  - no-force-miss: hitting a later note leaves an earlier one eligible until its own edge;
  - hold-head window inheritance: a shoulder hold's head uses the 150 ms Near window.

## Out of scope (later cycles)

- Hold cycle: grace periods (start + end rules), activated-at-EndTime floor, corrected
  short-duration rule.
- Slider cycle: real catch judgement for the head, hold-family child proportions, 200 ms segment
  grace, head-reference chain with catch-style pseudo-judgements, zero-length-segment resolution,
  slam-coincidence rule (same StartTime + same Side).
- Slam cycle: Near grade, gesture-timestamp exposure from `StickGestureTracker`, `Judgement.md`
  updates.
- Score values/health tuning, and removing the "differs from implementation" banner from
  `Judgement.md` once all cycles land.
