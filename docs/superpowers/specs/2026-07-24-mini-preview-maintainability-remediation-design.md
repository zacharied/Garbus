# Mini Preview Maintainability Remediation Design

**Date:** 2026-07-24
**Status:** Approved for implementation

## Purpose

Keep Mini Preview's product behavior while resolving the correctness, performance, dependency-direction,
test, and documentation findings from the whole-branch maintainability review.

Mini remains a checked-by-default, silent, read-only 190x190 gameplay view in Compose. It follows live
editor chart changes and transport, can be dragged across the Compose workspace, persists its offset,
suspends while another screen owns the editor, restores from authoritative state, and supports stopped
seeks, running playback, exact maximum results, and rewind.

The remediation must make that behavior fit naturally into the repository. It must not preserve
process-era abstractions, add editor dependencies to core gameplay, or impose work proportional to the
whole chart on every Compose frame.

## Scope

The work includes all Important and Minor findings from the maintainability review:

- deterministic warning indicators under stopped seek and rewind;
- perfect-autoplay presentation for active holds and sliders;
- atomic batches and one revision owner;
- removal of the unrelated inspector hover/dropdown behavior regression;
- crossing-based result processing rather than a full-chart frame scan;
- removal of the obsolete message/model transport shape;
- removal of `Edit.Preview` dependencies from shared gameplay;
- durable comments, smaller tests, fewer private-reflection assertions, current documentation, stale-name
  cleanup, and removal of unrelated branch changes.

The following behavior is intentionally preserved:

- direct `View > Mini Preview` checkbox and checked default;
- Compose-only visibility and keep-open checkbox menu behavior;
- authoritative reopen, Test suspension/restoration, failure disablement, and disposal;
- panel size, workspace drag bounds, persisted bottom/right offsets, and input ownership;
- chart, timing, design, scroll-speed, transport, result, nested-object, rewind, warning, and chord updates;
- retained same-type root drawable identity where safe;
- silent preview and unchanged ordinary gameplay input, audio, judgement, and lifetime behavior;
- Mini connector foreground ordering and screen-space-invariant stroke width; ordinary gameplay retains
  master connector ordering and stroke behavior.

## State Boundary

### Controller

`InlineChartPreviewController` remains the editor-facing adapter. It owns:

- editor subscriptions;
- stable IDs for editor hit-object references;
- frame coalescing of adds, removes, updates, structural state, scroll range, and transport;
- the pending-delta bound and authoritative-snapshot fallback;
- transport cadence while running or smoothly seeking;
- one monotonically increasing revision per emitted snapshot or batch.

The controller no longer emits a hierarchy of message records. It constructs one of two typed values:

- `ChartPreviewSnapshot`: a complete independent chart state, object IDs, scroll range, and transport;
- `ChartPreviewBatch`: one revision containing ordered object changes plus optional structural, scroll, and
  transport changes.

Hit-object changes contain independent cloned `GarbusHitObject` instances rather than JSON strings. The
canonical serializer may be used once at the producer boundary to clone a full chart or changed object,
as it already is for clipboard and Test mode, but content must never decode the same update again.

### Content

`ChartPreviewContent` is the sole accepted-state owner. It owns:

- the accepted revision;
- the independent chart clone;
- ID-to-object and ID-to-drawable maps;
- pending drawable-load refresh ownership;
- design overlay and scroll range;
- preview clock;
- the ordered result timeline.

`ChartPreviewMessage` and `ChartPreviewModel` are deleted. A small pure helper may be extracted from
content only when it has one clear reusable responsibility; revision and chart ownership must not be
duplicated.

## Atomicity And Revisions

An incremental batch is accepted only when its revision is exactly `acceptedRevision + 1`. A snapshot is
accepted when it is newer than the current accepted revision and replaces all state.

Before mutating content, an incremental batch validates all operations against a staged ID/type view:

- every remove references an existing ID at that point in the ordered batch;
- every upsert contains a supported independent object clone;
- same-ID type replacement is explicit;
- duplicate or contradictory operations have deterministic ordered semantics;
- structural, scroll, and transport payloads are valid.

Validation failure rejects the whole batch. No object, drawable, index, clock, or accepted revision may be
partially changed. The controller immediately sends an authoritative snapshot and does not emit later
operations from the rejected frame. Controller bookkeeping such as `sentObjects` is committed only after
content accepts the batch.

Snapshots and batches are synchronous on the update thread. There is no queue, IPC, background mutation,
or external consumer to accommodate.

## Result Playback

Mini result progression is crossing-based rather than scan-based.

Content maintains ordered result entries for every loaded root and nested drawable. Each entry records
the drawable generation and exact target result time. Per-root ownership allows removal, replacement, and
same-type nested rebuilds to remove stale entries before registering current ones.

On forward transport, content applies maximum results only to unjudged entries crossed since the previous
time, in chronological order. On rewind, it reverts judged entries crossed in reverse chronological order.
No LINQ pipeline or recursive whole-chart traversal runs on frames where no result boundary is crossed.

Object edits rebuild result entries only for affected roots. Structural-only changes do not rebuild the
result timeline. Replacing or disposing a drawable removes its entries before the old generation can be
processed.

The result controller uses narrow generic gameplay APIs for applying and reverting a drawable result.
Ordinary `Playfield` result tracking returns as close to `origin/master` as possible. Preview-specific
chronological lists, `JudgementResult.RawTimeChanged`, and editor namespace references are removed from
shared gameplay.

## Gameplay Presentation Policy

Shared gameplay code may depend on a gameplay-namespace presentation contract, never on `Edit.Preview`.
Normal gameplay has no special policy and retains existing input, result, sample, and lifetime behavior.
Mini caches a perfect-autoplay implementation in its dependency scope.

The policy supplies only cross-cutting behavior that drawables cannot infer from ordinary input:

- deterministic result time and lifetime policy;
- whether result samples are suppressed;
- whether an active hold is presented as held;
- whether an active slider angle is presented as caught;
- whether visual state must be reconstructed from absolute chart time.

Mini's policy presents active duration objects as successfully held/caught and awards exact maximum
results. It does not install a gameplay input manager or read live controller state. Ordinary gameplay
continues to use actual button and analog state.

All root and nested hold/slider consumers must use the policy consistently for presentation and any
intermediate state that affects visuals. This includes hold body colour, slider body/tip/escape state,
slider head and control-point style, and activation-derived presentation. Slam input checks remain
ordinary gameplay behavior; Mini's external exact-result progression remains authoritative.

## Warning Indicators

Ordinary gameplay retains transition-driven breathing and fade behavior.

When the deterministic presentation policy is active, warning output is derived directly from absolute
chart time:

- outside the warning interval, alpha is exactly zero and transforms are cleared;
- during the initial half-breath, alpha interpolates from zero to one;
- afterward, alpha follows the existing one-to-minimum breathing waveform using an absolute phase from
  the warning interval start;
- angle and eligible-object selection still come from `WarningIndicatorSchedule`.

Stopped seek-in, seek-out, forward seek, and rewind therefore reconstruct the same angle and alpha without
depending on prior frames. Exact interval start may intentionally be zero-alpha; every interior timestamp
must produce the deterministic phase value.

## Incremental Global Updates

Object batches update broad state once after the batch, and only when relevant:

- warning schedule rebuilds when a slider or slam can affect stick occupancy;
- chord index rebuilds when cardinal membership, start time, or angle can change;
- only chord drawables at affected old/new start times need visual refresh;
- same-type roots retain identity; type replacements create one new generation;
- structural-only changes replace design/timing state without refreshing unrelated drawables;
- scroll changes update the scrolling bindable without rebuilding object state.

The pending-load visual refresh remains generation-checked and one-shot. Its work is proportional only to
recently created/loading drawables and becomes empty after load.

No formal chart-size threshold is required. Acceptance is structural: an idle/running frame with no result
crossing performs no operation proportional to total chart objects and creates no full-chart LINQ ordering
pipeline.

## Inspector And Toolbox Restoration

The inspector hover/dropdown suppression, finite dropdown height, right-toolbox scrolling, and playfield
overlay changes were introduced for discarded inspector-bound Mini iterations. Final Mini is mounted by
`ComposeTab` and does not require them.

Restore `Inspector`, `ExpandingToolboxContainer`, `HitObjectComposer`, and `GarbusHitObjectComposer` to
`origin/master` for these changes. Remove the branch-only inspector scroll/dropdown test. This resolves the
hover staleness regression by removing unrelated behavior rather than introducing another update policy.

## Error Handling

- A rejected incremental batch triggers an immediate authoritative snapshot without exposing partial
  state.
- A snapshot clone/apply failure closes Mini, disables the checkbox through the existing failure path, and
  releases subscriptions.
- A content-side unsupported object type or invalid staged operation requests a snapshot; repeated snapshot
  failure closes Mini rather than retrying indefinitely.
- Removing, replacing, rebuilding, or disposing content clears pending visual refresh and result-timeline
  ownership for stale generations.
- Closing, leaving Compose, suspending, or disposing remains silent and idempotent.

## Repository Cleanup

### Dependency And Naming Cleanup

- Remove all `Garbus.Game.Edit.Preview` imports from gameplay, judgement, drawable, playfield, and UI code.
- Restore ordinary `Playfield` and `JudgementResult` behavior close to master where the generic policy/result
  API does not require a deliberate change.
- Remove the stale `"Mini preview overlay"` container by restoring the unrelated playfield-overlay changes
  to `origin/master`.
- Correct comments after the final ownership and revision model is in place. Comments should explain
  atomicity, generation ownership, absolute-time reconstruction, and crossing-based results; they should
  not restate assignments.

### Documentation

Replace the branch's superseded Mini implementation plans/specs with one concise canonical Mini design and
one corresponding final implementation plan, following repository convention. They cover current behavior,
ownership, result/rewind policy, drag persistence, and verification expectations. Delete documents that
describe inspector-bound placement, old mode menus, force-push procedure, a specific PR, deployment sessions,
local paths, or historical test counts. Add a concise present-state Mini summary to `CLAUDE.md`.

This remediation design remains as the rationale for the architectural cleanup. It must contain no
machine-specific paths, deployment host instructions, or transient commit IDs.

### Unrelated Changes

Restore unrelated inspector, toolbox, menu formatting, BottomBar comment, and composer-overlay changes to
`origin/master`. Retain the useful cursor-confinement integration coverage because it covers behavior already
shipped on `origin/master`, but normalize its changed fixture to LF so default whitespace checks pass.

Ordinary gameplay keeps master chord-connector layering and fixed local stroke behavior. Mini alone places
connectors above overlapping notes and compensates stroke width for its scaled canvas. Comments explicitly
describe the context-specific order.

## Test Organization

Preserve behavioral coverage while reducing implementation coupling:

- split the large content fixture into state/batch, playback/rewind, and rendering/lifecycle fixtures with
  a small shared test base;
- move panel scaling, dragging, persistence, and input ownership into a dedicated Mini panel fixture rather
  than continuing to expand BottomBar tests;
- retain controller tests for coalescing, cadence, snapshots, overflow, and subscription lifecycle;
- replace private dictionary/revision reflection with observable behavior or narrow internal diagnostics;
- avoid asserting framework-private child order when an explicit production layer/depth contract can be
  asserted instead;
- keep generation/disposal instrumentation only where stale asynchronous work cannot be proven from public
  behavior.

Test movement must not combine behavior changes with weakened assertions. Existing high-value identity,
rewind, nested-result, failure, suspension, input, connector, and warning geometry cases remain.

## Required TDD Coverage

Before each production change, add or adapt a regression and observe the expected failure:

1. stopped warning seek-in produces deterministic non-zero interior alpha;
2. stopped seek-out clears warning output and rewind restores the exact phase;
3. mid-hold body presents held and mid-slider body/head/control point presents caught;
4. ordinary gameplay still reflects real inactive input;
5. a deliberately invalid operation in the middle of a batch commits nothing and immediately resyncs;
6. revision gaps reject the entire batch;
7. no-crossing frames visit zero result entries regardless of chart contents;
8. forward jumps apply only crossed entries in chronological order;
9. rewinds revert only crossed entries in reverse order, including nested hold/slider results;
10. type replacement, same-type nested rebuild, remove, full snapshot, and disposal clear stale timeline
    generations;
11. restored master inspector behavior passes its existing selection/value coverage;
12. ordinary gameplay retains master connector order while Mini keeps foreground/readable connectors;
13. full state, live edit, Test return, checkbox reopen, warning, chord, connector, and panel interaction
    coverage remains green.

## Acceptance Criteria

- All Important and Minor review findings are resolved or removed from branch scope.
- Mini behavior listed under Scope is preserved.
- Active holds/sliders visually agree with perfect exact results.
- Stopped warning output is deterministic and history-independent.
- Incremental batches are atomic and contiguous; rejected batches expose no partial frame.
- `ChartPreviewMessage` and `ChartPreviewModel` no longer exist.
- Preview content does not decode object updates already cloned by the controller.
- Shared gameplay has no dependency on `Edit.Preview`.
- Ordinary gameplay behavior and tests retain `origin/master` connector ordering and stroke semantics;
  foreground/scaled connector behavior is confined to Mini.
- Frames without result crossings perform no full-chart result traversal or sort.
- Inspector/toolbox/menu behavior is restored to `origin/master` where final Mini does not depend on it.
- Superseded/local process documentation and unrelated branch changes are removed.
- Default whole-branch whitespace checks pass.
- Focused regressions, complete affected fixtures, no-incremental build, and full unfiltered suite pass.
- A final independent whole-branch review finds no Critical, Important, or Minor issues.
