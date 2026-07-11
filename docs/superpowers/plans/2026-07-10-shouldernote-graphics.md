# ShoulderNote Graphics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the ShoulderNote's static "paddle" sprite with two purple square sprites riding outward along the ±45° quadrant diagonals, joined by a circular arc that grows as they travel.

**Architecture:** `DrawableShoulderNote` is rewritten as an `ISelfPosition` drawable (the pattern `DrawableSliderBody` uses): it fills the playfield, is skipped by the scrolling container's point-positioner, and computes its children's polar positions each frame from `scrollingContainer.DistanceFromCentreAtTime`. A pure static helper (`ShoulderNoteGeometry`) owns the angle/position math so it can be unit-tested without the framework. Judgement is untouched — it stays a single `DrawableNote<ShoulderNote>`.

**Tech Stack:** C# / osu-framework, NUnit tests. Existing `Garbus.Game/UI/Arc.cs` (SmoothPath-based polar arc) and the `"square"` texture are reused.

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- Vendored/ported files keep their existing ppy "Ported from BigAssCircle" attribution header; new Garbus-only files need no ppy header.
- No version bumps, no backwards-compat shims (experimental project).
- Polar convention across the playfield: `x = cos(θ)·r`, `y = −sin(θ)·r` (θ = 0 points right, increases counter-clockwise).
- `ShoulderNote.AngleDeg` is 0 (Right → East) or 180 (Left → West).
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`.

---

### Task 1: `ShoulderNoteGeometry` pure helper

**Files:**
- Create: `Garbus.Game/Objects/Drawables/ShoulderNoteGeometry.cs`
- Test: `Garbus.Game.Tests/ShoulderNoteGeometryTest.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `const float ShoulderNoteGeometry.DiagonalOffsetDeg` (= 45f)
  - `static float ShoulderNoteGeometry.ToRadians(float degrees)`
  - `static Vector2 ShoulderNoteGeometry.Polar(float radians, float radius)`
  - `static Vector2 ShoulderNoteGeometry.SquarePosition(float baseAngleDeg, float radius, float offsetSign)` — the polar position of a square at `baseAngleDeg + offsetSign·45°` and the given `radius`. `offsetSign` is `+1f` or `-1f`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/ShoulderNoteGeometryTest.cs`:

```csharp
using System;
using Garbus.Game.Objects.Drawables;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ShoulderNoteGeometryTest
    {
        // 100 * cos/sin 45°
        private const float diag = 70.71068f;

        [Test]
        public void RightShoulderSquaresStraddleEast()
        {
            var plus = ShoulderNoteGeometry.SquarePosition(0f, 100f, +1f);
            var minus = ShoulderNoteGeometry.SquarePosition(0f, 100f, -1f);

            // +45° points up-right (y negative in screen space); -45° points down-right.
            Assert.That(plus.X, Is.EqualTo(diag).Within(0.01f));
            Assert.That(plus.Y, Is.EqualTo(-diag).Within(0.01f));
            Assert.That(minus.X, Is.EqualTo(diag).Within(0.01f));
            Assert.That(minus.Y, Is.EqualTo(diag).Within(0.01f));
        }

        [Test]
        public void LeftShoulderSquaresStraddleWest()
        {
            var plus = ShoulderNoteGeometry.SquarePosition(180f, 100f, +1f);
            var minus = ShoulderNoteGeometry.SquarePosition(180f, 100f, -1f);

            // 225° -> (-x, +y); 135° -> (-x, -y).
            Assert.That(plus.X, Is.EqualTo(-diag).Within(0.01f));
            Assert.That(plus.Y, Is.EqualTo(diag).Within(0.01f));
            Assert.That(minus.X, Is.EqualTo(-diag).Within(0.01f));
            Assert.That(minus.Y, Is.EqualTo(-diag).Within(0.01f));
        }

        [Test]
        public void VerticalGapGrowsWithRadius()
        {
            float gapNear = gap(50f);
            float gapFar = gap(200f);

            Assert.That(gapFar, Is.GreaterThan(gapNear));
            Assert.That(gapFar, Is.EqualTo(gapNear * 4f).Within(0.01f));

            static float gap(float radius)
            {
                var plus = ShoulderNoteGeometry.SquarePosition(0f, radius, +1f);
                var minus = ShoulderNoteGeometry.SquarePosition(0f, radius, -1f);
                return MathF.Abs(plus.Y - minus.Y);
            }
        }

        [Test]
        public void ZeroRadiusCollapsesToCentre()
        {
            var plus = ShoulderNoteGeometry.SquarePosition(0f, 0f, +1f);
            Assert.That(plus.Length, Is.EqualTo(0f).Within(0.01f));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~ShoulderNoteGeometryTest"`
Expected: FAIL to compile — `ShoulderNoteGeometry` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Create `Garbus.Game/Objects/Drawables/ShoulderNoteGeometry.cs`:

```csharp
using System;
using osuTK;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// Pure polar geometry for a <see cref="Objects.ShoulderNote"/>'s two-square-plus-arc visual. The two
/// squares sit at the ±45° quadrant diagonals either side of the note's cardinal angle (0° East for a
/// right shoulder, 180° West for a left one) and ride outward as the note's travel radius grows.
/// </summary>
public static class ShoulderNoteGeometry
{
    /// <summary>Angular offset of each square from the note's cardinal line — the quadrant diagonal.</summary>
    public const float DiagonalOffsetDeg = 45f;

    public static float ToRadians(float degrees) => degrees * MathF.PI / 180f;

    /// <summary>Playfield polar-to-cartesian: θ = 0 points right, increasing counter-clockwise.</summary>
    public static Vector2 Polar(float radians, float radius)
        => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);

    /// <summary>
    /// Position of a shoulder square at <paramref name="radius"/> from centre, offset
    /// <paramref name="offsetSign"/>·45° from <paramref name="baseAngleDeg"/>. Pass +1 / −1 for the two squares.
    /// </summary>
    public static Vector2 SquarePosition(float baseAngleDeg, float radius, float offsetSign)
        => Polar(ToRadians(baseAngleDeg + offsetSign * DiagonalOffsetDeg), radius);
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~ShoulderNoteGeometryTest"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Objects/Drawables/ShoulderNoteGeometry.cs Garbus.Game.Tests/ShoulderNoteGeometryTest.cs
git commit -m "Add ShoulderNoteGeometry helper for two-square shoulder visual"
```

---

### Task 2: Rewrite `DrawableShoulderNote` to draw two squares + growing arc

**Files:**
- Modify (full rewrite): `Garbus.Game/Objects/Drawables/DrawableShoulderNote.cs`
- Regression guard (unchanged, must still pass): `Garbus.Game.Tests/Visual/TestSceneGameplay.cs` (`TestUntouchedNotesMiss` already asserts shoulder notes miss when untouched)

**Interfaces:**
- Consumes: `ShoulderNoteGeometry.SquarePosition`, `ShoulderNoteGeometry.ToRadians`, `ShoulderNoteGeometry.DiagonalOffsetDeg` (Task 1); existing `Garbus.Game.UI.Arc(startRadians, endRadians, thickness)` with `StartRadians` / `EndRadians` / `Thickness` bindables; `GarbusScrollingHitObjectContainer.DistanceFromCentreAtTime(double)`; `ISelfPosition` marker.
- Produces: nothing new for other tasks (self-contained drawable).

- [ ] **Step 1: Replace the file contents**

Overwrite `Garbus.Game/Objects/Drawables/DrawableShoulderNote.cs` with:

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableShoulderNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: replaced the single "paddle" sprite with two square sprites on the ±45° quadrant
// diagonals joined by a growing circular arc; self-positions each frame instead of being point-placed.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.UI;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A shoulder note — the analog-shoulder counterpart of <see cref="DrawableCardinalNote"/>. It is still
/// judged as a single timed press (see <see cref="DrawableNote{T}"/>), but is drawn as two purple square
/// sprites riding outward along the ±45° diagonals of its side's quadrant (East for a right shoulder,
/// West for a left one), joined by a circular arc whose radius grows with the note's travel distance.
///
/// Implements <see cref="ISelfPosition"/> so the scrolling container skips point-positioning it; the
/// drawable instead fills the playfield and places its children in playfield-centre polar coordinates
/// every frame from <see cref="GarbusScrollingHitObjectContainer.DistanceFromCentreAtTime(double)"/>.
/// </summary>
public partial class DrawableShoulderNote : DrawableNote<ShoulderNote>, ISelfPosition
{
    private const float square_size = 80f;
    private const float arc_thickness = 15f;

    private Sprite squareA = null!;
    private Sprite squareB = null!;
    private Arc arc = null!;

    [Resolved]
    private GarbusScrollingHitObjectContainer scrollingContainer { get; set; } = null!;

    public DrawableShoulderNote(ShoulderNote hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Colour = Colour4.Purple;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var squareTexture = textures.Get("square");

        // Arc radius is driven by its own size (Arc draws at min(ChildSize)/2), so size it each frame to
        // grow with the note's travel. Angles are set each frame too; start collapsed (no span, no size).
        AddInternal(arc = new Arc(thickness: arc_thickness)
        {
            RelativeSizeAxes = Axes.None,
            Size = Vector2.Zero,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        });

        AddInternal(squareA = createSquare(squareTexture));
        AddInternal(squareB = createSquare(squareTexture));
    }

    private static Sprite createSquare(Texture texture) => new Sprite
    {
        Texture = texture,
        Size = new Vector2(square_size),
        FillMode = FillMode.Fit,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
    };

    protected override void Update()
    {
        base.Update();

        float baseAngleDeg = HitObject.AngleDeg;
        float radius = MathF.Max(0f, scrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime));

        squareA.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, radius, +1f);
        squareB.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, radius, -1f);

        arc.Size = new Vector2(2f * radius);
        arc.StartRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg - ShoulderNoteGeometry.DiagonalOffsetDeg);
        arc.EndRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg + ShoulderNoteGeometry.DiagonalOffsetDeg);
    }

    protected override void PrepareForUse()
    {
        // Spawn pop, scaled about the playfield centre (this drawable's centre origin).
        this.ScaleTo(0).ScaleTo(1, 125, Easing.In);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                this.FadeOut(350, Easing.OutQuint)
                    .ScaleTo(new Vector2(1.4f), 350, Easing.OutQuint)
                    .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                this.FadeColour(Color4.Red, duration);
                this.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors. (If `Arc`'s constructor signature differs, adjust the `new Arc(...)` call to match `Garbus.Game/UI/Arc.cs`.)

- [ ] **Step 3: Run the gameplay regression tests**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneGameplay"`
Expected: PASS — in particular `TestUntouchedNotesMiss` (shoulder notes still auto-miss) confirms judgement is unaffected by the visual rewrite.

- [ ] **Step 4: Visual verification**

Run the visual test browser and open `TestSceneGameplay`, or run the game (`dotnet run --project Garbus.Desktop`) and reach a chart with shoulder notes. Confirm:
- A shoulder note shows two purple squares emerging from the centre along the diagonals of its side (right → East, left → West), separating as they travel.
- A purple arc connects the two squares and bulges outward, reaching the quadrant's 90° outer edge as the note meets the ring.
- Hit fades + scales the whole visual up; miss reddens and fades it out.

Note in the commit/PR if UI could not be visually verified in this environment.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`
Expected: All tests PASS.

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Objects/Drawables/DrawableShoulderNote.cs
git commit -m "Draw ShoulderNote as two squares joined by a growing arc"
```

---

## Self-Review

**Spec coverage:**
- Two purple CardinalNote-style squares at ±45° offsets → Task 1 (geometry) + Task 2 (`square` sprites, `Colour4.Purple`, size 80). ✓
- Arc between the two sprites, growing with travel → Task 2 (`Arc` sized `2r` each frame). ✓
- Circular constant-radius arc that bulges as points separate → Task 1 `VerticalGapGrowsWithRadius` + Task 2 arc geometry. ✓
- Single hit object / judgement unchanged → Task 2 keeps `DrawableNote<ShoulderNote>`, no nested objects; `TestSceneGameplay` regression. ✓
- Spawn/hit/miss animations → Task 2 `PrepareForUse` / `UpdateHitStateTransforms`. ✓
- Drop the paddle texture → Task 2 full rewrite removes it. ✓
- Non-goal: HoldShoulderNote untouched → no HoldShoulder code modified. ✓

**Placeholder scan:** none — all steps contain concrete code and commands.

**Type consistency:** `ShoulderNoteGeometry.SquarePosition` / `ToRadians` / `DiagonalOffsetDeg` signatures match between Task 1 definition and Task 2 usage; `Arc` bindables (`StartRadians`, `EndRadians`, `Thickness`) match `Garbus.Game/UI/Arc.cs`.
