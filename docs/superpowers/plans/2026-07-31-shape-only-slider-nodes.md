# Shape-Only Slider Control Points Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a slider path control point be marked `ShapeOnly` so it shapes the body's sweep without being a judged node.

**Architecture:** The flag lives on `GarbusPathControlPoint`. `SliderBody.CreateNestedHitObjects` skips flagged points (no `SliderChild` → no judgement, hitsound, or gameplay nub, and the head-reference chain skips them for free), and `GetSegmentStartTime` walks back past them so the merged segment grades across the shaped sweep. Geometry (`AngleDegAt`, body rendering, `Duration`) is untouched. The last control point is never shape-only: the editor Inspector excludes final points from the toggle, node deletion auto-promotes the new final point, and the chart decoder rejects violating files.

**Tech Stack:** C# / .NET 8, osu-framework, System.Text.Json, NUnit (headless + visual test scenes).

**Spec:** `docs/superpowers/specs/2026-07-31-shape-only-slider-nodes-design.md`

## Global Constraints

- No version bump on the chart format; no compatibility layers (AGENTS.md experimental-project rule).
- Docs are present-tense, no historical framing.
- Test expectations are hand-derived and spec-anchored; no test may be a strict subset of a sibling; never assert bare styling values (colours, alphas, offsets) — assert relations instead.
- No new build or test warnings.
- New/reshaped visual elements ship with a Tuning scene (`Garbus.Game.Tests/Tuning/`).
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- Update the relevant domain doc (`docs/agents/*.md`) in the task that changes the behavior it describes.

---

### Task 1: `ShapeOnly` flag and nested-object skip

**Files:**
- Modify: `Garbus.Game/Objects/Path/GarbusPathControlPoint.cs`
- Modify: `Garbus.Game/Objects/SliderBody.cs` (CreateNestedHitObjects, ~line 108)
- Modify: `docs/rules-specs/Judgement.md` (Slider section, ~line 226)
- Test: `Garbus.Game.Tests/CompositeJudgementTest.cs`

**Interfaces:**
- Consumes: existing `SliderBody`, `SliderChild`, `GarbusPath` model.
- Produces: `GarbusPathControlPoint.ShapeOnly` (public bool field, default false) — every later task reads this exact member.

- [ ] **Step 1: Write the failing test**

Add to `CompositeJudgementTest` (the existing `createSlider(params double[] offsets)` helper builds a slider at StartTime 1000):

```csharp
[Test]
public void ShapeOnlyControlPointsSpawnNoChildren()
{
    var slider = createSlider(100, 300, 500);
    slider.Path.ControlPoints[1].ShapeOnly = true;
    slider.ApplyDefaults();

    var head = slider.NestedHitObjects.OfType<SliderHead>().Single();
    var children = slider.NestedHitObjects.OfType<SliderChild>().OrderBy(c => c.StartTime).ToArray();

    // Judged nodes are CP[0] (offset 100) and CP[2] (offset 500); CP[1] shapes only.
    Assert.That(children, Has.Length.EqualTo(2));
    Assert.That(children[0].StartTime, Is.EqualTo(1100));
    Assert.That(children[1].StartTime, Is.EqualTo(1500));

    // The head-reference chain skips the shape-only point.
    Assert.That(children[0].HeadReference, Is.SameAs(head));
    Assert.That(children[1].HeadReference, Is.SameAs(children[0]));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~ShapeOnlyControlPointsSpawnNoChildren`
Expected: FAIL — `'GarbusPathControlPoint' does not contain a definition for 'ShapeOnly'` (compile error counts as the red step; fix compilation by adding the field in Step 3 and the test then fails on `children` length 3 until the skip lands).

- [ ] **Step 3: Implement**

`GarbusPathControlPoint.cs` — add after `RotationOffset` (line 8):

```csharp
/// <summary>
/// Shapes the body's sweep without being a node: no <see cref="SliderChild"/> is created for this
/// point, so it yields no judgement, hitsound, or gameplay nub, and the body segment it sits in
/// merges into the segment ending at the next judged node. The last control point of a path is
/// never shape-only (the editor upholds this; the chart decoder rejects violations).
/// </summary>
public bool ShapeOnly;
```

`SliderBody.CreateNestedHitObjects` — skip flagged points:

```csharp
foreach (var controlPoint in Path.ControlPoints)
{
    // Shape-only points contribute geometry but are not nodes; previousNode advances only on
    // spawned children, so the head-reference chain skips them.
    if (controlPoint.ShapeOnly)
        continue;

    var childHitObject = new SliderChild(this, controlPoint, previousNode)
    {
        StartTime = StartTime + controlPoint.TimeOffset,
    };
    AddNested(childHitObject);
    previousNode = childHitObject;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~ShapeOnlyControlPointsSpawnNoChildren`
Expected: PASS

- [ ] **Step 5: Update `docs/rules-specs/Judgement.md`**

Replace the opening paragraph of `### Slider` (currently "Sliders are catch-timed at the head. A Slider has a head and zero or more children; …") with:

```markdown
Sliders are catch-timed at the head. A Slider's path is a sequence of control points, each either a
child-bearing point or **shape-only**. A **node** is the head or a non-shape-only control point; a
**child** is a node other than the head. **Every node yields exactly one Judgement**, so a Slider's
Judgement count always equals its node count: the head's Judgement is catch-timed (Perfect or Miss),
and each child's is the duration judgement of the body segment ending at that child, judged in the
**hold** family. A shape-only control point shapes the body's sweep but is not a node — it yields no
Judgement and no hitsound, and the body segment it sits in merges into the segment ending at the
next child. The last control point of a path is never shape-only. The body is the swept path
connecting the head through every control point (shape-only included) in time order. A head-only
Slider — no children — is judged by its head alone.
```

In the `#### Duration` subsection, after the paragraph beginning "Multi-child Sliders extend the duration rules…" (and its two bullets), add:

```markdown
A segment that spans shape-only control points is graded as one segment from its starting node to
its ending child: the activation requirement follows the swept angle through the shape-only points,
and the opening/ending grace windows apply at the segment's node endpoints only.
```

- [ ] **Step 6: Run the full suite, then commit**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS, no new warnings.

```bash
git add Garbus.Game/Objects/Path/GarbusPathControlPoint.cs Garbus.Game/Objects/SliderBody.cs Garbus.Game.Tests/CompositeJudgementTest.cs docs/rules-specs/Judgement.md
git commit -m "feat: shape-only slider control points spawn no child node"
```

---

### Task 2: Merged-segment timing and geometry pin

**Files:**
- Modify: `Garbus.Game/Objects/SliderBody.cs` (GetSegmentStartTime, ~line 41)
- Test: `Garbus.Game.Tests/CompositeJudgementTest.cs`

**Interfaces:**
- Consumes: `GarbusPathControlPoint.ShapeOnly` (Task 1).
- Produces: `SliderBody.GetSegmentStartTime(SliderChild)` now returns the previous **judged** node's absolute time; `DrawableSliderChild` needs no change (it already grades `[GetSegmentStartTime, StartTime]` sampling `AngleDegAt`).

- [ ] **Step 1: Write the failing test**

```csharp
[Test]
public void SegmentStartSkipsShapeOnlyPoints()
{
    var slider = createSlider(100, 300, 500, 700);
    slider.Path.ControlPoints[1].ShapeOnly = true;
    slider.Path.ControlPoints[2].ShapeOnly = true;
    slider.ApplyDefaults();

    var children = slider.NestedHitObjects.OfType<SliderChild>().OrderBy(c => c.StartTime).ToArray();

    // First segment: head (1000) → CP[0] (1100).
    Assert.That(slider.GetSegmentStartTime(children[0]), Is.EqualTo(1000));
    // Merged segment: CP[0] (1100) → CP[3] (1700), spanning both shape-only points.
    Assert.That(slider.GetSegmentStartTime(children[1]), Is.EqualTo(1100));
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~SegmentStartSkipsShapeOnlyPoints`
Expected: FAIL — second assert returns 1500 (CP[2]'s time) instead of 1100.

- [ ] **Step 3: Implement**

Replace the body of `GetSegmentStartTime`:

```csharp
public double GetSegmentStartTime(SliderChild child)
{
    // Node 0 is the head at StartTime; control point i is node i+1 at StartTime + TimeOffset.
    // IndexOf is by reference (each control point instance is unique), matching DrawableSliderBody.
    int index = Path.ControlPoints.IndexOf(child.ControlPoint) - 1;

    // Shape-only points are not nodes — the segment reaches back to the previous judged node.
    while (index >= 0 && Path.ControlPoints[index].ShapeOnly)
        index--;

    return index < 0 ? StartTime : StartTime + Path.ControlPoints[index].TimeOffset;
}
```

Update the method's XML doc summary to say "the judged node immediately preceding" instead of "the node immediately preceding".

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~SegmentStartSkipsShapeOnlyPoints`
Expected: PASS

- [ ] **Step 5: Add the geometry pin (expected to pass immediately)**

This is a regression pin, not red-green: it proves shape-only points stay in the swept geometry (the load-bearing claim that the player must still trace the shape). Hand-derivation: head 0° at t=1000, shape-only point +90° at t=1200, judged child 0° at t=1400; linear links (Easing.None, no smoothing) put the sweep at +45° halfway along each link.

```csharp
[Test]
public void ShapeOnlyPointsStillShapeTheSweep()
{
    var slider = createSlider(200, 400);
    slider.Path.ControlPoints[0].RotationOffset = 90;
    slider.Path.ControlPoints[0].ShapeOnly = true;
    slider.Path.ControlPoints[1].RotationOffset = 0;
    slider.ApplyDefaults();

    Assert.That(slider.AngleDegAt(1100), Is.EqualTo(45f).Within(0.001f));
    Assert.That(slider.AngleDegAt(1200), Is.EqualTo(90f).Within(0.001f));
    Assert.That(slider.AngleDegAt(1300), Is.EqualTo(45f).Within(0.001f));
}
```

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~ShapeOnlyPointsStillShapeTheSweep`
Expected: PASS with no production change. If it fails, geometry was accidentally filtered — stop and fix.

- [ ] **Step 6: Run the full suite, then commit**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS.

```bash
git add Garbus.Game/Objects/SliderBody.cs Garbus.Game.Tests/CompositeJudgementTest.cs
git commit -m "feat: merged slider segments grade across shape-only points"
```

---

### Task 3: Serialization and decode validation

**Files:**
- Modify: `Garbus.Game/Charts/Format/ChartFileDto.cs` (`PathControlPointDto`, ~line 101)
- Modify: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs` (toDto ~line 160, fromDto ~line 224)
- Modify: `docs/agents/charts.md`
- Test: `Garbus.Game.Tests/TestChartFormat.cs`

**Interfaces:**
- Consumes: `GarbusPathControlPoint.ShapeOnly` (Task 1).
- Produces: `shapeOnly` JSON property on slider control points; `InvalidDataException` from any decode path (chart file, song file, clipboard) when a slider's last control point is shape-only. The song serializer bridges hit objects through `GarbusChartSerializer`, so this one mapping covers all paths.

- [ ] **Step 1: Write the failing tests**

Add to `TestChartFormat`. These use the clipboard codec (`EncodeHitObjects`/`DecodeHitObjects`) — a distinct path from the generator-based `TestRoundtrip`, and encode-side is deliberately unvalidated so an in-memory invalid slider can reach the decoder:

```csharp
[Test]
public void ShapeOnlyControlPointRoundtrips()
{
    var slider = new SliderBody
    {
        StartTime = 1000, AngleDeg = 0, Side = HorizontalDirection.Right,
        Path = new GarbusPath
        {
            ControlPoints = new BindableList<GarbusPathControlPoint>([
                new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45, ShapeOnly = true },
                new GarbusPathControlPoint { TimeOffset = 1000, RotationOffset = 90 },
            ]),
        },
    };

    var decoded = (SliderBody)GarbusChartSerializer.DecodeHitObjects(
        GarbusChartSerializer.EncodeHitObjects(new[] { slider }))[0];

    Assert.That(decoded.Path.ControlPoints[0].ShapeOnly, Is.True);
    Assert.That(decoded.Path.ControlPoints[1].ShapeOnly, Is.False);
}

[Test]
public void TrailingShapeOnlyControlPointRejected()
{
    var slider = new SliderBody
    {
        StartTime = 1000, AngleDeg = 0, Side = HorizontalDirection.Left,
        Path = new GarbusPath
        {
            ControlPoints = new BindableList<GarbusPathControlPoint>([
                new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 },
                new GarbusPathControlPoint { TimeOffset = 1000, RotationOffset = 90, ShapeOnly = true },
            ]),
        },
    };

    string json = GarbusChartSerializer.EncodeHitObjects(new[] { slider });

    Assert.Throws<InvalidDataException>(() => GarbusChartSerializer.DecodeHitObjects(json));
}
```

Required usings already present in the file except `osu.Framework.Bindables` — add it.

Also extend the slider case of `assertChartsEqual`'s control-point loop (~line 225) with:

```csharp
Assert.That(ac.ShapeOnly, Is.EqualTo(ec.ShapeOnly));
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~ShapeOnlyControlPoint`
Expected: FAIL — compile error until the DTO field exists; after Step 3's DTO-only portion, `ShapeOnlyControlPointRoundtrips` fails on `decoded…ShapeOnly == false` until the mapping lands, and `TrailingShapeOnlyControlPointRejected` fails (no exception) until the validation lands.

- [ ] **Step 3: Implement**

`ChartFileDto.cs` — in `PathControlPointDto`, after `Smooth` (line 103):

```csharp
public bool ShapeOnly { get; set; }
```

`GarbusChartSerializer.cs` — encode side, in the `SliderBody slider =>` arm's control-point projection, add:

```csharp
ShapeOnly = c.ShapeOnly,
```

Decode side: replace the `SliderBodyDto slider => new SliderBody { … }` arm with `SliderBodyDto slider => decodeSlider(slider),` and add the private method (keeping the exact existing field mapping plus the new field):

```csharp
private static SliderBody decodeSlider(SliderBodyDto dto)
{
    // The last control point is never shape-only — a trailing one would leave the body's tail
    // ungraded. The editor upholds this, so a violation means a hand-edited file.
    if (dto.ControlPoints.Count > 0 && dto.ControlPoints[^1].ShapeOnly)
        throw new InvalidDataException("A slider's last control point cannot be shape-only.");

    return new SliderBody
    {
        AngleDeg = dto.AngleDeg,
        Side = parseEnum<HorizontalDirection>(dto.Side),
        Path = new GarbusPath
        {
            ControlPoints = new BindableList<GarbusPathControlPoint>(dto.ControlPoints.Select(c => new GarbusPathControlPoint
            {
                TimeOffset = c.TimeOffset,
                RotationOffset = c.RotationOffset,
                Smooth = c.Smooth,
                SweepEasing = parseEnum<Easing>(c.SweepEasing),
                ShapeOnly = c.ShapeOnly,
            })),
        },
    };
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~ShapeOnlyControlPoint`
Expected: PASS (both).

- [ ] **Step 5: Update `docs/agents/charts.md`**

In the **Format invariants** list, append a bullet:

```markdown
- Slider control points carry a `shapeOnly` flag (default false): a shape-only point shapes the
  body's sweep but spawns no judged child. **A slider's last control point is never shape-only** —
  the decoder rejects violating files on every path (chart, song, clipboard).
```

- [ ] **Step 6: Run the full suite, then commit**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS.

```bash
git add Garbus.Game/Charts/Format/ChartFileDto.cs Garbus.Game/Charts/Format/GarbusChartSerializer.cs Garbus.Game.Tests/TestChartFormat.cs docs/agents/charts.md
git commit -m "feat: serialize shapeOnly control points, reject trailing shape-only on decode"
```

---

### Task 4: Bundled test chart carries a shape-only point

**Files:**
- Modify: `Garbus.Game/Charts/GarbusTestChartGenerator.cs` (~line 45)
- Regenerate: `Garbus.Resources/Charts/test-chart.garbus`
- Possibly modify: any test whose pinned expectation depends on the bundled chart's slider child count

**Interfaces:**
- Consumes: `shapeOnly` serialization (Task 3).
- Produces: a bundled chart exercising the field end-to-end (`TestRoundtrip` and `TestBundledChartMatchesGenerator` then cover it via the extended `assertChartsEqual`).

- [ ] **Step 1: Mark one generator control point shape-only**

In the **first** slider (StartTime 2000, Side Right), mark the middle control point:

```csharp
new GarbusPathControlPoint()
{
    RotationOffset = 90, TimeOffset = 2000, SweepEasing = Easing.None, ShapeOnly = true
},
```

- [ ] **Step 2: Regenerate the bundled chart**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~RegenerateBundledTestChart`
Expected: PASS, prints the written path.

- [ ] **Step 3: Run the full suite and reconcile pins**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: `TestRoundtrip` / `TestBundledChartMatchesGenerator` PASS. Any other failure means a test pinned an expectation derived from the bundled chart's node count (candidates: gameplay scenes, the editor mini-preview pooling pins). For each: re-derive the expected value by hand from the new chart content (that slider now has 2 judged children, not 3) and update the pin with a comment tracing the derivation. Do **not** revert the generator change to dodge a failure; if a pin cannot be hand-re-derived, stop and flag it.

- [ ] **Step 4: Commit**

```bash
git add Garbus.Game/Charts/GarbusTestChartGenerator.cs Garbus.Resources/Charts/test-chart.garbus
git commit -m "chore: bundled test chart exercises a shape-only control point"
```

(Include any reconciled test files in the same commit.)

---

### Task 5: Inspector "Shape only" checkbox and merge propagation

**Files:**
- Modify: `Garbus.Game/Edit/Inspector.cs` (per-node controls ~line 302; mergeSliders ~line 421)
- Modify: `docs/agents/editor.md`
- Test: `Garbus.Game.Tests/Editor/InspectorShapeOnlyEligibilityTest.cs` (create)

**Interfaces:**
- Consumes: `GarbusPathControlPoint.ShapeOnly`; existing `MultiValue.Aggregate`, `addMultiValueCheckbox(string, MultiValue<bool>, Action<bool>)`, `editorChart.Update(SliderBody)`, `changeHandler` transaction pattern (all already used by the Smoothing checkbox).
- Produces: `Inspector.ShapeOnlyEligible(IReadOnlyCollection<GarbusPathControlPoint> nodes, IEnumerable<SliderBody> sliders)` — internal static, returns the nodes eligible for the toggle (everything except each slider's final control point). Task 6's invariant reasoning assumes the Inspector can never set a final point shape-only.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/InspectorShapeOnlyEligibilityTest.cs` (plain NUnit, no host):

```csharp
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public class InspectorShapeOnlyEligibilityTest
{
    [Test]
    public void FinalControlPointOfEachSliderIsExcluded()
    {
        var sliderA = makeSlider(100, 200, 300);
        var sliderB = makeSlider(150, 250);

        // Select every node of both sliders.
        var nodes = sliderA.Path.ControlPoints.Concat(sliderB.Path.ControlPoints).ToArray();

        var eligible = Inspector.ShapeOnlyEligible(nodes, new[] { sliderA, sliderB });

        // Each slider's final point (offsets 300 and 250) is excluded; all others remain.
        Assert.That(eligible, Is.EquivalentTo(new[]
        {
            sliderA.Path.ControlPoints[0], sliderA.Path.ControlPoints[1],
            sliderB.Path.ControlPoints[0],
        }));
    }

    private static SliderBody makeSlider(params double[] offsets)
        => new()
        {
            StartTime = 1000,
            AngleDeg = 0,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>(
                    offsets.Select(o => new GarbusPathControlPoint { TimeOffset = o }).ToList()),
            },
        };
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~FinalControlPointOfEachSliderIsExcluded`
Expected: FAIL — `Inspector` has no `ShapeOnlyEligible`. (If `Inspector` is not visible to the test project, make the class `public` or add the method on whichever containing type is already accessible; keep the method `internal` + `InternalsVisibleTo` only if that mechanism is already in use in this repo — otherwise `public static` on `Inspector`.)

- [ ] **Step 3: Implement**

In `Inspector.cs`, add the helper (near the other static helpers such as `timeRangesDisjoint`):

```csharp
/// <summary>
/// The selected nodes eligible for the Shape-only toggle: every node except its owning slider's
/// final control point, which is never shape-only.
/// </summary>
public static GarbusPathControlPoint[] ShapeOnlyEligible(
    IReadOnlyCollection<GarbusPathControlPoint> nodes, IEnumerable<SliderBody> sliders)
{
    var finals = sliders.Where(s => s.Path.ControlPoints.Count > 0)
                        .Select(s => s.Path.ControlPoints[^1])
                        .ToHashSet();
    return nodes.Where(n => !finals.Contains(n)).ToArray();
}
```

Then in the per-node controls block (directly after the existing Smoothing checkbox, inside `if (selectedNodes.Count > 0)` where `nodes` and `affectedSliders` are already in scope):

```csharp
// Shape-only: a shape-only point shapes the sweep without being judged. Each slider's final
// point is excluded (never shape-only), so select-all-then-toggle keeps the invariant.
var eligibleNodes = ShapeOnlyEligible(nodes, affectedSliders);

if (eligibleNodes.Length > 0)
{
    var shapeOnlyState = MultiValue.Aggregate(eligibleNodes, n => n.ShapeOnly);

    addMultiValueCheckbox("Shape only", shapeOnlyState, value =>
    {
        changeHandler?.BeginChange();
        foreach (var n in eligibleNodes) n.ShapeOnly = value;
        foreach (var s in affectedSliders) editorChart.Update(s);
        changeHandler?.EndChange();
    });
}
```

And in `mergeSliders`, the reparenting loop that copies `Smooth` / `SweepEasing` (~line 427) gains:

```csharp
ShapeOnly = cp.ShapeOnly,
```

(The joined slider's head becomes a new control point with the segment defaults — it stays judged, which is correct: it was a judged head.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~FinalControlPointOfEachSliderIsExcluded`
Expected: PASS

- [ ] **Step 5: Update `docs/agents/editor.md`**

In the section describing the Inspector's per-node slider controls (Easing/Smoothing), add one sentence:

```markdown
A "Shape only" checkbox sits alongside them; it applies to every selected node except each
slider's final control point (never shape-only), aggregating its tri-state over eligible nodes only.
Merging sliders preserves the flag.
```

- [ ] **Step 6: Run the full suite, then commit**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS.

```bash
git add Garbus.Game/Edit/Inspector.cs Garbus.Game.Tests/Editor/InspectorShapeOnlyEligibilityTest.cs docs/agents/editor.md
git commit -m "feat: editor Shape only toggle on slider nodes, final point excluded"
```

---

### Task 6: Node deletion auto-promotes the new final point

**Files:**
- Modify: `Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs` (`removeNodes` ~line 708, `removeSelection` head path ~line 766)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`

**Interfaces:**
- Consumes: `GarbusPathControlPoint.ShapeOnly`; existing blueprint delete flow (`removeNodes`, `removeSelection`) and the compose test harness (`nodeHandleScreen(i)`, `input.Key(Key.Delete)`, editorChart-direct setup steps).
- Produces: the invariant "last control point is never shape-only" survives every editor delete path (Delete key, head-promotion delete, Shift+RightClick quick delete — all route through these two methods).

- [ ] **Step 1: Write the failing test**

Add to `TestSceneComposeSelection`, following the file's existing pattern of adding a slider directly to `editorChart` and parking the clock (mirror the "add 315° slider with two offset-0 nodes + park clock" setup step and the `nodeHandleScreen` helper):

```csharp
[Test]
public void TestDeletingFinalNodePromotesShapeOnlySurvivor()
{
    waitForComposer();

    SliderBody slider = null!;

    AddStep("add slider with shape-only middle node + park clock", () =>
    {
        slider = new SliderBody
        {
            StartTime = 1000, AngleDeg = 0, Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>([
                    new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 30 },
                    new GarbusPathControlPoint { TimeOffset = 1000, RotationOffset = 60, ShapeOnly = true },
                    new GarbusPathControlPoint { TimeOffset = 1500, RotationOffset = 90 },
                ]),
            },
        };
        editorChart.Add(slider);
        editorClock.Stop();
        editorClock.Seek(slider.StartTime);
    });

    AddStep("select slider", () => composer.SelectionHandler.HandleSelected(
        composer.SelectionBlueprints.Single(b => b.Item == slider)));

    AddStep("click final node handle", () =>
    {
        input.MoveMouseTo(nodeHandleScreen(2));
        input.Click(MouseButton.Left);
    });

    AddStep("press delete", () => input.Key(Key.Delete));

    AddAssert("two control points remain", () => slider.Path.ControlPoints.Count == 2);
    AddAssert("new final point promoted to judged", () => !slider.Path.ControlPoints[^1].ShapeOnly);
}
```

Adapt the selection step to whatever idiom the sibling tests actually use to get a slider selected (e.g. clicking the polyline midpoint via the file's `selectSliderByLine`-style helper) — the assertion steps are the contract.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~TestDeletingFinalNodePromotesShapeOnlySurvivor`
Expected: FAIL on "new final point promoted to judged" — the surviving CP (offset 1000) still has `ShapeOnly == true`.

- [ ] **Step 3: Implement**

In `removeNodes`, after the removal loop and before `editorChart.Update(HitObject)`:

```csharp
foreach (var cp in nodes)
    controlPoints.Remove(cp);

// The last control point is never shape-only; deleting the final judged node promotes the new
// final point so the invariant survives every delete.
if (controlPoints.Count > 0)
    controlPoints[^1].ShapeOnly = false;

editorChart.Update(HitObject);
```

In `removeSelection`'s head-selected path, after the head-promotion rebasing loop (`cp.TimeOffset -= deltaTime; …`) and before `editorChart.Update(HitObject)`, add the same guard:

```csharp
// Same invariant guard as removeNodes: the dropped selection may have included the final point.
if (controlPoints.Count > 0)
    controlPoints[^1].ShapeOnly = false;
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~TestDeletingFinalNodePromotesShapeOnlySurvivor`
Expected: PASS

- [ ] **Step 5: Run the full suite, then commit**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS.

```bash
git add Garbus.Game/Edit/Blueprints/SliderSelectionBlueprint.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: node deletion auto-promotes a trailing shape-only point to judged"
```

---

### Task 7: Compose-view marker distinction + Tuning scene

**Files:**
- Create: `Garbus.Game/Edit/Drawables/SliderNodeMarker.cs`
- Modify: `Garbus.Game/Edit/Drawables/SliderPolylineVisual.cs` (PathCopy markers, ~lines 106–146; buildGeometry caller, ~line 148)
- Create: `Garbus.Game.Tests/Tuning/TestSceneSliderNodeMarkerTuning.cs`
- Modify: `docs/agents/editor.md`
- Test: `Garbus.Game.Tests/Editor/SliderNodeMarkerTest.cs` (create)

**Interfaces:**
- Consumes: `GarbusPathControlPoint.ShapeOnly`; `EditorSliderPolyline.Build` (unchanged — node order is head then control points in order, so flags are built alongside, not inside it).
- Produces: `SliderNodeMarker : CircularContainer` (public, in `Garbus.Game.Edit.Drawables`) with `public bool ShapeOnly { set; }` — filled dot when false, hollow ring when true. `SliderSelectionBlueprint` node handles are deliberately unchanged.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/SliderNodeMarkerTest.cs`. Styling is asserted as a **relation** (filled vs hollow), never as bare values:

```csharp
using Garbus.Game.Edit.Drawables;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public class SliderNodeMarkerTest
{
    [Test]
    public void ShapeOnlyMarkerIsHollowJudgedMarkerIsFilled()
    {
        var judged = new SliderNodeMarker { ShapeOnly = false };
        var shapeOnly = new SliderNodeMarker { ShapeOnly = true };

        // Relation, not absolute styling: the judged marker's fill is visible and the shape-only
        // marker's is not, while only the shape-only marker carries a border ring.
        Assert.That(judged.FillVisible, Is.True);
        Assert.That(shapeOnly.FillVisible, Is.False);
        Assert.That(shapeOnly.BorderThickness, Is.GreaterThan(judged.BorderThickness));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~ShapeOnlyMarkerIsHollow`
Expected: FAIL — `SliderNodeMarker` does not exist.

- [ ] **Step 3: Implement the marker**

`Garbus.Game/Edit/Drawables/SliderNodeMarker.cs`:

```csharp
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// A node dot on the editor slider polyline: a filled circle for a judged node, a hollow ring for a
/// shape-only control point — the fill/ring distinction is the compose view's only judged-vs-shape
/// signal, since gameplay renders shape-only points seamlessly.
/// </summary>
public partial class SliderNodeMarker : CircularContainer
{
    private readonly Box fill;

    /// <summary>Whether the fill box is currently visible (test seam for the filled/hollow relation).</summary>
    public bool FillVisible => fill.Alpha > 0;

    public SliderNodeMarker()
    {
        Size = new Vector2(10);
        Origin = Anchor.Centre;
        Masking = true;
        BorderColour = Colour4.White;
        // AlwaysPresent keeps the masked content drawn when the fill is invisible, so the border
        // ring still renders on hollow markers.
        InternalChild = fill = new Box { RelativeSizeAxes = Axes.Both, AlwaysPresent = true };
    }

    public bool ShapeOnly
    {
        set
        {
            BorderThickness = value ? 2.5f : 0;
            fill.Alpha = value ? 0 : 1;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter Name~ShapeOnlyMarkerIsHollow`
Expected: PASS

- [ ] **Step 5: Wire it into `SliderPolylineVisual`**

- Add a flags list beside `nodePositions`: `private readonly List<bool> nodeShapeFlags = new List<bool>();`
- In `Update()`, build the new flags next to `newNodes` (node order matches `EditorSliderPolyline.Build`: head first, then each control point):

```csharp
var newFlags = new List<bool>(1 + slider.Path.ControlPoints.Count) { false }; // head is always judged
foreach (var cp in slider.Path.ControlPoints)
    newFlags.Add(cp.ShapeOnly);
```

- Include flags in the early-out (a toggle alone must rebuild): extend the `if (vertexListEquals(newVertices) && wrapCopies.SequenceEqual(newCopies))` condition with `&& nodeShapeFlags.SequenceEqual(newFlags)`, and copy `newFlags` into `nodeShapeFlags` beside the other list copies.
- Change `PathCopy.SetGeometry` to `SetGeometry(IReadOnlyList<Vector2> pathVertices, IReadOnlyList<Vector2> nodePositions, IReadOnlyList<bool> nodeShapeFlags, float offsetX)`; replace `Container<Circle> markers` with `Container<SliderNodeMarker> markers`, `new Circle { Size = new Vector2(10), Origin = Anchor.Centre }` with `new SliderNodeMarker()`, and set both position and flag in the final loop:

```csharp
for (int i = 0; i < nodePositions.Count; i++)
{
    markers[i].Position = nodePositions[i];
    markers[i].ShapeOnly = nodeShapeFlags[i];
}
```

- Update the `rebuildCopies` call site: `copyPool[i].SetGeometry(vertices, nodePositions, nodeShapeFlags, -wrapCopies[i] * 360 * pxPerDeg);`

- [ ] **Step 6: Create the Tuning scene**

`Garbus.Game.Tests/Tuning/TestSceneSliderNodeMarkerTuning.cs` — follow the structure of `TestSceneSliderGlowTuning` (same namespace/base class). Content: a horizontal row of markers alternating judged/shape-only against a dark background, with live controls:

```csharp
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Garbus.Game.Tests.Tuning;

[TestFixture]
public partial class TestSceneSliderNodeMarkerTuning : GarbusTestScene
{
    private FillFlowContainer<SliderNodeMarker> row = null!;

    [SetUp]
    public void SetUp() => Schedule(() =>
    {
        Child = row = new FillFlowContainer<SliderNodeMarker>
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Spacing = new Vector2(30),
        };

        for (int i = 0; i < 6; i++)
            row.Add(new SliderNodeMarker { ShapeOnly = i % 2 == 1, Anchor = Anchor.CentreLeft, Origin = Anchor.Centre });
    });

    [Test]
    public void TuneMarkers()
    {
        AddSliderStep("marker size", 4f, 24f, 10f, size =>
        {
            if (row.IsNotNull())
                foreach (var m in row) m.Size = new Vector2(size);
        });

        AddSliderStep("ring thickness", 1f, 6f, 2.5f, thickness =>
        {
            if (row.IsNotNull())
                foreach (var m in row)
                {
                    if (m.BorderThickness > 0) m.BorderThickness = thickness;
                }
        });
    }
}
```

(Adjust the null-guard idiom to whatever `TestSceneSliderGlowTuning` uses; the scene must run headless without assertions failing and expose the two slider steps in the visual browser.)

- [ ] **Step 7: Update `docs/agents/editor.md`**

In the compose/mini-preview visuals section describing `SliderPolylineVisual`, amend the node-dot sentence:

```markdown
Node dots are `SliderNodeMarker`s: filled for judged nodes, hollow rings for shape-only control
points (tuned in `TestSceneSliderNodeMarkerTuning`). Gameplay renders shape-only points seamlessly —
the compose dot is the only place the distinction is visible.
```

- [ ] **Step 8: Run the full suite, then commit**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS, no new warnings.

```bash
git add Garbus.Game/Edit/Drawables/SliderNodeMarker.cs Garbus.Game/Edit/Drawables/SliderPolylineVisual.cs Garbus.Game.Tests/Tuning/TestSceneSliderNodeMarkerTuning.cs Garbus.Game.Tests/Editor/SliderNodeMarkerTest.cs docs/agents/editor.md
git commit -m "feat: hollow compose markers for shape-only slider points, with tuning scene"
```

---

## Verification (whole feature)

- `dotnet build Garbus.Desktop.slnf` — warning-clean.
- `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` — full suite green.
- `docs/rules-specs/Judgement.md`, `docs/agents/charts.md`, `docs/agents/editor.md` updated (Tasks 1, 3, 5, 7).
- Spec cross-check: model/judgement (Tasks 1–2), gameplay presentation (falls out of Task 1), serialization & validation (Tasks 3–4), editor toggle/markers/delete-guard/merge (Tasks 5–7), testing (every task).
