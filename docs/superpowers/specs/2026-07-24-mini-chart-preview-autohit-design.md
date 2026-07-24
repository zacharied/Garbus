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
  `hitEnd + animationDuration` — and **never calls `Expire()`**. Presence becomes a
  pure function of the clock, so seeking/rewinding is automatic.
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
  `EditorClock`**;
- holds a **cloned `GarbusChart`** (deep clone, as the existing F5/Test path already
  does) so editor state and playable state never share mutable instances — directly
  addressing the "mixing editor state with playable state" objection;
- renders the clone as `autoHit` (silent) drawables;
- subscribes to the editor's **existing change events** (`HitObjectAdded`,
  `HitObjectRemoved`, `HitObjectUpdated`, timing via `ControlPointInfo`, design,
  scroll-speed) and mirrors each into its clone and playfield — the same
  event-driven pattern the composer already uses for its editor drawables. A live
  edit refreshes the affected drawable **in place** (re-apply), not by recreating a
  framebuffer-backed slider every drag frame.

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
  after a rewind, with no dependence on how it got there (statelessness);
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
