# Mini Preview Design

**Status:** Implemented

## Purpose

Mini Preview is a silent, read-only gameplay view embedded in the editor's Compose workspace. It gives
authors immediate visual feedback without taking ownership of editor state, audio, or input.

## User behavior

- `View > Mini Preview` is checked by default.
- The 190x190 panel appears only in Compose. It stays above Compose controls and below the editor menu.
- Dragging is confined to the Compose workspace. Bottom and right offsets are saved at drag end and
  clamped again when layout bounds change.
- The panel owns pointer input only inside its bounds; uncovered editor controls remain usable.
- Unchecking closes synchronization. Rechecking opens from an authoritative snapshot, so edits made while
  closed appear immediately.
- Entering Test temporarily suspends Mini. Returning restores the user's prior setting and opens from an
  authoritative snapshot at the returned play time.
- A synchronization failure closes Mini, unchecks the menu item, and reports the error without terminating
  the editor.

## Ownership and state flow

`InlineChartPreviewPanel` owns layout, visibility, drag persistence, content, and the controller lifetime.
`InlineChartPreviewController` is the editor-facing adapter: it subscribes to chart and clock changes,
assigns stable non-reused IDs to editor object references, and coalesces one frame of changes.
`ChartPreviewContent` owns all accepted preview state and renders an independent chart clone.

The controller sends one of two typed values on the update thread:

- `ChartPreviewSnapshot` is a complete chart, object-ID set, scroll range, and transport state.
- `ChartPreviewBatch` is one atomic revision containing ordered removals and upserts plus optional chart
  structure, scroll range, and transport changes.

Each emitted value owns exactly one monotonically increasing revision. Content accepts only the next batch
revision; a newer snapshot replaces all state. A batch is fully staged and validated before mutation. If it
is rejected, producer bookkeeping is not committed and the controller immediately sends an authoritative
snapshot. Clone, snapshot, or batch failures close the controller and release subscriptions.

Hit objects cross this boundary as independent typed clones. Content owns ID-to-object and ID-to-drawable
maps, generation-checked pending visual refreshes, design state, scroll range, preview clock, and result
timeline. Same-type updates retain the root drawable when safe; type replacement creates a new generation
and invalidates stale work.

## Gameplay presentation

Mini installs a gameplay-owned presentation policy in its dependency scope. Shared gameplay code knows only
that policy, never editor preview types. The policy suppresses result samples, reconstructs visuals from
absolute chart time, presents active holds and sliders as successful, and supplies exact maximum-result
timing. Ordinary gameplay uses its normal input, sound, judgement, lifetime, and result-stack behavior.

Warnings derive their angle and alpha from absolute chart time under Mini's policy, so stopped seeks and
rewinds reproduce the same frame regardless of seek history. Mini-only connector layout compensates for the
scaled canvas and keeps connectors above overlapping notes; ordinary gameplay retains its normal ordering
and stroke behavior.

## Result playback

Content maintains an ordered result timeline for each current root and nested drawable generation. Forward
transport applies only entries crossed since the prior time, in chronological order. Rewind reverts crossed
entries in reverse chronological order. Removing, replacing, rebuilding, or disposing a root removes its
entries before the stale generation can run.

Frames that cross no result boundary perform no work proportional to the whole chart. Object edits rebuild
timeline entries only for affected roots; structural-only changes do not rebuild results.

## Verification contract

The durable regression boundary includes:

- typed snapshot/batch validation, coalescing, rejection recovery, stable IDs, and fatal cleanup;
- live chart, timing, design, scroll, and transport synchronization;
- exact result ordering, nested generations, rewind, edited times, and lifetime;
- warning, chord, connector, hold, slider, and spawn presentation;
- panel scaling, drag persistence, clamping, input ownership, and screen-space geometry;
- checkbox, Compose-only visibility, Test suspension/return, reopen, failure, and disposal;
- ordinary gameplay regressions alongside Mini-focused tests.

Run the affected suite with:

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~Preview|FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneMiniPreviewPanel|FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneTestMode|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~Chord|FullyQualifiedName~Warning"
```

Before release, also build `Garbus.Desktop.slnf` and run the test project without a filter.
