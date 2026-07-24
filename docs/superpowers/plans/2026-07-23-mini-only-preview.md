# Mini-Only Chart Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove External preview and its process/IPC architecture while preserving Mini exactly, then review and tighten the complete Mini pull request.

**Architecture:** Delete External as one vertical slice from Desktop entrypoint through editor lifecycle, transport, child game, tests, and documentation. Keep Mini's existing in-process controller/model/content pipeline, but remove External-shaped APIs that Mini does not consume and apply concrete whole-diff review findings without redesigning Mini.

**Tech Stack:** C# 12, .NET 8, osu-framework drawables/input/DI, NUnit visual test scenes, GitHub CLI, Pebble Windows playtest tooling.

## Global Constraints

- Preserve Mini's fixed 190x190 appearance, rendering, border, rounded corners, silence, and input behavior.
- Preserve full-Compose-workspace dragging, bottom/right offset persistence, all-edge clamping, and menu/transport boundaries.
- Preserve live edits, chart switching, timing/design changes, scroll speed, clock transport, rewind/replay, exact-time results, and nested hold/slider results.
- Preserve Compose-only visibility, `View > Mini Preview` checkbox behavior, Test suspension/restoration, local failure handling, and editor disposal.
- Remove External completely; do not leave disabled UI, compatibility constructors, launcher abstractions, command-line aliases, process code, pipe code, or stale documentation.
- Do not change song-select audio preview or chart `PreviewTime` behavior.
- Do not redesign Mini unless a concrete review finding requires a local fix.
- Ordinary gameplay, editor input, chart serialization, judgement, audio, ordering, expiration, and visuals remain unchanged.

## File Structure

- `Garbus.Desktop/Program.cs`: return to the normal single-game desktop entrypoint.
- `Garbus.Game/GarbusGameBase.cs` and `Garbus.Game/GarbusGame.cs`: remove preview-launcher dependency injection.
- `Garbus.Game/Edit/Screens/GarbusEditor.cs`: own the Mini visibility boolean and Mini lifecycle.
- `Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs`: retain Mini UI/lifetime unchanged except naming if review requires it.
- `Garbus.Game/Edit/Preview/InlineChartPreviewController.cs`: retain Mini state production; own its pending-delta bound.
- `Garbus.Game/Edit/Preview/ChartPreviewContent.cs`, `ChartPreviewModel.cs`, `ChartPreviewClock.cs`, and `ChartPreviewContext.cs`: retain the Mini renderer/model and narrow access where possible.
- `Garbus.Game/Edit/Preview/ChartPreviewMessage.cs`: retain only state records consumed in-process by Mini; remove wire serialization and process-control messages.
- External source files listed in Task 1: delete.
- Mini test files listed in Task 1: retain or migrate shared behavioral coverage.
- External test files listed in Task 1: delete.
- `docs/superpowers/`: retain Mini-specific design/plan files and remove External-focused planning history.

---

### Task 1: Remove The External Preview Vertical Slice

**Files:**
- Modify: `Garbus.Desktop/Program.cs`
- Delete: `Garbus.Desktop/DesktopChartPreviewProcessLauncher.cs`
- Delete: `Garbus.Desktop/AssemblyInfo.cs`
- Modify: `Garbus.Game/GarbusGameBase.cs`
- Modify: `Garbus.Game/GarbusGame.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Delete: `Garbus.Game/Edit/Preview/EditorPreviewMode.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewMessage.cs`
- Modify: `Garbus.Game/Edit/Preview/InlineChartPreviewController.cs`
- Delete: `Garbus.Game/Edit/Preview/IChartPreviewProcessLauncher.cs`
- Delete: `Garbus.Game/Edit/Preview/ChartPreviewController.cs`
- Delete: `Garbus.Game/Edit/Preview/ChartPreviewPipe.cs`
- Delete: `Garbus.Game/Edit/Preview/ChartPreviewMessageQueue.cs`
- Delete: `Garbus.Game/Edit/Preview/GarbusPreviewGame.cs`
- Delete: `Garbus.Game/Edit/Preview/ChartPreviewView.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneEditorShell.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneTestMode.cs`
- Rename: `Garbus.Game.Tests/Editor/TestSceneChartPreviewController.cs` to `Garbus.Game.Tests/Editor/TestSceneInlineChartPreviewController.cs`
- Rename: `Garbus.Game.Tests/Editor/TestSceneChartPreviewView.cs` to `Garbus.Game.Tests/Editor/TestSceneChartPreviewContent.cs`
- Delete: `Garbus.Game.Tests/Editor/TestDesktopChartPreviewProcessLauncher.cs`
- Delete: `Garbus.Game.Tests/Editor/TestChartPreviewPipe.cs`
- Delete: `Garbus.Game.Tests/Editor/TestChartPreviewMessageQueue.cs`
- Delete: `Garbus.Game.Tests/Editor/TestSceneGarbusPreviewGame.cs`

**Interfaces:**
- Produces: `GarbusGame()` with no launcher parameter.
- Produces: `GarbusGameBase(Vector2? targetDrawSize = null)` with no launcher parameter.
- Produces: `MiniPreviewEnabled`, checked by default.
- Produces: a direct `View > Mini Preview` checkbox.
- Preserves: `ChartPreviewFullState`, `ChartPreviewObjectUpsert`, `ChartPreviewObjectRemove`, `ChartPreviewStructuralState`, `ChartPreviewTransport`, and `ChartPreviewScrollSpeed` for Mini's in-process pipeline.
- Removes: every process, pipe, queue, child-game, connection, shutdown, ready, closing, and External-mode interface.

- [ ] **Step 1: Change the menu test to specify the Mini checkbox**

Open View in `TestPreviewModesViaViewMenu` and assert:

```csharp
AddAssert("mini preview checkbox exists", () => menuItem("Mini Preview"), () => Is.TypeOf<ToggleMenuItem>());
AddAssert("mini preview checked by default", () => toggleItem("Mini Preview").State.Value);
AddAssert("no preview submenu", () => editor.ChildrenOfType<Menu.DrawableMenuItem>()
    .All(i => i.Item.Text.Value.ToString() is not "Preview" and not "Hide" and not "Mini"));
```

Click `Mini Preview` directly to close Mini, edit while closed, then click it again and assert an authoritative current state.

- [ ] **Step 2: Run the changed menu test and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestPreviewModesViaViewMenu"
```

Expected: failure because the old menu has a Preview submenu and no direct `Mini Preview` checkbox.

- [ ] **Step 3: Simplify the Desktop and game constructors**

Make `Program.Main` unconditionally create the normal game:

```csharp
using (GameHost host = Host.GetSuitableDesktopHost(@"Garbus"))
using (osu.Framework.Game game = new GarbusGame())
    host.Run(game);
```

Remove preview imports, argument parsing, the launcher field, launcher constructor parameters, and launcher DI caching. `GarbusGame` should use its implicit parameterless constructor or an explicit parameterless constructor only if required by the existing class pattern.

- [ ] **Step 4: Replace editor preview mode with a boolean**

Delete `PreviewControllerForTests`, `previewController`, launcher injection, External construction, `AddInternal(previewController)`, process failure handling, process disposal, dead-session polling, and the External menu item.

Use a checked-by-default boolean and the single Mini visibility rule:

```csharp
internal BindableBool MiniPreviewEnabled { get; } = new(true);

private void updateInlinePreviewVisibility()
    => inlinePreviewPanel.SetVisible(MiniPreviewEnabled.Value && Tab.Value == EditorTab.Compose);
```

Keep suspension/restoration with `bool? miniPreviewEnabledBeforeSuspension` so the user setting is
captured once, temporarily disabled, and restored. Mini failure and editor exit disable the boolean.

- [ ] **Step 5: Delete External transport and retain only Mini state records**

Delete the process interface, launcher, external controller, pipe, queue, child game, and external view files. Remove `System.Text.Json.Serialization`, `JsonPolymorphic`, `JsonDerivedType`, `ChartPreviewReady`, `ChartPreviewClosing`, `ChartPreviewShutdown`, and `ChartPreviewResyncRequest` from `ChartPreviewMessage.cs`.

Move the one Mini-consumed queue constant into `InlineChartPreviewController`:

```csharp
private const int max_pending_object_deltas = 4096;
```

Use it in `enforcePendingObjectBound()` and preserve the current full-state fallback behavior.

- [ ] **Step 6: Remove External-only tests and migrate shared behavior**

Delete the four External-only test files listed above. In `TestSceneEditorShell`, delete External launch/failure/tab/exit/disposal cases and all fake process/dependency infrastructure. Keep Mini checkbox/resync, shared song timing, selected-chart structure, suspension, failure, and disposal coverage.

In `TestSceneTestMode`, delete `TestExternalPreviewRestartsOnceAtReturnedPlayTime` and External helper setup. Keep and strengthen Mini suspension/return assertions so the panel reopens at the returned play time with current chart state.

Rename the renderer fixture to `TestSceneChartPreviewContent`, instantiate `ChartPreviewContent` directly, and preserve all rendering/result/rewind tests.

Rename the controller fixture to `TestSceneInlineChartPreviewController`. Retain Mini's existing rejected-seek test and migrate these producer behaviors from the old external harness to the inline harness:

```text
effective shared timing
transport captured after pending chart changes
chart rebind sends an authoritative full state
same-frame upserts coalesce
pending-delta overflow falls back to full state
remove before unsent upsert does not emit a stale object
structural state and scroll speed propagate
running transport cadence remains bounded
resync request produces a full state
```

Do not retain assertions about launch, readiness, connection timeout, process exit, shutdown, kill, pipe framing, or peer knowledge.

- [ ] **Step 7: Run focused Mini/editor tests and observe GREEN**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneTestMode|FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneInlineChartPreviewController|FullyQualifiedName~TestSceneChartPreviewContent|FullyQualifiedName~TestChartPreviewModel|FullyQualifiedName~TestChartPreviewClock"
```

Expected: all selected tests pass with no External fixture present.

- [ ] **Step 8: Prove no External implementation symbols remain**

```bash
rg -n "External|IChartPreviewProcess|DesktopChartPreviewProcessLauncher|ChartPreviewController|ChartPreviewPipe|ChartPreviewMessageQueue|GarbusPreviewGame|ChartPreviewReady|ChartPreviewClosing|ChartPreviewShutdown|ChartPreviewResyncRequest|--chart-preview" Garbus.Desktop Garbus.Game Garbus.Game.Tests
```

Expected: no matches related to chart preview. Unrelated words such as a timing test's “external BPM change” are allowed only after manual inspection.

- [ ] **Step 9: Commit the vertical-slice removal**

```bash
git add -A Garbus.Desktop Garbus.Game Garbus.Game.Tests
git commit -m "refactor: remove external chart preview"
```

---

### Task 2: Prune Documentation And Review The Remaining Mini Diff

**Files:**
- Modify: `Garbus.Game/Edit/Preview/InlineChartPreviewController.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewContent.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewModel.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewClock.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewContext.cs`
- Modify: `Garbus.Game/Edit/Preview/ChartPreviewMessage.cs`
- Modify as findings require: changed Mini/gameplay integration files under `Garbus.Game/Gameplay/`, `Garbus.Game/Objects/`, and `Garbus.Game/UI/`
- Delete: External-focused plans/specifications under `docs/superpowers/`
- Modify: surviving Mini plans/specifications under `docs/superpowers/`
- Test: all retained Mini and gameplay tests

**Interfaces:**
- Consumes: the Mini-only production/test surface from Task 1.
- Produces: a complete `master...HEAD` diff with no External architecture, stale documentation, unnecessary public preview API, or unexplained non-obvious Mini policy.
- Preserves: Mini behavior and all ordinary gameplay behavior.

- [ ] **Step 1: Prune External planning history**

Delete External-focused design/plan files, including the original chart-popout process design and the process lifecycle/review-fix plans. Retain the Mini layout/inspector/workspace documents and the approved Mini-only design. Search surviving documentation and remove claims that External exists:

```bash
rg -n "External|popout|child process|named pipe|IPC|--chart-preview" docs/superpowers
```

Expected: no product claim that External remains. Mentions inside this removal plan/spec are allowed.

- [ ] **Step 2: Narrow remaining preview access**

Change remaining preview implementation types from `public` to `internal` where they are consumed only by `Garbus.Game` and its friend test assembly. This includes `ChartPreviewContent`, `ChartPreviewModel`, `ChartPreviewClock`, `ChartPreviewContext`, and the retained state records.

- [ ] **Step 3: Add rationale only at non-obvious Mini invariants**

Add short comments that explain why, not what:

```csharp
// Bound edit bursts so a hidden frame cannot retain an unbounded set of object references.
// A full state is authoritative and cheaper than replaying an oversized delta batch.
```

Place this above the pending-delta fallback in `InlineChartPreviewController`.

Document strict revision handling in `ChartPreviewContent`/`ChartPreviewModel` as protection against same-frame remove/upsert ordering and authoritative full-state replacement, not as IPC compatibility. Add similarly focused rationale to preview-specific gameplay branches only where the normal gameplay invariant is deliberately bypassed. Do not add comments to obvious assignments, menu construction, or test steps.

- [ ] **Step 4: Review the complete production diff from master**

Review every changed production file in:

```bash
git diff --name-only origin/master...HEAD -- Garbus.Desktop Garbus.Game
```

Check ownership/disposal, state transitions, repeated preview conditionals, ordinary-gameplay fallthrough, rendering policy, result ordering, lifetime/rewind behavior, and test seams. Fix all Critical and Important findings. Fix Minor findings only when local and behavior-preserving. Reject speculative Mini rewrites.

- [ ] **Step 5: Run focused regressions after review fixes**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~Preview|FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneTestMode|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~Chord|FullyQualifiedName~Warning"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit documentation and review fixes**

```bash
git add -A
git commit -m "refactor: tighten mini preview integration"
```

---

### Task 3: Verify, Re-Squash, Update PR, And Redeploy

**Files:**
- Update: PR #4 title/body/comments.
- Deploy: Pebble through `tools/pebble`.

**Interfaces:**
- Consumes: reviewed Mini-only tree from Task 2.
- Produces: one clean feature commit directly on current `origin/master`, a merge-clean PR, and a matching running Pebble build.

- [ ] **Step 1: Run source and build gates**

```bash
git diff --check
dotnet build Garbus.Desktop.slnf --no-restore --configuration Debug --no-incremental
```

Expected: build succeeds with zero errors; report all warnings exactly.

- [ ] **Step 2: Run focused and unfiltered suites**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug --filter "FullyQualifiedName~Preview|FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneTestMode|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~Chord|FullyQualifiedName~Warning"
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug
```

Expected: zero failed tests in both runs, with no filters on the full suite.

- [ ] **Step 3: Request final whole-branch review**

Review `origin/master...HEAD` for behavioral regressions, remaining External code/docs, Mini lifecycle and input behavior, process-era abstractions, condition-heavy control flow, public API scope, missing rationale, gameplay changes, and missing tests. Resolve all Critical and Important findings before continuing.

- [ ] **Step 4: Rewrite the reviewed tree to one commit on current master**

Fetch first and stop if `origin/master` advanced; merge/rebase and reverify before rewriting. Preserve a local recovery ref for the pre-rewrite head. Create one commit with the exact reviewed tree and `origin/master` as its sole parent, update the feature branch ref, and verify old/new tree hashes are identical.

Use the final subject:

```text
feat: add mini chart preview
```

- [ ] **Step 5: Force-push safely and update PR #4**

Use an explicit `--force-with-lease` against the known remote head. Update the PR title/body to describe Mini only, remove External/process/IPC claims, and include fresh build/test/review evidence. Verify GitHub reports one commit and a clean merge state.

- [ ] **Step 6: Rebuild and relaunch Pebble**

```bash
tools/pebble run
tools/pebble status
tools/pebble logs
```

Expected: Windows build succeeds, Garbus runs in interactive session 1, Direct3D 11 initializes, `MainMenuScreen` is entered, and no fatal error appears.

- [ ] **Step 7: Verify deployed source identity**

Compare SHA-256 hashes for the changed Mini/editor/gameplay source files between the local reviewed tree and Pebble. Post deployment evidence to PR #4 and report the remaining GitHub review decision separately from merge cleanliness.
