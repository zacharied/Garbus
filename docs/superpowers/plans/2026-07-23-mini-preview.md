# Mini Preview Implementation Record

**Status:** Implemented

## Outcome

Garbus now has one in-process Mini Preview: a checked-by-default 190x190 panel in Compose with persisted
workspace-relative positioning, authoritative reopen semantics, Test-mode suspension, typed atomic state,
deterministic presentation, and crossing-based result playback.

The canonical behavior and architecture are described in
[`../specs/2026-07-23-mini-preview-design.md`](../specs/2026-07-23-mini-preview-design.md). The later
maintainability remediation design and plan retain the rationale for the final ownership and performance
model.

## Implemented slices

### Panel and editor lifecycle

- `InlineChartPreviewPanel` owns the scaled canvas, border, input boundary, drag clamping, persisted bottom
  and right offsets, and the controller/content subtree.
- `GarbusEditor` exposes the direct `View > Mini Preview` checkbox and applies the single visibility rule:
  enabled and in Compose.
- Close/reopen, screen suspension, Test return, chart selection, and failures all converge on an
  authoritative reopen or a closed controller with released subscriptions.

### Typed synchronization

- `InlineChartPreviewController` observes the selected editor chart, structure, scroll range, and clock.
- Object references receive stable, monotonic IDs. Changes within one frame become one typed atomic batch.
- `ChartPreviewSnapshot` replaces all state; `ChartPreviewBatch` advances exactly one revision.
- Producer bookkeeping commits only after content accepts a value. Rejected batches recover immediately
  with a snapshot; clone/apply failures close the controller.
- `GarbusChartCloner` provides independent domain clones without a preview-specific wire format.

### Content and presentation

- `ChartPreviewContent` is the sole accepted-state owner and keeps the chart, object/drawable maps, pending
  visual generations, design overlay, scroll range, clock, and results together.
- A gameplay-owned policy supplies Mini's silent exact-result behavior and absolute-time hold, slider,
  warning, and connector presentation without introducing editor dependencies into shared gameplay.
- Broad warning/chord state refreshes once after relevant object batches. Same-type root updates retain
  resources where safe; stale loading generations cannot mutate current state.

### Results

- `PreviewResultTimeline` indexes current root and nested drawable generations by exact result time.
- Forward and backward transport process only crossed entries in deterministic order.
- Remove, replacement, nested rebuild, snapshot replacement, and disposal invalidate stale entries.
- Ordinary `Playfield` keeps its normal stack semantics; narrow exact apply/revert APIs support Mini.

### Tests and documentation

- Content coverage is split into primary state, results, and visuals partials.
- Panel behavior lives in `TestSceneMiniPreviewPanel`; BottomBar remains transport-focused.
- Mini editor lifecycle lives in `TestSceneEditorShell.MiniPreview`; controller coverage is split by object
  and transport concerns.
- Narrow internal diagnostics replace project-private reflection. Framework reflection remains only where
  disposal or internal child order is otherwise unobservable.
- Superseded placement, drag, checkbox, and process-era documents were removed in favor of the canonical
  design and this implementation record.

## Durable verification

```bash
dotnet build Garbus.Desktop.slnf --configuration Debug --no-incremental
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug --filter "FullyQualifiedName~Preview|FullyQualifiedName~Mini|FullyQualifiedName~TestMode|FullyQualifiedName~Gameplay|FullyQualifiedName~Chord|FullyQualifiedName~Warning|FullyQualifiedName~Result|FullyQualifiedName~Serializer"
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug
```

Source checks should also confirm that shared gameplay/UI has no dependency on `Garbus.Game.Edit.Preview`
and that no per-frame whole-chart result ordering path has returned.
