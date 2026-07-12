# Editor Angle View Direction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Default the compose editor's unrolled-angle grid to South-at-center, and add a floating toggle that reflects the mapping about the East–West axis to put North-at-center.

**Architecture:** `EditorAngleMapping` (a static utility, the sole angle↔x authority) gets its origin shifted from 135° to 90° and a `Direction` sign that reflects `θ → −θ` at the mapping boundary. A `BindableBool` on the (already `[Cached]`) `GarbusHitObjectComposer` is the source of truth; one subscription mirrors it into `EditorAngleMapping.Direction`. Per-frame x-from-angle drawables update automatically; the one cached consumer (`AngleGrid`) regenerates on change. A floating button toggles the bindable.

**Tech Stack:** C# / osu-framework, NUnit headless test scenes.

## Global Constraints

- Nullability enabled solution-wide; DI/BDL fields use `= null!`.
- Vendored-file attribution headers stay intact; deviate minimally.
- No serialization of the view state — it is a global view preference, reset to South-centered each editor load.
- Terminology: "chart" not "beatmap"; `Garbus*` prefixes.
- **The layout shift is a uniform −45°.** Origin 135→90 and shoulder strips 225/45→180/0 are all −45°. Any test angle chosen for its *grid position* (edge/seam proximity) shifts by −45° (mod 360) to stay faithful; **absolute-angle placements/assertions that round-trip through `ToX`/`ToAngle` (e.g. "place a slam at 315°, assert AngleDeg==315") are origin-independent and MUST NOT change.** Relative `RotationOffset`s never change.
- **Angle convention:** East=0°, North=90°, West=180°, South=270° (CCW positive).
- Build: `dotnet build Garbus.Desktop.slnf`. Test: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter <FullyQualifiedName~...>`.

---

### Task 1: South-centered origin + Direction reflection in `EditorAngleMapping`

Shift the origin to 90° (South at center) and add the `Direction` reflection. Migrate every origin-dependent test so the full suite stays green. Shoulder strips are left at 225/45 in this task (Task 2 moves them); their placement tests use live `ToX` and stay green here.

**Files:**
- Modify: `Garbus.Game/Edit/EditorAngleMapping.cs`
- Test: `Garbus.Game.Tests/Editor/TestEditorAngleMapping.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneEditorPlayfield.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneEditorIntegration.cs` (comment-only)

**Interfaces:**
- Produces: `EditorAngleMapping.ANGLE_ORIGIN == 90`; `static int EditorAngleMapping.Direction { get; set; } = 1` (only `+1`/`−1` valid). `ToGridDegrees`, `ToX`, `ToAngle`, `GhostTwinX`, `SnapX` signatures unchanged but now honor `Direction`.

- [ ] **Step 1: Write the failing reversed-direction unit tests**

Add to `TestEditorAngleMapping.cs` (inside the class), and add a `[TearDown]` so the static can't bleed between tests:

```csharp
[TearDown]
public void ResetDirection() => EditorAngleMapping.Direction = 1;

// --- Direction reflection (reversed view: reflect about the E–W axis) ---

[Test]
public void TestNormalModeSouthAtCentre()
{
    EditorAngleMapping.Direction = 1;
    // South (270°) maps to the grid centre; the centre x-fraction is 0.5.
    Assert.That(EditorAngleMapping.ToX(270), Is.EqualTo(0.5f).Within(1e-5f));
    // West is left of centre, East is right of centre.
    Assert.That(EditorAngleMapping.ToX(180), Is.LessThan(0.5f));
    Assert.That(EditorAngleMapping.ToX(0), Is.GreaterThan(0.5f));
}

[Test]
public void TestReversedModeNorthAtCentreWestStillLeft()
{
    EditorAngleMapping.Direction = -1;
    // North (90°) maps to the grid centre.
    Assert.That(EditorAngleMapping.ToX(90), Is.EqualTo(0.5f).Within(1e-5f));
    // West stays on the left, East stays on the right (reflection about the E–W axis).
    Assert.That(EditorAngleMapping.ToX(180), Is.LessThan(0.5f));
    Assert.That(EditorAngleMapping.ToX(0), Is.GreaterThan(0.5f));
}

[Test]
public void TestReversedModeRoundTrip()
{
    EditorAngleMapping.Direction = -1;
    for (int a = 0; a < 360; a += 15)
    {
        float recovered = EditorAngleMapping.ToAngle(EditorAngleMapping.ToX(a));
        Assert.That(recovered, Is.EqualTo(a).Within(0.01f), $"reversed round-trip failed for {a}");
    }
}
```

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: FAIL — `EditorAngleMapping` has no `Direction` member.

- [ ] **Step 3: Implement origin + Direction in `EditorAngleMapping.cs`**

Change `ANGLE_ORIGIN`:

```csharp
    /// <summary>
    /// The absolute angle at the grid centre column's reference — the origin from which grid-degrees are
    /// measured (grid-degree 0 is the left edge). At 90° the grid centre (grid-degree 180) lands on South
    /// (270°), so the cardinals read North(edge) · West · South(centre) · East · North(edge).
    /// </summary>
    public const int ANGLE_ORIGIN = 90;
```

Add, immediately after `TOTAL_DEGREES`:

```csharp
    /// <summary>
    /// View direction: <c>+1</c> unrolls the circle counter-clockwise with increasing x (South at the
    /// grid centre); <c>−1</c> reflects the mapping about the East–West axis (North at the centre,
    /// clockwise, West still left / East still right). Only +1 and −1 are valid. This is a global view
    /// preference — never serialized.
    /// </summary>
    public static int Direction { get; set; } = 1;

    /// <summary>Applies the current <see cref="Direction"/> reflection (θ → −θ when reversed).</summary>
    private static float applyDirection(float angleDeg) => Direction == 1 ? angleDeg : NormalizeDeg(-angleDeg);

    private static int applyDirection(int angleDeg) => Direction == 1 ? angleDeg : NormalizeDeg(-angleDeg);
```

Update `ToGridDegrees`, `ToAngle`, and `SnapX` to route through `applyDirection` (`ToX` and `GhostTwinX` call `ToGridDegrees`, so they inherit it — leave them as-is):

```csharp
    public static float ToGridDegrees(float angleDeg) => NormalizeDeg(applyDirection(angleDeg) - ANGLE_ORIGIN);
```

```csharp
    public static float ToAngle(float xFrac) => applyDirection(NormalizeDeg(xFrac * TOTAL_DEGREES - GHOST_DEGREES + ANGLE_ORIGIN));
```

In `SnapX`, change only the returned angle (the x-fraction stays in the direction-independent x domain):

```csharp
        return (snappedX, applyDirection(NormalizeDeg((int)snappedUnwrapped)));
```

Also update the class-level `<summary>` (lines ~10–24): replace the "left edge sits on the North/West quadrant boundary (135°) … West, South, East, North" description with the origin-90 layout ("grid centre is South (270°); cardinals read North(edge) · West · South(centre) · East · North(edge); the reflected `Direction == −1` view centres North").

- [ ] **Step 4: Migrate the origin-dependent asserts in `TestEditorAngleMapping.cs`**

Apply these exact edits (all are the −45° / origin-90 restatement):

- Line ~78 `TestToGridDegreesAtOriginIsZero`: `ToGridDegrees(135)` → `ToGridDegrees(90)`. Update the doc comment above it: "135° is 0 grid-degrees" → "90° is 0 grid-degrees"; "ToX(135) == 0" → "ToX(90) == 0"; "ToGridDegrees(135)" → "ToGridDegrees(90)".
- Line ~61 comment: "ANGLE_ORIGIN (135°)" → "ANGLE_ORIGIN (90°)".
- `TestSnapXInLeftGhostBandSnapsToGridOrigin` (lines ~107–116): comment "unwrapped angle = 120° → nearest 45° is 135°" → "unwrapped angle = 75° → nearest 45° is 90°"; "snappedX = (135 - 135 + 30) / 420" → "snappedX = (90 - 90 + 30) / 420"; "angleDeg = 135" → "angleDeg = 90". Assert `Is.EqualTo(135)` → `Is.EqualTo(90)` and its message "nearest 45° snap from 120° unwrapped should be 135°" → "nearest 45° snap from 75° unwrapped should be 90°".
- `TestGhostTwinXNonNullForNearEdgeAngle` (lines ~172–174): comment "ANGLE_ORIGIN (135°)" → "(90°)"; `GhostTwinX(135)` → `GhostTwinX(90)`.
- `TestGhostTwinXForRightEdgeAngle` (lines ~180–182): comment "134°" → "89°", "NormalizeDeg(134 - 135) = 359" → "NormalizeDeg(89 - 90) = 359"; `GhostTwinX(134)` → `GhostTwinX(89)`.

(`TestToXAtOriginIsGhostFraction` uses `ANGLE_ORIGIN` live and `TestToXRoundTrip`/`TestSnapXRoundsToIncrement` are origin-agnostic — leave them.)

- [ ] **Step 5: Migrate the seam-dependent scene fixtures**

`TestSceneComposeSelection.cs`:
- `TestSelectViaGhostTwin` (~L220–221): comment "150° is within GHOST_DEGREES of the left edge (135°)" → "105° is within GHOST_DEGREES of the left edge (90°)"; `placeNoteAt(150)` → `placeNoteAt(105)`.
- `TestIncrementalDragAcrossSeamTracksCursor` (~L324–347): comment "grid 360/0 at absolute 135°" → "grid 360/0 at absolute 90°"; "The note must land on 180" → "The note must land on 135"; `placeNoteAt(90)` → `placeNoteAt(45)`; the assert description "note followed cursor across seam to 180" → "…to 135" and `Is.EqualTo(180)` → `Is.EqualTo(135)`.
- `TestEveryWrapCopyOfANodeIsClickable` fixture (~L727–738): comment "grid 20°, absolute 155°" → "grid 20°, absolute 110°"; `AngleDeg = 155, // grid 20` → `AngleDeg = 110, // grid 20`.

`TestSceneEditorPlayfield.cs` — `TestSliderPolylineVisualRendersWrapCopiesForSeamCrossingSlider`:
- L81 comment "AngleDeg = 135 (the seam)" → "AngleDeg = 85 (near the seam)".
- L87 `AngleDeg = 130, // near left seam` → `AngleDeg = 85, // near left seam`.
- L106 comment "The slider (AngleDeg=130, bodyGridDeg=355, offset=-40)" → "(AngleDeg=85, bodyGridDeg=355, offset=-40)". (Grid range `[315,355]` and `VisibleWrapCopies(315,355) → {0,1}` are unchanged — `85 − 90 = −5 → 355`.)

`TestSceneEditorIntegration.cs` — `makeSeamCrossingSlider` (~L165–166), comment only (the roundtrip asserts are angle-agnostic; the slider need not actually cross the new seam): "seam is the diagonal quadrant boundary (315°) opposite the grid's left edge (135°)" → "left edge is now South-adjacent (grid-degree 0 at 90°)". Leave the head angle as-is.

- [ ] **Step 6: Run the editor test suite to verify green**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~Editor"`
Expected: PASS (all editor scenes + `TestEditorAngleMapping`). If any other test fails on a hardcoded angle, apply the Global-Constraint −45° rule (grid-relative angles shift −45°; absolute round-trip placements unchanged).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/EditorAngleMapping.cs Garbus.Game.Tests/Editor/
git commit -m "feat: centre the compose grid on South + add reflectable view direction"
```

---

### Task 2: Move shoulder strips to the West/East lanes

Shoulder notes travel West (Left) / East (Right); drop the diagonal offset so the editor strips sit on those lanes. West (180°) and East (0°) lie on the E–W reflection axis, so the strips stay put under the reverse toggle.

**Files:**
- Modify: `Garbus.Game/Edit/GarbusEditorPlayfield.cs:28-31`
- Test: `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: `LEFT_SHOULDER_ANGLE_DEG == 180`, `RIGHT_SHOULDER_ANGLE_DEG == 0`. `ShoulderAngle`/`ShoulderXFraction`/placement side-pick keep working via the constants.

- [ ] **Step 1: Update the shoulder placement scene test (expected new positions)**

`TestSceneComposePlacement.cs`:
- `TestPlaceShoulderNote` (~L114–115): comment "Left strip is the West–South boundary (225°); Right strip the East–North boundary (45°)." → "Left strip is the West lane (180°); Right strip the East lane (0°)."; `positionAtAngle(225)` → `positionAtAngle(180)`.
- `TestPlaceShoulderHoldWithDrag` (~L125, L127): `positionAtAngle(45, 0.6f)` → `positionAtAngle(0, 0.6f)`; `positionAtAngle(45, 0.3f)` → `positionAtAngle(0, 0.3f)`. Update the "move near right strip" step comment/label if present.

Scan both `TestSceneComposePlacement.cs` and `TestSceneComposeSelection.cs` for any other shoulder-tool step targeting `positionAtAngle(225)` (left strip) or `positionAtAngle(45, …)` (right strip) and apply the same 225→180 / 45→0 change. **Do not** touch `positionAtAngle(45)`/`(315)` used for slam-edge or slider-node placement (those are absolute placements, not shoulder strips — e.g. `placeSlamEdgeAt(45)`, `positionAtAngle(315, 0.4f)` for a slam).

- [ ] **Step 2: Run the shoulder tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposePlacement"`
Expected: FAIL — strips still at 225/45, so the cursor at 180/0 no longer picks the right side.

- [ ] **Step 3: Move the shoulder constants**

`GarbusEditorPlayfield.cs`:

```csharp
    /// <summary>Absolute angle of the Left-shoulder lane strip — the West lane (a left shoulder travels West).</summary>
    public const int LEFT_SHOULDER_ANGLE_DEG = 180;

    /// <summary>Absolute angle of the Right-shoulder lane strip — the East lane (a right shoulder travels East).</summary>
    public const int RIGHT_SHOULDER_ANGLE_DEG = 0;
```

- [ ] **Step 4: Run the editor suite to verify green**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~Editor"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/GarbusEditorPlayfield.cs Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs
git commit -m "feat: align editor shoulder strips with the West/East lanes"
```

---

### Task 3: `ReverseAngleView` state + wiring + grid regeneration

Add the source-of-truth bindable and make the direction actually flip live (static `Direction` + `AngleGrid` regen).

**Files:**
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs`
- Modify: `Garbus.Game/Edit/GarbusEditorPlayfield.cs` (the `AngleGrid` inner class)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `EditorAngleMapping.Direction` (Task 1).
- Produces: `public readonly BindableBool GarbusHitObjectComposer.ReverseAngleView` (default `false`). Setting `.Value = true` sets `EditorAngleMapping.Direction = -1` and regenerates the grid.

- [ ] **Step 1: Write the failing wiring test**

Add to `TestSceneComposeSelection.cs` (it already exposes a real `GarbusHitObjectComposer composer` and `playfield`):

```csharp
[Test]
public void TestReverseAngleViewMovesNorthToCentre()
{
    waitForComposer();
    placeNoteAt(90); // North

    AddAssert("North starts off-centre", () =>
        Math.Abs(EditorAngleMapping.ToX(90) - 0.5f) > 0.1f);

    AddStep("reverse the view", () => composer.ReverseAngleView.Value = true);

    AddAssert("Direction flipped", () => EditorAngleMapping.Direction, () => Is.EqualTo(-1));
    AddAssert("North now centred", () =>
        Math.Abs(EditorAngleMapping.ToX(90) - 0.5f) < 1e-4f);
    AddUntilStep("North drawable tracks to centre", () =>
    {
        var d = composer.HitObjects.OfType<EditorDrawableCardinalNote>().FirstOrDefault();
        return d != null && Math.Abs(d.X - 0.5f) < 0.01f;
    });
}
```

Ensure a `using System;` and the `EditorDrawableCardinalNote` namespace (`Garbus.Game.Edit.Drawables`) are imported in the file (add if missing).

- [ ] **Step 2: Build/run to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestReverseAngleViewMovesNorthToCentre"`
Expected: FAIL — `composer.ReverseAngleView` does not exist (compile error).

- [ ] **Step 3: Add the bindable and wire it in `GarbusHitObjectComposer.cs`**

After the `AngleSnap` field:

```csharp
    /// <summary>
    /// Whether the angle view is reversed — North at the centre, clockwise (reflected about the E–W axis).
    /// View-only; drives <see cref="EditorAngleMapping.Direction"/>. Never serialized.
    /// </summary>
    public readonly BindableBool ReverseAngleView = new BindableBool();
```

At the end of `load()`:

```csharp
        // The composer is [Cached] and loads before the playfield's AngleGrid, so this direct subscriber
        // sets Direction before the grid's (bound) subscriber regenerates — the grid reads a fresh value.
        ReverseAngleView.BindValueChanged(v => EditorAngleMapping.Direction = v.NewValue ? -1 : 1, true);
```

(`BindableBool` is already available via `using osu.Framework.Bindables;`.)

- [ ] **Step 4: Regenerate the `AngleGrid` on direction change**

In `GarbusEditorPlayfield.cs`, inside the `AngleGrid` inner class, add a bindable field next to `angleSnap`:

```csharp
        private readonly IBindable<bool> reverseView = new BindableBool();
```

In `AngleGrid.LoadComplete`, bind it (mirroring the `angleSnap` pattern; no `runOnceImmediately` since `angleSnap`'s initial `regenerate()` already covers first draw):

```csharp
            if (composer != null)
            {
                angleSnap.BindTo(composer.AngleSnap);
                reverseView.BindTo(composer.ReverseAngleView);
            }
            angleSnap.BindValueChanged(_ => regenerate(), true);
            reverseView.BindValueChanged(_ => regenerate());
```

(The `reverseView` field is auto-unbound by `Drawable`'s `UnbindAllBindables` on dispose — same as `angleSnap` — so no manual `Dispose` and no leak. Add `using osu.Framework.Bindables;` if not already present — it is.)

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestReverseAngleViewMovesNorthToCentre"`
Expected: PASS.

- [ ] **Step 6: Run the editor suite to verify no regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~Editor"`
Expected: PASS (each scene recreates the composer, whose `load()` asserts `Direction = 1` from the default-false bindable — resetting the static per scene).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/GarbusHitObjectComposer.cs Garbus.Game/Edit/GarbusEditorPlayfield.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: wire ReverseAngleView bindable to the angle mapping direction"
```

---

### Task 4: Floating direction-toggle button

A small floating button at the top-left of the compose playfield, on the N/E/S/W label row, showing the current x→θ direction and toggling `ReverseAngleView`.

**Files:**
- Create: `Garbus.Game/Edit/AngleDirectionToggleButton.cs`
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs` (host it)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `GarbusHitObjectComposer.ReverseAngleView` (Task 3).
- Produces: `AngleDirectionToggleButton` (a `CompositeDrawable`) hosted in the composer's `PlayfieldContentContainer`; label reads `⇄ CCW` (normal) / `⇄ CW` (reversed).

- [ ] **Step 1: Write the failing button test**

Add to `TestSceneComposeSelection.cs`:

```csharp
[Test]
public void TestDirectionToggleButtonFlipsView()
{
    waitForComposer();

    AddAssert("starts CCW / not reversed", () => !composer.ReverseAngleView.Value);
    AddAssert("button shows CCW", () =>
        composer.ChildrenOfType<AngleDirectionToggleButton>().Single().LabelText == "⇄ CCW");

    AddStep("click the toggle", () =>
    {
        var button = composer.ChildrenOfType<AngleDirectionToggleButton>().Single();
        input.MoveMouseTo(button.ScreenSpaceDrawQuad.Centre);
        input.Click(MouseButton.Left);
    });

    AddAssert("now reversed", () => composer.ReverseAngleView.Value);
    AddAssert("button shows CW", () =>
        composer.ChildrenOfType<AngleDirectionToggleButton>().Single().LabelText == "⇄ CW");
}
```

Ensure `using Garbus.Game.Edit;` (for `AngleDirectionToggleButton`) and `osu.Framework.Testing` (`ChildrenOfType`) are imported.

- [ ] **Step 2: Build to verify it fails**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: FAIL — `AngleDirectionToggleButton` does not exist.

- [ ] **Step 3: Create `AngleDirectionToggleButton.cs`**

```csharp
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK.Graphics;

namespace Garbus.Game.Edit;

/// <summary>
/// A floating toggle at the top-left of the compose playfield that flips the angle-view direction
/// (<see cref="GarbusHitObjectComposer.ReverseAngleView"/>). The label shows the current x→θ rotation
/// sense: <c>⇄ CCW</c> when normal (South centred), <c>⇄ CW</c> when reversed (North centred).
/// </summary>
public partial class AngleDirectionToggleButton : CompositeDrawable
{
    [Resolved]
    private GarbusHitObjectComposer composer { get; set; } = null!;

    private readonly BindableBool reversed = new BindableBool();
    private SpriteText label = null!;

    /// <summary>The current label text (exposed for tests).</summary>
    public string LabelText => label.Text.ToString();

    [BackgroundDependencyLoader]
    private void load()
    {
        AutoSizeAxes = Axes.Both;
        Anchor = Anchor.TopLeft;
        Origin = Anchor.TopLeft;
        Margin = new MarginPadding(4);

        InternalChild = new Container
        {
            AutoSizeAxes = Axes.Both,
            Masking = true,
            CornerRadius = 4,
            Children = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0f, 0f, 0f, 0.6f) },
                label = new SpriteText
                {
                    Margin = new MarginPadding { Horizontal = 8, Vertical = 3 },
                    Font = FontUsage.Default.With(size: 16),
                    Colour = Color4.Yellow,
                },
            },
        };
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        reversed.BindTo(composer.ReverseAngleView);
        reversed.BindValueChanged(v => label.Text = v.NewValue ? "⇄ CW" : "⇄ CCW", true);
    }

    protected override bool OnClick(ClickEvent e)
    {
        composer.ReverseAngleView.Value = !composer.ReverseAngleView.Value;
        return true;
    }
}
```

- [ ] **Step 4: Host the button in `GarbusHitObjectComposer.load()`**

Add after the `flipPivotOverlay` line:

```csharp
        PlayfieldContentContainer.Add(new AngleDirectionToggleButton());
```

(`PlayfieldContentContainer`'s top-left coincides with the playfield's top-left, where `AngleGrid` draws the N/E/S/W labels — so the `Margin(4)` button lands on that row. It is added after `flipPivotOverlay`, which only steals input while a flip is active, so the button stays clickable.)

- [ ] **Step 5: Run the button test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestDirectionToggleButtonFlipsView"`
Expected: PASS.

- [ ] **Step 6: Run the full editor suite + build the app**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~Editor"`
Expected: PASS.
Run: `dotnet build Garbus.Desktop.slnf`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/AngleDirectionToggleButton.cs Garbus.Game/Edit/GarbusHitObjectComposer.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: add floating angle-direction toggle to the compose editor"
```

---

## Self-Review

**Spec coverage:**
- South at center (`ANGLE_ORIGIN = 90`) → Task 1. ✓
- Reverse reflection about E–W (`Direction` sign) → Task 1 (math) + Task 3 (state/redraw). ✓
- Shoulder strips → West/East → Task 2. ✓
- DI `BindableBool` source of truth + per-frame auto-update + `AngleGrid` regen + no leak → Task 3. ✓
- Floating button, top-left on the label row, `⇄ CCW`/`⇄ CW` → Task 4. ✓
- View-only / never serialized → no serialization touched; `Direction` reset per scene via composer load. ✓
- Tests: origin migration, reversed round-trip, `TearDown` reset, toggle scene coverage → Tasks 1,3,4. ✓

Deviation from spec (intentional): the bindable lives on `GarbusHitObjectComposer` (where `AngleSnap` already lives and `AngleGrid` already resolves it), not `GarbusEditor`. Simpler and avoids a second cache point.

**Placeholder scan:** none — every code and test edit is spelled out; the −45° rule is a deterministic transform with all known sites enumerated, plus a catch-all for any missed hardcoded angle.

**Type consistency:** `ReverseAngleView` (`BindableBool`), `EditorAngleMapping.Direction` (`static int`, ±1), `AngleDirectionToggleButton.LabelText` (`string`), `LEFT/RIGHT_SHOULDER_ANGLE_DEG` (`int`) are used identically across tasks.
