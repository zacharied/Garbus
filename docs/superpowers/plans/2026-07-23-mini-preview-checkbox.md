# Mini Preview Checkbox Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Hide/Mini radio submenu with one direct `View > Mini Preview` checkbox.

**Architecture:** Replace `Bindable<EditorPreviewMode>` with `BindableBool MiniPreviewEnabled`. Bind a normal `ToggleMenuItem` directly to that boolean, retain the existing panel lifecycle, and delete the now-unused enum and radio-menu rendering infrastructure.

**Tech Stack:** C# 12, .NET 8, osu-framework bindables/menu/input, NUnit visual test scenes, GitHub CLI, Pebble.

## Global Constraints

- The checkbox label is exactly `Mini Preview` and appears directly under View.
- The checkbox is checked by default.
- Unchecking closes Mini; checking reopens it with an authoritative current state.
- Mini remains visible only while enabled and Compose is active.
- Suspension temporarily disables Mini and restores the prior boolean on return.
- Mini failure unchecks the checkbox.
- Preserve Mini rendering, 190x190 size, drag bounds, offsets, input ownership, live edits, clock, results, rewind, Test return behavior, and disposal.
- Preserve ordinary gameplay and all unrelated View menu toggles.

---

### Task 1: Replace Preview Mode With A Checkbox

**Files:**
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Modify: `Garbus.Game/Edit/GarbusMenu.cs`
- Delete: `Garbus.Game/Edit/Preview/EditorPreviewMode.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneEditorShell.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneBottomBar.cs`
- Modify: `Garbus.Game.Tests/Editor/TestSceneTestMode.cs`
- Modify: `docs/superpowers/specs/2026-07-23-mini-only-preview-design.md`
- Modify: `docs/superpowers/plans/2026-07-23-mini-only-preview.md`

**Interfaces:**
- Produces: `internal BindableBool MiniPreviewEnabled { get; } = new(true);`
- Produces: direct `new ToggleMenuItem("Mini Preview", MiniPreviewEnabled)` under View.
- Removes: `EditorPreviewMode`, `IRadioMenuItem`, `RadioMenuItem<T>`, and radio drawable/content code.

- [ ] **Step 1: Rewrite the real menu test for a direct checkbox**

Update `TestPreviewModesViaViewMenu` to open View and assert:

```csharp
AddAssert("mini preview checkbox exists", () => menuItem("Mini Preview"), () => Is.TypeOf<ToggleMenuItem>());
AddAssert("mini preview checked by default", () => toggleItem("Mini Preview").State.Value);
AddAssert("no preview submenu", () => editor.ChildrenOfType<Menu.DrawableMenuItem>()
    .All(i => i.Item.Text.Value.ToString() is not "Preview" and not "Hide" and not "Mini"));
```

Click `Mini Preview` directly to hide, edit while closed, click again, and assert the checked state and authoritative resync.

- [ ] **Step 2: Run RED**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestPreviewModesViaViewMenu"
```

Expected: fail because `Mini Preview` does not exist and Preview remains a submenu.

- [ ] **Step 3: Replace enum state with a boolean**

In `GarbusEditor` use:

```csharp
internal BindableBool MiniPreviewEnabled { get; } = new(true);
private bool? miniPreviewEnabledBeforeSuspension;
```

Bind `MiniPreviewEnabled` in `LoadComplete`. Visibility remains:

```csharp
private void updateInlinePreviewVisibility()
    => inlinePreviewPanel.SetVisible(MiniPreviewEnabled.Value && Tab.Value == EditorTab.Compose);
```

Suspension stores the boolean once, sets it false, then restores it on resume. Exit and Mini failure set it false.

- [ ] **Step 4: Replace the menu and delete radio infrastructure**

Add this directly to the View items list:

```csharp
new ToggleMenuItem("Mini Preview", MiniPreviewEnabled),
```

Delete `EditorPreviewMode.cs`, `IRadioMenuItem`, `RadioMenuItem<T>`, `DrawableRadioMenuItem`, and `RadioContent`. Keep checkbox rendering and its keep-menu-open behavior unchanged.

- [ ] **Step 5: Update lifecycle tests and documentation**

Replace test assignments/assertions:

```csharp
editor.MiniPreviewEnabled.Value = false;
editor.MiniPreviewEnabled.Value = true;
```

Retain all suspension, failure, Hide/reopen, panel visibility, live-state, and Test return assertions. Rename descriptions from Hide/Mini mode to checkbox enabled/disabled where useful. Update current Mini-only docs to describe `View > Mini Preview`, not Hide/Mini radio choices.

- [ ] **Step 6: Run focused tests and commit**

```bash
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneTestMode"
git add -A
git commit -m "refactor: toggle mini preview with checkbox"
```

---

### Task 2: Verify And Republish

**Files:**
- Update: PR #4 title/body/comment.
- Deploy: Pebble.

- [ ] **Step 1: Run build and full tests**

```bash
dotnet build Garbus.Desktop.slnf --no-restore --configuration Debug --no-incremental
dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --no-build --configuration Debug
```

- [ ] **Step 2: Review the complete checkbox diff**

Check menu placement/state, suspension restoration, failure fallback, removal of all enum/radio references, documentation, and unchanged Mini behavior. Resolve all Critical and Important findings.

- [ ] **Step 3: Re-squash and force-push safely**

Preserve a recovery ref, create one commit with the reviewed tree directly on current `origin/master`, verify tree identity, and force-push with an explicit lease against the current remote head. Keep subject `feat: add mini chart preview`.

- [ ] **Step 4: Refresh PR and Pebble**

Update PR #4 to describe `View > Mini Preview`, post fresh verification, run `tools/pebble run`, and verify status/logs/source identity.
