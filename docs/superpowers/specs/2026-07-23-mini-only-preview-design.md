# Mini-Only Chart Preview Design

## Goal

Remove the External chart preview completely while preserving Mini's approved appearance and behavior.
After removal, review the complete pull request for concrete architectural and code-quality problems,
fix findings within the Mini preview scope, and leave a smaller implementation whose ownership and
control flow are clear.

## User Experience

The editor exposes a checked-by-default `View > Mini Preview` checkbox. External is not shown,
disabled, or retained as a compatibility option.

Mini remains a fixed 190x190 panel with the current border, rounded corners, rendering, silence, and
input behavior. It remains draggable across the complete Compose workspace while staying below the
editor menu and above the transport bar. Position is persisted at drag end using positive bottom and
right offsets and reclamped after layout changes.

Mini remains visible only on Compose when the checkbox is checked. Entering Test temporarily closes
Mini, and returning to the editor restores the checkbox with an authoritative state at the returned
play time. Unchecking closes Mini and rechecking resynchronizes current chart state. Covered controls
remain blocked by Mini; uncovered editor controls retain normal input.

## Removal Boundary

Remove the complete External vertical slice:

- the Desktop `--chart-preview` command-line mode;
- child-process launch, ownership, graceful close, kill, and exit handling;
- named-pipe framing and IPC queues;
- the parent External controller and child preview game;
- launcher dependency injection through the game and editor;
- the External preview mode, menu item, failure handling, and lifecycle branches;
- External-only tests, fakes, assembly exposure, plans, and specifications.

No compatibility shim remains. External mode is not persisted, the command-line mode is not a shipped
contract, and this experimental project does not require backward compatibility.

Song-select audio preview and chart `PreviewTime` are unrelated and remain unchanged.

## Mini Architecture

Keep the existing in-process Mini pipeline unless review identifies a concrete defect:

1. `InlineChartPreviewPanel` owns visibility, layout, drag persistence, content, and controller
   lifetime.
2. `InlineChartPreviewController` observes the editor chart, structural state, scroll speed, and
   editor clock.
3. `ChartPreviewContent`, `ChartPreviewModel`, and `ChartPreviewClock` maintain a silent, read-only
   gameplay representation and deterministic preview time/results.
4. Existing preview state records remain where Mini consumes them. External-only control records and
   JSON polymorphism are removed.

Types should become internal when Desktop no longer consumes them. External abstractions must not be
retained merely to avoid changing constructors or tests. Shared constants currently owned by deleted
IPC types move to the Mini component that uses them.

## Code Review Standard

After External removal, review the complete diff from `master`, not only the deletion commit. Focus on:

- ownership and disposal boundaries;
- condition-heavy control flow that obscures state transitions;
- External policy checks left scattered through editor or gameplay code;
- duplicated preview behavior that can be centralized without redesigning Mini;
- unnecessary public APIs and abstractions;
- stale names, comments, tests, and documentation;
- missing rationale around non-obvious Mini lifecycle, rewind, result, and rendering behavior;
- regressions to ordinary gameplay, editor input, Test mode, or chart editing.

Fix Critical and Important findings. Fix Minor findings when the change is local and clearly improves
the Mini implementation. Do not perform speculative rewrites or unrelated cleanup.

## Error Handling

All process, pipe, connection, and External failure paths disappear. Mini keeps its existing local
failure boundary: a Mini failure unchecks `Mini Preview` and reports the failure without terminating
the editor. Normal disposal must detach editor subscriptions and release preview drawables.

## Testing

Use test-driven removal:

- first update menu and lifecycle tests to require the direct `Mini Preview` checkbox;
- retain or migrate External-controller tests that actually specify Mini producer/content behavior;
- delete tests that specify only process, pipe, connection, queue, or child-game behavior;
- retain Mini rendering, live-edit, clock, rewind, exact-time result, nested result, resize, drag,
  persistence, scaling, input ownership, tab switching, suspension, and Test-return coverage;
- add negative checks that no External menu item, CLI mode, launcher API, pipe API, or External symbol
  remains;
- run the focused editor/preview suites, a no-incremental desktop build, and the full unfiltered suite.

The final pull request must be based directly on current `master`, have a clean merge state, describe
Mini only, and include fresh verification and Pebble deployment evidence.
