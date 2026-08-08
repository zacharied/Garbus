# Spawn Halo Ring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Draw the spawn halo — the radius hit objects appear on and hold at — as a thin gray ring on the playfield.

**Architecture:** A new `SpawnHaloRing` container wraps an unmodified `Arc` and sizes itself to `SpawnHaloFraction`. `Arc` already derives its radius from its own `ChildSize`, so the wrapper's relative size *is* the halo radius, and resize tracking plus live tuning response come free. It joins `Ring`'s furniture list between `ComboDisplay` and `HitObjectContainer`.

**Tech Stack:** C#, .NET 8, osu!framework (`Container`, `BindableFloat`, `SmoothPath` via the existing `Arc`), NUnit visual test scenes.

## Global Constraints

- **Do not add new warnings — including in tests.** Build and test output stays warning-clean.
- **Test expectations are independent and spec-anchored.** Pinned constants trace to a spec doc and are hand-derived; no test may derive its expected value from the implementation's own constants or functions.
- **New visual elements ship with a Tuning test** exposing their configurable parameters as live sliders.
- **Do not run the app.** Tests (headless and visual scenes) are how you verify.
- Build: `dotnet build Garbus.Desktop.slnf`
- Test: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`

## File Structure

| File | Responsibility |
| --- | --- |
| `Garbus.Game/UI/SpawnHaloRing.cs` (create) | The drawable. Owns the halo→size mapping and the ring's styling. |
| `Garbus.Game/UI/Ring.cs` (modify) | Adds the ring to the furniture list at the correct z-order. |
| `docs/presentation-specs/Playfield.md` (modify) | Canonical spec. Currently asserts the halo is *not* drawn; that becomes false. |
| `Garbus.Game.Tests/Visual/TestSceneSpawnHaloRing.cs` (create) | Pins the ring's radius against the spec formula. |
| `Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs` (modify) | Gains ring thickness + alpha sliders. One scene owns all halo tuning. |

`Arc.cs` is **not** modified.

---

### Task 1: SpawnHaloRing drawable, wiring, and geometry tests

**Files:**
- Create: `Garbus.Game/UI/SpawnHaloRing.cs`
- Modify: `Garbus.Game/UI/Ring.cs` (the `AddRangeInternal` list, around line 72-84)
- Modify: `docs/presentation-specs/Playfield.md` (the line reading "The halo is not drawn. Objects simply appear at that radius.")
- Test: `Garbus.Game.Tests/Visual/TestSceneSpawnHaloRing.cs`

**Interfaces:**
- Consumes: `Arc(float startRadians, float endRadians, float thickness)` with `int Resolution { get; init; }` and `BindableFloat Thickness`; `GarbusScrollingInfo.SpawnHaloFraction` (`BindableDouble`).
- Produces: `public sealed partial class SpawnHaloRing : Container` with `public BindableFloat Thickness { get; }`. Task 2 drives `Thickness.Value` and the inherited `Alpha`.

**Key derivation — do not get this wrong.** The wrapper's relative size is the fraction **itself**, not twice it. `ScrollLength` is already a radius (`min(W, H) / 2` of the playfield), so the wrapper's halving and the playfield's cancel. Check: a 400×400 container at fraction 0.25 gives a wrapper of 100 px and a drawn radius of 50, matching `0.25 × 200`. Sizing to `2 × fraction` would draw the ring at twice the halo radius.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneSpawnHaloRing.cs`:

```csharp
// Pins the spawn halo ring's radius against the halo formula in
// docs/presentation-specs/Playfield.md ("Spawn halo and spawn phase").
//
// Calibration anchor — a 460x460 playfield less its 30px padding gives the ring 400x400, so
// ScrollLength is 200. Hand-derived from haloRadius = ScrollLength * SpawnHaloFraction:
//   fraction 0.25 on ScrollLength 200 -> radius 50
//   fraction 0.10 on ScrollLength 200 -> radius 20
//   fraction 0.25 on ScrollLength 100 -> radius 25   (playfield 260 -> content 200)

using System.Linq;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSpawnHaloRing : GarbusTestScene
    {
        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private GarbusPlayfield playfield = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("set halo fraction", () => scrollingInfo.SpawnHaloFraction.Value = 0.25);

            AddStep("build playfield", () => Child = new GarbusInputManager
            {
                Child = playfield = new GarbusPlayfield
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(460),
                },
            });

            AddUntilStep("scroll length 200", () =>
                Precision.AlmostEquals(scrollingContainer().ScrollLength, 200, 0.001));
        }

        private GarbusScrollingHitObjectContainer scrollingContainer()
            => playfield.ChildrenOfType<GarbusScrollingHitObjectContainer>().First();

        // The ring's drawn radius is half its own draw width — a public framework property, so this
        // asserts real geometry without any test-only member on the production type.
        private float drawnRadius() => playfield.ChildrenOfType<SpawnHaloRing>().Single().DrawSize.X / 2;

        [Test]
        public void TestRadiusIsHaloFractionOfScrollLength()
        {
            AddUntilStep("radius 50", () => Precision.AlmostEquals(drawnRadius(), 50, 0.5));
        }

        [Test]
        public void TestLiveFractionChangeMovesTheRing()
        {
            AddUntilStep("radius 50", () => Precision.AlmostEquals(drawnRadius(), 50, 0.5));

            AddStep("shrink halo fraction", () => scrollingInfo.SpawnHaloFraction.Value = 0.1);
            AddUntilStep("radius 20", () => Precision.AlmostEquals(drawnRadius(), 20, 0.5));
        }

        [Test]
        public void TestRingTracksPlayfieldResize()
        {
            AddUntilStep("radius 50", () => Precision.AlmostEquals(drawnRadius(), 50, 0.5));

            // 260 less the 30px padding each side leaves 200, halving ScrollLength to 100.
            AddStep("halve the playfield content", () => playfield.Size = new Vector2(260));
            AddUntilStep("radius 25", () => Precision.AlmostEquals(drawnRadius(), 25, 0.5));
        }

        [Test]
        public void TestZeroFractionDrawsNoRing()
        {
            AddStep("zero the halo fraction", () => scrollingInfo.SpawnHaloFraction.Value = 0);
            AddUntilStep("ring has no extent", () => Precision.AlmostEquals(drawnRadius(), 0, 0.001));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~SpawnHaloRing"`

Expected: FAIL to compile with `CS0246: The type or namespace name 'SpawnHaloRing' could not be found`. That is the correct failure — the type does not exist yet.

- [ ] **Step 3: Create the drawable**

Create `Garbus.Game/UI/SpawnHaloRing.cs`:

```csharp
using System;
using Garbus.Game.Gameplay.UI.Scrolling;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// Draws the spawn halo — the radius a hit object appears on and holds at while its spawn animation
/// plays — as a thin ring. Specified in docs/presentation-specs/Playfield.md ("Spawn halo and spawn
/// phase") and docs/superpowers/specs/2026-08-01-spawn-halo-ring-design.md.
///
/// The radius comes entirely from this container's own size: <see cref="Arc"/> derives its radius
/// from its <c>ChildSize</c>, so sizing this wrapper to the halo fraction is what puts the stroke on
/// the halo. Every size in the chain is relative, so a playfield resize needs no handling.
/// </summary>
public sealed partial class SpawnHaloRing : Container
{
    private const float default_thickness = 2;

    // The outer ring is opaque white; this reads as gray against the dark playfield. Translucency
    // matters specifically because the ring draws in FRONT of the centre combo counter (see Ring's
    // child order) — it tints the digits rather than slicing them.
    private const float default_alpha = 0.35f;

    // Arc's default 32 segments would give ~12px chords at the halo's ~120px diameter, which reads as
    // visible faceting. The halo is a small circle, so a higher resolution is cheap.
    private const int resolution = 64;

    /// <summary>Full width of the drawn ring stroke, in pixels.</summary>
    public BindableFloat Thickness { get; } = new BindableFloat(default_thickness);

    [Resolved(CanBeNull = true)]
    private GarbusScrollingInfo? scrollingInfo { get; set; }

    // Mirrors GarbusScrollingHitObjectContainer's fallback: a bare test scene with no cached
    // GarbusScrollingInfo still gets the production default rather than a second literal to drift.
    private readonly GarbusScrollingInfo fallbackScrollingInfo = new GarbusScrollingInfo();

    private readonly IBindable<double> spawnHaloFraction = new BindableDouble();

    private readonly Arc arc;

    public SpawnHaloRing()
    {
        RelativeSizeAxes = Axes.Both;
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        Alpha = default_alpha;

        AddInternal(arc = new Arc(0, 2 * MathF.PI, default_thickness) { Resolution = resolution });
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        arc.Thickness.BindTo(Thickness);

        spawnHaloFraction.BindTo((scrollingInfo ?? fallbackScrollingInfo).SpawnHaloFraction);
        spawnHaloFraction.BindValueChanged(_ => updateSize(), true);
    }

    // The relative size is the fraction ITSELF, not twice it. ScrollLength is already a radius
    // (min(W, H) / 2 of the playfield), so this container's halving and the playfield's cancel: a
    // relative size of `fraction` puts Arc's own min(ChildSize) / 2 at exactly
    // fraction * ScrollLength. Sizing to 2 * fraction would draw the ring at twice the halo radius.
    //
    // A zero fraction collapses this to zero size, and Arc already skips a non-positive radius, so
    // nothing is drawn — no separate hide is needed.
    private void updateSize() => Size = new Vector2((float)spawnHaloFraction.Value);
}
```

- [ ] **Step 4: Wire it into the Ring's furniture list**

In `Garbus.Game/UI/Ring.cs`, the `AddRangeInternal` call in the constructor. Update the comment and insert `SpawnHaloRing` between `ComboDisplay` and `HitObjectContainer`:

```csharp
        // Back-to-front: radial spokes, chord connectors (under all notes), the centre combo counter
        // (drawn beneath every note), the spawn halo ring, cross-lane paths, the lanes, judgement
        // feedback above every hit object, then the ring.
        //
        // The halo ring draws in FRONT of the combo counter — at the default halo fraction it lands
        // almost exactly around the combo digits, and the halo radius should read exactly rather than
        // be broken up by whatever the combo currently is. It draws BEHIND the hit objects, though:
        // unlike the outer ring, which only ever clips an object in passing, objects sit on the halo
        // for their whole spawn hold, so a front-most halo would put a line through every spawning
        // note for the duration of it.
        AddRangeInternal([
            new PlayfieldRadialLines(),
            new ChordConnectorOverlay(),
            new ComboDisplay(),
            new SpawnHaloRing(),
            HitObjectContainer,
            laneContainer,
            judgementFeedback,
            new Arc(0, 2 * MathF.PI)
            {
                Resolution = 128,
                Colour = Colour4.White,
            },
        ]);
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~SpawnHaloRing"`

Expected: PASS, 4 tests.

If `TestRadiusIsHaloFractionOfScrollLength` reports radius 100 instead of 50, the wrapper was sized to `2 * fraction` — re-read the derivation note above.

- [ ] **Step 6: Update the presentation spec**

In `docs/presentation-specs/Playfield.md`, find this line in the "Spawn halo and spawn phase" section:

```markdown
The halo is not drawn. Objects simply appear at that radius.
```

Replace it with:

```markdown
The halo is drawn, as a thin translucent gray ring at `haloRadius`. It is furniture: static, with no
animation, and it reacts to nothing. It draws in front of the centre combo counter so the halo radius
always reads exactly, and behind hit objects so an object holding on the halo is never sliced by it.
```

- [ ] **Step 7: Verify the whole suite is still green and warning-clean**

Run: `dotnet build Garbus.Desktop.slnf` — expect `0 Warning(s)`, `0 Error(s)`.
Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` — expect all tests passing (845 = the current 841 plus these 4).

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/UI/SpawnHaloRing.cs Garbus.Game/UI/Ring.cs docs/presentation-specs/Playfield.md Garbus.Game.Tests/Visual/TestSceneSpawnHaloRing.cs
git commit -m "feat: draw the spawn halo as a thin gray ring"
```

---

### Task 2: Fold ring tuning into the existing halo tuning scene

**Files:**
- Modify: `Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs`

**Interfaces:**
- Consumes: `SpawnHaloRing.Thickness` (`BindableFloat`) and the inherited `Alpha` (`float`), both from Task 1.
- Produces: nothing consumed downstream.

The scene already drives a real `GarbusPlayfield` over a looping stream, so the ring is present with no extra setup. Two things change: the playfield becomes a field so the sliders can reach it, and two sliders are added.

- [ ] **Step 1: Promote the playfield to a field**

In `TestSceneSpawnHaloTuning`, add a field beside the existing ones:

```csharp
        private GarbusPlayfield playfield = null!;
```

Then in `LoadComplete`, delete the local declaration line `GarbusPlayfield playfield;` and assign the field instead — the existing `Child = new Container { ... playfield = new GarbusPlayfield { Size = Vector2.One } ... }` assignment already writes to `playfield`, so removing the local declaration is the only edit needed there.

- [ ] **Step 2: Add the two sliders**

In the `TestSceneSpawnHaloTuning()` constructor, after the existing `"scroll time range (ms)"` slider and before `"playback rate"`, add:

```csharp
            AddSliderStep("halo ring thickness", 0f, 10f, 2f,
                v => { if (IsLoaded && haloRing() is { } ring) ring.Thickness.Value = v; });

            AddSliderStep("halo ring alpha", 0f, 1f, 0.35f,
                v => { if (IsLoaded && haloRing() is { } ring) ring.Alpha = v; });
```

- [ ] **Step 3: Add the lookup helper**

Add beside the other private members. It returns null before `LoadComplete` has built the playfield, which is why the sliders pattern-match rather than dereference:

```csharp
        private SpawnHaloRing? haloRing() => playfield?.ChildrenOfType<SpawnHaloRing>().SingleOrDefault();
```

This needs `using osu.Framework.Testing;` for `ChildrenOfType`. `System.Linq` is already imported.

- [ ] **Step 4: Update the scene's header comment**

Replace the first paragraph of the file comment with:

```csharp
// Interactive tuning scene for the spawn halo: halo radius, spawn duration, scroll speed and the
// halo ring's thickness and alpha are sliders in the test browser's step sidebar, over a looping
// stream of mixed objects on all four cardinal angles plus both shoulders — so the hold reads on
// point notes and durationed objects at once. Every parameter is live, so nothing rebuilds on
// change. [Explicit] so it never runs in a headless "run all"; pick it in the test browser.
```

- [ ] **Step 5: Verify it compiles and the suite is green**

The scene is `[Explicit]`, so it does not execute in a headless run — compiling clean and leaving the suite green is the check.

Run: `dotnet build Garbus.Desktop.slnf` — expect `0 Warning(s)`, `0 Error(s)`.
Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` — expect all tests passing, same count as Task 1.

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs
git commit -m "test: add halo ring sliders to the spawn halo tuning scene"
```

---

## Plan Self-Review

**Spec coverage.** Every section of `2026-08-01-spawn-halo-ring-design.md` maps to a task: the component and its size derivation (Task 1 Step 3), placement and z-order (Task 1 Step 4), the three parameters (Task 1 Step 3 — `Thickness` bindable, `default_alpha`, `resolution`), data flow and the `CanBeNull` fallback (Task 1 Step 3), all four test assertions (Task 1 Step 1), and the merged tuning scene (Task 2).

One item is in this plan but **not** in the design spec: the `Playfield.md` update (Task 1 Step 6). The spec omitted it, but that document states "The halo is not drawn," which drawing the ring makes false — and it is the canonical spec other tests anchor to, so it cannot be left stale.

The spec's edge case "**Fraction 0.** The wrapper hides rather than handing `Arc` a degenerate zero-radius path" is implemented as zero *size* rather than an explicit hide, because `Arc.regeneratePath` already guards `radius > 0` and draws nothing. The observable behaviour the spec asks for — no ring at fraction 0 — is unchanged, and `TestZeroFractionDrawsNoRing` pins it. This is noted rather than silently diverged.

**Placeholder scan.** No TBD/TODO, no "handle edge cases", no "similar to Task N". Every code step carries the actual code.

**Type consistency.** `SpawnHaloRing.Thickness` is a `BindableFloat` in Task 1 and driven as `ring.Thickness.Value` in Task 2. `Alpha` is the inherited `float`, set directly in both. `haloRing()` returns `SpawnHaloRing?` and every call site pattern-matches it. `Arc(0, 2 * MathF.PI, default_thickness)` matches `Arc(float startRadians = 0, float endRadians = 0, float thickness = 5)`, and `Resolution` is `init`-only, set in the object initialiser.
