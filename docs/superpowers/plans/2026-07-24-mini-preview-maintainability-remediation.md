# Mini Preview Maintainability Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `subagent-driven-development` to execute this plan task by task. Keep one implementation worker at a time for production changes because the tasks share state; use independent review workers after each commit.

**Goal:** Preserve Mini Preview's validated product behavior while replacing its process-shaped in-process protocol, eliminating history-dependent visuals and full-chart per-frame work, restoring natural gameplay dependency direction, and removing all maintainability-review residue.

**Architecture:** `InlineChartPreviewController` synchronously transfers detached typed `ChartPreviewSnapshot` and atomic `ChartPreviewBatch` values to `ChartPreviewContent`. Content alone owns accepted revision, chart state, drawables, and an ordered result timeline. Shared gameplay resolves an optional gameplay-owned presentation policy; it never references editor preview code. Ordinary gameplay remains on its existing input, result-stack, animation, sample, and connector paths.

**Tech Stack:** C# 12, .NET 8, osu-framework drawables/DI/clocks, NUnit visual test scenes.

**Design:** `docs/superpowers/specs/2026-07-24-mini-preview-maintainability-remediation-design.md`

## Global Constraints

- Work from `origin/master...HEAD`; local `master` may not be current.
- Use TDD for every behavior change: add or adapt one focused regression, run it and confirm the expected failure, make the smallest production change, then rerun the focused fixture.
- Preserve the direct checked-by-default `View > Mini Preview` checkbox, Compose-only visibility, authoritative reopen, Test suspension/restoration, failure handling, dragging, persistence, input ownership, live edits, exact results, and rewind.
- Preserve same-type root drawable identity while transferring it to an independent replacement object.
- Mini remains silent and does not install or consume live gameplay input.
- Ordinary gameplay must not change except for generic internal seams with a no-policy path identical to `origin/master`.
- Mini alone uses foreground chord connectors and screen-space-compensated connector width.
- Do not preserve compatibility wrappers for `ChartPreviewMessage`, `ChartPreviewModel`, `ChartPreviewContext`, or removed preview JSON APIs.
- Do not use wall-clock performance assertions. Expose narrow internal counters and assert visited result entries.
- Do not weaken existing disposal, generation, nested-result, lifecycle, or interaction assertions while reorganizing fixtures.
- Commit after each task only when its focused verification passes.

## File Structure

### New Production Files

- `Garbus.Game/Charts/GarbusChartCloner.cs`: exhaustive direct detached copies for chart metadata, timing/design state, paths, and every supported hit-object type.
- `Garbus.Game/Gameplay/IGameplayPresentationPolicy.cs`: optional gameplay-owned contract and ordinary-default helpers.
- `Garbus.Game/Edit/Preview/ChartPreviewState.cs`: typed IDs, transport, structure, object state, snapshots, and batches.
- `Garbus.Game/Edit/Preview/PreviewGameplayPresentationPolicy.cs`: Mini's perfect-autoplay, silent, clock-driven policy.
- `Garbus.Game/Edit/Preview/PreviewResultTimeline.cs`: ordered crossing cursor for root and nested drawable results.

### Deleted Production Files

- `Garbus.Game/Edit/Preview/ChartPreviewMessage.cs`
- `Garbus.Game/Edit/Preview/ChartPreviewModel.cs`
- `Garbus.Game/Edit/Preview/ChartPreviewContext.cs`

### Primary Modified Production Files

- `Garbus.Game/Edit/Preview/InlineChartPreviewController.cs`: typed cloning producer, stable IDs, one revision per accepted frame, synchronous rejection/resnapshot handling.
- `Garbus.Game/Edit/Preview/ChartPreviewContent.cs`: sole state/revision owner, staged batch validation/commit, retained drawable application, timeline ownership.
- `Garbus.Game/Edit/Preview/ChartPreviewClock.cs`: consume typed transport without owning revision.
- `Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs`: connect content rejection/fatal failure to the existing close/disable lifecycle.
- `Garbus.Game/Edit/EditorChart.cs`: remove preview-only serializable-chart construction.
- `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`: remove preview-only structural/decode API while retaining undo/clipboard APIs.
- `Garbus.Game/Gameplay/Judgements/JudgementResult.cs`: restore plain `RawTime` storage.
- `Garbus.Game/Gameplay/UI/Playfield.cs`: restore ordinary result-stack semantics and add narrow exact apply/revert support.
- `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs`: generic policy integration and exact-result application.
- `Garbus.Game/UI/GarbusPlayfield.cs`: omit gameplay input only through the generic policy.
- `Garbus.Game/UI/WarningIndicatorDisplay.cs`: absolute-time Mini warning alpha, unchanged ordinary transforms.
- `Garbus.Game/UI/ChordConnectorOverlay.cs` and `Garbus.Game/UI/Ring.cs`: policy-based Mini rendering, master ordinary rendering.
- Hold/slider/note/slam drawable files under `Garbus.Game/Objects/Drawables/`: replace `IsInPreview` and inactive-input visual reads with policy decisions.

### Test Organization

- Add `Garbus.Game.Tests/Charts/GarbusChartClonerTest.cs`.
- Replace `Garbus.Game.Tests/Editor/TestChartPreviewModel.cs` with `TestChartPreviewState.cs` or content-level typed-state coverage.
- Split `TestSceneChartPreviewContent.cs` into primary, `.Results.cs`, and `.Visuals.cs` partial files.
- Split `TestSceneInlineChartPreviewController.cs` into primary, `.Objects.cs`, and `.Transport.cs` partial files.
- Add `TestSceneMiniPreviewPanel.cs`; move Mini panel geometry/drag/input tests out of `TestSceneBottomBar.cs`.
- Add `TestSceneEditorShell.MiniPreview.cs`; move Mini shell cases out of the primary partial fixture.
- Keep framework reflection only for disposal, exact child order, and rejected clock states with no practical public API; centralize repeated reflection helpers.

---

### Task 1: Remove Unrelated Branch Residue

**Files:**
- Restore branch changes from: `Garbus.Game/Edit/Inspector.cs`
- Restore branch changes from: `Garbus.Game/Edit/Compose/ExpandingToolboxContainer.cs`
- Restore branch changes from: `Garbus.Game/Edit/Compose/HitObjectComposer.cs`
- Restore branch changes from: `Garbus.Game/Edit/GarbusHitObjectComposer.cs`
- Restore branch changes from: `Garbus.Game/Edit/GarbusMenu.cs`
- Restore branch changes from: `Garbus.Game/Edit/Screens/BottomBar/BottomBar.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneGarbusGame.cs`

**Interfaces:**
- Preserve: final Mini mounting under `ComposeTab`.
- Remove: inspector hover/menu deferral, right-toolbox scrolling, stale playfield overlay, and unrelated formatting/comment changes.
- Preserve: cursor-confinement integration coverage already exercising behavior on `origin/master`.

- [ ] **Step 1: Capture the exact branch-only hunks**

Run:

```bash
git diff origin/master...HEAD -- Garbus.Game/Edit/Inspector.cs Garbus.Game/Edit/Compose/ExpandingToolboxContainer.cs Garbus.Game/Edit/Compose/HitObjectComposer.cs Garbus.Game/Edit/GarbusHitObjectComposer.cs Garbus.Game/Edit/GarbusMenu.cs Garbus.Game/Edit/Screens/BottomBar/BottomBar.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
```

Confirm each production hunk is from a discarded inspector-bound Mini iteration and that current `ComposeTab` does not consume it.

- [ ] **Step 2: Restore only those hunks to `origin/master`**

Manually patch the listed files to their `origin/master` behavior. Remove `TestInspectorScrollAndRealEasingDropdownInteraction` and its now-unused user-interface import. Do not overwrite unrelated concurrent work.

- [ ] **Step 3: Normalize the changed cursor fixture to LF**

Keep both cursor tests in `TestSceneGarbusGame.cs`; normalize only this changed file's line endings so whole-branch `git diff --check` no longer reports every line.

- [ ] **Step 4: Verify restored editor behavior**

Run:

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneComposeSelection|FullyQualifiedName~TestSceneGarbusGame"
git diff --check origin/master...HEAD
```

Expected: selected tests pass; any remaining whitespace output is investigated rather than ignored.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs Garbus.Game.Tests/Visual/TestSceneGarbusGame.cs
git commit -m "refactor: remove obsolete mini preview residue"
```

---

### Task 2: Add Exhaustive Direct Chart Cloning

**Files:**
- Create: `Garbus.Game/Charts/GarbusChartCloner.cs`
- Create: `Garbus.Game.Tests/Charts/GarbusChartClonerTest.cs`
- Modify as required for direct copy APIs: `Garbus.Game/Charts/Design/DesignPoint.cs`
- Modify as required for direct copy APIs: `Garbus.Game/Charts/Design/DesignPointInfo.cs`
- Reuse: `Garbus.Game/Charts/Timing/ControlPointInfo.cs`
- Inspect exhaustively: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`

**Interfaces:**

```csharp
internal static class GarbusChartCloner
{
    public static GarbusHitObject CloneHitObject(GarbusHitObject source);
    public static ChartMetadata CloneMetadata(ChartMetadata source);
    public static DesignPointInfo CloneDesignPointInfo(DesignPointInfo source);
    public static GarbusChart CloneChart(GarbusChart source, ControlPointInfo effectiveControlPointInfo);
}
```

The exact visibility may be `internal`; tests use the existing friend assembly. The clone owns every mutable descendant and does not apply defaults to or mutate the source.

- [ ] **Step 1: Write exhaustive clone tests**

Construct every top-level hit-object runtime type supported by `GarbusChartSerializer` and populate every mutable property, including side, angle, times, easing, slider path/control points, and samples. Also create non-default metadata, timing points, and each concrete design-point type.

Assert:

- equivalent values and runtime types;
- no shared hit-object, path, control-point, metadata, timing, design-point, or collection references;
- mutating the source after cloning does not change the clone;
- applying defaults to the clone does not mutate source nested objects;
- unsupported future runtime types fail explicitly.

- [ ] **Step 2: Run the clone tests and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~GarbusChartClonerTest"
```

Expected: compile failure because `GarbusChartCloner` does not exist.

- [ ] **Step 3: Implement exhaustive direct copies**

Mirror the serializer's exhaustive type switches, but instantiate domain objects directly. Reuse `ControlPointInfo.DeepClone()`. Add the smallest direct design-point copy support needed. Copy mutable path/control-point collections element by element. Throw for unsupported types rather than sharing references.

- [ ] **Step 4: Prove direct ownership**

Run the focused clone test and existing chart serializer tests. Search `GarbusChartCloner` for serializer calls; expected none.

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~GarbusChartClonerTest|FullyQualifiedName~Serializer"
rg -n "GarbusChartSerializer|Encode|Decode" Garbus.Game/Charts/GarbusChartCloner.cs
```

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Charts Garbus.Game.Tests/Charts
git commit -m "refactor: add direct chart state cloning"
```

---

### Task 3: Introduce A Gameplay-Owned Presentation Policy

**Files:**
- Create: `Garbus.Game/Gameplay/IGameplayPresentationPolicy.cs`
- Create: `Garbus.Game/Edit/Preview/PreviewGameplayPresentationPolicy.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewContent.cs`
- Modify: `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs`
- Modify: `Garbus.Game/UI/GarbusPlayfield.cs`
- Modify: `Garbus.Game/UI/ChordConnectorOverlay.cs`
- Modify: `Garbus.Game/UI/Ring.cs`
- Modify note/hold/slider/slam files currently reading `IsInPreview`.
- Test: `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.cs`
- Test: `Garbus.Game.Tests/Gameplay/TestSceneGameplay.cs`

**Interfaces:**

Keep the contract narrow and behavior-based. It must cover only observed alternate presentation behavior:

```csharp
internal interface IGameplayPresentationPolicy
{
    bool HandlesInput { get; }
    bool PlaysSamples { get; }
    bool PlaysSpawnAnimations { get; }
    bool UsesExternalResults { get; }
    bool UsesClockDrivenVisuals { get; }
    bool PresentsHoldAsHeld(DrawableHoldNote hold);
    bool PresentsSliderAngleAsCaught(Side side, double angleDeg);
}
```

Add lifetime/effect properties only where required to replace an existing `ChartPreviewContext` branch. Absence of the policy is ordinary gameplay, with behavior byte-for-byte equivalent where practical.

- [ ] **Step 1: Add RED continuous-visual and ordinary-gameplay tests**

At stopped times inside active duration objects, assert Mini displays:

- cardinal and shoulder hold bodies in held colour rather than dropped colour;
- slider body, tip/escape state, head, and control point as caught;
- exact maximum results remain unchanged.

Add a paired ordinary gameplay assertion with no active input that the same objects still show inactive/dropped state. Use observable colours/state or narrow internal diagnostics, not preview namespace type checks in gameplay.

- [ ] **Step 2: Run focused tests and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneChartPreviewContent|FullyQualifiedName~TestSceneGameplay"
```

Expected: Mini active hold/slider presentation disagrees with exact autoplay results.

- [ ] **Step 3: Add the optional gameplay policy and Mini implementation**

Place the interface under `Gameplay`. Cache only `PreviewGameplayPresentationPolicy` in Mini content's dependency container. Replace every `IsInPreview` and `ChartPreviewContext` gameplay branch with either an optional policy property or a single helper on `DrawableHitObject`.

For ordinary gameplay, continue to read actual hold presses, analog catchers, play samples, animate spawn, and expire normally. For Mini, do not construct a disposable `AnalogInputManager`; policy-based hold/slider presentation must not read it.

- [ ] **Step 4: Confine connector behavior to Mini**

Restore master connector layer/stroke behavior for ordinary gameplay. Under the clock-driven Mini policy only:

- place connectors above overlapping notes;
- derive alpha from absolute preview time;
- compensate path radius for Mini canvas scaling.

Update comments to describe the two deliberate paths.

- [ ] **Step 5: Verify dependencies and behavior**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneChartPreviewContent|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~Chord"
rg -n "Garbus.Game.Edit.Preview|ChartPreviewContext|IsInPreview" Garbus.Game/Gameplay Garbus.Game/Objects Garbus.Game/UI
```

Expected: focused tests pass; the search has no matches.

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Gameplay Garbus.Game/Objects Garbus.Game/UI Garbus.Game/Edit/Preview Garbus.Game.Tests
git commit -m "refactor: decouple preview presentation from gameplay"
```

---

### Task 4: Make Warning Indicators Deterministic In Mini

**Files:**
- Modify: `Garbus.Game/UI/WarningIndicatorDisplay.cs`
- Reuse: `Garbus.Game/UI/WarningIndicatorSchedule.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.Visuals.cs` after the fixture split, or the current content fixture before the split.
- Test: existing ordinary warning/gameplay fixtures.

**Interface:**
- Ordinary path: existing transition-driven reveal/breath/fade.
- Clock-driven policy path: output angle and alpha derived only from current chart time and schedule interval.

- [ ] **Step 1: Add history-independence regressions**

For a stopped preview warning interval, record alpha at an interior timestamp. Seek out, seek back to the same timestamp, and assert exact alpha restoration. Also assert:

- interior seek-in is non-zero;
- seek before/after interval is exactly zero;
- rewind into the interval restores angle and phase;
- equivalent timestamps reached by forward and backward history produce equal alpha.

- [ ] **Step 2: Run the warning cases and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~Warning"
```

Expected: stopped seek history leaves transforms frozen or produces a different alpha.

- [ ] **Step 3: Implement absolute-time Mini alpha**

Under `UsesClockDrivenVisuals`, clear stale transforms and derive alpha from the schedule interval:

- zero outside the interval;
- interpolate zero to one during the initial half-breath;
- then use the existing one-to-minimum breathing amplitude and duration with phase anchored at interval start.

Do not alter ordinary transition code.

- [ ] **Step 4: Verify warning and ordinary gameplay fixtures**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~Warning|FullyQualifiedName~TestSceneGameplay"
```

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/UI/WarningIndicatorDisplay.cs Garbus.Game.Tests
git commit -m "fix: make mini warning visuals deterministic"
```

---

### Task 5: Define Typed Preview State And Atomic Content Commits

**Files:**
- Create: `Garbus.Game/Edit/Preview/ChartPreviewState.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewContent.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewClock.cs`
- Delete: `Garbus.Game/Edit/Preview/ChartPreviewModel.cs`
- Delete: `Garbus.Game/Edit/Preview/ChartPreviewMessage.cs`
- Delete: `Garbus.Game.Tests/Editor/TestChartPreviewModel.cs`
- Add/Modify: typed state tests in `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.cs`

**Interfaces:**

```csharp
internal readonly record struct PreviewObjectId(long Value);
internal readonly record struct PreviewTransportState(double Time, bool IsRunning, double Rate, long Timestamp);
internal sealed record PreviewObjectState(PreviewObjectId Id, GarbusHitObject HitObject);
internal sealed record PreviewChartStructure(/* detached metadata, timing, design, chart identity */);
internal sealed record ChartPreviewSnapshot(/* revision, structure, objects, range, transport */);
internal sealed record ChartPreviewBatch(/* revision, removes, upserts, optional structure/range, transport */);
```

Use immutable/read-only collection boundaries. One batch structurally places removals before upserts; do not recreate a polymorphic message union.

Content exposes synchronous `Replace(ChartPreviewSnapshot)` and `Apply(ChartPreviewBatch)` acceptance. It alone exposes internal `AcceptedRevision`, `CurrentChart`, and narrow counters needed by tests.

- [ ] **Step 1: Replace protocol tests with atomic state regressions**

Add tests for:

- one multi-object batch consumes one revision;
- revision gap rejects the whole batch;
- duplicate IDs, missing removals, remove/upsert overlap, invalid range/rate, and unsupported objects reject;
- an invalid later operation changes no chart object, drawable, clock, range, or revision;
- a newer snapshot authoritatively replaces all state;
- same-type upsert retains the root drawable but applies the incoming detached object;
- type-changing upsert replaces and disposes the drawable;
- equal-valued source objects remain distinct by ID.

- [ ] **Step 2: Run typed-state tests and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestChartPreviewModel|FullyQualifiedName~TestSceneChartPreviewContent"
```

Expected: tests do not compile against the not-yet-created typed API.

- [ ] **Step 3: Implement staging and validation without live mutation**

Before touching drawables or content fields:

- require `Revision == AcceptedRevision + 1` for batches;
- validate non-default collections, positive unique IDs, remove/upsert disjointness, existing removals, supported detached objects, finite transport/range, and chart identity;
- build the complete next ID/object dictionary and sorted chart list in temporary collections;
- build a drawable action plan for retain, replace, create, and dispose.

Return `false` on expected validation rejection. Leave all live state untouched.

- [ ] **Step 4: Implement non-throwing commit order**

On accepted state:

1. detach removed/replaced roots;
2. swap prepared chart/object state;
3. apply incoming same-type objects to retained roots via `DrawableHitObject.Apply()`;
4. create type replacements/new roots;
5. dispose stale roots/generations;
6. refresh global playfield consumers once;
7. replace structure/range only when present;
8. apply captured transport;
9. publish accepted revision last.

Unexpected commit exceptions are fatal to that preview instance; do not attempt rollback of a partly changed framework drawable tree.

- [ ] **Step 5: Remove model/message ownership**

Move remaining chart/object maps into content. Delete `ChartPreviewModel`, `ChartPreviewMessage`, and duplicate revision gates. Adapt `ChartPreviewClock` to `PreviewTransportState` without revision storage.

- [ ] **Step 6: Verify atomicity**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneChartPreviewContent|FullyQualifiedName~TestChartPreviewClock"
rg -n "ChartPreviewMessage|ChartPreviewModel" Garbus.Game Garbus.Game.Tests
```

Expected: focused tests pass; search has no production/test references.

- [ ] **Step 7: Commit**

```bash
git add -A Garbus.Game/Edit/Preview Garbus.Game.Tests/Editor
git commit -m "refactor: make preview state typed and atomic"
```

---

### Task 6: Add Crossing-Based Result Playback

**Files:**
- Create: `Garbus.Game/Edit/Preview/PreviewResultTimeline.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewContent.cs`
- Modify: `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs`
- Modify: `Garbus.Game/Gameplay/UI/Playfield.cs`
- Modify: `Garbus.Game/Gameplay/Judgements/JudgementResult.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.Results.cs`
- Test: `Garbus.Game.Tests/Gameplay/TestSceneGameplay.cs`

**Interfaces:**

```csharp
internal void DrawableHitObject.ApplyResultAt(HitResult result, double rawTime);
internal bool Playfield.RevertResult(JudgementResult result);
```

`PreviewResultTimeline` stores immutable sorted entries `(time, rootId, treeOrder, drawable)`, a cursor, and an internal visited-entry counter for structural performance assertions.

- [ ] **Step 1: Add RED chronology and complexity tests**

Cover:

- forward jump applies only crossed entries in `(time, root ID, post-order tree order)`;
- rewind reverts the exact reverse order;
- exact boundary is applied, rewind below boundary reverts;
- slider final child applies before body at equal end time and reverts after body;
- hold head/start and hold body/end chronology;
- exact `Judgement.MaxResult`, including ignored body results;
- `RawTime` equals scheduled result time, not frame time;
- editing a judged object later than stopped current time reverts it;
- live insertion earlier than stopped current time catches up correctly;
- remove, type replacement, same-type nested rebuild, full snapshot, and disposal cannot process stale generations;
- repeated no-crossing frames visit zero entries on a large chart;
- jumping over `k` results visits exactly `k` independent of total count.

- [ ] **Step 2: Run result cases and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~Result|FullyQualifiedName~Rewind|FullyQualifiedName~Lifetime"
```

Expected: existing code traverses/sorts the complete drawable tree each rendered frame and cannot satisfy the visit counter.

- [ ] **Step 3: Restore ordinary result ownership close to master**

In `JudgementResult`, remove `RawTimeChanged` and restore plain storage. In `Playfield`, restore ordinary stack chronology and remove preview-specific mutable-time ordering. Add only narrow exact apply/revert seams needed by the external timeline; assert stack-top chronology on revert.

- [ ] **Step 4: Implement the timeline**

Build entries when complete root/nested drawable trees are ready. Traverse each root post-order, assign stable tree order, and sort once by time/root/tree order. Forward processing advances while `entry.Time <= currentTime`; rewind retreats while `entry.Time > currentTime`.

For any object batch, use the safe first implementation:

1. revert currently applied timeline entries in reverse;
2. commit typed object state;
3. rebuild entries from current generations;
4. reset cursor;
5. advance to unchanged captured time.

Do not scan all objects during ordinary `Update()` when no edit or result boundary occurs.

- [ ] **Step 5: Handle nested readiness without overtaking**

If an affected retained/new root has not completed nested drawable loading, mark its timeline rebuild pending. The earliest due unready generation blocks later due results; do not let later results overtake chronology. Remove pending ownership on replacement/removal/disposal.

- [ ] **Step 6: Verify preview and ordinary result behavior**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneChartPreviewContent|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~Result|FullyQualifiedName~Rewind|FullyQualifiedName~Lifetime"
rg -n "SelectMany.*Nested|OrderBy.*Result|RawTimeChanged|Garbus.Game.Edit.Preview" Garbus.Game/Gameplay
```

Expected: result tests pass; no per-frame full-tree ordering pipeline or preview dependency remains in gameplay.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Preview Garbus.Game/Gameplay Garbus.Game.Tests
git commit -m "refactor: index mini preview result playback"
```

---

### Task 7: Convert The Controller To Typed Atomic Batches

**Files:**
- Modify: `Garbus.Game/Edit/Preview/InlineChartPreviewController.cs`
- Modify: `Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs`
- Modify: `Garbus.Game/Edit/EditorChart.cs`
- Modify: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneInlineChartPreviewController.cs`
- Add: `Garbus.Game.Tests/Editor/TestSceneInlineChartPreviewController.Objects.cs`
- Add: `Garbus.Game.Tests/Editor/TestSceneInlineChartPreviewController.Transport.cs`

**Interfaces:**
- Stable IDs are producer-owned source-reference identities and never reused during one controller lifetime.
- One accepted snapshot or nonempty frame batch advances producer and consumer revision exactly once.
- Every batch contains transport captured after chart changes.
- Synchronous `false` acceptance immediately triggers one authoritative snapshot.

- [ ] **Step 1: Rewrite controller tests around snapshots/batches**

Preserve existing coverage for initial state, rebind, effective timing, coalescing, remove-before-upsert, overflow, structural/range changes, running cadence, stopped seek, rejected seek, subscriptions, and disposal. Change protocol assertions to typed boundaries:

- same-frame multi-object commit emits one batch/revision;
- removals and upserts occupy designated arrays;
- captured objects/structure share no editor references;
- rejected batch advances neither revision and is followed by one authoritative snapshot;
- source bookkeeping commits only after acceptance;
- source IDs remain stable across updates/snapshots, removed references are released, and IDs are never reused.

- [ ] **Step 2: Run controller tests and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneInlineChartPreviewController"
```

- [ ] **Step 3: Emit one typed value per frame**

Clone all pending source state before clearing pending collections. Compute one candidate revision, build one `ChartPreviewBatch`, call content synchronously, and advance revision/bookkeeping only on `true`. Empty frames allocate no revision. Cadence-only transport is an empty-object batch.

On open, rebind, overflow, explicit rejection, or content recreation, build one authoritative snapshot with detached structure and all top-level objects.

- [ ] **Step 4: Remove serializer-shaped preview APIs**

Use `GarbusChartCloner` once at the producer boundary. Remove `EditorChart.CreateSerializableChart()` and preview-only serializer structural/decode methods. Keep serializer methods used by undo, clipboard, or persisted chart formats.

- [ ] **Step 5: Wire rejection and fatal failure**

Expected validation rejection causes an immediate authoritative replacement with no partial display. Clone/apply exceptions use the existing panel failure callback to close Mini, disable its checkbox, and release subscriptions. Prevent retry loops after a replacement failure.

- [ ] **Step 6: Split the controller fixture without behavior changes**

Use a partial class:

- primary: setup, full state, rebind, structural state;
- `.Objects.cs`: IDs, coalescing, object order, overflow, rejection/resync;
- `.Transport.cs`: discrete/smooth seek, running cadence, rejected source seek.

Replace private controller revision/object-map reflection with internal read-only diagnostics or captured typed values. Keep only the shared framework clock reflection required to force an otherwise unreachable rejected seek.

- [ ] **Step 7: Verify controller and editor lifecycle**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneInlineChartPreviewController|FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneTestMode"
rg -n "CreateSerializableChart|DecodeHitObject|EncodeStructural|ChartPreviewMessage|ChartPreviewModel" Garbus.Game Garbus.Game.Tests
```

Expected: focused tests pass; removed APIs/types have no references.

- [ ] **Step 8: Commit**

```bash
git add -A Garbus.Game Garbus.Game.Tests
git commit -m "refactor: synchronize mini preview with typed batches"
```

---

### Task 8: Reorganize Preview Tests And Remove Reflection Coupling

**Files:**
- Split: `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.cs`
- Create: `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.Results.cs`
- Create: `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.Visuals.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneBottomBar.cs`
- Create: `Garbus.Game.Tests/Editor/TestSceneMiniPreviewPanel.cs`
- Modify as partial: `Garbus.Game.Tests/Editor/TestSceneEditorShell.cs`
- Create: `Garbus.Game.Tests/Editor/TestSceneEditorShell.MiniPreview.cs`
- Modify: `Garbus.Game.Tests/Gameplay/TestSceneGameplay.cs`
- Modify production only for narrow diagnostics: preview/controller/panel files.

**Constraint:** This task changes organization and test seams, not product behavior.

- [ ] **Step 1: Split content tests by concern**

Keep setup/shared chart helpers in the primary partial file. Move warning, chord, visual upsert, pending refresh, and spawn behavior to `.Visuals.cs`. Move exact result, rewind, nested result tree, edited times, and lifetime behavior to `.Results.cs`.

- [ ] **Step 2: Move Mini panel tests out of BottomBar**

Move scaling, border, workspace dragging, persistence, warning geometry, and input ownership into `TestSceneMiniPreviewPanel`. Leave transport controls and BottomBar's own fixture contract in `TestSceneBottomBar`.

- [ ] **Step 3: Move Mini shell tests into a partial file**

Keep shared editor setup and generic menu helpers in the primary file. Move checkbox, Compose visibility, Test suspension, reopen/resync, failure, and disposal cases to `.MiniPreview.cs`. Rename stale “preview modes” tests to describe the direct Mini checkbox.

- [ ] **Step 4: Replace project-owned private reflection**

Prefer observable state. Where asynchronous generation or cadence cannot be asserted externally, add narrow internal read-only diagnostics such as current revision, tracked object count, pending generation count, and timeline visits. Replace direct mutation of timestamps with an injected timestamp provider.

Retain/centralize framework reflection only for:

- explicit leak regressions through `Drawable.IsDisposed` or `OnDispose`;
- a focused z-order contract if no robust render assertion exists;
- forcing the framework clock's rejected-seek path.

- [ ] **Step 5: Verify test movement did not lose cases**

Compare test names before/after and run all affected fixtures:

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~Preview|FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneMiniPreviewPanel|FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneTestMode|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~Chord|FullyQualifiedName~Warning"
```

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game Garbus.Game.Tests
git commit -m "test: organize mini preview coverage"
```

---

### Task 9: Consolidate Durable Documentation

**Files:**
- Canonical design: `docs/superpowers/specs/2026-07-23-mini-preview-design.md`
- Implementation record: `docs/superpowers/plans/2026-07-23-mini-preview.md`
- Repository guidance: `CLAUDE.md`
- Architectural rationale: this remediation design and plan.

- [x] **Step 1: Write one present-state Mini design**

Document only current user behavior, layout/drag persistence, typed controller/content ownership, gameplay policy, atomic state, result timeline, lifecycle/error handling, and verification expectations. Mark it implemented. Remove discarded External, inspector placement, old mode menu, and session history.

- [x] **Step 2: Write one final implementation record**

Record the final architecture and durable build/test commands without delivery-specific or machine-specific
instructions.

- [x] **Step 3: Add concise repository guidance**

In `CLAUDE.md`, add one present-state paragraph describing the checked-by-default Compose Mini panel, its persisted position, authoritative reopen/Test suspension, and typed in-process controller/content pipeline.

- [x] **Step 4: Delete superseded documents and scan**

Superseded placement, drag, checkbox, and process-era documents are removed. Surviving Mini documents
contain no session-local operating instructions; process-shaped abstractions remain only as architectural
rationale for their absence.

- [ ] **Step 5: Commit**

```bash
git add -A docs/superpowers CLAUDE.md
git commit -m "docs: consolidate mini preview documentation"
```

---

### Task 10: Final Verification And Independent Review

**Files:** all changed files in `origin/master...HEAD`.

- [ ] **Step 1: Run source gates**

```bash
git diff --check origin/master...HEAD
rg -n "Garbus.Game.Edit.Preview|ChartPreviewContext|ChartPreviewMessage|ChartPreviewModel|IsInPreview" Garbus.Game/Gameplay Garbus.Game/Objects Garbus.Game/UI
rg -n "SelectMany.*Nested|OrderBy.*Result|RawTimeChanged" Garbus.Game
```

Expected: whitespace clean; no editor dependency in shared gameplay/UI; no obsolete protocol/model; no per-frame result ordering pipeline.

- [ ] **Step 2: Run focused affected suites**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~Preview|FullyQualifiedName~Mini|FullyQualifiedName~TestMode|FullyQualifiedName~Gameplay|FullyQualifiedName~Chord|FullyQualifiedName~Warning|FullyQualifiedName~Result|FullyQualifiedName~Serializer"
```

- [ ] **Step 3: Run a clean no-incremental build**

```bash
dotnet build Garbus.Desktop.slnf --no-restore --configuration Debug --no-incremental --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

Record exact errors and warnings. Zero errors is required.

- [ ] **Step 4: Run the complete unfiltered suite**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug --disable-build-servers -m:1 -p:UseSharedCompilation=false
```

Zero failures is required. Do not infer success from filtered runs.

- [ ] **Step 5: Request independent whole-branch review**

Review `origin/master...HEAD` for correctness, ordinary-gameplay regression, state atomicity, revision ownership, clone isolation, result chronology, disposal/generation safety, idle complexity, lifecycle/failure loops, comments, test quality, and documentation durability. Resolve every Critical, Important, and Minor finding and repeat affected verification.

- [ ] **Step 6: Verify worktree and branch state**

```bash
git status --short --branch
git log --oneline --decorate origin/master..HEAD
```

Expected: clean worktree with the remediation commits and no untracked build/test artifacts.
