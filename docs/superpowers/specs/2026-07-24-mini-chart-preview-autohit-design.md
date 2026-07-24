# Mini chart preview via a general `autoHit` drawable capability

## Goal

Re-implement PR 4's "Mini preview" — a small, silent, read-only live gameplay
preview docked in the Compose workspace — without the two things that made that
PR ugly:

1. an **in-process message protocol** (`ChartPreviewMessage` full-state / upsert /
   remove / transport records, revision counters, per-object JSON encode/decode,
   `RequestResync`, delta batching) that was a leftover of the abandoned
   external-process/named-pipe preview and is pure incidental complexity now that
   everything runs in one process; and
2. **`previewContext != null` conditionals threaded through shared gameplay
   classes** (`DrawableHitObject`, `Playfield`, `GarbusPlayfield`) that existed
   only to make the same gameplay drawables auto-hit deterministically and survive
   arbitrary editor-clock seeks.

The user-visible feature is unchanged: a draggable ~190×190 preview that plays the
chart at the editor's current time, reflects live edits / timing / design /
scroll-speed changes, is seekable and rewindable with the editor clock, and shows
notes being hit as they reach the ring.

## Core idea

The gameplay classes stay ignorant of the editor and of "preview." Instead, a
gameplay drawable gains a general, presentation-only **`autoHit`** capability that
the preview host sets individually on the objects it spawns. Because an `autoHit`
drawable is a *pure function of the current clock time*, the preview is stateless
— scrub anywhere, rewind, jump — and needs no tracking, no revisions, and no
special-casing inside `Playfield`.

## `autoHit` — a general gameplay-drawable capability

A drawable constructed with `autoHit: true`:

- **derives its lifetime purely from time** — alive from `start − timeRange` until
  `hitEnd + animationDuration` — so presence is a pure function of the clock and
  seeking/rewinding is automatic. "Never calls `Expire()`" is the *outcome*, not a
  free property: the shared gameplay drawables expire themselves — the base
  auto-sets `LifetimeEnd` in `UpdateState` (`DrawableHitObject.cs:414`) and every
  concrete drawable calls `Expire()` from its Hit-arm `.OnComplete`. Those are
  imperative, `Time.Current`-derived `LifetimeEnd` writes; fired mid-scrub or on
  rewind they pin lifetime to a path-dependent value and the object vanishes or
  lingers, destroying statelessness. So `autoHit` must actively **neutralize
  drawable-side `LifetimeEnd` writes and own the window from time instead** — the
  same split the editor already ships (`EditorDrawableGarbusHitObject.cs:110`
  no-ops its `LifetimeEnd` setter; the container writes the entry lifetime). Not
  expiring does **not** leak: eviction is driven by the framework lifetime window,
  not by `Expire()`, so a finite computed `LifetimeEnd` still recycles the drawable.
- **schedules its hit animation at its hit time via an absolute-time transform
  sequence** (`BeginAbsoluteSequence(hitTime)` around the existing
  `UpdateHitStateTransforms(ArmedState.Hit)` visuals). osu-framework transforms are
  clock-addressable in both directions, so the animation plays forward and unplays
  on rewind for free.
- **skips the normal input / miss-check result path entirely** — it never produces
  a `JudgementResult`, never scores, never triggers judgement feedback. Auto-hit is
  **presentation only** in every context, now and later.
- **optionally plays a hitsound** via a *forward-crossing trigger*: a one-shot
  sample fired once as the clock passes the hit time going forward, and gated by a
  flag. It does nothing on rewind or backward scrub. One-shot samples can't be
  seeked, so this stays a forward-only side effect — correct for forward playback
  (a future audible Test-mode auto-hit) and simply left off for the Mini preview.

`autoHit` and the hitsound flag are set **individually per drawable** by whoever
spawns it. They are **not** derived from playfield input-manager presence — the two
concerns are orthogonal.

Auto-hit never emitting a `JudgementResult` is what lets `Playfield` revert to its
master implementation: with no results flowing from preview drawables, the original
`Stack`-based rewind loop is simply never exercised.

**All of this lives on the base `DrawableHitObject` as one additive flag — no
per-type subclasses.** `AutoHit` is a `public bool { get; init; }` on the base;
lifetime-swallow, result-path skip, absolute-time hit animation, and the hitsound
crossing are each a guard keyed off that one flag, written once and inherited by all
seven concrete drawables. The preview reuses the real gameplay drawables verbatim
(that is the point — it shows the true hit visuals), so a flag on the shared base is
correct where the editor's parallel `EditorDrawable*` hierarchy was not: those are
genuinely different objects, this is the same object in a different mode. `AutoHit`
is a self-contained property of the drawable, meaningful with no editor present — so
`if (autoHit)` on the base is the sanctioned general capability, not the
`if (previewContext != null)` editor-back-reference the design deletes.

## Non-interactive playfield

`GarbusPlayfield` gains a construction option to **not install the input manager**
(`analogInputManager` / gameplay bindings). This replaces the PR's
`previewContext`-null-check-then-`Dispose()` hack with an explicit, editor-agnostic
constructor choice. It is independent of `autoHit`: the host constructs the preview
playfield non-interactive *and* stamps `autoHit` on its drawables, as two separate
decisions.

## `MiniPreview` host

A self-contained host (no message protocol) that:

- owns a `GarbusPlayfield` constructed non-interactive, on a clock **slaved to the
  `EditorClock`** — the subtree's `Clock` is set to the resolved `EditorClock`, the
  same wiring `ComposeTab` already uses for the composer subtree;
- renders the editor's **live `GarbusHitObject` instances directly** as `autoHit`
  (silent) drawables — **no clone**. (Supersedes the earlier "cloned `GarbusChart`"
  decision.) The composer's cheap in-place refresh only works *because* it renders
  over the editor's real instances: `EditorChart` mutation → `ApplyDefaults()` →
  `HitObject.DefaultsApplied` → the existing drawable re-`Apply()`s in place. A clone
  loses that signal and there is no copy-state-onto-instance API on `GarbusHitObject`
  (only whole-object serialize/deserialize), so a clone forces either per-edit
  drawable recreation (the slider-recreation GC storm the design forbids) or brittle
  per-type field copy. Sharing instances is safe here **because `autoHit` drawables
  are strictly read-only observers** of their hit object: they never judge, score, or
  emit a `JudgementResult`, and never mutate the `HitObject` (nested-object regen is
  driven by `EditorChart`, seen by editor and preview drawables alike). This
  read-only guarantee is the load-bearing invariant of the shared-instance approach
  and is pinned by a test (no `JudgementResult`, no `HitObject` mutation from the
  preview path).
- subscribes to the editor's **existing change events** (`HitObjectAdded`,
  `HitObjectRemoved`, `HitObjectUpdated`) to add / remove+dispose / (implicitly,
  via `DefaultsApplied`) refresh drawables — the same event-driven `drawableMap`
  pattern the composer already uses for its editor drawables. Timing / design /
  scroll-speed changes flow through automatically: timing and design because the
  drawables read the shared `ControlPointInfo` / chart state on re-apply, scroll-speed
  because the preview's scrolling container binds the same cached `GarbusScrollingInfo`
  (a live `TimeRange` change invalidates its layout). A live edit refreshes the
  affected drawable **in place** (re-apply), never by recreating it.

Removed preview drawables must be explicitly `Dispose()`d (the non-pooled zombie
gotcha: `HitObjectContainer` detaches with `RemoveInternal(…, false)`, and an
undisposed drawable stays subscribed to `DefaultsApplied` and re-`Apply()`s forever).
All lambda subscriptions keep a field reference and are unsubscribed in `Dispose`
(per the known editor leak gotcha).

## Panel / UI (reused from PR 4)

The panel chrome is not the ugly part and is reused largely as-is, re-parented onto
the new host:

- the draggable docked panel that stays below the menu and above the transport bar,
  clamped to the Compose workspace, with persisted bottom/right offsets;
- the `View › Mini Preview` checkbox (checked by default; unchecking closes the
  preview, rechecking reopens it from authoritative editor state);
- suspend while Test or another screen owns the editor, then restore and resync at
  the returned play time.

## Net effect on the codebase

- **Deleted:** the entire `Edit/Preview/Chart*` message stack —
  `ChartPreviewMessage`, `ChartPreviewModel`, `ChartPreviewClock`,
  `ChartPreviewContent`'s protocol layer, `InlineChartPreviewController`, revisions,
  JSON upsert/remove/resync.
- **Reverted to master:** every `previewContext` branch in `DrawableHitObject`
  (`IsPresent`, `LifetimeEnd`, `UpdateResult`, `RawTime`, sample suppression,
  `ApplyPreviewResult`, `ExpireAfterTransforms`); all of `Playfield`'s new
  judged-entry machinery (back to the original `Stack` and its rewind loop); the
  `ChordConnectorOverlay` / `Ring` z-order change (connectors stay **under** the
  notes, per master) and any other change the PR made to gameplay visuals or
  behavior (`ChordConnectorOverlay`, `WarningIndicatorDisplay`, `ChordHighlighter`).
- **Added (general, editor-agnostic):** the `autoHit` drawable capability (+ its
  optional hitsound flag) and the non-interactive `GarbusPlayfield` option.
- **Kept:** the visually-neutral "compute colour/rotation in `OnApply`"
  re-applyability refactors from the PR (same pixels; they let a live edit refresh a
  note in place). Each is verified to not shift gameplay appearance; anything that
  does is dropped.
- **Reused:** the panel / checkbox / drag / persist / suspend UI.

## Testing

Headless test scenes:

- an `autoHit` drawable renders the correct visual at an arbitrary *seeked* time and
  after a rewind, with no dependence on how it got there (statelessness) —
  specifically, **scrubbing forward across the hit time and back leaves `LifetimeEnd`
  at the computed `hitEnd + animationDuration`**, proving the drawable-side
  `Expire()` / auto-expire writes are swallowed and did not pin lifetime to a
  path-dependent clock moment;
- an `autoHit` drawable with the hitsound flag off emits no sample; with it on,
  fires exactly once on a forward crossing and not on rewind;
- a live edit (add / remove / move a note, timing change) is reflected in the
  preview, and a retained slider drawable is refreshed in place rather than
  recreated;
- the preview installs no input manager and produces no `JudgementResult`;
- `Playfield` and the reverted gameplay drawables behave identically to master under
  normal gameplay (no regression from the reverts).

## Scope

The clean Mini preview is the deliverable. The `autoHit` hitsound capability is
*built* but wired only to "off" here. The editor **Test-mode auto-hit toggle**
(which flips the Test playfield's drawables to audible `autoHit`) is a natural
follow-up this design supports, not part of this change.
