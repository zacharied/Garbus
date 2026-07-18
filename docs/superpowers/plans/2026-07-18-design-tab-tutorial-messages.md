# Design Tab — Design Points & Tutorial Messages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Design editor tab (between Timing and Verify) that authors time-ranged design points — starting with the TutorialMessage effect — and renders those effects during gameplay.

**Architecture:** Design points are a first-class peer of timing points: a sorted `DesignPointInfo` container with a single `DesignPointsChanged` event and structural moves. The whole feature threads through the existing chart model → serializer → whole-chart-JSON undo/redo → editor tab (mirroring the Timing tab) → gameplay overlay. Only `TutorialMessage` is concrete; a `"type"` discriminator keeps the format extensible.

**Tech Stack:** C# / .NET 8, osu-framework (no osu.Game), System.Text.Json, NUnit visual+headless test scenes.

## Global Constraints

- Build: `dotnet build Garbus.Desktop.slnf`
- Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` (add `--filter "FullyQualifiedName~<Name>"` to scope)
- Nullability is enabled solution-wide; DI/BDL fields use `= null!`.
- Terminology: "chart" not "beatmap"; `Garbus*`/`Design*` prefixes.
- No format version bump (`GarbusChartSerializer.CURRENT_VERSION` stays `1`) — backwards compat never matters (CLAUDE.md).
- Do NOT increment any version numbers or add compatibility layers.
- The JSON `"type"` discriminator must remain the FIRST property of each polymorphic object — System.Text.Json writes it first automatically; keep it first when hand-editing.
- Overlay opacity is a fixed constant (`TutorialMessage.OVERLAY_OPACITY = 0.6f`), never authored.
- In this environment, commits go through the `mcp__nimbalyst__developer_git_commit_proposal` tool (stages + commits atomically). The `git commit` lines below describe intent; use the tool.

---

## File Structure

**New source files**
- `Garbus.Game/Charts/Design/DesignPoint.cs` — abstract base (StartTime/EndTime bindables).
- `Garbus.Game/Charts/Design/TutorialMessage.cs` — concrete effect (Text bindable + opacity const).
- `Garbus.Game/Charts/Design/DesignPointInfo.cs` — sorted container + `DesignPointsChanged` + `MoveDesignPoint`.
- `Garbus.Game/Edit/Screens/DesignTab.cs` — the tab shell (mirrors `TimingTab`).
- `Garbus.Game/Edit/Screens/Design/DesignPointList.cs` — left pane list (mirrors `TimingPointList`).
- `Garbus.Game/Edit/Screens/Design/DesignPointSettings.cs` — right pane editor.
- `Garbus.Game/Edit/Screens/Timeline/TimelineDesignRegionDisplay.cs` — translucent region bands.
- `Garbus.Game/Screens/DesignOverlay.cs` — gameplay effect renderer.

**Modified source files**
- `Garbus.Game/Charts/GarbusChart.cs` — add `DesignPointInfo`.
- `Garbus.Game/Charts/Format/ChartFileDto.cs` — add `DesignPoints` list + DTOs.
- `Garbus.Game/Charts/Format/GarbusChartSerializer.cs` — map design points to/from DTO.
- `Garbus.Game/Edit/EditorChart.cs` — expose `DesignPointInfo`.
- `Garbus.Game/Edit/GarbusChartChangeHandler.cs` — rebuild design points in `ApplyStateChange`.
- `Garbus.Game/Edit/Screens/EditorTab.cs` — insert `Design`.
- `Garbus.Game/Edit/Screens/GarbusEditor.cs` — DI-cache `DesignPointInfo`; wire the tab; View toggle.
- `Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs` — add the region layer.
- `Garbus.Game/Configuration/GarbusSetting.cs` + `GarbusConfigManager.cs` — `EditorShowDesignRegions`.
- `Garbus.Game/Screens/PlayScreen.cs` — host `DesignOverlay` under the gameplay clock.

**Test files**
- `Garbus.Game.Tests/Charts/TestDesignPointInfo.cs` (new)
- `Garbus.Game.Tests/TestChartFormat.cs` (add tests)
- `Garbus.Game.Tests/Editor/TestChangeHandler.cs` (add test)
- `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs` (new)
- `Garbus.Game.Tests/Visual/TestSceneDesignOverlay.cs` (new)

---

## Task 1: Design point model + sorted container

**Files:**
- Create: `Garbus.Game/Charts/Design/DesignPoint.cs`
- Create: `Garbus.Game/Charts/Design/TutorialMessage.cs`
- Create: `Garbus.Game/Charts/Design/DesignPointInfo.cs`
- Modify: `Garbus.Game/Charts/GarbusChart.cs`
- Test: `Garbus.Game.Tests/Charts/TestDesignPointInfo.cs`

**Interfaces:**
- Produces:
  - `abstract class DesignPoint` with `BindableDouble StartTimeBindable`, `BindableDouble EndTimeBindable`, `double StartTime`, `double EndTime`.
  - `class TutorialMessage : DesignPoint` with `Bindable<string> TextBindable`, `string Text`, `const float OVERLAY_OPACITY = 0.6f`.
  - `class DesignPointInfo` with `IReadOnlyList<DesignPoint> DesignPoints`, `event Action DesignPointsChanged`, `void Add(DesignPoint)`, `void Remove(DesignPoint)`, `void Clear()`, `void MoveDesignPoint(DesignPoint, double newStart, double newEnd)`. Points are kept sorted by `StartTime` (stable append at ties).
  - `GarbusChart.DesignPointInfo` (`{ get; init; }`).

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Charts/TestDesignPointInfo.cs`:

```csharp
// Model tests for DesignPointInfo: sorted insertion, structural moves, and the single change event.
// Plain NUnit — no game host required.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts.Design;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts
{
    [TestFixture]
    public class TestDesignPointInfo
    {
        [Test]
        public void TestAddKeepsSortedOrder()
        {
            var info = new DesignPointInfo();
            info.Add(new TutorialMessage { StartTime = 3000, EndTime = 4000 });
            info.Add(new TutorialMessage { StartTime = 1000, EndTime = 2000 });
            info.Add(new TutorialMessage { StartTime = 2000, EndTime = 2500 });

            Assert.That(info.DesignPoints.Select(p => p.StartTime), Is.EqualTo(new[] { 1000.0, 2000.0, 3000.0 }));
        }

        [Test]
        public void TestAddRaisesChangeEvent()
        {
            var info = new DesignPointInfo();
            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            info.Add(new TutorialMessage { StartTime = 0, EndTime = 100 });

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void TestMoveReordersAndRaisesEvent()
        {
            var info = new DesignPointInfo();
            var a = new TutorialMessage { StartTime = 1000, EndTime = 1500 };
            var b = new TutorialMessage { StartTime = 2000, EndTime = 2500 };
            info.Add(a);
            info.Add(b);

            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            info.MoveDesignPoint(a, 3000, 3500);

            Assert.That(raised, Is.EqualTo(1));
            Assert.That(a.StartTime, Is.EqualTo(3000));
            Assert.That(a.EndTime, Is.EqualTo(3500));
            Assert.That(info.DesignPoints.Select(p => p.StartTime), Is.EqualTo(new[] { 2000.0, 3000.0 }));
        }

        [Test]
        public void TestTextSetDoesNotRaiseOrReorder()
        {
            var info = new DesignPointInfo();
            var a = new TutorialMessage { StartTime = 1000, EndTime = 1500, Text = "old" };
            info.Add(a);

            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            a.Text = "new"; // in-place edit of an effect parameter, not a structural change

            Assert.That(raised, Is.EqualTo(0));
            Assert.That(info.DesignPoints.Single().StartTime, Is.EqualTo(1000));
        }

        [Test]
        public void TestRemoveAndClearRaiseEvent()
        {
            var info = new DesignPointInfo();
            var a = new TutorialMessage { StartTime = 0, EndTime = 100 };
            info.Add(a);

            int raised = 0;
            info.DesignPointsChanged += () => raised++;

            info.Remove(a);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(info.DesignPoints, Is.Empty);

            info.Add(new TutorialMessage { StartTime = 0, EndTime = 100 });
            raised = 0;
            info.Clear();
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(info.DesignPoints, Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestDesignPointInfo"`
Expected: FAIL — compile error, `DesignPoint`/`TutorialMessage`/`DesignPointInfo` do not exist.

- [ ] **Step 3: Create `DesignPoint.cs`**

```csharp
// Base for a time-ranged visual effect applied to the chart during gameplay. Concrete subclasses
// (e.g. TutorialMessage) add their own effect parameters. StartTime/EndTime are bindables so the
// editor's list rows and settings pane react to edits and auto-unbind on disposal.

using osu.Framework.Bindables;

namespace Garbus.Game.Charts.Design
{
    public abstract class DesignPoint
    {
        public readonly BindableDouble StartTimeBindable = new BindableDouble();
        public readonly BindableDouble EndTimeBindable = new BindableDouble();

        public double StartTime
        {
            get => StartTimeBindable.Value;
            set => StartTimeBindable.Value = value;
        }

        public double EndTime
        {
            get => EndTimeBindable.Value;
            set => EndTimeBindable.Value = value;
        }
    }
}
```

- [ ] **Step 4: Create `TutorialMessage.cs`**

```csharp
// A design point that dims the gameplay screen with a translucent black overlay and shows a text
// message while active. The reference (first) concrete design-point effect. Overlay opacity is a
// fixed constant, not an authored value.

using osu.Framework.Bindables;

namespace Garbus.Game.Charts.Design
{
    public class TutorialMessage : DesignPoint
    {
        public const float OVERLAY_OPACITY = 0.6f;

        public readonly Bindable<string> TextBindable = new Bindable<string>(string.Empty);

        public string Text
        {
            get => TextBindable.Value;
            set => TextBindable.Value = value;
        }
    }
}
```

- [ ] **Step 5: Create `DesignPointInfo.cs`**

```csharp
// Sorted container of DesignPoints (by StartTime) — the design-side peer of ControlPointInfo.
// Raises DesignPointsChanged on any STRUCTURAL change (add, remove, clear, or a positional move via
// MoveDesignPoint) so the tab list, undo/redo rebuild, and timeline overlay refresh off one event.
// In-place edits of effect parameters (e.g. TutorialMessage.Text) deliberately do NOT raise it.

using System;
using System.Collections.Generic;

namespace Garbus.Game.Charts.Design
{
    public class DesignPointInfo
    {
        private readonly List<DesignPoint> designPoints = new List<DesignPoint>();

        public IReadOnlyList<DesignPoint> DesignPoints => designPoints;

        public event Action? DesignPointsChanged;

        public void Add(DesignPoint point)
        {
            insertSorted(point);
            DesignPointsChanged?.Invoke();
        }

        public void Remove(DesignPoint point)
        {
            if (designPoints.Remove(point))
                DesignPointsChanged?.Invoke();
        }

        public void Clear()
        {
            if (designPoints.Count == 0)
                return;

            designPoints.Clear();
            DesignPointsChanged?.Invoke();
        }

        /// <summary>
        /// Structurally moves a point: updates its start/end, re-sorts, then raises the change event.
        /// Editing position through here (rather than the bindables directly) keeps the sorted order
        /// and lets the single event drive every consumer. Analog of TimingPointChanges.MoveGroup.
        /// </summary>
        public void MoveDesignPoint(DesignPoint point, double newStartTime, double newEndTime)
        {
            point.StartTime = newStartTime;
            point.EndTime = newEndTime;
            designPoints.Remove(point);
            insertSorted(point);
            DesignPointsChanged?.Invoke();
        }

        // Stable insert: append after all points with an equal-or-earlier start time.
        private void insertSorted(DesignPoint point)
        {
            int i = 0;
            while (i < designPoints.Count && designPoints[i].StartTime <= point.StartTime)
                i++;
            designPoints.Insert(i, point);
        }
    }
}
```

- [ ] **Step 6: Add `DesignPointInfo` to `GarbusChart`**

In `Garbus.Game/Charts/GarbusChart.cs`, add the using and the property. After `using Garbus.Game.Charts.Timing;` add:

```csharp
using Garbus.Game.Charts.Design;
```

After the `ControlPointInfo` property (`public ControlPointInfo ControlPointInfo { get; init; } = new ControlPointInfo();`) add:

```csharp
    public DesignPointInfo DesignPointInfo { get; init; } = new DesignPointInfo();
```

- [ ] **Step 7: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestDesignPointInfo"`
Expected: PASS (5 tests).

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/Charts/Design/ Garbus.Game/Charts/GarbusChart.cs Garbus.Game.Tests/Charts/TestDesignPointInfo.cs
git commit -m "feat: design point model + sorted DesignPointInfo container"
```

---

## Task 2: Chart serialization for design points

**Files:**
- Modify: `Garbus.Game/Charts/Format/ChartFileDto.cs`
- Modify: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`
- Test: `Garbus.Game.Tests/TestChartFormat.cs`

**Interfaces:**
- Consumes: `DesignPointInfo`, `DesignPoint`, `TutorialMessage` (Task 1).
- Produces: `ChartFileDto.DesignPoints` (`List<DesignPointDto>`); `abstract DesignPointDto { double StartTime; double EndTime; }`; `TutorialMessageDto : DesignPointDto { string Text; }` with discriminator `"tutorial-message"`. Serializer round-trips `chart.DesignPointInfo`.

- [ ] **Step 1: Write the failing tests**

In `Garbus.Game.Tests/TestChartFormat.cs`, add `using Garbus.Game.Charts.Design;` to the usings, then add these tests inside the class:

```csharp
        [Test]
        public void TestDesignPointsRoundtrip()
        {
            var chart = new GarbusChart();
            chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 1000, EndTime = 3000, Text = "Welcome!" });
            chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 5000, EndTime = 6000, Text = "Press the buttons" });

            var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

            Assert.That(decoded.DesignPointInfo.DesignPoints, Has.Count.EqualTo(2));
            var first = (TutorialMessage)decoded.DesignPointInfo.DesignPoints[0];
            Assert.That(first.StartTime, Is.EqualTo(1000));
            Assert.That(first.EndTime, Is.EqualTo(3000));
            Assert.That(first.Text, Is.EqualTo("Welcome!"));
            var second = (TutorialMessage)decoded.DesignPointInfo.DesignPoints[1];
            Assert.That(second.Text, Is.EqualTo("Press the buttons"));
        }

        [Test]
        public void TestChartWithoutDesignPointsDecodesEmpty()
        {
            var chart = new GarbusChart();

            var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

            Assert.That(decoded.DesignPointInfo.DesignPoints, Is.Empty);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChartFormat.TestDesignPointsRoundtrip"`
Expected: FAIL — compile error, `ChartFileDto.DesignPoints` / `GarbusChart.DesignPointInfo` mapping do not exist yet.

- [ ] **Step 3: Add the DTOs**

In `Garbus.Game/Charts/Format/ChartFileDto.cs`, add to `ChartFileDto` (after the `HitObjects` property):

```csharp
    public List<DesignPointDto> DesignPoints { get; set; } = new List<DesignPointDto>();
```

At the bottom of the file (after the last hit-object DTO), add:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TutorialMessageDto), "tutorial-message")]
public abstract class DesignPointDto
{
    public double StartTime { get; set; }
    public double EndTime { get; set; }
}

public class TutorialMessageDto : DesignPointDto
{
    public string Text { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Map design points in the serializer**

In `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`:

Add to the usings:

```csharp
using Garbus.Game.Charts.Design;
```

In `toDto(GarbusChart chart)`, add a `DesignPoints` initializer to the `new ChartFileDto { ... }` (after `HitObjects = ...`):

```csharp
        DesignPoints = chart.DesignPointInfo.DesignPoints.Select(toDto).ToList(),
```

Add these mapping helpers (place next to the hit-object `toDto`/`fromDto` helpers):

```csharp
    private static DesignPointDto toDto(DesignPoint point) => point switch
    {
        TutorialMessage tm => new TutorialMessageDto { StartTime = tm.StartTime, EndTime = tm.EndTime, Text = tm.Text },
        _ => throw new ArgumentOutOfRangeException(nameof(point), point.GetType().Name, "design point type has no chart format representation")
    };

    private static DesignPoint fromDto(DesignPointDto dto) => dto switch
    {
        TutorialMessageDto tm => new TutorialMessage { StartTime = tm.StartTime, EndTime = tm.EndTime, Text = tm.Text },
        _ => throw new ArgumentOutOfRangeException(nameof(dto), dto.GetType().Name, "unknown design point dto type")
    };
```

In `fromDto(ChartFileDto dto)`, after the `foreach (var timing in dto.TimingPoints) { ... }` loop and before `return chart;`, add:

```csharp
        foreach (var designPoint in dto.DesignPoints)
            chart.DesignPointInfo.Add(fromDto(designPoint));
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChartFormat"`
Expected: PASS (all existing TestChartFormat tests plus the two new ones).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Charts/Format/ChartFileDto.cs Garbus.Game/Charts/Format/GarbusChartSerializer.cs Garbus.Game.Tests/TestChartFormat.cs
git commit -m "feat: serialize design points in the chart format"
```

---

## Task 3: Undo/redo integration + editor exposure

**Files:**
- Modify: `Garbus.Game/Edit/EditorChart.cs`
- Modify: `Garbus.Game/Edit/GarbusChartChangeHandler.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Test: `Garbus.Game.Tests/Editor/TestChangeHandler.cs`

**Interfaces:**
- Consumes: `DesignPointInfo` (Task 1), whole-chart serialization (Task 2).
- Produces: `EditorChart.DesignPointInfo` (passthrough to `Chart.DesignPointInfo`); design points participate in undo/redo via `ApplyStateChange`; `DesignPointInfo` cached in editor DI for `[Resolved]`.

- [ ] **Step 1: Write the failing test**

In `Garbus.Game.Tests/Editor/TestChangeHandler.cs`, add `using Garbus.Game.Charts.Design;` to the usings, then add:

```csharp
        [Test]
        public void TestUndoRedoDesignPoint()
        {
            chart.BeginChange();
            chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 1000, EndTime = 3000, Text = "hi" });
            chart.SaveState();
            chart.EndChange();

            Assert.That(chart.DesignPointInfo.DesignPoints, Has.Count.EqualTo(1));
            Assert.That(handler.CanUndo.Value, Is.True);

            handler.Undo();
            Assert.That(chart.DesignPointInfo.DesignPoints, Is.Empty);

            handler.Redo();
            Assert.That(chart.DesignPointInfo.DesignPoints, Has.Count.EqualTo(1));
            var tm = (TutorialMessage)chart.DesignPointInfo.DesignPoints[0];
            Assert.That(tm.StartTime, Is.EqualTo(1000));
            Assert.That(tm.EndTime, Is.EqualTo(3000));
            Assert.That(tm.Text, Is.EqualTo("hi"));
        }
```

Note: `chart` here is the fixture's `EditorChart` (see `SetUp`); it needs the `DesignPointInfo` passthrough from Step 3.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChangeHandler.TestUndoRedoDesignPoint"`
Expected: FAIL — compile error, `EditorChart.DesignPointInfo` does not exist; and after undo the container would not clear.

- [ ] **Step 3: Expose `DesignPointInfo` on `EditorChart`**

In `Garbus.Game/Edit/EditorChart.cs`, after the line
`public Charts.Timing.ControlPointInfo ControlPointInfo => Chart.ControlPointInfo;` add:

```csharp
        public Charts.Design.DesignPointInfo DesignPointInfo => Chart.DesignPointInfo;
```

- [ ] **Step 4: Rebuild design points on state change**

In `Garbus.Game/Edit/GarbusChartChangeHandler.cs`, at the end of `ApplyStateChange` (after the `// --- ControlPointInfo ---` block that rebuilds timing points), add:

```csharp
            // --- DesignPointInfo ---
            // Rebuild design points from the decoded target. The decoded points are fresh instances
            // (deep-cloned through the serializer), so adding them directly is safe.
            editorChart.DesignPointInfo.Clear();
            foreach (var designPoint in targetChart.DesignPointInfo.DesignPoints)
                editorChart.DesignPointInfo.Add(designPoint);
```

- [ ] **Step 5: Cache `DesignPointInfo` in editor DI**

In `Garbus.Game/Edit/Screens/GarbusEditor.cs`, in `CreateChildDependencies`, after the line
`dependencies.CacheAs(EditorChart.ControlPointInfo);` add:

```csharp
            // Cache DesignPointInfo directly so the Design tab components and the timeline region
            // display can resolve it without going through EditorChart.
            dependencies.CacheAs(EditorChart.DesignPointInfo);
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestChangeHandler"`
Expected: PASS (existing change-handler tests plus the new one).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/EditorChart.cs Garbus.Game/Edit/GarbusChartChangeHandler.cs Garbus.Game/Edit/Screens/GarbusEditor.cs Garbus.Game.Tests/Editor/TestChangeHandler.cs
git commit -m "feat: design points participate in editor undo/redo"
```

---

## Task 4: Design tab shell + editor wiring

**Files:**
- Modify: `Garbus.Game/Edit/Screens/EditorTab.cs`
- Create: `Garbus.Game/Edit/Screens/DesignTab.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs`

**Interfaces:**
- Consumes: `EditorTab` enum, `TimelineStrip`, `EditorTabScreen`.
- Produces: `EditorTab.Design` (ordinal between `Timing` and `Verify`); `partial class DesignTab : EditorTabScreen` hosting a `TimelineStrip` + a 40/60 grid with two placeholder cells (left filled in Task 5, right in Task 6); `GarbusEditor` shows/hides it.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs`:

```csharp
// Tests for the Design tab: enum ordering, tab visibility + timeline strip, and (later tasks) the
// point list, settings pane, and timeline region overlay.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Timeline;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneDesignTab : GarbusTestScene
    {
        private GarbusEditor editor = null!;
        private osu.Framework.Testing.Input.ManualInputManager input = null!;

        private void setupEditor() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new Garbus.Game.Charts.Timing.TimingControlPoint { BeatLength = 500 });

            var chartFile = new ChartFile(chart);
            editor = new GarbusEditor(chartFile);
            Child = input = new osu.Framework.Testing.Input.ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private void switchToDesignTab()
        {
            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Design tab", () => editor.Tab.Value = EditorTab.Design);
            AddUntilStep("design tab visible", () =>
                editor.ChildrenOfType<DesignTab>().Any() &&
                editor.ChildrenOfType<DesignTab>().First().State.Value == Visibility.Visible);
        }

        [Test]
        public void TestDesignTabIsBetweenTimingAndVerify()
        {
            AddAssert("Design after Timing", () => (int)EditorTab.Design > (int)EditorTab.Timing);
            AddAssert("Design before Verify", () => (int)EditorTab.Design < (int)EditorTab.Verify);
        }

        [Test]
        public void TestDesignTabShowsTimelineStrip()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("timeline strip present in design tab", () =>
                editor.ChildrenOfType<DesignTab>().First().ChildrenOfType<TimelineStrip>().Any());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab"`
Expected: FAIL — compile error, `EditorTab.Design` and `DesignTab` do not exist.

- [ ] **Step 3: Insert `Design` into the enum**

In `Garbus.Game/Edit/Screens/EditorTab.cs`, change the enum body so `Design` sits between `Timing` and `Verify`:

```csharp
    public enum EditorTab
    {
        Setup,
        Compose,
        Timing,
        Design,
        Verify,
    }
```

- [ ] **Step 4: Create `DesignTab.cs` (shell with placeholder cells)**

```csharp
// Design tab: timeline strip on top; point list (left 40%) + editable point details (right 60%)
// below — the layout mirrors TimingTab. Instead of timing points it edits design points (time-ranged
// visual effects). The two grid cells are placeholders here; the list is filled in the DesignPointList
// task and the details pane in the DesignPointSettings task.

using Garbus.Game.Charts.Design;
using Garbus.Game.Edit.Screens.Timeline;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens
{
    public partial class DesignTab : EditorTabScreen
    {
        // Shared selection between the list (Task 5) and settings pane (Task 6).
        private readonly Bindable<DesignPoint?> selectedPoint = new Bindable<DesignPoint?>();

        private TimelineStrip timelineStrip = null!;

        public DesignTab()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            const float zoom_button_width = 26;

            InternalChildren = new Drawable[]
            {
                timelineStrip = new TimelineStrip(),
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = zoom_button_width,
                    Height = TimelineStrip.HEIGHT / 2,
                    Text = "–",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value - 1f,
                },
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = zoom_button_width,
                    Height = TimelineStrip.HEIGHT / 2,
                    Position = new osuTK.Vector2(-zoom_button_width, 0),
                    Text = "+",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value + 1f,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = TimelineStrip.HEIGHT },
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Relative, 0.40f),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                // Left: DesignPointList (Task 5).
                                new Container { RelativeSizeAxes = Axes.Both },
                                // Right: scrollable DesignPointSettings (Task 6).
                                new Container { RelativeSizeAxes = Axes.Both },
                            },
                        },
                    },
                },
            };
        }
    }
}
```

- [ ] **Step 5: Wire the tab into `GarbusEditor`**

In `Garbus.Game/Edit/Screens/GarbusEditor.cs`:

Add a field next to the other tab fields (after `private TimingTab timingTab = null!;`):

```csharp
        private DesignTab designTab = null!;
```

In `load`, in the `tabContainer.Children = new Drawable[] { ... }` initializer, add the design tab between the timing and verify tabs:

```csharp
                designTab = new DesignTab { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Hidden } },
```

In `updateTabVisibility`, after the `timingTab.State.Value = ...` line add:

```csharp
            designTab.State.Value = activeTab == EditorTab.Design ? Visibility.Visible : Visibility.Hidden;
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab"`
Expected: PASS (2 tests).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Screens/EditorTab.cs Garbus.Game/Edit/Screens/DesignTab.cs Garbus.Game/Edit/Screens/GarbusEditor.cs Garbus.Game.Tests/Editor/TestSceneDesignTab.cs
git commit -m "feat: add Design editor tab shell between Timing and Verify"
```

---

## Task 5: Design point list (left pane)

**Files:**
- Create: `Garbus.Game/Edit/Screens/Design/DesignPointList.cs`
- Modify: `Garbus.Game/Edit/Screens/DesignTab.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs`

**Interfaces:**
- Consumes: `EditorChart`, `EditorClock`, `IEditorChangeHandler`, `DesignPointInfo` (DI-cached, Task 3), `DesignPoint`/`TutorialMessage` (Task 1).
- Produces: `partial class DesignPointList : CompositeDrawable` with `Bindable<DesignPoint?> SelectedPoint`; an "Add" button (adds a `TutorialMessage` at the snapped playhead, `EndTime = start + 2000`, `Text = "New message"`), a "Delete" button, and `DesignPointRow`s. Up/Down arrow navigation. `DesignTab` hosts it in the left cell and binds `selectedPoint` to it.

- [ ] **Step 1: Write the failing tests**

Append to `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs` (add `using System;` and `using Garbus.Game.Charts.Design;` and `using Garbus.Game.Edit.Screens.Design;` to the usings), inside the class:

```csharp
        private DesignPointList designList() => editor.ChildrenOfType<DesignPointList>().First();

        private osu.Framework.Graphics.UserInterface.BasicButton designButton(string text) =>
            designList().ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                        .First(b => b.Text.ToString() == text);

        [Test]
        public void TestAddCreatesTutorialMessageAtPlayhead()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            AddStep("seek clock to 4000ms", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(4000));

            AddStep("really click Add", () =>
            {
                input.MoveMouseTo(designButton("Add"));
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("one design point exists", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.Count == 1);
            AddAssert("it is a TutorialMessage near 4000 spanning 2000ms", () =>
            {
                var p = editor.EditorChart.DesignPointInfo.DesignPoints[0] as TutorialMessage;
                return p != null && Math.Abs(p.StartTime - 4000) < 100 && Math.Abs(p.EndTime - p.StartTime - 2000) < 1;
            });
        }

        [Test]
        public void TestDeleteRemovesSelectedPoint()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            AddStep("seek to 4000 and add", () =>
            {
                editor.ChildrenOfType<EditorClock>().First().Seek(4000);
                input.MoveMouseTo(designButton("Add"));
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("one point + selected", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.Count == 1 &&
                designList().SelectedPoint.Value != null);

            AddStep("really click Delete", () =>
            {
                input.MoveMouseTo(designButton("Delete"));
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("no design points remain", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.Count == 0);
        }

        [Test]
        public void TestSelectingRowSeeksClock()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            AddStep("add a point at 5000", () =>
            {
                editor.ChildrenOfType<EditorClock>().First().Seek(5000);
                input.MoveMouseTo(designButton("Add"));
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddStep("seek away to 0", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(0));

            AddStep("click the row", () =>
            {
                var row = editor.ChildrenOfType<DesignPointRow>().First();
                input.MoveMouseTo(row);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddUntilStep("clock seeked back near 5000", () =>
                Math.Abs(editor.ChildrenOfType<EditorClock>().First().CurrentTime - 5000) < 100);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab.TestAddCreatesTutorialMessageAtPlayhead"`
Expected: FAIL — compile error, `DesignPointList` / `DesignPointRow` do not exist.

- [ ] **Step 3: Create `DesignPointList.cs`**

```csharp
// Left panel of the Design tab: header row + one row per design point (StartTime, EndTime, text
// preview). Mirrors TimingPointList. Selecting a row seeks the editor clock to the point's StartTime.
// Add creates a TutorialMessage at the snapped playhead; Delete removes the selected point. Both go
// through the change handler so they are one undo step each. Up/Down navigate the list.

using System;
using System.Linq;
using Garbus.Game.Charts.Design;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Screens.Design
{
    public partial class DesignPointList : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        [Resolved]
        private DesignPointInfo designPointInfo { get; set; } = null!;

        public readonly Bindable<DesignPoint?> SelectedPoint = new Bindable<DesignPoint?>();

        private const float header_height = 24;

        private FillFlowContainer<DesignPointRow> rowContainer = null!;
        private BasicButton addButton = null!;
        private BasicButton deleteButton = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Text = "Start",
                            X = 8,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                        new SpriteText
                        {
                            Text = "End",
                            X = DesignPointRow.START_COLUMN_WIDTH,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                        new SpriteText
                        {
                            Text = "Message",
                            X = DesignPointRow.START_COLUMN_WIDTH + DesignPointRow.END_COLUMN_WIDTH,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                    },
                },
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = header_height, Bottom = 40 },
                    Child = rowContainer = new FillFlowContainer<DesignPointRow>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 1),
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Children = new Drawable[]
                    {
                        addButton = new BasicButton
                        {
                            Text = "Add",
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Action = addAtPlayhead,
                        },
                        deleteButton = new BasicButton
                        {
                            Text = "Delete",
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Action = deleteSelected,
                        },
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            deleteButton.Enabled.Value = SelectedPoint.Value != null;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            designPointInfo.DesignPointsChanged += scheduleRefresh;
            refreshRows();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat || e.ControlPressed || e.AltPressed || e.ShiftPressed || e.SuperPressed)
                return base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Up:
                    return moveSelection(-1);

                case Key.Down:
                    return moveSelection(1);
            }

            return base.OnKeyDown(e);
        }

        private bool moveSelection(int direction)
        {
            var points = designPointInfo.DesignPoints;
            if (points.Count == 0)
                return false;

            int currentIndex = -1;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == SelectedPoint.Value)
                {
                    currentIndex = i;
                    break;
                }
            }

            int targetIndex = currentIndex == -1
                ? (direction > 0 ? 0 : points.Count - 1)
                : Math.Clamp(currentIndex + direction, 0, points.Count - 1);

            var target = points[targetIndex];
            if (target != SelectedPoint.Value)
            {
                SelectedPoint.Value = target;
                editorClock.Seek(target.StartTime);
            }

            return true;
        }

        private void scheduleRefresh() => Scheduler.AddOnce(refreshRows);

        private void refreshRows()
        {
            rowContainer.Clear();

            foreach (var point in designPointInfo.DesignPoints)
            {
                var row = new DesignPointRow(point)
                {
                    IsSelected = { BindTarget = SelectedPoint },
                    Action = p =>
                    {
                        if (SelectedPoint.Value == p)
                        {
                            SelectedPoint.Value = null;
                            return;
                        }

                        SelectedPoint.Value = p;
                        editorClock.Seek(p.StartTime);
                    },
                };
                rowContainer.Add(row);
            }

            // Reselect the point at the same start time if the previous selection was replaced
            // (undo/redo rebuilds the container with new instances).
            if (SelectedPoint.Value != null)
            {
                var stillExists = designPointInfo.DesignPoints
                    .FirstOrDefault(p => Math.Abs(p.StartTime - SelectedPoint.Value.StartTime) < 1);
                SelectedPoint.Value = stillExists;
            }
        }

        private void addAtPlayhead()
        {
            double start = editorChart.ControlPointInfo.GetClosestSnappedTime(editorClock.CurrentTime);

            changeHandler.BeginChange();
            var point = new TutorialMessage { StartTime = start, EndTime = start + 2000, Text = "New message" };
            designPointInfo.Add(point);
            editorChart.SaveState();
            changeHandler.EndChange();

            SelectedPoint.Value = point;
        }

        private void deleteSelected()
        {
            if (SelectedPoint.Value == null)
                return;

            changeHandler.BeginChange();
            designPointInfo.Remove(SelectedPoint.Value);
            editorChart.SaveState();
            changeHandler.EndChange();

            SelectedPoint.Value = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (designPointInfo != null)
                designPointInfo.DesignPointsChanged -= scheduleRefresh;
        }
    }

    public partial class DesignPointRow : ClickableContainer
    {
        public const float START_COLUMN_WIDTH = 90;
        public const float END_COLUMN_WIDTH = 90;

        private static readonly Colour4 row_background = new Colour4(42, 42, 48, 255);
        private static readonly Colour4 selected_background = new Colour4(70, 90, 140, 255);

        private readonly DesignPoint point;

        public readonly Bindable<DesignPoint?> IsSelected = new Bindable<DesignPoint?>();

        public new Action<DesignPoint>? Action;

        // Bound copies stored as fields so drawable disposal auto-unbinds them (lambda-leak gotcha).
        private readonly IBindable<double> startTime;
        private readonly IBindable<double> endTime;
        private readonly IBindable<string>? text;

        private Box background = null!;
        private SpriteText startText = null!;
        private SpriteText endText = null!;
        private SpriteText messageText = null!;

        public DesignPointRow(DesignPoint point)
        {
            this.point = point;

            startTime = point.StartTimeBindable.GetBoundCopy();
            endTime = point.EndTimeBindable.GetBoundCopy();
            if (point is TutorialMessage tm)
                text = tm.TextBindable.GetBoundCopy();

            RelativeSizeAxes = Axes.X;
            Height = 32;

            base.Action = () => Action?.Invoke(point);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = row_background,
                },
                startText = new SpriteText
                {
                    X = 8,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                endText = new SpriteText
                {
                    X = START_COLUMN_WIDTH,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                messageText = new SpriteText
                {
                    X = START_COLUMN_WIDTH + END_COLUMN_WIDTH,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = FontUsage.Default.With(size: 14),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            IsSelected.BindValueChanged(e =>
            {
                bool selected = e.NewValue == point;
                background.Colour = selected ? selected_background : row_background;
                Alpha = selected ? 1f : 0.85f;
            }, true);

            startTime.BindValueChanged(_ => startText.Text = $"{point.StartTime:0}ms", true);
            endTime.BindValueChanged(_ => endText.Text = $"{point.EndTime:0}ms", true);
            if (text != null)
                text.BindValueChanged(_ => messageText.Text = preview(text.Value), true);
            else
                messageText.Text = string.Empty;
        }

        private static string preview(string value)
        {
            value = value.Replace("\n", " ");
            return value.Length <= 24 ? value : value.Substring(0, 24) + "…";
        }
    }
}
```

- [ ] **Step 4: Host the list in `DesignTab`'s left cell**

In `Garbus.Game/Edit/Screens/DesignTab.cs`, add `using Garbus.Game.Edit.Screens.Design;` to the usings and a field:

```csharp
        private DesignPointList designPointList = null!;
```

Replace the left placeholder cell (the first `new Container { RelativeSizeAxes = Axes.Both }` in the grid `Content`) with:

```csharp
                                designPointList = new DesignPointList { RelativeSizeAxes = Axes.Both },
```

At the end of `LoadComplete`, after the `InternalChildren = ...` assignment, add:

```csharp
            selectedPoint.BindTo(designPointList.SelectedPoint);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab"`
Expected: PASS (all Design tab tests including the three new list tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Edit/Screens/Design/DesignPointList.cs Garbus.Game/Edit/Screens/DesignTab.cs Garbus.Game.Tests/Editor/TestSceneDesignTab.cs
git commit -m "feat: design point list with add/select/delete"
```

---

## Task 6: Design point settings (right pane)

**Files:**
- Create: `Garbus.Game/Edit/Screens/Design/DesignPointSettings.cs`
- Modify: `Garbus.Game/Edit/Screens/DesignTab.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs`

**Interfaces:**
- Consumes: `EditorChart`, `EditorClock`, `IEditorChangeHandler`, `DesignPointInfo` (Task 3), `DesignPointList.SelectedPoint` (Task 5).
- Produces: `partial class DesignPointSettings : CompositeDrawable` with `Bindable<DesignPoint?> SelectedPoint` and test seams `SetStartAndCommit(double)`, `SetEndAndCommit(double)`, `SetTextAndCommit(string)`. Start and End rows each pair a text box with an inline "Now" button (sets that field to the clock's current time). `DesignTab` hosts it in the right cell inside a scroll container and binds `selectedPoint`.

Note (v1 simplification): the message uses a single-line `BasicTextBox`. osu-framework's `Basic*` widgets have no multiline text box; gameplay still wraps the text via `TextFlowContainer`. Authoring literal newlines is deferred.

- [ ] **Step 1: Write the failing tests**

Append to `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs`, inside the class:

```csharp
        private DesignPointSettings designSettings() => editor.ChildrenOfType<DesignPointSettings>().First();

        private void addPointAt(double time) => AddStep($"add point at {time}", () =>
        {
            editor.ChildrenOfType<EditorClock>().First().Seek(time);
            input.MoveMouseTo(designButton("Add"));
            input.Click(osuTK.Input.MouseButton.Left);
        });

        [Test]
        public void TestSettingsEditStartEndText()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            addPointAt(4000);
            AddUntilStep("settings present + selected", () =>
                editor.ChildrenOfType<DesignPointSettings>().Any() &&
                designSettings().SelectedPoint.Value != null);

            AddStep("set start 2500", () => designSettings().SetStartAndCommit(2500));
            AddStep("set end 5500", () => designSettings().SetEndAndCommit(5500));
            AddStep("set text", () => designSettings().SetTextAndCommit("Hello world"));

            AddAssert("model updated", () =>
            {
                var p = editor.EditorChart.DesignPointInfo.DesignPoints.OfType<TutorialMessage>().First();
                return Math.Abs(p.StartTime - 2500) < 0.01 && Math.Abs(p.EndTime - 5500) < 0.01 && p.Text == "Hello world";
            });
        }

        [Test]
        public void TestStartNowButtonUsesClockTime()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            addPointAt(4000);
            AddUntilStep("settings present", () => editor.ChildrenOfType<DesignPointSettings>().Any());

            AddStep("seek to 1234", () => editor.ChildrenOfType<EditorClock>().First().Seek(1234));
            AddStep("really click start Now", () =>
            {
                var button = designSettings().ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                    .First(b => b.Name == "start-now");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("start moved to ~1234", () =>
                Math.Abs(editor.EditorChart.DesignPointInfo.DesignPoints.First().StartTime - 1234) < 1);
        }

        [Test]
        public void TestSettingsEditIsUndoable()
        {
            setupEditor();
            switchToDesignTab();

            GarbusChartChangeHandler handler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                handler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            addPointAt(4000);
            AddUntilStep("settings present", () => editor.ChildrenOfType<DesignPointSettings>().Any());

            AddStep("set text to Foo", () => designSettings().SetTextAndCommit("Foo"));
            AddAssert("text is Foo", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.OfType<TutorialMessage>().First().Text == "Foo");

            AddStep("undo", () => handler.Undo());
            AddAssert("text reverted", () =>
                editor.EditorChart.DesignPointInfo.DesignPoints.OfType<TutorialMessage>().First().Text == "New message");
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab.TestSettingsEditStartEndText"`
Expected: FAIL — compile error, `DesignPointSettings` does not exist.

- [ ] **Step 3: Create `DesignPointSettings.cs`**

```csharp
// Right-panel details editor for the selected design point. Start and End rows each pair a text box
// with an inline "Now" button (sets that field to the editor clock's current time). For a
// TutorialMessage a message text box is shown. Edits go through the change handler (one undo step
// each): position edits via DesignPointInfo.MoveDesignPoint (structural, so the timeline overlay and
// list refresh off the single event); text edits set the Text bindable in place.

using System;
using System.Globalization;
using Garbus.Game.Charts.Design;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens.Design
{
    public partial class DesignPointSettings : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        [Resolved]
        private DesignPointInfo designPointInfo { get; set; } = null!;

        public readonly Bindable<DesignPoint?> SelectedPoint = new Bindable<DesignPoint?>();

        private BasicTextBox startBox = null!;
        private BasicTextBox endBox = null!;
        private BasicButton startNowButton = null!;
        private BasicButton endNowButton = null!;
        private BasicTextBox messageBox = null!;

        private bool updatingFromModel;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding(12);

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new SpriteText { Text = "Start time (ms)" },
                    timeRow(out startBox, out startNowButton, "start-now", useCurrentStart),

                    new SpriteText { Text = "End time (ms)" },
                    timeRow(out endBox, out endNowButton, "end-now", useCurrentEnd),

                    new SpriteText { Text = "Message" },
                    messageBox = new BasicTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        PlaceholderText = "Message text",
                    },
                },
            };
        }

        // A textbox that fills the row with a fixed-width "Now" button inline to its right.
        private Drawable timeRow(out BasicTextBox box, out BasicButton nowButton, string buttonName, Action nowAction)
        {
            box = new BasicTextBox { RelativeSizeAxes = Axes.Both, PlaceholderText = "0" };
            nowButton = new BasicButton
            {
                Name = buttonName,
                RelativeSizeAxes = Axes.Y,
                Width = 60,
                Text = "Now",
                Action = nowAction,
            };

            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, 66), // 60 button + 6 spacing
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        box,
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = 6 },
                            Child = nowButton,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedPoint.BindValueChanged(_ => updateFromModel(), true);

            startBox.OnCommit += (_, _) => commitStart();
            endBox.OnCommit += (_, _) => commitEnd();
            messageBox.OnCommit += (_, _) => commitText();
        }

        private void updateFromModel()
        {
            var p = SelectedPoint.Value;
            bool has = p != null;

            startBox.ReadOnly = !has;
            endBox.ReadOnly = !has;
            messageBox.ReadOnly = !has;
            startNowButton.Enabled.Value = has;
            endNowButton.Enabled.Value = has;

            if (!has)
            {
                updatingFromModel = true;
                startBox.Text = string.Empty;
                endBox.Text = string.Empty;
                messageBox.Text = string.Empty;
                updatingFromModel = false;
                return;
            }

            updatingFromModel = true;
            startBox.Text = p!.StartTime.ToString("0", CultureInfo.InvariantCulture);
            endBox.Text = p.EndTime.ToString("0", CultureInfo.InvariantCulture);
            messageBox.Text = p is TutorialMessage tm ? tm.Text : string.Empty;
            updatingFromModel = false;
        }

        private void commitStart()
        {
            var p = SelectedPoint.Value;
            if (updatingFromModel || p == null) return;

            if (!double.TryParse(startBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newStart)
                || newStart >= p.EndTime)
            {
                updateFromModel();
                return;
            }

            if (Math.Abs(newStart - p.StartTime) < 0.01) return;

            move(p, newStart, p.EndTime);
        }

        private void commitEnd()
        {
            var p = SelectedPoint.Value;
            if (updatingFromModel || p == null) return;

            if (!double.TryParse(endBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newEnd)
                || newEnd <= p.StartTime)
            {
                updateFromModel();
                return;
            }

            if (Math.Abs(newEnd - p.EndTime) < 0.01) return;

            move(p, p.StartTime, newEnd);
        }

        private void commitText()
        {
            if (updatingFromModel || SelectedPoint.Value is not TutorialMessage tm) return;
            if (tm.Text == messageBox.Text) return;

            changeHandler.BeginChange();
            tm.Text = messageBox.Text;
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private void move(DesignPoint p, double start, double end)
        {
            changeHandler.BeginChange();
            designPointInfo.MoveDesignPoint(p, start, end);
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private void useCurrentStart()
        {
            if (SelectedPoint.Value == null) return;
            startBox.Text = editorClock.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
            commitStart();
        }

        private void useCurrentEnd()
        {
            if (SelectedPoint.Value == null) return;
            endBox.Text = editorClock.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
            commitEnd();
        }

        /// <summary>Test seam: set the start textbox and commit (as if typing + Enter).</summary>
        public void SetStartAndCommit(double start)
        {
            startBox.Text = start.ToString("0.##", CultureInfo.InvariantCulture);
            commitStart();
        }

        /// <summary>Test seam: set the end textbox and commit.</summary>
        public void SetEndAndCommit(double end)
        {
            endBox.Text = end.ToString("0.##", CultureInfo.InvariantCulture);
            commitEnd();
        }

        /// <summary>Test seam: set the message textbox and commit.</summary>
        public void SetTextAndCommit(string text)
        {
            messageBox.Text = text;
            commitText();
        }
    }
}
```

- [ ] **Step 4: Host the settings in `DesignTab`'s right cell**

In `Garbus.Game/Edit/Screens/DesignTab.cs`, add a field:

```csharp
        private DesignPointSettings designPointSettings = null!;
```

Replace the right placeholder cell (the second `new Container { RelativeSizeAxes = Axes.Both }` in the grid `Content`) with a scroll container hosting the settings:

```csharp
                                new BasicScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ScrollbarOverlapsContent = false,
                                    Child = designPointSettings = new DesignPointSettings
                                    {
                                        RelativeSizeAxes = Axes.X,
                                    },
                                },
```

At the end of `LoadComplete` (after `selectedPoint.BindTo(designPointList.SelectedPoint);`) add:

```csharp
            designPointSettings.SelectedPoint.BindTo(selectedPoint);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab"`
Expected: PASS (all Design tab tests including the three settings tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Edit/Screens/Design/DesignPointSettings.cs Garbus.Game/Edit/Screens/DesignTab.cs Garbus.Game.Tests/Editor/TestSceneDesignTab.cs
git commit -m "feat: design point details pane with inline set-current-time buttons"
```

---

## Task 7: Timeline region overlay + View toggle

**Files:**
- Create: `Garbus.Game/Edit/Screens/Timeline/TimelineDesignRegionDisplay.cs`
- Modify: `Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs`
- Modify: `Garbus.Game/Configuration/GarbusSetting.cs`
- Modify: `Garbus.Game/Configuration/GarbusConfigManager.cs`
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs`

**Interfaces:**
- Consumes: `DesignPointInfo` (DI-cached, Task 3), `EditorClock`, `TimelineStrip`, `GarbusSetting`, `GarbusConfigManager`.
- Produces: `partial class TimelineDesignRegionDisplay : CompositeDrawable` (one translucent `Box` per design point spanning `[StartTime, EndTime]` as track fractions); a `GarbusSetting.EditorShowDesignRegions` (default `true`) wired to its `Alpha`; a "Show Design Regions" View menu toggle.

- [ ] **Step 1: Write the failing test**

Append to `Garbus.Game.Tests/Editor/TestSceneDesignTab.cs` (add `using Garbus.Game.Edit.Screens.Timeline;` if not present), inside the class:

```csharp
        [Test]
        public void TestTimelineRegionSpansTrackFraction()
        {
            setupEditor();
            switchToDesignTab();

            AddUntilStep("list present", () => editor.ChildrenOfType<DesignPointList>().Any());
            addPointAt(4000); // start 4000, end 6000 (default 2000ms span)

            // The editor's default (no-audio) track is a TrackVirtual(60000), so TrackLength == 60000.
            // Expected fractions: X = 4000/60000 ≈ 0.0667, Width = 2000/60000 ≈ 0.0333.
            AddUntilStep("region box matches the point", () =>
            {
                var display = editor.ChildrenOfType<DesignTab>().First()
                    .ChildrenOfType<TimelineDesignRegionDisplay>().FirstOrDefault();
                if (display == null) return false;

                var box = display.ChildrenOfType<osu.Framework.Graphics.Shapes.Box>()
                    .FirstOrDefault(b => b.Alpha > 0);
                if (box == null) return false;

                return Math.Abs(box.X - 4000f / 60000f) < 0.005f
                       && Math.Abs(box.Width - 2000f / 60000f) < 0.005f;
            });
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab.TestTimelineRegionSpansTrackFraction"`
Expected: FAIL — compile error, `TimelineDesignRegionDisplay` does not exist.

- [ ] **Step 3: Create `TimelineDesignRegionDisplay.cs`**

```csharp
// Draws a translucent horizontal band per design point across the timeline content area, from its
// StartTime to EndTime (as fractions of the track). Modeled on TimelineTimingChangeDisplay, but a
// spanning region instead of a vertical line. Recreated on DesignPointInfo.DesignPointsChanged — that
// single event covers add/remove AND Start/End moves, because MoveDesignPoint is structural.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using Garbus.Game.Charts.Design;

namespace Garbus.Game.Edit.Screens.Timeline
{
    public partial class TimelineDesignRegionDisplay : CompositeDrawable
    {
        [Resolved]
        private DesignPointInfo designPointInfo { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Cached regionCache = new Cached();
        private readonly List<Box> boxPool = new List<Box>();

        private void onDesignPointsChanged() => regionCache.Invalidate();

        public TimelineDesignRegionDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            designPointInfo.DesignPointsChanged += onDesignPointsChanged;
        }

        protected override void Update()
        {
            base.Update();

            if (!regionCache.IsValid)
                recreateRegions();
        }

        private void recreateRegions()
        {
            double trackLength = editorClock.TrackLength;
            if (trackLength <= 0) return;

            int idx = 0;
            foreach (var dp in designPointInfo.DesignPoints)
            {
                var box = getOrCreateBox(idx++);
                box.RelativePositionAxes = Axes.X;
                box.RelativeSizeAxes = Axes.Both;
                box.Anchor = Anchor.TopLeft;
                box.Origin = Anchor.TopLeft;
                box.X = (float)(dp.StartTime / trackLength);
                box.Width = (float)Math.Max(0, (dp.EndTime - dp.StartTime) / trackLength);
                box.Height = 1;
                box.Colour = new Color4(90, 140, 220, 60);
                box.Alpha = 1;
            }

            for (int i = idx; i < boxPool.Count; i++)
                boxPool[i].Alpha = 0;

            regionCache.Validate();
        }

        private Box getOrCreateBox(int index)
        {
            if (index < boxPool.Count)
                return boxPool[index];

            var box = new Box();
            boxPool.Add(box);
            AddInternal(box);
            return box;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (designPointInfo != null)
                designPointInfo.DesignPointsChanged -= onDesignPointsChanged;
        }
    }
}
```

- [ ] **Step 4: Add the config setting + default**

In `Garbus.Game/Configuration/GarbusSetting.cs`, after `EditorShowTimingChanges,` add:

```csharp
        /// <summary>Show translucent design-point regions in the timeline strip.</summary>
        EditorShowDesignRegions,
```

In `Garbus.Game/Configuration/GarbusConfigManager.cs`, after `SetDefault(GarbusSetting.EditorShowTimingChanges, true);` add:

```csharp
            SetDefault(GarbusSetting.EditorShowDesignRegions, true);
```

- [ ] **Step 5: Add the layer to `TimelineStrip`**

In `Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs`:

Add a field next to the other layer fields (after `private TimelineTimingChangeDisplay timingChanges = null!;`):

```csharp
        private TimelineDesignRegionDisplay designRegions = null!;
```

Add a stored bindable field (after `private Bindable<bool>? showTimingChangesBindable;`):

```csharp
        private Bindable<bool>? showDesignRegionsBindable;
```

In `load`, store the bindable (after `showTimingChangesBindable = config.GetBindable<bool>(GarbusSetting.EditorShowTimingChanges);`):

```csharp
            showDesignRegionsBindable = config.GetBindable<bool>(GarbusSetting.EditorShowDesignRegions);
```

Add a local alias (after `var showTimingChanges = showTimingChangesBindable;`):

```csharp
            var showDesignRegions = showDesignRegionsBindable;
```

In the `AddRange(new Drawable[] { ... })` layer list, insert the region display immediately AFTER the `waveform` and BEFORE `ticks` is fine, but to keep it below the timing lines add it right before `timingChanges`:

```csharp
                designRegions = new TimelineDesignRegionDisplay(),
                timingChanges = new TimelineTimingChangeDisplay(),
```

(i.e. replace the existing `timingChanges = new TimelineTimingChangeDisplay(),` line with the two lines above.)

Wire its visibility toggle (after `showTimingChanges.BindValueChanged(e => timingChanges.Alpha = e.NewValue ? 1 : 0, true);`):

```csharp
            showDesignRegions.BindValueChanged(e => designRegions.Alpha = e.NewValue ? 1 : 0, true);
```

- [ ] **Step 6: Add the View menu toggle**

In `Garbus.Game/Edit/Screens/GarbusEditor.cs`, in `createViewMenuItems`, in the returned `new MenuItem[] { ... }`, after the `new ToggleMenuItem("Show Timing Changes", ...)` line add:

```csharp
                new ToggleMenuItem("Show Design Regions", config.GetBindable<bool>(GarbusSetting.EditorShowDesignRegions)),
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignTab"`
Expected: PASS (all Design tab tests including the region test).

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/Edit/Screens/Timeline/TimelineDesignRegionDisplay.cs Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs Garbus.Game/Configuration/GarbusSetting.cs Garbus.Game/Configuration/GarbusConfigManager.cs Garbus.Game/Edit/Screens/GarbusEditor.cs Garbus.Game.Tests/Editor/TestSceneDesignTab.cs
git commit -m "feat: design point region overlay on the editor timeline"
```

---

## Task 8: Gameplay rendering (DesignOverlay in PlayScreen)

**Files:**
- Create: `Garbus.Game/Screens/DesignOverlay.cs`
- Modify: `Garbus.Game/Screens/PlayScreen.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneDesignOverlay.cs`

**Interfaces:**
- Consumes: `GarbusChart.DesignPointInfo`, `TutorialMessage` (Task 1).
- Produces: `partial class DesignOverlay : CompositeDrawable` (ctor `DesignOverlay(GarbusChart chart)`), stateless per-frame renderer keyed off `Clock.CurrentTime`; test seams `float DimAlphaForTests`, `bool MessageVisibleForTests`, `string MessageTextForTests`. Hosted inside `PlayScreen`'s gameplay-clock subtree, above the playfield.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneDesignOverlay.cs`:

```csharp
// DesignOverlay drives the tutorial-message effect off a gameplay clock. Driven by a ManualClock so
// headless runs can seek deterministically (mirrors TestSceneGameplay's manual-clock harness).

using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Screens;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneDesignOverlay : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private DesignOverlay overlay = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay with one tutorial message", () =>
            {
                manualClock = new ManualClock { Rate = 1 };

                var chart = new GarbusChart();
                chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 2000, EndTime = 4000, Text = "Tutorial!" });

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = overlay = new DesignOverlay(chart) { RelativeSizeAxes = Axes.Both },
                };
            });

            AddUntilStep("overlay loaded", () => overlay.IsLoaded);
        }

        [Test]
        public void TestOverlayVisibleOnlyDuringWindow()
        {
            AddStep("seek before window (1000)", () => manualClock.CurrentTime = 1000);
            AddUntilStep("hidden before", () => !overlay.MessageVisibleForTests && overlay.DimAlphaForTests == 0);

            AddStep("seek into window (3000)", () => manualClock.CurrentTime = 3000);
            AddUntilStep("visible during", () =>
                overlay.MessageVisibleForTests
                && overlay.DimAlphaForTests == TutorialMessage.OVERLAY_OPACITY
                && overlay.MessageTextForTests == "Tutorial!");

            AddStep("seek after window (5000)", () => manualClock.CurrentTime = 5000);
            AddUntilStep("hidden after", () => !overlay.MessageVisibleForTests && overlay.DimAlphaForTests == 0);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignOverlay"`
Expected: FAIL — compile error, `DesignOverlay` does not exist.

- [ ] **Step 3: Create `DesignOverlay.cs`**

```csharp
// Renders active design-point effects during gameplay. For a TutorialMessage active at the current
// gameplay time, dims the screen with a translucent black box and shows its text centered on top.
// Stateless per frame (recomputed from Clock.CurrentTime) so it is rewind-safe with no revert
// bookkeeping. v1 assumes non-overlapping tutorial messages (first active one wins).

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Garbus.Game.Screens
{
    public partial class DesignOverlay : CompositeDrawable
    {
        private readonly IReadOnlyList<DesignPoint> designPoints;

        private Box dim = null!;
        private TextFlowContainer message = null!;

        public DesignOverlay(GarbusChart chart)
        {
            designPoints = chart.DesignPointInfo.DesignPoints;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                dim = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0,
                },
                message = new TextFlowContainer(t => t.Font = FontUsage.Default.With(size: 32))
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    TextAnchor = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Width = 0.6f,
                    AutoSizeAxes = Axes.Y,
                    Alpha = 0,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            var active = activeMessage(Clock.CurrentTime);

            if (active != null)
            {
                dim.Alpha = TutorialMessage.OVERLAY_OPACITY;
                message.Alpha = 1;
                if (message.Text.ToString() != active.Text)
                    message.Text = active.Text;
            }
            else
            {
                dim.Alpha = 0;
                message.Alpha = 0;
            }
        }

        private TutorialMessage? activeMessage(double time) =>
            designPoints.OfType<TutorialMessage>()
                        .FirstOrDefault(m => time >= m.StartTime && time < m.EndTime);

        /// <summary>Test seam: current dim-overlay alpha.</summary>
        public float DimAlphaForTests => dim.Alpha;

        /// <summary>Test seam: whether the message text is currently shown.</summary>
        public bool MessageVisibleForTests => message.Alpha > 0;

        /// <summary>Test seam: the currently displayed message text.</summary>
        public string MessageTextForTests => message.Text.ToString();
    }
}
```

- [ ] **Step 4: Run the overlay test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneDesignOverlay"`
Expected: PASS.

- [ ] **Step 5: Host `DesignOverlay` in `PlayScreen` under the gameplay clock**

In `Garbus.Game/Screens/PlayScreen.cs`:

Add a field near the other drawable fields (after `private GarbusPlayfield playfield = null!;`):

```csharp
        private DesignOverlay designOverlay = null!;
```

In `load`, replace the gameplay-clock container entry:

```csharp
                gameplayClock = new MasterGameplayClockContainer(track, StartTime)
                {
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield
                        {
                            Size = Vector2.One,
                        },
                    },
                },
```

with a version that layers the overlay above the playfield, still inside the gameplay-clock subtree (so it reads gameplay time, per the GameplayClockContainer gotcha in CLAUDE.md):

```csharp
                gameplayClock = new MasterGameplayClockContainer(track, StartTime)
                {
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new GarbusInputManager
                            {
                                Child = playfield = new GarbusPlayfield
                                {
                                    Size = Vector2.One,
                                },
                            },
                            designOverlay = new DesignOverlay(chart)
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                    },
                },
```

- [ ] **Step 6: Build to verify PlayScreen wiring compiles**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded.

- [ ] **Step 7: Run the play-screen smoke tests to verify nothing regressed**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestScenePlayScreen"`
Expected: PASS (existing smoke tests still pass with the overlay in the tree).

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/Screens/DesignOverlay.cs Garbus.Game/Screens/PlayScreen.cs Garbus.Game.Tests/Visual/TestSceneDesignOverlay.cs
git commit -m "feat: render tutorial-message design points during gameplay"
```

---

## Task 9: Full-suite verification

**Files:** none (verification only).

- [ ] **Step 1: Build the whole desktop solution filter**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run the entire headless test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — all pre-existing tests plus the new `TestDesignPointInfo`, `TestChartFormat` additions, `TestChangeHandler` addition, `TestSceneDesignTab`, and `TestSceneDesignOverlay`.

- [ ] **Step 3: Commit (only if any fixup was needed)**

```bash
git add -A
git commit -m "test: verify design tab feature suite is green"
```

---

## Self-Review Notes

**Spec coverage check:**
- Data model (DesignPoint / TutorialMessage / DesignPointInfo, Option B) → Task 1. ✓
- Serialization with `"type"` discriminator, no version bump → Task 2. ✓
- Undo/redo rebuild + EditorChart passthrough + DI cache → Task 3. ✓
- Design tab mirroring Timing layout (strip + 40/60 grid) → Tasks 4–6. ✓
- List: add(TutorialMessage @ playhead, +2000ms), select+seek, delete, arrow nav → Task 5. ✓
- Settings: Start/End with inline "Now" buttons, message text, validation, undoable → Task 6. ✓
- Timeline region overlay + "Show Design Regions" View toggle + config default → Task 7. ✓
- Gameplay-only rendering via stateless DesignOverlay in PlayScreen (gameplay clock subtree) → Task 8. ✓

**Deviations from spec (documented):**
- The message box is a single-line `BasicTextBox` (osu-framework has no Basic multiline widget); gameplay text still wraps via `TextFlowContainer`. Authoring literal newlines is deferred — noted in Task 6.
- End-time validation rejects-and-restores when `EndTime ≤ StartTime` (spec's chosen v1 behavior).

**Type consistency:** `DesignPointInfo` API (`Add`/`Remove`/`Clear`/`MoveDesignPoint`/`DesignPoints`/`DesignPointsChanged`), `SelectedPoint` bindable name, and the `"tutorial-message"` discriminator are used identically across Tasks 1–8. Column-width constants (`START_COLUMN_WIDTH`/`END_COLUMN_WIDTH`) are defined on `DesignPointRow` and referenced only by `DesignPointList`'s header.

**Assumptions:** non-overlapping tutorial windows (first active wins); the editor's default no-audio track is `TrackVirtual(60000)` so `TrackLength == 60000` in the Task 7 fraction test.
