# Editor Slider Easing Display Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the editor compose-view slider polyline render the same swept geometry (`SweepEasing` + `Smooth`) that gameplay draws, instead of straight chords.

**Architecture:** Extract gameplay's per-link angle evaluation (`DrawableSliderBody.thetaAt` + its Catmull-Rom slope computation) into a pure static helper `SliderSweep`. Gameplay delegates to it (behavior unchanged). The editor's `SliderPolylineVisual` subdivides each link through the same helper, so the editor curve and the played curve are computed by one shared function and cannot drift.

**Tech Stack:** C# / .NET, osu-framework (`SmoothPath`, `Interpolation.ApplyEasing`, `Easing`), NUnit.

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL fields use `= null!` (copied verbatim from CLAUDE.md).
- This is an experimental project — NO backwards-compatibility layers, NO version bumps, NO historical notes in code or docs.
- osu terminology: "beatmap" → "chart", `Bac*` → `Garbus*`.
- Build: `dotnet build Garbus.Desktop.slnf`
- Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`

---

### Task 1: Shared `SliderSweep` angle-evaluation helper

**Files:**
- Create: `Garbus.Game/Objects/Path/SliderSweep.cs`
- Test: `Garbus.Game.Tests/SliderSweepTest.cs`

**Interfaces:**
- Consumes: nothing (pure math over `osu.Framework.Graphics.Easing` and `osu.Framework.Utils.Interpolation`).
- Produces:
  - `public const int SliderSweep.SegmentsPerLink = 12;`
  - `public static float[] SliderSweep.ComputeSlopes(IReadOnlyList<float> values, IReadOnlyList<double> times)`
  - `public static float SliderSweep.ValueAt(IReadOnlyList<float> values, IReadOnlyList<float> slopes, IReadOnlyList<double> times, Easing linkEasing, bool linkSmooth, int link, float t)`

  Both are unit-agnostic in `values` (radians for gameplay, degree-offsets for the editor). `values`/`times`/`slopes` are per-node (length `N`); `link` indexes the segment `node[link] → node[link+1]` (0..N-2); `t` is 0..1 along that link. `ValueAt` applies `linkEasing` to `t`, then linear-interpolates (default) or cubic-Hermite (when `linkSmooth`, using `slopes`) between the two node values. Endpoints are preserved (`t=0` → `values[link]`, `t=1` → `values[link+1]`).

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/SliderSweepTest.cs`:

```csharp
// Pure-math tests for the shared slider sweep evaluation used by both the gameplay body
// (DrawableSliderBody) and the editor polyline (SliderPolylineVisual). Plain NUnit — no game host.

using System.Collections.Generic;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class SliderSweepTest
    {
        // Two-node path: values 10 -> 50 over times 0 -> 100.
        private static readonly float[] two_values = { 10f, 50f };
        private static readonly double[] two_times = { 0.0, 100.0 };

        [Test]
        public void EndpointsPreservedLinear()
        {
            var slopes = SliderSweep.ComputeSlopes(two_values, two_times);

            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, false, 0, 0f), Is.EqualTo(10f).Within(1e-4));
            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, false, 0, 1f), Is.EqualTo(50f).Within(1e-4));
        }

        [Test]
        public void EndpointsPreservedSmooth()
        {
            var slopes = SliderSweep.ComputeSlopes(two_values, two_times);

            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, true, 0, 0f), Is.EqualTo(10f).Within(1e-4));
            Assert.That(SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, true, 0, 1f), Is.EqualTo(50f).Within(1e-4));
        }

        [Test]
        public void LinearParity()
        {
            var slopes = SliderSweep.ComputeSlopes(two_values, two_times);

            // No easing, not smooth => plain lerp: 10 + (50-10)*0.25 = 20.
            float actual = SliderSweep.ValueAt(two_values, slopes, two_times, Easing.None, false, 0, 0.25f);
            Assert.That(actual, Is.EqualTo(20f).Within(1e-4));
        }

        [Test]
        public void EasingIsApplied()
        {
            float[] values = { 0f, 100f };
            double[] times = { 0.0, 100.0 };
            var slopes = SliderSweep.ComputeSlopes(values, times);

            // InQuint at t=0.5 => 100 * ApplyEasing(InQuint, 0.5), which is far from the linear midpoint 50.
            float expected = (float)(100.0 * Interpolation.ApplyEasing(Easing.InQuint, 0.5));
            float actual = SliderSweep.ValueAt(values, slopes, times, Easing.InQuint, false, 0, 0.5f);

            Assert.That(actual, Is.EqualTo(expected).Within(1e-4));
            Assert.That(actual, Is.Not.EqualTo(50f).Within(1f));
        }

        [Test]
        public void SmoothHermiteMatchesGoldenValue()
        {
            // 3 nodes with curvature so Hermite differs from linear.
            float[] values = { 0f, 0f, 90f };
            double[] times = { 0.0, 100.0, 300.0 };
            var slopes = SliderSweep.ComputeSlopes(values, times);

            // Link 1 (node1 -> node2), smooth, t=0.5. Hand-computed cubic Hermite = 41.25
            // (linear midpoint would be 45), pinning the exact gameplay formula.
            float actual = SliderSweep.ValueAt(values, slopes, times, Easing.None, true, 1, 0.5f);
            Assert.That(actual, Is.EqualTo(41.25f).Within(1e-3));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~SliderSweepTest"`
Expected: FAIL to compile — `SliderSweep` does not exist.

- [ ] **Step 3: Create `SliderSweep`**

Create `Garbus.Game/Objects/Path/SliderSweep.cs`:

```csharp
// Shared per-link angle evaluation for slider paths: ease the value's progress across a segment,
// optionally cubic-Hermite (Catmull-Rom) smoothing it for a continuous sweep velocity through nodes.
// Used by both the gameplay body (DrawableSliderBody, values in radians) and the editor polyline
// (SliderPolylineVisual, values in degree-offsets) so the two representations cannot drift.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace Garbus.Game.Objects;

public static class SliderSweep
{
    /// <summary>Straight sub-segments used to approximate each link (arc/curve in value space).</summary>
    public const int SegmentsPerLink = 12;

    /// <summary>
    /// Catmull-Rom tangents (d value / d time) at each node: centred difference for interior nodes,
    /// one-sided at the ends (the Min/Max clamps collapse the difference to the single neighbour).
    /// </summary>
    public static float[] ComputeSlopes(IReadOnlyList<float> values, IReadOnlyList<double> times)
    {
        int count = values.Count;
        var slopes = new float[count];

        for (int n = 0; n < count; n++)
        {
            int lo = Math.Max(0, n - 1);
            int hi = Math.Min(count - 1, n + 1);
            double dt = times[hi] - times[lo];

            slopes[n] = dt > 0 ? (float)((values[hi] - values[lo]) / dt) : 0f;
        }

        return slopes;
    }

    /// <summary>
    /// The value at parameter <paramref name="t"/> (0..1) along <paramref name="link"/>
    /// (node[link] → node[link+1]). Easing reshapes the progress only; endpoints are preserved
    /// (ease(0)=0, ease(1)=1). Linear by default; cubic Hermite when <paramref name="linkSmooth"/>.
    /// </summary>
    public static float ValueAt(IReadOnlyList<float> values, IReadOnlyList<float> slopes, IReadOnlyList<double> times, Easing linkEasing, bool linkSmooth, int link, float t)
    {
        if (linkEasing != Easing.None)
            t = (float)Interpolation.ApplyEasing(linkEasing, t);

        float v0 = values[link];
        float v1 = values[link + 1];

        if (!linkSmooth)
            return v0 + (v1 - v0) * t;

        // Tangents are d value / d time; scale by the link duration to express them per unit of t.
        float h = (float)(times[link + 1] - times[link]);
        float m0 = slopes[link] * h;
        float m1 = slopes[link + 1] * h;

        float t2 = t * t;
        float t3 = t2 * t;

        // Cubic Hermite basis functions.
        float h00 = 2f * t3 - 3f * t2 + 1f;
        float h10 = t3 - 2f * t2 + t;
        float h01 = -2f * t3 + 3f * t2;
        float h11 = t3 - t2;

        return h00 * v0 + h10 * m0 + h01 * v1 + h11 * m1;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~SliderSweepTest"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Objects/Path/SliderSweep.cs Garbus.Game.Tests/SliderSweepTest.cs
git commit -m "feat: extract shared SliderSweep angle-evaluation helper"
```

---

### Task 2: Gameplay body delegates to `SliderSweep`

Pure refactor — `DrawableSliderBody` keeps identical behavior but computes slopes and per-link angle through `SliderSweep`. The existing gameplay/editor test suites are the safety net (no new test).

**Files:**
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderBody.cs`

**Interfaces:**
- Consumes: `SliderSweep.ComputeSlopes`, `SliderSweep.ValueAt`, `SliderSweep.SegmentsPerLink` (Task 1).
- Produces: no new public surface; `thetaAt` / `AngleDegAt` behavior unchanged.

- [ ] **Step 1: Point `segments_per_link` at the shared constant**

In `DrawableSliderBody.cs`, replace the local constant declaration (currently `private const int segments_per_link = 12;`) with:

```csharp
    // Straight sub-segments per link — shared with the editor polyline via SliderSweep.
    private const int segments_per_link = SliderSweep.SegmentsPerLink;
```

(This file declares `namespace Garbus.Game.Objects.Drawables`; `SliderSweep` is in the parent namespace `Garbus.Game.Objects`, so it is in scope as `SliderSweep` with no extra `using`.)

- [ ] **Step 2: Replace the slope loop in `rebuildNodes`**

In `rebuildNodes()`, replace the entire Catmull-Rom tangent loop (the `for (int n = 0; n < count; n++)` block that fills `nodeThetaSlopes`, including its comment) with:

```csharp
        // Catmull-Rom tangents for the smoothed-angle Hermite interpolation (shared with the editor).
        nodeThetaSlopes = SliderSweep.ComputeSlopes(nodeRadians, nodeTimes);
```

- [ ] **Step 3: Replace the body of `thetaAt` with a delegate call**

Replace the whole body of `private float thetaAt(int link, float t)` (everything after the doc comment, keeping the method signature) with:

```csharp
    private float thetaAt(int link, float t)
        => SliderSweep.ValueAt(nodeRadians, nodeThetaSlopes, nodeTimes, linkEasing[link], linkSmooth[link], link, t);
```

Delete the now-unused `lerp` helper only if nothing else references it. Search first:

Run: `grep -n "lerp(" Garbus.Game/Objects/Drawables/DrawableSliderBody.cs`
If `lerp` is still used elsewhere (e.g. in `pointAt`), keep it. If it has no remaining callers, delete its declaration `private static float lerp(float a, float b, float t) => a + (b - a) * t;`.

- [ ] **Step 4: Build and run the gameplay + editor suites**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~TestSceneComposerLifecycle|FullyQualifiedName~TestSceneComposeSelection|FullyQualifiedName~SliderSweepTest"`
Expected: PASS — the slider node-drag / sweep behavior is unchanged.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Objects/Drawables/DrawableSliderBody.cs
git commit -m "refactor: gameplay slider body delegates sweep math to SliderSweep"
```

---

### Task 3: Editor polyline subdivides each link through `SliderSweep`

`SliderPolylineVisual.computeVertices` becomes a subdivided polyline; node dots are split into a separate list so they stay on real nodes only.

**Files:**
- Modify: `Garbus.Game/Edit/Drawables/SliderPolylineVisual.cs`

**Interfaces:**
- Consumes: `SliderSweep.ComputeSlopes`, `SliderSweep.ValueAt`, `SliderSweep.SegmentsPerLink` (Task 1).
- Produces: no new public surface; the visual now renders eased/smoothed curves with dots at nodes.

- [ ] **Step 1: Add a node-positions field**

In `SliderPolylineVisual`, next to `private readonly List<Vector2> vertices = new List<Vector2>();`, add:

```csharp
    // One entry per real node (head + each control point) — where the dot markers go. Distinct from
    // `vertices`, which is the subdivided polyline fed to the SmoothPath.
    private readonly List<Vector2> nodePositions = new List<Vector2>();
```

- [ ] **Step 2: Replace `computeVertices` with subdivided geometry**

Replace the entire `computeVertices` method with a `buildGeometry` method that fills both the subdivided polyline and the node-position list. The file already has `using osu.Framework.Graphics;` (for `Easing`) and `using Garbus.Game.Objects;` (for `SliderSweep`), so no new imports are needed.

```csharp
    private void buildGeometry(float pxPerDeg, List<Vector2> polyline, List<Vector2> nodes)
    {
        double duration = slider.Duration;
        if (duration <= 0)
            return;

        float centreX = DrawWidth / 2;

        var controlPoints = slider.Path.ControlPoints;
        int count = 1 + controlPoints.Count;

        // Node value = angle offset in degrees (head = 0); node time = TimeOffset (head = 0).
        var values = new float[count];
        var times = new double[count];
        var linkEasing = new Easing[count - 1];
        var linkSmooth = new bool[count - 1];

        values[0] = 0f;
        times[0] = 0.0;

        for (int i = 0; i < controlPoints.Count; i++)
        {
            var cp = controlPoints[i];

            values[i + 1] = cp.RotationOffset;
            times[i + 1] = cp.TimeOffset;

            // A control point governs the segment leading into it: link[i] ends at node[i+1] = CP[i].
            linkEasing[i] = cp.SweepEasing;
            linkSmooth[i] = cp.Smooth;
        }

        var slopes = SliderSweep.ComputeSlopes(values, times);

        // Map an (angle-offset, time-offset) node/sub-point into editor space: x from angle, y from time
        // (head at the bottom = DrawHeight, later times rising). Time stays linear (matches gameplay).
        Vector2 toPoint(float angleOffset, double timeOffset)
            => new Vector2(centreX + angleOffset * pxPerDeg, DrawHeight * (float)(1 - timeOffset / duration));

        for (int n = 0; n < count; n++)
            nodes.Add(toPoint(values[n], times[n]));

        polyline.Add(toPoint(values[0], times[0]));

        for (int link = 0; link < count - 1; link++)
        {
            for (int k = 1; k <= SliderSweep.SegmentsPerLink; k++)
            {
                float t = (float)k / SliderSweep.SegmentsPerLink;
                float angle = SliderSweep.ValueAt(values, slopes, times, linkEasing[link], linkSmooth[link], link, t);
                double time = times[link] + (times[link + 1] - times[link]) * t;
                polyline.Add(toPoint(angle, time));
            }
        }
    }
```

- [ ] **Step 3: Update `Update()` to build both lists and store nodes**

In `Update()`, replace the block from `var newVertices = computeVertices(pxPerDeg);` through the `rebuildCopies(pxPerDeg);` call with:

```csharp
        var newVertices = new List<Vector2>();
        var newNodes = new List<Vector2>();
        buildGeometry(pxPerDeg, newVertices, newNodes);
        var newCopies = computeWrapCopies();

        if (vertexListEquals(newVertices) && wrapCopies.SequenceEqual(newCopies))
            return;

        vertices.Clear();
        vertices.AddRange(newVertices);
        nodePositions.Clear();
        nodePositions.AddRange(newNodes);
        wrapCopies.Clear();
        wrapCopies.AddRange(newCopies);

        rebuildCopies(pxPerDeg);
```

(The `vertexListEquals(newVertices)` early-out still compares the polyline; since nodes are a subset of what changes when the polyline changes, comparing the polyline alone is sufficient.)

- [ ] **Step 4: Pass node positions through `rebuildCopies` → `SetGeometry`**

In `rebuildCopies`, change the `SetGeometry` call to pass the node list:

```csharp
            copyPool[i].SetGeometry(vertices, nodePositions, -wrapCopies[i] * 360 * pxPerDeg);
```

In the nested `PathCopy` class, replace `SetGeometry` with a two-list version (path vertices for the line, node positions for the dots):

```csharp
        public void SetGeometry(IReadOnlyList<Vector2> pathVertices, IReadOnlyList<Vector2> nodePositions, float offsetX)
        {
            Alpha = 1;
            X = offsetX;

            path.Vertices = pathVertices;
            // Path auto-sizes to its vertex bounds; undo the bounding-box offset so vertex coordinates
            // land in our local space (same idiom as the gameplay DrawableSliderBody).
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);

            while (markers.Count > nodePositions.Count)
                markers.Remove(markers[^1], true);
            while (markers.Count < nodePositions.Count)
                markers.Add(new Circle { Size = new Vector2(10), Origin = Anchor.Centre });

            for (int i = 0; i < nodePositions.Count; i++)
                markers[i].Position = nodePositions[i];
        }
```

- [ ] **Step 5: Build and run the editor suite**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneComposeSelection|FullyQualifiedName~TestSceneComposePlacement|FullyQualifiedName~TestSceneComposerLifecycle|FullyQualifiedName~TestSceneEditorIntegration"`
Expected: PASS — polyline rebuild/wrap/selection behavior unchanged; dots still sit on nodes.

- [ ] **Step 6: Manual verification in the app**

Run: `dotnet run --project Garbus.Desktop`
Steps: open the editor → Compose → place a seam/multi-node slider → select it → in the right-toolbox Inspector set a node's Easing to `InQuint` (and toggle Smooth on another). Confirm the polyline between the affected nodes now bows into a curve (instead of a straight chord) and that the dots remain exactly on the nodes. Close the app.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Drawables/SliderPolylineVisual.cs
git commit -m "feat: render slider easing and smoothing in editor compose view"
```

---

## Full-suite gate (after Task 3)

- [ ] Run the whole headless suite to confirm nothing regressed:

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green.

## Out of scope (follow-up)

Overshoot easings (`Back`/`Elastic`/`Bounce`) can push the curve past a node's angle; the editor wrap-copy visibility range (`computeWrapCopies`, derived from node `RotationOffset` extremes only) does not account for that, so an overshoot near the seam could clip in its ghost twin. Tightening that range is a separate change.
