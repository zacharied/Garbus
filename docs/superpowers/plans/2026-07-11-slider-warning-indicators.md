# Slider/Slam Warning Indicators (GAR-3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Telegraph an approaching analog-stick object (slider head or SlamCentered) with a blurred colored arc around the outside of the playfield, so the player can pre-position the stick.

**Architecture:** A pure logic class (`WarningIndicatorSchedule`) precomputes, per Side, which indicated objects are eligible for a warning and answers which one — if any — should be revealed at a given time. A thin drawable (`WarningIndicatorDisplay`) queries that schedule every frame and drives two blurred per-side arcs. `GarbusPlayfield` owns the display and forwards the chart's hit objects to it; `PlayScreen` hands over `chart.HitObjects` at load.

**Tech Stack:** C# / osu-framework. Rendering via the existing polar-coordinate `Arc` (`SmoothPath`) wrapped in a framework `BufferedContainer` for blur. Headless NUnit tests + a visual `GarbusTestScene`.

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL fields use `= null!`.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- New files under `Garbus.Game/` are original (no ppy attribution header needed); they build on vendored primitives.
- Terminology: "chart" not "beatmap"; `Garbus*` prefixes.
- No version bumps, no compatibility shims (experimental project).

## Design Decisions (resolved ambiguities — reflected in `docs/rules-specs/Inputs.md`)

These interpret the spec's reveal rules; each is baked into `WarningIndicatorSchedule` and documented in the spec by Task 1:

1. **Indicated objects** = slider heads only (represented by their `SliderBody`). `GarbusSlamCentered` and `GarbusSlamEdge` are *stick objects* — they occupy the stick and so count toward the gap rule — but are **not** indicated: a Slam is a sudden, precisely timed flick, and a "pre-position the stick here" cue works against the timing it demands.
2. **Stick objects** (for the gap rule) = `SliderBody`, `GarbusSlamCentered`, `GarbusSlamEdge` — anything occupying a side's analog stick.
3. **Reveal window** = `[x.StartTime - WarningTime, x.StartTime)`. Hidden at and after `StartTime` (the object has arrived / is being hit).
4. **Gap** for object `x` = `x.StartTime - previousEnd`, where `previousEnd` is the greatest end time among same-side stick objects that *start* before `x`. Measured from the previous object's **end** because that is when the stick frees up. When there is no earlier same-side stick object the gap is `+∞` (an isolated object always warns).
5. **`WarningTime` default = 600 ms** (a tunable constant; below the 700 ms scroll `TimeRange` so the arc appears while the object is on screen).
6. **At most one indicated object per side is ever revealed at once** — a consequence of the gap rule (any two same-side eligible objects are `> WarningTime` apart in start time, so their reveal windows are disjoint). The display therefore needs exactly one arc per side.

## File Structure

- **Create** `Garbus.Game/UI/WarningIndicatorSchedule.cs` — pure reveal logic. One responsibility: given hit objects + `WarningTime`, answer `Revealed(side, time)`.
- **Create** `Garbus.Game/UI/WarningIndicatorDisplay.cs` — `CompositeDrawable` owning two blurred per-side arcs; queries the schedule each `Update`.
- **Modify** `Garbus.Game/UI/GarbusPlayfield.cs` — construct/add the display; add `SetHitObjects` + a `WarningIndicators` accessor.
- **Modify** `Garbus.Game/Screens/PlayScreen.cs` — call `playfield.SetHitObjects(chart.HitObjects)` at load.
- **Modify** `docs/rules-specs/Inputs.md` — tighten the reveal-rule wording (Task 1).
- **Create** `Garbus.Game.Tests/WarningIndicatorScheduleTest.cs` — headless unit tests for the schedule.
- **Create** `Garbus.Game.Tests/Visual/TestSceneWarningIndicator.cs` — visual + manual-clock tests for the display and the playfield wiring.

---

### Task 1: `WarningIndicatorSchedule` — pure reveal logic

**Files:**
- Create: `Garbus.Game/UI/WarningIndicatorSchedule.cs`
- Create: `Garbus.Game.Tests/WarningIndicatorScheduleTest.cs`
- Modify: `docs/rules-specs/Inputs.md` (reveal-rule wording)

**Interfaces:**
- Consumes: `GarbusHitObject`, `SliderBody`, `GarbusSlamCentered`, `GarbusSlamEdge` (namespace `Garbus.Game.Objects`), `HorizontalDirection` (`Garbus.Game.Core`).
- Produces:
  - `WarningIndicatorSchedule(IEnumerable<GarbusHitObject> objects, double warningTime)`
  - `readonly record struct WarningIndicatorSchedule.IndicatedObject(HorizontalDirection Side, int AngleDeg, double StartTime)`
  - `IndicatedObject? Revealed(HorizontalDirection side, double time)`

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/WarningIndicatorScheduleTest.cs`:

```csharp
using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class WarningIndicatorScheduleTest
    {
        private const double warning_time = 600;

        private static GarbusSlamCentered Slam(HorizontalDirection side, int angle, double start)
            => new GarbusSlamCentered { AngleDeg = angle, Side = side, StartTime = start };

        private static SliderBody Slider(HorizontalDirection side, int angle, double start, double duration)
            => new SliderBody
            {
                AngleDeg = angle,
                Side = side,
                StartTime = start,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = duration, RotationOffset = 0 },
                    },
                },
            };

        [Test]
        public void IsolatedSliderIsEligibleWithinWindow()
        {
            var schedule = new WarningIndicatorSchedule(
                new GarbusHitObject[] { Slider(HorizontalDirection.Left, 90, 5000, 200) }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4300), Is.Null);              // before window
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4600)?.AngleDeg, Is.EqualTo(90)); // in window
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4999)?.AngleDeg, Is.EqualTo(90));
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5000), Is.Null);              // at start: hidden
        }

        [Test]
        public void SlamIsNotIndicated()
        {
            // A SlamCentered occupies the stick (it counts for the gap rule) but is never itself telegraphed.
            var schedule = new WarningIndicatorSchedule(
                new GarbusHitObject[] { Slam(HorizontalDirection.Left, 90, 5000) }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4600), Is.Null);
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4999), Is.Null);
        }

        [Test]
        public void CloseSameSidePriorSuppressesWarning()
        {
            // A slam at 5000 keeps the stick busy through 5000; the slider head at 5300 is only 300ms later
            // (< 600) → the slider is not telegraphed. The slam itself is never telegraphed either.
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slam(HorizontalDirection.Left, 90, 5000),
                Slider(HorizontalDirection.Left, 180, 5300, 200),
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 4700), Is.Null); // slam not indicated
            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5100), Is.Null); // slider suppressed
        }

        [Test]
        public void DistantSameSidePriorIsEligible()
        {
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slam(HorizontalDirection.Left, 90, 5000),
                Slider(HorizontalDirection.Left, 180, 6000, 200), // head 1000ms after the slam (> 600)
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5500)?.AngleDeg, Is.EqualTo(180));
        }

        [Test]
        public void SliderGapMeasuredFromEndTime()
        {
            // Slider A occupies 5000..5800. Slider B's head at 6200 is 1200ms after A's START but only 400ms
            // after A ENDS (< 600) → B is NOT eligible.
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slider(HorizontalDirection.Left, 0, 5000, 800),
                Slider(HorizontalDirection.Left, 90, 6200, 200),
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Left, 5900), Is.Null);
        }

        [Test]
        public void OppositeSidesAreIndependent()
        {
            var schedule = new WarningIndicatorSchedule(new GarbusHitObject[]
            {
                Slider(HorizontalDirection.Left, 90, 5000, 200),
                Slider(HorizontalDirection.Right, 270, 5200, 200),
            }, warning_time);

            Assert.That(schedule.Revealed(HorizontalDirection.Right, 4900)?.AngleDeg, Is.EqualTo(270));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~WarningIndicatorScheduleTest"`
Expected: FAIL to compile — `WarningIndicatorSchedule` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Garbus.Game/UI/WarningIndicatorSchedule.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Objects;

namespace Garbus.Game.UI;

/// <summary>
/// Pure reveal logic for the slider/slam warning indicators (GAR-3). Precomputes, per <see cref="HorizontalDirection"/>,
/// which indicated objects (slider heads and <see cref="GarbusSlamCentered"/>) are eligible for a warning, and answers
/// which one — if any — should be revealed at a given time. Only slider heads are indicated (slams occupy
/// the stick but are not telegraphed). See docs/rules-specs/Inputs.md "Warning indicator".
/// </summary>
public sealed class WarningIndicatorSchedule
{
    public readonly record struct IndicatedObject(HorizontalDirection Side, int AngleDeg, double StartTime);

    private readonly double warningTime;

    private readonly Dictionary<HorizontalDirection, List<IndicatedObject>> eligibleBySide = new()
    {
        [HorizontalDirection.Left] = new List<IndicatedObject>(),
        [HorizontalDirection.Right] = new List<IndicatedObject>(),
    };

    public WarningIndicatorSchedule(IEnumerable<GarbusHitObject> objects, double warningTime)
    {
        this.warningTime = warningTime;

        var all = objects.ToList();

        // Stick objects: anything occupying a side's analog stick (both slam types + sliders), as (start, end).
        var stickBySide = new Dictionary<HorizontalDirection, List<(double Start, double End)>>
        {
            [HorizontalDirection.Left] = new(),
            [HorizontalDirection.Right] = new(),
        };

        foreach (var o in all)
        {
            switch (o)
            {
                case SliderBody s:
                    stickBySide[s.Side].Add((s.StartTime, s.EndTime));
                    break;
                case GarbusSlamCentered sc:
                    stickBySide[sc.Side].Add((sc.StartTime, sc.StartTime));
                    break;
                case GarbusSlamEdge se:
                    stickBySide[se.Side].Add((se.StartTime, se.StartTime));
                    break;
            }
        }

        // Indicated objects: slider heads only. Slams occupy the stick (counted above) but are not
        // telegraphed. Eligible when the same-side stick has been idle longer than warningTime before the
        // object (gap measured from the previous object's end time), or when there is no earlier same-side
        // stick object at all.
        foreach (var o in all)
        {
            IndicatedObject? indicated = o switch
            {
                SliderBody s => new IndicatedObject(s.Side, s.AngleDeg, s.StartTime),
                _ => null,
            };

            if (indicated is not { } x)
                continue;

            if (gapBefore(stickBySide[x.Side], x.StartTime) > warningTime)
                eligibleBySide[x.Side].Add(x);
        }

        foreach (var list in eligibleBySide.Values)
            list.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
    }

    // Idle time on the stick immediately before startTime: startTime minus the greatest end time of any
    // same-side stick object that starts strictly earlier. +Infinity when there is no earlier object.
    private static double gapBefore(List<(double Start, double End)> sameSide, double startTime)
    {
        double previousEnd = double.NegativeInfinity;
        bool found = false;

        foreach (var (start, end) in sameSide)
        {
            if (start < startTime)
            {
                found = true;
                if (end > previousEnd)
                    previousEnd = end;
            }
        }

        return found ? startTime - previousEnd : double.PositiveInfinity;
    }

    /// <summary>
    /// The indicated object whose warning should be showing for <paramref name="side"/> at
    /// <paramref name="time"/>, or null if none. At most one object per side is ever revealed at once.
    /// </summary>
    public IndicatedObject? Revealed(HorizontalDirection side, double time)
    {
        foreach (var x in eligibleBySide[side])
        {
            if (time >= x.StartTime - warningTime && time < x.StartTime)
                return x;
        }

        return null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~WarningIndicatorScheduleTest"`
Expected: PASS (6 tests).

- [ ] **Step 5: Tighten the spec wording to match the resolved semantics**

In `docs/rules-specs/Inputs.md`, under "### Warning indicator", replace the second reveal bullet:

```markdown
- The gap between the previous same-side stick object and *x* is greater than `WarningTime`. This rule considers *stick objects* (any Slider, SlamCentered, or SlamEdge on the same Side), not only indicated objects — so a warning appears only when the stick has been idle on that Side, not when *x* follows closely on recent same-side activity that already has the player's stick engaged.
```

with:

```markdown
- The gap between the previous same-side stick object and *x* is greater than `WarningTime`. This rule considers *stick objects* (any Slider, SlamCentered, or SlamEdge on the same Side), not only indicated objects. The gap is measured from the previous stick object's **end** (when the stick frees up) to *x*'s StartTime; when there is no earlier same-side stick object the gap is unbounded, so an isolated object always warns. This means a warning appears only when the stick has been idle on that Side, not when *x* follows closely on recent same-side activity that already has the player's stick engaged.
```

Also, after the reveal-condition list, add this sentence:

```markdown
The reveal window is `[x.StartTime - WarningTime, x.StartTime)`: the indicator hides once *x* reaches the edge. Because two eligible same-side objects are always more than `WarningTime` apart, at most one indicated object per Side is shown at any instant.
```

- [ ] **Step 6: Build to confirm nothing else broke**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/UI/WarningIndicatorSchedule.cs Garbus.Game.Tests/WarningIndicatorScheduleTest.cs docs/rules-specs/Inputs.md
git commit -m "feat: add warning-indicator reveal schedule (GAR-3)"
```

---

### Task 2: `WarningIndicatorDisplay` — blurred per-side arcs

**Files:**
- Create: `Garbus.Game/UI/WarningIndicatorDisplay.cs`
- Create: `Garbus.Game.Tests/Visual/TestSceneWarningIndicator.cs`

**Interfaces:**
- Consumes: `WarningIndicatorSchedule` (Task 1), `Arc` (`Garbus.Game.UI`), `HorizontalDirection` (`Garbus.Game.Core`), `MathUtils.DegToRad` (`Garbus.Game.Utils`), `BufferedContainer` (`osu.Framework.Graphics.Containers`).
- Produces:
  - `WarningIndicatorDisplay() : CompositeDrawable`
  - `const double WarningIndicatorDisplay.WARNING_TIME = 600`
  - `void SetHitObjects(IEnumerable<GarbusHitObject> hitObjects)`
  - `int? RevealedAngleDeg(HorizontalDirection side)` — the angle currently revealed for a side (null if hidden); test-facing.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneWarningIndicator.cs`:

```csharp
using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneWarningIndicator : GarbusTestScene
    {
        // Driven entirely by a ManualClock — pace steps per-frame (see TestSceneGameplay).
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private WarningIndicatorDisplay display = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create display", () =>
            {
                manualClock = new ManualClock { Rate = 1 };
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = display = new WarningIndicatorDisplay(),
                };
            });

            AddUntilStep("loaded", () => display.IsLoaded);
        }

        [Test]
        public void TestSliderWarningRevealsInWindow()
        {
            AddStep("set objects", () => display.SetHitObjects(new GarbusHitObject[]
            {
                new SliderBody
                {
                    AngleDeg = 90,
                    Side = HorizontalDirection.Left,
                    StartTime = 5000,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 0 },
                        },
                    },
                },
            }));

            AddStep("seek before window", () => manualClock.CurrentTime = 4000);
            AddUntilStep("hidden", () => display.RevealedAngleDeg(HorizontalDirection.Left) == null);

            AddStep("seek into window", () => manualClock.CurrentTime = 4700);
            AddUntilStep("revealed at 90", () => display.RevealedAngleDeg(HorizontalDirection.Left) == 90);

            AddAssert("no right warning", () => display.RevealedAngleDeg(HorizontalDirection.Right) == null);

            AddStep("seek past start", () => manualClock.CurrentTime = 5200);
            AddUntilStep("hidden again", () => display.RevealedAngleDeg(HorizontalDirection.Left) == null);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneWarningIndicator"`
Expected: FAIL to compile — `WarningIndicatorDisplay` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Garbus.Game/UI/WarningIndicatorDisplay.cs`:

```csharp
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using Garbus.Game.Utils;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// Draws a blurred colored arc around the outside of the playfield for an approaching slider head or
/// SlamCentered (GAR-3). One arc per Side; the reveal logic lives in <see cref="WarningIndicatorSchedule"/>.
/// </summary>
public sealed partial class WarningIndicatorDisplay : CompositeDrawable
{
    /// <summary>How far before an indicated object's StartTime the warning appears, in ms. Tunable.</summary>
    public const double WARNING_TIME = 600;

    // Visual tuning. radius_scale sits just inside 1.0 so the outward blur has headroom inside the
    // BufferedContainer framebuffer (the arc renders around the outside of the ring).
    private const float radius_scale = 0.94f;
    private const float thickness = 16f;
    private const float blur_sigma = 8f;
    private const float arc_half_width_deg = 15f;
    private const double fade_ms = 150;

    private WarningIndicatorSchedule? schedule;

    private readonly Dictionary<HorizontalDirection, SideArc> sideArcs = new();

    public WarningIndicatorDisplay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        foreach (var side in new[] { HorizontalDirection.Left, HorizontalDirection.Right })
        {
            var arc = new Arc(thickness: thickness)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Size = new Vector2(radius_scale),
                Colour = side == HorizontalDirection.Left ? Colour4.Blue : Colour4.Red,
            };

            var buffer = new BufferedContainer
            {
                RelativeSizeAxes = Axes.Both,
                BlurSigma = new Vector2(blur_sigma),
                Alpha = 0,
                Child = arc,
            };

            sideArcs[side] = new SideArc(buffer, arc);
            AddInternal(buffer);
        }
    }

    public void SetHitObjects(IEnumerable<GarbusHitObject> hitObjects)
        => schedule = new WarningIndicatorSchedule(hitObjects, WARNING_TIME);

    /// <summary>The angle (deg) currently revealed for <paramref name="side"/>, or null if hidden. Test-facing.</summary>
    public int? RevealedAngleDeg(HorizontalDirection side)
        => sideArcs.TryGetValue(side, out var s) ? s.RevealedAngleDeg : null;

    protected override void Update()
    {
        base.Update();

        foreach (var (side, s) in sideArcs)
        {
            var revealed = schedule?.Revealed(side, Time.Current);

            if (revealed is { } x)
            {
                if (s.RevealedAngleDeg != x.AngleDeg)
                {
                    float centre = MathUtils.DegToRad(x.AngleDeg);
                    float half = MathUtils.DegToRad(arc_half_width_deg);
                    s.Arc.StartRadians.Value = centre - half;
                    s.Arc.EndRadians.Value = centre + half;
                }

                if (s.RevealedAngleDeg == null)
                    s.Buffer.FadeIn(fade_ms);

                s.RevealedAngleDeg = x.AngleDeg;
            }
            else
            {
                if (s.RevealedAngleDeg != null)
                    s.Buffer.FadeOut(fade_ms);

                s.RevealedAngleDeg = null;
            }
        }
    }

    private sealed class SideArc
    {
        public readonly BufferedContainer Buffer;
        public readonly Arc Arc;
        public int? RevealedAngleDeg;

        public SideArc(BufferedContainer buffer, Arc arc)
        {
            Buffer = buffer;
            Arc = arc;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneWarningIndicator"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/UI/WarningIndicatorDisplay.cs Garbus.Game.Tests/Visual/TestSceneWarningIndicator.cs
git commit -m "feat: render blurred per-side warning arcs (GAR-3)"
```

---

### Task 3: Wire the display into the playfield and play loop

**Files:**
- Modify: `Garbus.Game/UI/GarbusPlayfield.cs`
- Modify: `Garbus.Game/Screens/PlayScreen.cs:208-213`
- Modify: `Garbus.Game.Tests/Visual/TestSceneWarningIndicator.cs`

**Interfaces:**
- Consumes: `WarningIndicatorDisplay` (Task 2), `GarbusPlayfield`, `GarbusInputManager` (`Garbus.Game.Input`), `GarbusTestChartGenerator` (`Garbus.Game.Charts`).
- Produces:
  - `void GarbusPlayfield.SetHitObjects(IEnumerable<GarbusHitObject> hitObjects)`
  - `WarningIndicatorDisplay GarbusPlayfield.WarningIndicators { get; }`

- [ ] **Step 1: Write the failing integration test**

Add this method (and the two `using`s below) to `Garbus.Game.Tests/Visual/TestSceneWarningIndicator.cs`.

Add at the top with the other usings:

```csharp
using Garbus.Game.Input;
using osuTK;
```

Add this test method inside the class:

```csharp
[Test]
public void TestPlayfieldForwardsWarnings()
{
    GarbusPlayfield playfield = null!;

    AddStep("create playfield", () =>
    {
        manualClock = new ManualClock { Rate = 1 };
        Child = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Clock = new FramedClock(manualClock),
            Child = new GarbusInputManager
            {
                Child = playfield = new GarbusPlayfield { Size = Vector2.One },
            },
        };
    });

    AddUntilStep("playfield loaded", () => playfield.IsLoaded);

    AddStep("hand over a left slider at 5000", () => playfield.SetHitObjects(new GarbusHitObject[]
    {
        new SliderBody
        {
            AngleDeg = 90,
            Side = HorizontalDirection.Left,
            StartTime = 5000,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 0 },
                },
            },
        },
    }));

    AddStep("seek into window", () => manualClock.CurrentTime = 4700);
    AddUntilStep("warning revealed", () => playfield.WarningIndicators.RevealedAngleDeg(HorizontalDirection.Left) == 90);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneWarningIndicator.TestPlayfieldForwardsWarnings"`
Expected: FAIL to compile — `GarbusPlayfield.SetHitObjects` / `WarningIndicators` do not exist.

- [ ] **Step 3: Add the display to `GarbusPlayfield`**

In `Garbus.Game/UI/GarbusPlayfield.cs`, add these usings at the top:

```csharp
using System.Collections.Generic;
using Garbus.Game.Objects;
```

Add the field alongside the stick-indicator fields (after line 26):

```csharp
    private readonly WarningIndicatorDisplay warningIndicators = new WarningIndicatorDisplay();
```

Add `warningIndicators` to the internal children so it draws on top of the ring and stick indicators. Replace the `AddRangeInternal([...])` block in `load()` with:

```csharp
        AddRangeInternal([
            analogInputManager,
            ring,
            stickIndicatorL,
            stickIndicatorR,
            warningIndicators,
        ]);
```

Add these members after the `Remove(DrawableHitObject h)` override (end of class):

```csharp
    /// <summary>
    /// Hand the full set of chart hit objects to the warning-indicator display so it can telegraph
    /// approaching slider heads and SlamCentered objects. Call once after adding drawables.
    /// </summary>
    public void SetHitObjects(IEnumerable<GarbusHitObject> hitObjects) => warningIndicators.SetHitObjects(hitObjects);

    /// <summary>The warning-indicator display (GAR-3). Exposed for wiring and tests.</summary>
    public WarningIndicatorDisplay WarningIndicators => warningIndicators;
```

- [ ] **Step 4: Feed the chart objects from `PlayScreen`**

In `Garbus.Game/Screens/PlayScreen.cs`, the load method currently ends the object-adding block at lines 208-213:

```csharp
            foreach (var hitObject in chart.HitObjects)
                playfield.Add(CreateDrawableRepresentation(hitObject));

            playfield.NewResult += onNewResult;
            playfield.RevertResult += onRevertResult;
```

Insert the `SetHitObjects` call after the `foreach`:

```csharp
            foreach (var hitObject in chart.HitObjects)
                playfield.Add(CreateDrawableRepresentation(hitObject));

            playfield.SetHitObjects(chart.HitObjects);

            playfield.NewResult += onNewResult;
            playfield.RevertResult += onRevertResult;
```

- [ ] **Step 5: Run the integration test to verify it passes**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneWarningIndicator"`
Expected: PASS (both tests).

- [ ] **Step 6: Run the full suite + build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded.

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`
Expected: PASS (all existing tests + the new ones).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/UI/GarbusPlayfield.cs Garbus.Game/Screens/PlayScreen.cs Garbus.Game.Tests/Visual/TestSceneWarningIndicator.cs
git commit -m "feat: wire warning indicators into playfield and play loop (GAR-3)"
```

---

## Self-Review

**1. Spec coverage** (against `docs/rules-specs/Inputs.md` "Warning indicator"):
- Blurred colored arc around the outside → `WarningIndicatorDisplay` (BufferedContainer blur + `Arc`, colored by Side, `radius_scale` just outside the ring). ✔ (Task 2)
- Base arc shape not visible → `BufferedContainer.DrawOriginal` left `false` (default), so only the blurred framebuffer draws; `blur_sigma` tunable. ✔ Note: the exact sigma/thickness that make the crisp shape fully disappear are visual-tuning constants — verify by eye in the test browser; adjust `blur_sigma`/`thickness` if the arc still reads as a sharp line.
- Indicated objects = SliderHead only (slams excluded, but still stick objects for the gap rule) → schedule's `IndicatedObject` switch keeps only `SliderBody`; slams remain in the stick-object switch. ✔ (Task 1)
- Reveal condition `x.StartTime - CurrentTime < WarningTime` → `Revealed` window lower bound. ✔
- Reveal condition gap `> WarningTime`, over stick objects (Slider/SlamCentered/SlamEdge), from previous end → `gapBefore`. ✔

**2. Placeholder scan:** No TBD/"add error handling"/"similar to Task N" — all code is inline. `GarbusSlamEdge` gameplay drawable is intentionally out of scope (slams have no drawable yet; the indicator works off the hit-object model, not drawables), so no placeholder there. ✔

**3. Type consistency:** `SetHitObjects(IEnumerable<GarbusHitObject>)` and `RevealedAngleDeg(HorizontalDirection)` and `WARNING_TIME` are named identically across the schedule, display, playfield, and tests. `IndicatedObject.AngleDeg` (int) matches `RevealedAngleDeg` (int?). `Arc.StartRadians/EndRadians` are `BindableFloat` set via `.Value`. ✔

## Known follow-ups (out of scope for this plan)
- Visual polish: final `blur_sigma`, `thickness`, `radius_scale`, `arc_half_width_deg`, and fade curve are first-pass values; tune in the test browser.
- `GarbusSlamCentered`/`GarbusSlamEdge` still have no gameplay `DrawableHitObject` (`PlayScreen.CreateDrawableRepresentation` throws for them). A chart containing slams can't be *played* yet, though the warning schedule already accounts for them as stick objects (they gate a following slider's warning even though they are never themselves telegraphed). Adding slam drawables is separate work.
