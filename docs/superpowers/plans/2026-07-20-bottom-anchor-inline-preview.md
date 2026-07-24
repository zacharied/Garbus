# Bottom-Anchored Inline Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Anchor the existing 190x190 inline preview to the bottom-right of the Compose inspector column, 5px above and left of the Compose boundary.

**Architecture:** The full-height right-toolbox host becomes a protected composer surface. Inspector fields remain in the existing top-aligned flow, while `GarbusHitObjectComposer` attaches the preview directly to the host and the panel uses host-local bottom-right anchoring.

**Tech Stack:** C# 12, .NET 8, osu-framework drawables, NUnit visual test scenes, Git, GitHub CLI, Pebble Windows playtest tooling.

## Global Constraints

- Keep the preview 190x190 inside the fixed 200px Compose inspector column.
- Inset the preview exactly 5px from the inspector column's right and bottom edges.
- Keep inspector fields top-aligned and do not use a flexible flow spacer.
- Preserve inline toggle state, hidden-tab unsubscribe/resync behavior, rendering, and failure handling.
- Do not change preview synchronization or menu behavior.
- Use `/tmp/garbus-dotnet/dotnet` locally and `TMPDIR=/tmp` for preview tests.
- Use `TMPDIR=/home/chis/.cache` for `tools/pebble` commands.
- Update draft PR #4 after pushing the verified correction.

---

### Task 1: Anchor The Preview To The Inspector Column Bottom

**Files:**
- Modify: `Garbus.Game.Tests/Editor/TestSceneBottomBar.cs`
- Modify: `Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs`
- Modify: `Garbus.Game/Edit/Compose/HitObjectComposer.cs`
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs`

**Interfaces:**
- Consumes: the existing optional `InlineChartPreviewPanel` passed into `GarbusHitObjectComposer`.
- Produces: protected `Container RightToolboxHost { get; private set; }` for ruleset-specific anchored toolbox content.
- Preserves: the existing `RightToolbox` top flow and all inline controller/editor interfaces.

- [ ] **Step 1: Replace the flow-parent assertion with bottom-right geometry assertions**

In `TestSceneBottomBar`, remove the now-unused `Garbus.Game.Edit.Compose` import and `hasAncestor<T>()` helper. Replace the `inline preview belongs to inspector group` assertion with:

```csharp
AddAssert("inline preview attached to right toolbox host", () =>
    editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Parent?.Name,
    () => Is.EqualTo("Right toolbox"));
```

After the existing width and height assertions, add:

```csharp
AddAssert("inline preview inset from compose bottom right", () =>
{
    var panel = editor.ChildrenOfType<InlineChartPreviewPanel>().Single();
    var composer = editor.ChildrenOfType<GarbusHitObjectComposer>().Single();
    Vector2 panelBottomRight = panel.ToScreenSpace(new Vector2(panel.DrawWidth, panel.DrawHeight));
    Vector2 composerBottomRight = composer.ToScreenSpace(new Vector2(composer.DrawWidth, composer.DrawHeight));

    return System.Math.Abs(composerBottomRight.X - panelBottomRight.X - 5) < 0.01
           && System.Math.Abs(composerBottomRight.Y - panelBottomRight.Y - 5) < 0.01;
});
```

Keep the live update, scrub, tab-hide, authoritative resync, and disable assertions unchanged.

- [ ] **Step 2: Run the focused test and observe behavioral RED**

```bash
TMPDIR=/tmp /tmp/garbus-dotnet/dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj \
  --no-restore --configuration Debug \
  --filter "FullyQualifiedName~TestSceneBottomBar.TestInlinePreviewToggleAndLiveUpdate" \
  --logger "console;verbosity=minimal"
```

Expected: FAIL because the panel is currently inside the inspector content flow rather than directly parented and bottom-anchored to `Right toolbox`.

- [ ] **Step 3: Make the right toolbox's full-height host available to subclasses**

In `HitObjectComposer`, add the protected host beside `RightToolbox`:

```csharp
/// <summary>The full-height host for the right toolbox background, top flow, and anchored content.</summary>
protected Container RightToolboxHost { get; private set; } = null!;
```

Assign the existing named right-toolbox container to it without changing its layout or children:

```csharp
RightToolboxHost = new Container
{
    Name = "Right toolbox",
    Anchor = Anchor.TopRight,
    Origin = Anchor.TopRight,
    RelativeSizeAxes = Axes.Y,
    Width = TOOLBOX_WIDTH_RIGHT,
    Children = new Drawable[]
    {
        new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = new Colour4(20, 20, 26, 255),
        },
        RightToolbox = new ExpandingToolboxContainer(TOOLBOX_WIDTH_RIGHT),
    },
},
```

- [ ] **Step 4: Give the panel host-local bottom-right geometry**

In `InlineChartPreviewPanel`, restore `using osuTK;` and replace relative-width sizing with:

```csharp
Anchor = Anchor.BottomRight;
Origin = Anchor.BottomRight;
Position = new Vector2(-5);
Size = new Vector2(SIZE);
```

Keep masking, border, alpha, lazy creation, and controller lifecycle unchanged.

- [ ] **Step 5: Separate top inspector content from the anchored preview**

In `GarbusHitObjectComposer`, remove the `osu.Framework.Graphics.Containers` import and replace the combined inspector flow with:

```csharp
RightToolbox.Add(new EditorToolboxGroup("inspector")
{
    Child = new Inspector(),
});

if (inlinePreviewPanel != null)
    RightToolboxHost.Add(inlinePreviewPanel);
```

This leaves inspector fields in the top flow and makes the preview a direct, anchored child of the full-height host.

- [ ] **Step 6: Run focused GREEN and regression tests**

Run the Step 2 command again.

Expected: 1 passed, 0 failed, with the 5px right/bottom geometry assertion passing.

Then run:

```bash
TMPDIR=/tmp /tmp/garbus-dotnet/dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj \
  --no-restore --configuration Debug \
  --filter "FullyQualifiedName~TestSceneBottomBar|FullyQualifiedName~TestSceneChartPreviewView|FullyQualifiedName~TestSceneEditorShell|FullyQualifiedName~TestSceneTestMode|FullyQualifiedName~TestSceneChartPreviewController" \
  --logger "console;verbosity=minimal"
```

Expected: 82 passed, 0 failed, 0 skipped.

- [ ] **Step 7: Review and commit**

```bash
git -c core.whitespace=cr-at-eol diff --check
git diff --stat
git diff -- Garbus.Game.Tests/Editor/TestSceneBottomBar.cs Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs Garbus.Game/Edit/Compose/HitObjectComposer.cs Garbus.Game/Edit/GarbusHitObjectComposer.cs
git status --short
```

Confirm only the four planned files changed, then commit:

```bash
git add Garbus.Game.Tests/Editor/TestSceneBottomBar.cs \
  Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs \
  Garbus.Game/Edit/Compose/HitObjectComposer.cs \
  Garbus.Game/Edit/GarbusHitObjectComposer.cs
git commit -m "fix: anchor inline preview to inspector bottom"
```

### Task 2: Verify, Hydrate PR, And Relaunch Pebble

**Files:**
- Verify: `Garbus.Desktop.slnf`
- Verify: `Garbus.Game.Tests/Garbus.Game.Tests.csproj`
- Update: draft PR #4
- Deploy: `tools/pebble`

**Interfaces:**
- Consumes: the reviewed bottom-anchor implementation from Task 1.
- Produces: a pushed branch, updated PR description, and Pebble process running hash-matched source.

- [ ] **Step 1: Build and run the Linux-compatible full suite**

```bash
/tmp/garbus-dotnet/dotnet build Garbus.Desktop.slnf \
  --no-restore --configuration Debug --no-incremental --verbosity minimal
TMPDIR=/tmp /tmp/garbus-dotnet/dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj \
  --no-restore --configuration Debug \
  --filter "FullyQualifiedName!~TestNoRealWasapiOutputStarted" \
  --logger "console;verbosity=minimal"
```

Expected: build succeeds with 0 errors and the three known warnings; 760 tests pass with 0 failures and 0 skips.

- [ ] **Step 2: Push the verified branch**

```bash
git status --short --branch
git push origin feat/chart-popout-preview
```

Expected: clean worktree and successful push of the corrected spec, plan, and implementation.

- [ ] **Step 3: Hydrate draft PR #4**

Update the PR description to state that the inline preview is 190x190, anchored at the bottom-right of the Compose inspector column, remains enabled but unsubscribed outside Compose, and authoritatively resyncs on return. Update verification to the observed focused, build, and full-suite results.

Verify the rendered PR metadata after the update:

```bash
gh pr view 4 --json url,isDraft,title,body
```

Expected: PR #4 remains a draft and its body describes the bottom-anchored inline placement and current verification results.

- [ ] **Step 4: Rebuild and launch Pebble**

```bash
TMPDIR=/home/chis/.cache /home/chis/Garbus/tools/pebble run
TMPDIR=/home/chis/.cache /home/chis/Garbus/tools/pebble status
TMPDIR=/home/chis/.cache /home/chis/Garbus/tools/pebble logs
```

Expected: Windows build succeeds with 0 errors; Garbus runs in interactive session 1; startup log contains no fatal error.

- [ ] **Step 5: Verify source hash**

```bash
sha256sum Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs
ssh -o BatchMode=yes pebble 'powershell.exe -NoProfile -NonInteractive -Command "Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $env:USERPROFILE '\''Desktop\Garbus\Garbus.Game\Edit\Preview\InlineChartPreviewPanel.cs'\'') | Select-Object -ExpandProperty Hash"'
```

Expected: local and Pebble SHA-256 hashes match case-insensitively.
