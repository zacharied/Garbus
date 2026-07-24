# Mini Preview Workspace Drag Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve Mini preview behavior while expanding its draggable bounds to the complete Compose tab workspace.

**Architecture:** Move ownership of `InlineChartPreviewPanel` from the playfield-only overlay in `GarbusHitObjectComposer` to a final transparent overlay in `ComposeTab`. The panel's existing parent-local drag, clamping, persistence, rendering, and input code remains unchanged; only its parent bounds expand.

**Tech Stack:** C# 12, .NET 8, osu-framework drawables and input, NUnit visual test scenes, Pebble Windows playtest tooling.

## Global Constraints

- Mini's rendering, 190x190 size, border, rounded corners, preview controller, and visibility lifecycle remain unchanged.
- Mini may cover the Compose timeline, playfield, left toolbox, or inspector.
- Mini remains fully inside the Compose tab and cannot cover the editor's top menu or bottom transport bar.
- Mini continues consuming left press, drag, wheel, and modified-wheel input only inside its rectangle.
- Uncovered editor controls continue receiving input.
- Existing positive bottom-right offsets remain persisted only at drag end and reclamped after layout or resize.
- Ordinary gameplay behavior remains unchanged.

---

### Task 1: Reparent Mini To The Compose Workspace

**Files:**
- Modify: `Garbus.Game/Edit/Screens/ComposeTab.cs`
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneBottomBar.cs`

**Interfaces:**
- Produces: `ComposeTab` child container named `Mini preview workspace overlay`, sized to the full tab.
- Removes: `InlineChartPreviewPanel` constructor ownership from `GarbusHitObjectComposer`.
- Preserves: `InlineChartPreviewPanel` implementation and persisted offset keys.

- [ ] **Step 1: Rewrite layout assertions before production changes**

Change the existing Mini parent assertions to require:

```csharp
Assert.That(panel.Parent?.Name, Is.EqualTo("Mini preview workspace overlay"));
Assert.That(panel.Parent?.Parent, Is.TypeOf<PopoverContainer>());
```

Replace the no-inspector-overlap assertion with screen-space checks proving the overlay spans the
timeline, playfield, left toolbox, and inspector while remaining inside the Compose tab. Add a drag
step that places Mini over the inspector and asserts actual overlap, then moves it over the timeline
and asserts overlap there.

- [ ] **Step 2: Run the focused layout tests and observe RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestInlinePreviewToggleAndLiveUpdate|FullyQualifiedName~TestMiniPreviewRestoresOffsetsAndClampsToPlayfieldBounds|FullyQualifiedName~TestMiniPreviewOwnsDragAndWheelButOverlayPassesUncoveredInput"
```

Expected: parent and workspace overlap assertions fail because Mini remains under the playfield-only overlay.

- [ ] **Step 3: Add the Compose-level overlay**

In `ComposeTab.load()`, keep the existing `PopoverContainer` as the full-tab root but give it two
children: the existing content container and a final container:

```csharp
new Container
{
    Name = "Mini preview workspace overlay",
    RelativeSizeAxes = Axes.Both,
    Children = inlinePreviewPanel == null ? [] : [inlinePreviewPanel],
}
```

The overlay must be the final child so Mini draws above the timeline and composer. Do not override
input handling on the overlay.

- [ ] **Step 4: Remove composer ownership**

Remove the `InlineChartPreviewPanel` field and constructor parameter from `GarbusHitObjectComposer`,
remove its addition to `PlayfieldOverlayContainer`, and construct `GarbusHitObjectComposer()` without
the panel in `ComposeTab`.

- [ ] **Step 5: Update drag/input tests for full-workspace coordinates**

Rename the clamp test to `TestMiniPreviewRestoresOffsetsAndClampsToWorkspaceBounds`. Preserve its
all-edge, persistence, unresolved-layout, and resize checks against `panel.Parent`. In the input test,
choose uncovered playfield coordinates from the actual playfield rather than the overlay's top-left,
which now lies over the timeline/left toolbox.

Add assertions that dragging over inspector/timeline controls does not activate the covered control,
while the same controls work after Mini moves away.

- [ ] **Step 6: Run editor regressions and commit**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneComposeSelection"
git add Garbus.Game/Edit/Screens/ComposeTab.cs Garbus.Game/Edit/GarbusHitObjectComposer.cs Garbus.Game.Tests/Editor/TestSceneBottomBar.cs docs/superpowers/specs/2026-07-22-mini-preview-workspace-drag-design.md docs/superpowers/plans/2026-07-22-mini-preview-workspace-drag.md
git commit -m "fix: let mini move across compose workspace"
```

---

### Task 2: Verify, Review, Push, And Redeploy

**Files:**
- Update: PR #4 verification evidence after successful gates.
- Deploy: `tools/pebble`.

- [ ] **Step 1: Run diff and build gates**

```bash
git diff --check
dotnet build Garbus.Desktop.slnf --no-restore --configuration Debug --no-incremental
```

- [ ] **Step 2: Run focused and unfiltered suites**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug --filter "FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneComposeSelection|FullyQualifiedName~ChartPreview"
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug
```

- [ ] **Step 3: Request final code review**

Review the complete change for overlay z-order, positional input pass-through, drag coordinates,
persistence, resize clamping, Mini lifecycle preservation, and unrelated gameplay changes.
Resolve all Critical and Important findings.

- [ ] **Step 4: Push and refresh PR #4**

Push `feat/chart-popout-preview` and update verification evidence without changing the approved scroll-speed wording.

- [ ] **Step 5: Rebuild and relaunch Pebble**

```bash
tools/pebble run
tools/pebble status
tools/pebble logs
```

Expected: Windows build has 0 errors, Garbus runs in interactive session 1, startup has no fatal
error, and local/Pebble hashes match for `ComposeTab.cs`, `GarbusHitObjectComposer.cs`, and
`TestSceneBottomBar.cs`.
