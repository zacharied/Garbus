# Editor Barlines Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show barlines at every measure boundary (one per `TimeSignature.Numerator` beats) in the editor compose playfield, each with a measure-number label.

**Architecture:** A reusable, timing-only `BarLineGenerator` walks `ControlPointInfo` and emits `BarLine` objects (never serialized to the chart), mirroring osu!mania's `BarLineGenerator`. An `EditorBarLineDisplay` inside `GarbusEditorPlayfield` feeds those into a `ScrollingHitObjectContainer` (the same mechanism `BeatSnapGrid` uses) as `DrawableBarLine`s — full-width horizontal lines with a left-edge measure label — regenerating whenever timing points change.

**Tech Stack:** C# / .NET, osu-framework (NUnit headless tests, `ScrollingHitObjectContainer`, DI/BDL), `osu.Framework.Utils.Precision`.

## Global Constraints

- Nullability enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- Barlines are **derived from timing, never serialized** to `.garbus` (no chart-format change).
- No `Major`/minor distinction — all barlines uniform (differs from osu, which carries a `Major` flag).
- Terminology: osu "beatmap" = "chart"; `Bac*` → `Garbus*`.
- Vendored/adapted osu files keep the ppy MIT header + an "Adapted for Garbus:" line. `BarLineGenerator` is *adapted* from osu's `osu.Game/Rulesets/Objects/BarLineGenerator.cs` — include that header.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`.
- Lambda event subscriptions leak — keep a field reference and unsubscribe in `Dispose` (editor components subscribe to `ControlPointInfo.ControlPointsChanged`).

---

### Task 1: `BarLine` object + `BarLineGenerator`

**Files:**
- Create: `Garbus.Game/Gameplay/Objects/BarLine.cs`
- Create: `Garbus.Game/Gameplay/Objects/BarLineGenerator.cs`
- Test: `Garbus.Game.Tests/Charts/TestBarLineGenerator.cs`

**Interfaces:**
- Consumes: `Garbus.Game.Charts.Timing.ControlPointInfo` (`.TimingPoints`, ordered), `TimingControlPoint` (`.Time`, `.BeatLength`, `.TimeSignature.Numerator`, `.OmitFirstBarLine`), `Garbus.Game.Gameplay.Objects.HitObject` (`.StartTime`).
- Produces:
  - `class BarLine : HitObject { int MeasureIndex { get; set; } }`
  - `static class BarLineGenerator` with `static List<BarLine> Generate(ControlPointInfo controlPointInfo, double endTime)`.
  - Semantics: one `BarLine` per measure, stepping `barLength = BeatLength × Numerator`; each section runs from `point.Time` up to (strictly less than) the next timing point's time, or `endTime` for the last section; `OmitFirstBarLine` advances the section start by one `barLength`; `MeasureIndex` is a running counter starting at **1** across the whole chart.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Charts/TestBarLineGenerator.cs`:

```csharp
using System.Linq;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Gameplay.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Charts
{
    [TestFixture]
    public class TestBarLineGenerator
    {
        private static ControlPointInfo cpi(params (double time, double beatLength, int numerator, bool omitFirst)[] points)
        {
            var info = new ControlPointInfo();
            foreach (var (time, beatLength, numerator, omitFirst) in points)
            {
                info.Add(time, new TimingControlPoint
                {
                    BeatLength = beatLength,
                    TimeSignature = new TimeSignature(numerator),
                    OmitFirstBarLine = omitFirst,
                });
            }
            return info;
        }

        [Test]
        public void TestSingleQuadrupleSection()
        {
            // beatLength 500, 4/4 => barLength 2000. endTime 8000 is exclusive.
            var lines = BarLineGenerator.Generate(cpi((0, 500, 4, false)), 8000);

            Assert.That(lines.Select(l => l.StartTime), Is.EqualTo(new double[] { 0, 2000, 4000, 6000 }));
            Assert.That(lines.Select(l => l.MeasureIndex), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void TestNumeratorChangeAcrossTwoSections()
        {
            // Section A: 3/4 @500 => barLength 1500, runs [0,3000). Section B: 4/4 @500 => 2000, runs [3000,7000).
            var lines = BarLineGenerator.Generate(cpi((0, 500, 3, false), (3000, 500, 4, false)), 7000);

            Assert.That(lines.Select(l => l.StartTime), Is.EqualTo(new double[] { 0, 1500, 3000, 5000 }));
            Assert.That(lines.Select(l => l.MeasureIndex), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void TestOmitFirstBarLineSkipsSectionStart()
        {
            // barLength 2000, omit first => start at 2000.
            var lines = BarLineGenerator.Generate(cpi((0, 500, 4, true)), 8000);

            Assert.That(lines.Select(l => l.StartTime), Is.EqualTo(new double[] { 2000, 4000, 6000 }));
            Assert.That(lines.Select(l => l.MeasureIndex), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void TestNoTimingPointsYieldsEmpty()
        {
            Assert.That(BarLineGenerator.Generate(new ControlPointInfo(), 8000), Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter TestBarLineGenerator`
Expected: FAIL to **compile** — `BarLine` / `BarLineGenerator` do not exist.

- [ ] **Step 3: Write `BarLine`**

Create `Garbus.Game/Gameplay/Objects/BarLine.cs`:

```csharp
namespace Garbus.Game.Gameplay.Objects
{
    /// <summary>
    /// A bar line marking a measure boundary. Derived from timing and never serialized to a chart.
    /// </summary>
    public class BarLine : HitObject
    {
        /// <summary>The 1-based measure number this bar line begins.</summary>
        public int MeasureIndex { get; set; }
    }
}
```

- [ ] **Step 4: Write `BarLineGenerator`**

Create `Garbus.Game/Gameplay/Objects/BarLineGenerator.cs`:

```csharp
// Adapted from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Objects/BarLineGenerator.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: generic BarLineGenerator<TBarLine>/IBarLine and beatmap coupling removed — this
// takes ControlPointInfo + an explicit endTime and returns concrete BarLine objects. The Major flag is
// dropped (all bar lines uniform); a running 1-based MeasureIndex replaces osu's per-section beat/major
// bookkeeping. Section ranges are half-open (strictly less than the next timing point / endTime) via
// Precision.DefinitelyBigger so a boundary bar line is emitted once, by the incoming section.

using System;
using System.Collections.Generic;
using Garbus.Game.Charts.Timing;
using osu.Framework.Utils;

namespace Garbus.Game.Gameplay.Objects
{
    /// <summary>
    /// Generates <see cref="BarLine"/>s at every measure boundary (one per
    /// <see cref="TimeSignature.Numerator"/> beats) from timing information.
    /// </summary>
    public static class BarLineGenerator
    {
        public static List<BarLine> Generate(ControlPointInfo controlPointInfo, double endTime)
        {
            var barLines = new List<BarLine>();
            var timingPoints = controlPointInfo.TimingPoints;

            if (timingPoints.Count == 0)
                return barLines;

            int measureIndex = 1;

            for (int i = 0; i < timingPoints.Count; i++)
            {
                var point = timingPoints[i];
                double barLength = point.BeatLength * point.TimeSignature.Numerator;
                double sectionEnd = i < timingPoints.Count - 1 ? timingPoints[i + 1].Time : endTime;

                double startTime = point.Time;
                if (point.OmitFirstBarLine)
                    startTime += barLength;

                for (double t = startTime; Precision.DefinitelyBigger(sectionEnd, t); t += barLength)
                {
                    double rounded = Math.Round(t, MidpointRounding.AwayFromZero);
                    if (Precision.AlmostEquals(t, rounded))
                        t = rounded;

                    barLines.Add(new BarLine { StartTime = t, MeasureIndex = measureIndex++ });
                }
            }

            return barLines;
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter TestBarLineGenerator`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Gameplay/Objects/BarLine.cs Garbus.Game/Gameplay/Objects/BarLineGenerator.cs Garbus.Game.Tests/Charts/TestBarLineGenerator.cs
git commit -m "Add BarLine object and timing-driven BarLineGenerator"
```

---

### Task 2: `EditorBarLineDisplay` + `DrawableBarLine` wired into the editor playfield

**Files:**
- Create: `Garbus.Game/Edit/EditorBarLineDisplay.cs` (contains `EditorBarLineDisplay` and a private nested `DrawableBarLine`)
- Modify: `Garbus.Game/Edit/GarbusEditorPlayfield.cs` (add the display to `InternalChildren`, behind the masked `HitObjectContainer`)
- Test: `Garbus.Game.Tests/Editor/TestSceneEditorBarLines.cs`

**Interfaces:**
- Consumes: `BarLineGenerator.Generate(ControlPointInfo, double)` and `BarLine` (Task 1); `EditorChart` (DI — exposes `.ControlPointInfo`); `EditorClock` (DI — exposes `.TrackLength`); `ControlPointInfo.ControlPointsChanged` (event `Action`); `ScrollingHitObjectContainer`, `IScrollingInfo` (DI, composer-cached), `DrawableHitObject`.
- Produces:
  - `partial class EditorBarLineDisplay : CompositeDrawable` — `RelativeSizeAxes = Axes.Both`; exposes `public IReadOnlyList<BarLine> BarLines { get; }` (the last generated set, independent of on-screen culling) for tests/inspection.
  - Regenerates on load and on every `ControlPointInfo.ControlPointsChanged`; unsubscribes in `Dispose`.

**Reference patterns (read before implementing):**
- `Garbus.Game/Edit/Compose/BeatSnapGrid.cs` — how a `ScrollingHitObjectContainer` is created, how `DrawableGridLine` handles `IScrollingInfo.Direction` → anchor/origin and horizontal-vs-vertical sizing. `DrawableBarLine` copies the direction handling but **must not** replicate `DrawableGridLine.UpdateInitialTransforms` (which forces a short `LifetimeEnd`).
- `Garbus.Game/Gameplay/UI/Scrolling/ScrollingHitObjectContainer.cs:243,256` — the container sets `LifetimeStart`/`LifetimeEnd` from each entry, so barlines scroll on/off automatically. Do not override lifetime in `DrawableBarLine`.
- `Garbus.Game/Edit/GarbusEditorPlayfield.cs:51-83` — the `InternalChildren` list; insert the display immediately **before** the masked `Container { Child = HitObjectContainer }` so barlines render behind the notes.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneEditorBarLines.cs`. This reuses the harness style from `TestSceneEditorPlayfield.cs` (a `ScrollingHitObjectComposer<GarbusHitObject>` hosting a `GarbusEditorPlayfield`, with `EditorChart`/`EditorClock`/`BindableBeatDivisor` cached). Track length is 60000 and the single 4/4 timing point has `BeatLength = 500` (barLength 2000) ⇒ generator yields 30 barlines over `[0, 60000)`.

```csharp
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using System;
using System.Collections.Generic;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorBarLines : GarbusTestScene
    {
        private Harness harness = null!;
        private EditorChart editorChart = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);
            Child = harness = new Harness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        [Test]
        public void TestBarLinesGeneratedForWholeTrack()
        {
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);

            AddAssert("display generated 30 barlines", () =>
            {
                var display = harness.Composer.Playfield.ChildrenOfType<EditorBarLineDisplay>().Single();
                return display.BarLines.Count == 30
                       && display.BarLines.Count == BarLineGenerator.Generate(editorChart.ControlPointInfo, 60000).Count;
            });

            AddUntilStep("at least one DrawableBarLine visible", () =>
                harness.Composer.Playfield.ChildrenOfType<EditorBarLineDisplay>().Single()
                    .ChildrenOfType<Drawable>().Any(d => d.GetType().Name == "DrawableBarLine"));
        }

        [Test]
        public void TestRegeneratesWhenTimingChanges()
        {
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);

            AddStep("add a second timing point halving the bar length", () =>
                editorChart.ControlPointInfo.Add(30000, new TimingControlPoint { BeatLength = 250 }));

            AddAssert("barline count reflects both sections", () =>
            {
                var display = harness.Composer.Playfield.ChildrenOfType<EditorBarLineDisplay>().Single();
                int expected = BarLineGenerator.Generate(editorChart.ControlPointInfo, 60000).Count;
                return display.BarLines.Count == expected && expected > 30;
            });
        }

        private partial class Harness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;
            public EditorBarLineComposer Composer { get; private set; } = null!;

            public Harness(EditorChart editorChart) => this.editorChart = editorChart;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                var beatDivisor = new BindableBeatDivisor(4);
                var editorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                editorClock.ChangeSource(new TrackVirtual(60000));
                dependencies.Cache(editorChart);
                dependencies.Cache(editorClock);
                dependencies.Cache(beatDivisor);
                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = Composer = new EditorBarLineComposer { RelativeSizeAxes = Axes.Both };
                AddInternal(dependencies.Get<EditorClock>());
            }
        }

        private partial class EditorBarLineComposer : ScrollingHitObjectComposer<GarbusHitObject>
        {
            protected override IReadOnlyList<CompositionTool> CompositionTools => Array.Empty<CompositionTool>();
            protected override Playfield CreatePlayfield() => new GarbusEditorPlayfield();
            protected override DrawableHitObject? CreateDrawableRepresentation(GarbusHitObject hitObject) => null;
            protected override ComposeBlueprintContainer CreateBlueprintContainer() => new MinimalBlueprintContainer(this);
        }

        private partial class MinimalBlueprintContainer : ComposeBlueprintContainer
        {
            public MinimalBlueprintContainer(HitObjectComposer composer) : base(composer) { }
            public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject) => null;
            protected override bool TryMoveBlueprints(
                osu.Framework.Input.Events.DragEvent e,
                IList<(SelectionBlueprint<GarbusHitObject> blueprint, osuTK.Vector2[] originalSnapPositions)> blueprints) => false;
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter TestSceneEditorBarLines`
Expected: FAIL to **compile** — `EditorBarLineDisplay` does not exist.

- [ ] **Step 3: Write `EditorBarLineDisplay` (with nested `DrawableBarLine`)**

Create `Garbus.Game/Edit/EditorBarLineDisplay.cs`:

```csharp
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI.Scrolling;
using osuTK.Graphics;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Always-visible measure bar lines in the editor compose playfield: one full-width horizontal
    /// line per measure (from <see cref="BarLineGenerator"/>), each labelled with its measure number.
    /// Regenerates whenever the timing changes. Distinct from <see cref="Compose.BeatSnapGrid"/>, whose
    /// lines are transient and near the cursor only.
    /// </summary>
    public partial class EditorBarLineDisplay : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly ScrollingHitObjectContainer lines = new ScrollingHitObjectContainer();

        private readonly List<BarLine> barLines = new List<BarLine>();

        /// <summary>The last generated set of bar lines (independent of on-screen culling).</summary>
        public IReadOnlyList<BarLine> BarLines => barLines;

        public EditorBarLineDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = lines;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            editorChart.ControlPointInfo.ControlPointsChanged += regenerate;
            regenerate();
        }

        private void regenerate()
        {
            lines.Clear();
            barLines.Clear();
            barLines.AddRange(BarLineGenerator.Generate(editorChart.ControlPointInfo, editorClock.TrackLength));

            foreach (var barLine in barLines)
                lines.Add(new DrawableBarLine(barLine));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (editorChart.IsNotNull())
                editorChart.ControlPointInfo.ControlPointsChanged -= regenerate;
        }

        private partial class DrawableBarLine : DrawableHitObject
        {
            [Resolved]
            private IScrollingInfo scrollingInfo { get; set; } = null!;

            private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

            public new BarLine HitObject => (BarLine)base.HitObject;

            public DrawableBarLine(BarLine barLine)
                : base(barLine)
            {
                RelativeSizeAxes = Axes.X;
                Height = 2;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White, Alpha = 0.6f });
                AddInternal(new SpriteText
                {
                    Text = HitObject.MeasureIndex.ToString(),
                    Colour = Color4.White,
                    Font = FontUsage.Default.With(size: 12),
                    Padding = new MarginPadding { Left = 4, Bottom = 2 },
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                });

                direction.BindTo(scrollingInfo.Direction);
                direction.BindValueChanged(onDirectionChanged, true);
            }

            private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> dir)
            {
                switch (dir.NewValue)
                {
                    case ScrollingDirection.Up:
                        Anchor = Anchor.TopLeft;
                        Origin = Anchor.CentreLeft;
                        break;

                    case ScrollingDirection.Down:
                        Anchor = Anchor.BottomLeft;
                        Origin = Anchor.CentreLeft;
                        break;
                }
            }

            // Do not fade or clamp lifetime here: the ScrollingHitObjectContainer manages
            // LifetimeStart/LifetimeEnd from the entry so bar lines scroll on/off across the track.
            protected override void UpdateInitialTransforms()
            {
            }
        }
    }
}
```

Notes for the implementer:
- `IsNotNull()` is `osu.Framework.Extensions.ObjectExtensions.IsNotNull` — add `using osu.Framework.Extensions.ObjectExtensions;` if the analyzer flags the null-check pattern; a plain `!= null` on a `[Resolved]` field is also acceptable here.
- The Garbus editor playfield scrolls vertically (Up/Down); only those two directions are wired, matching how it is used. If `IScrollingInfo` never fires for this subtree, confirm the display sits inside the playfield (Step 4) so the composer-cached `IScrollingInfo` resolves.

- [ ] **Step 4: Add the display to the playfield**

In `Garbus.Game/Edit/GarbusEditorPlayfield.cs`, insert `new EditorBarLineDisplay()` into `InternalChildren` immediately **before** the masked `Container { … Child = HitObjectContainer }` (so barlines draw behind notes but above the angle grid). Locate the block at lines 63-65:

```csharp
            // masked to the timeline bounds so slider wrap copies (and anything else) don't paint outside
            // it; the ghost bands lie within the bounds, so their clones still show.
            new Container { RelativeSizeAxes = Axes.Both, Masking = true, Child = HitObjectContainer },
```

Change to:

```csharp
            new EditorBarLineDisplay(),
            // masked to the timeline bounds so slider wrap copies (and anything else) don't paint outside
            // it; the ghost bands lie within the bounds, so their clones still show.
            new Container { RelativeSizeAxes = Axes.Both, Masking = true, Child = HitObjectContainer },
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter TestSceneEditorBarLines`
Expected: PASS (both tests). If `TestBarLinesGeneratedForWholeTrack`'s "at least one DrawableBarLine visible" step times out, the scrolling container has culled every line off-screen — verify the display is inside the playfield subtree (Step 4) and that `DrawableBarLine` does not force a short `LifetimeEnd`.

- [ ] **Step 6: Run the full editor test suite (guard against regressions)**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter Editor`
Expected: PASS (existing editor scenes — playfield, shell, timeline — still green; the new display is behind notes and non-interactive).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/EditorBarLineDisplay.cs Garbus.Game/Edit/GarbusEditorPlayfield.cs Garbus.Game.Tests/Editor/TestSceneEditorBarLines.cs
git commit -m "Render measure barlines in the editor compose playfield"
```

---

## Follow-up (out of scope)

Gameplay barlines (visual style A — a full-width line, no measure label) reuse `BarLineGenerator.Generate` against the gameplay `ControlPointInfo` and track length, wrapping each `BarLine` in a gameplay drawable added to `GarbusPlayfield`'s scrolling container. Not built in this plan.

## Self-Review

- **Spec coverage:** `BarLine`/`BarLineGenerator` (spec §Components 1-2) → Task 1. `EditorBarLineDisplay`/`DrawableBarLine` incl. measure label, behind-notes placement, regenerate-on-timing-change, unsubscribe-in-Dispose (spec §Components 3-4, §Data flow) → Task 2. Generator unit tests (times, numerator change, omit-first, empty) → Task 1 Step 1. Editor headless tests (populate + count-matches-generator, regenerate on timing edit) → Task 2 Step 1. "Never serialized" honored — no chart-format touch anywhere. Global measure numbering from 1 → generator `measureIndex = 1`.
- **Deviation from spec (deliberate, documented):** section ranges are half-open via `Precision.DefinitelyBigger` (spec said "until the next timing point"), preventing a duplicate bar line at a section boundary; the last section excludes an exact-`endTime` line. Numbering is uniform/global as specced. The spec's separate "track-end boundary" generator test is folded into `TestSingleQuadrupleSection` (8000 excluded) rather than a standalone test — same assertion.
- **Placeholder scan:** none — every code step is complete.
- **Type consistency:** `BarLine.MeasureIndex`, `BarLineGenerator.Generate(ControlPointInfo, double)`, `EditorBarLineDisplay.BarLines` used identically across tasks and tests.
