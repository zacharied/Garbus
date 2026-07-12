# Slam Gesture Detection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `GarbusSlamCentered` and `GarbusSlamEdge` judge in gameplay by detecting the required analog-stick gesture (flick / directional sweep) and awarding Perfect on a match, Miss when the window elapses.

**Architecture:** A new per-side `StickGestureTracker` inside `AnalogInputManager` retains a short buffer of recent stick samples and answers two motion queries (`FlickedTowards`, `SweptThrough`). The two slam drawables poll their side's tracker each frame in `CheckForResult`, mirroring how `DrawableSliderChild` polls `SliderCatcher`.

**Tech Stack:** C# / osu-framework, NUnit. `System.Numerics.Vector2` for stick math (matches `AnalogInputManager`).

## Global Constraints

- Nullability enabled solution-wide; DI/BDL fields use `= null!`.
- No new judgement class this pass: base `Judgement` already gives `MaxResult = Perfect` / `MinResult = Miss`.
- Angle convention is `SliderCatcher`'s: `angle = MathF.Atan2(-pos.Y, pos.X)` radians. Increasing angle = anticlockwise. `RotationalDirection.Clockwise = 1`, `Anticlockwise = -1`.
- First-cut judgement only: gesture in a **symmetric 200 ms** window → Perfect; window elapsing → Miss. Near grade / asymmetry are out of scope (see the design's Deferred section).
- Spec: `docs/superpowers/specs/2026-07-12-slam-gesture-design.md`.

**Build:** `dotnet build Garbus.Desktop.slnf`
**Test (all):** `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
**Test (one):** `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~StickGestureTrackerTest.TheTest"`

---

## File Structure

- Create `Garbus.Game/Input/StickGestureTracker.cs` — the gesture machine (buffer + two queries). No framework/drawable/hit-object dependencies, so unit-testable in isolation.
- Create `Garbus.Game.Tests/StickGestureTrackerTest.cs` — plain NUnit coverage of the tracker.
- Modify `Garbus.Game/Input/AnalogInputManager.cs` — expose `SliderCatcher.Position`, own two trackers, feed them per frame.
- Modify `Garbus.Game/Objects/Drawables/DrawableSlamCentered.cs` — poll `FlickedTowards`.
- Modify `Garbus.Game/Objects/Drawables/DrawableSlamEdge.cs` — poll `SweptThrough`.

---

## Task 1: `StickGestureTracker` — buffer + `FlickedTowards`

**Files:**
- Create: `Garbus.Game/Input/StickGestureTracker.cs`
- Test: `Garbus.Game.Tests/StickGestureTrackerTest.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `class StickGestureTracker` (namespace `Garbus.Game.Input`)
  - `const float FLICK_THRESHOLD = 0.7f`, `const float EDGE_THRESHOLD = 0.7f`, `const float ANGLE_TOLERANCE_DEG = 30f`, `const double SAMPLE_RETENTION_MS = 350`
  - `void AddSample(double time, Vector2 position)`
  - `bool FlickedTowards(int angleDeg, double sinceTime)`

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/StickGestureTrackerTest.cs`:

```csharp
// Unit tests for the slam gesture machine. Plain NUnit — no game host. Positions are built from the
// SliderCatcher angle convention (angle = atan2(-y, x)): a point at `angleDeg` and radius r is
// (r*cos, -r*sin), so +x is 0deg and increasing angle sweeps anticlockwise.

using System;
using System.Numerics;
using Garbus.Game.Core;
using Garbus.Game.Input;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class StickGestureTrackerTest
    {
        private static Vector2 At(float angleDeg, float radius)
        {
            float rad = angleDeg * MathF.PI / 180f;
            return new Vector2(radius * MathF.Cos(rad), -radius * MathF.Sin(rad));
        }

        [Test]
        public void TestFlickTowardsAngleDetected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));      // centred
            t.AddSample(10, At(0, 0.8f));   // crossed threshold outward at 0deg

            Assert.That(t.FlickedTowards(0, sinceTime: -1000), Is.True);
        }

        [Test]
        public void TestFlickOffAngleRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));
            t.AddSample(10, At(0, 0.8f));   // flick at 0deg

            Assert.That(t.FlickedTowards(90, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestSlowDriftNeverCrossesThreshold()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0.1f));
            t.AddSample(10, At(0, 0.3f));
            t.AddSample(20, At(0, 0.5f));   // never reaches 0.7

            Assert.That(t.FlickedTowards(0, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestFlickBeforeSinceTimeRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));
            t.AddSample(10, At(0, 0.8f));   // crossing at t=10

            Assert.That(t.FlickedTowards(0, sinceTime: 100), Is.False);
        }

        [Test]
        public void TestStaleSamplesPruned()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(0, 0f));
            t.AddSample(10, At(0, 0.8f));   // crossing at t=10
            t.AddSample(500, At(0, 0.8f));  // 500 - 350 = 150 > 10, prunes the crossing pair

            Assert.That(t.FlickedTowards(0, sinceTime: -1000), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~StickGestureTrackerTest"`
Expected: FAIL — `StickGestureTracker` does not exist (compile error).

- [ ] **Step 3: Write the minimal implementation**

Create `Garbus.Game/Input/StickGestureTracker.cs`:

```csharp
// The slam gesture machine: a per-side rolling buffer of recent analog-stick samples that answers
// motion queries for slam judgement. Scanning a recency buffer (rather than an edge-triggered
// "this frame" flag) lets a gesture that completed slightly before the poll — including before the
// object's StartTime, as early-permissive slams require — still register.

using System;
using System.Collections.Generic;
using System.Numerics;
using Garbus.Game.Core;

namespace Garbus.Game.Input;

public class StickGestureTracker
{
    // Tunable placeholders for the first-cut judgement (see the design doc). Not final values.
    public const float FLICK_THRESHOLD = 0.7f;      // outward radius crossing that counts as a flick
    public const float EDGE_THRESHOLD = 0.7f;       // radius at/beyond which the stick is "at the edge"
    public const float ANGLE_TOLERANCE_DEG = 30f;   // flick angle must be within this of the slam angle
    public const double SAMPLE_RETENTION_MS = 350;  // buffer horizon; larger than the widest window

    private readonly struct Sample
    {
        public readonly double Time;
        public readonly Vector2 Position;
        public Sample(double time, Vector2 position) { Time = time; Position = position; }

        public float Radius => Position.Length();
        public float Angle => MathF.Atan2(-Position.Y, Position.X);
    }

    private readonly List<Sample> samples = new();

    public void AddSample(double time, Vector2 position)
    {
        samples.Add(new Sample(time, position));

        double cutoff = time - SAMPLE_RETENTION_MS;
        int drop = 0;
        while (drop < samples.Count && samples[drop].Time < cutoff)
            drop++;
        if (drop > 0)
            samples.RemoveRange(0, drop);
    }

    /// <summary>
    /// True if, at or after <paramref name="sinceTime"/>, the stick radius crossed the flick threshold
    /// outward with its angle within tolerance of <paramref name="angleDeg"/>.
    /// </summary>
    public bool FlickedTowards(int angleDeg, double sinceTime)
    {
        float target = angleDeg * MathF.PI / 180f;
        float tol = ANGLE_TOLERANCE_DEG * MathF.PI / 180f;

        for (int i = 1; i < samples.Count; i++)
        {
            Sample prev = samples[i - 1], cur = samples[i];
            if (cur.Time < sinceTime)
                continue;

            bool crossedOutward = prev.Radius < FLICK_THRESHOLD && cur.Radius >= FLICK_THRESHOLD;
            if (!crossedOutward)
                continue;

            if (MathF.Abs(WrapPi(target - cur.Angle)) <= tol)
                return true;
        }

        return false;
    }

    /// <summary>Shortest signed angular distance, wrapped to (-pi, pi].</summary>
    protected static float WrapPi(float x) => x - MathF.Tau * MathF.Floor((x + MathF.PI) / MathF.Tau);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~StickGestureTrackerTest"`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

Stage `Garbus.Game/Input/StickGestureTracker.cs` and `Garbus.Game.Tests/StickGestureTrackerTest.cs`.
Message: `feat: add StickGestureTracker with flick detection`

---

## Task 2: `SweptThrough` — directional edge sweep

**Files:**
- Modify: `Garbus.Game/Input/StickGestureTracker.cs`
- Test: `Garbus.Game.Tests/StickGestureTrackerTest.cs`

**Interfaces:**
- Consumes: `StickGestureTracker`, `WrapPi`, `EDGE_THRESHOLD` from Task 1.
- Produces: `bool SweptThrough(int angleDeg, RotationalDirection dir, double sinceTime)`

- [ ] **Step 1: Write the failing tests**

Append these tests to `StickGestureTrackerTest.cs` (inside the class):

```csharp
        [Test]
        public void TestSweepThroughAngleInMatchingDirection()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(-20, 0.8f));   // at edge, -20deg
            t.AddSample(10, At(20, 0.8f));   // at edge, +20deg -> swept anticlockwise through 0deg

            Assert.That(t.SweptThrough(0, RotationalDirection.Anticlockwise, sinceTime: -1000), Is.True);
        }

        [Test]
        public void TestSweepWrongDirectionRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(-20, 0.8f));
            t.AddSample(10, At(20, 0.8f));   // anticlockwise sweep

            Assert.That(t.SweptThrough(0, RotationalDirection.Clockwise, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestSweepInsideEdgeRejected()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(-20, 0.3f));   // not at edge
            t.AddSample(10, At(20, 0.3f));

            Assert.That(t.SweptThrough(0, RotationalDirection.Anticlockwise, sinceTime: -1000), Is.False);
        }

        [Test]
        public void TestSweepAcrossSeamHandled()
        {
            var t = new StickGestureTracker();
            t.AddSample(0, At(170, 0.8f));    // at edge, 170deg
            t.AddSample(10, At(190, 0.8f));   // at edge, 190deg (== -170) -> anticlockwise through 180deg

            Assert.That(t.SweptThrough(180, RotationalDirection.Anticlockwise, sinceTime: -1000), Is.True);
        }
```

- [ ] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~StickGestureTrackerTest.TestSweep"`
Expected: FAIL — `SweptThrough` not defined (compile error).

- [ ] **Step 3: Implement `SweptThrough`**

Add to `StickGestureTracker` (after `FlickedTowards`):

```csharp
    /// <summary>
    /// True if, at or after <paramref name="sinceTime"/>, the stick — with both endpoints of a sample
    /// step at or beyond the edge threshold — swept through <paramref name="angleDeg"/> travelling in
    /// <paramref name="dir"/>.
    /// </summary>
    public bool SweptThrough(int angleDeg, RotationalDirection dir, double sinceTime)
    {
        float target = angleDeg * MathF.PI / 180f;
        // Increasing angle (atan2(-y, x)) is anticlockwise; Clockwise = 1, Anticlockwise = -1.
        int expectedSign = -(int)dir;

        for (int i = 1; i < samples.Count; i++)
        {
            Sample prev = samples[i - 1], cur = samples[i];
            if (cur.Time < sinceTime)
                continue;

            if (prev.Radius < EDGE_THRESHOLD || cur.Radius < EDGE_THRESHOLD)
                continue;

            float d = WrapPi(cur.Angle - prev.Angle);   // signed travel this step
            float t = WrapPi(target - prev.Angle);       // target offset from step start

            bool crossed = expectedSign > 0
                ? d > 0 && t >= 0 && t <= d
                : d < 0 && t <= 0 && t >= d;

            if (crossed)
                return true;
        }

        return false;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~StickGestureTrackerTest"`
Expected: PASS (9 tests).

- [ ] **Step 5: Commit**

Stage `Garbus.Game/Input/StickGestureTracker.cs` and `Garbus.Game.Tests/StickGestureTrackerTest.cs`.
Message: `feat: add directional edge-sweep detection to StickGestureTracker`

---

## Task 3: Wire trackers into `AnalogInputManager`

**Files:**
- Modify: `Garbus.Game/Input/AnalogInputManager.cs`

**Interfaces:**
- Consumes: `StickGestureTracker` (Task 1), `SliderCatcher` (existing).
- Produces:
  - `SliderCatcher.Position` → `Vector2` (public getter of current stick position)
  - `AnalogInputManager.StickGestureTrackers` → `ImmutableDictionary<HorizontalDirection, StickGestureTracker>`

- [ ] **Step 1: Expose the current stick position on `SliderCatcher`**

In `AnalogInputManager.cs`, inside `class SliderCatcher`, change the existing private accessor to public. Find:

```csharp
        private Vector2 joystickPosition => new Vector2(xAxisLast, yAxisLast);
```

Replace with:

```csharp
        public Vector2 Position => new Vector2(xAxisLast, yAxisLast);
```

Then update the two in-class references from `joystickPosition` to `Position`:

```csharp
            Angle = MathF.Atan2(-Position.Y, Position.X);
            Activated = Position.Length() > DEADZONE;
```

- [ ] **Step 2: Add the trackers and per-frame sampling**

In `class AnalogInputManager`, add a trackers dictionary next to `SliderCatchers`:

```csharp
    public readonly ImmutableDictionary<HorizontalDirection, StickGestureTracker> StickGestureTrackers = new Dictionary<HorizontalDirection, StickGestureTracker> {
        [HorizontalDirection.Left] = new StickGestureTracker(),
        [HorizontalDirection.Right] = new StickGestureTracker(),
    }.ToImmutableDictionary();
```

Add an `Update` override that feeds one sample per side per frame (place it after `OnJoystickAxisMove`):

```csharp
    protected override void Update()
    {
        base.Update();

        StickGestureTrackers[HorizontalDirection.Left].AddSample(Time.Current, SliderCatchers[HorizontalDirection.Left].Position);
        StickGestureTrackers[HorizontalDirection.Right].AddSample(Time.Current, SliderCatchers[HorizontalDirection.Right].Position);
    }
```

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded (no errors).

- [ ] **Step 4: Run the full test suite (nothing should regress)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all existing tests + the 9 tracker tests).

- [ ] **Step 5: Commit**

Stage `Garbus.Game/Input/AnalogInputManager.cs`.
Message: `feat: own per-side StickGestureTrackers in AnalogInputManager`

---

## Task 4: Judge `DrawableSlamCentered`

**Files:**
- Modify: `Garbus.Game/Objects/Drawables/DrawableSlamCentered.cs`

**Interfaces:**
- Consumes: `AnalogInputManager.StickGestureTrackers` (Task 3), `StickGestureTracker.FlickedTowards` (Task 1).
- Produces: a judging `DrawableSlamCentered` (Perfect on flick, Miss on window elapse).

- [ ] **Step 1: Add the resolved input manager, window constant, and `CheckForResult`**

In `DrawableSlamCentered.cs`, add these usings if not present:

```csharp
using osu.Framework.Allocation;
using Garbus.Game.Input;
```

Add a resolved field and window constant inside the class (near the top, after `sprite`):

```csharp
    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    // Symmetric first-cut window (ms). Replaced by a proper early-permissive HitWindows when the Near
    // grade lands — see the design's Deferred section.
    private const double window = 200;
```

Add the override (after `UpdateHitStateTransforms`):

```csharp
    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < -window)
            return; // early-permissive watch window not open yet

        if (analogInput.StickGestureTrackers[HitObject.Side].FlickedTowards(HitObject.AngleDeg, HitObject.StartTime - window))
        {
            ApplyMaxResult(); // Perfect
            return;
        }

        if (timeOffset > window)
            ApplyMinResult(); // Miss — window elapsed with no flick
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (no regressions).

- [ ] **Step 4: Commit**

Stage `Garbus.Game/Objects/Drawables/DrawableSlamCentered.cs`.
Message: `feat: judge SlamCentered via stick flick detection`

---

## Task 5: Judge `DrawableSlamEdge`

**Files:**
- Modify: `Garbus.Game/Objects/Drawables/DrawableSlamEdge.cs`

**Interfaces:**
- Consumes: `AnalogInputManager.StickGestureTrackers` (Task 3), `StickGestureTracker.SweptThrough` (Task 2).
- Produces: a judging `DrawableSlamEdge` (Perfect on directional sweep, Miss on window elapse).

- [ ] **Step 1: Add the resolved input manager, window constant, and `CheckForResult`**

In `DrawableSlamEdge.cs`, add these usings if not present:

```csharp
using osu.Framework.Allocation;
using Garbus.Game.Input;
```

Add a resolved field and window constant inside the class (near the top, after `sprite`):

```csharp
    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    // Symmetric first-cut window (ms). Replaced by a proper early-permissive HitWindows when the Near
    // grade lands — see the design's Deferred section.
    private const double window = 200;
```

Add the override (after `UpdateHitStateTransforms`):

```csharp
    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < -window)
            return; // early-permissive watch window not open yet

        if (analogInput.StickGestureTrackers[HitObject.Side].SweptThrough(HitObject.AngleDeg, HitObject.Direction, HitObject.StartTime - window))
        {
            ApplyMaxResult(); // Perfect
            return;
        }

        if (timeOffset > window)
            ApplyMinResult(); // Miss — window elapsed with no sweep
    }
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (no regressions).

- [ ] **Step 4: Commit**

Stage `Garbus.Game/Objects/Drawables/DrawableSlamEdge.cs`.
Message: `feat: judge SlamEdge via directional stick sweep`

---

## Self-review notes

- **Spec coverage:** StickGestureTracker (Tasks 1–2) ✓; AnalogInputManager ownership + per-frame sampling (Task 3) ✓; both drawables' `CheckForResult` with Perfect/Miss + early-permissive watch open at `StartTime − window` (Tasks 4–5) ✓; tunable placeholders as named constants (Task 1) ✓; direct unit tests as primary coverage (Tasks 1–2) ✓. Deferred items (Near grade, per-event sampling) intentionally excluded.
- **Type consistency:** `StickGestureTrackers` / `FlickedTowards(int, double)` / `SweptThrough(int, RotationalDirection, double)` / `Position` used identically across producing and consuming tasks. `HitObject.Side` and `HitObject.AngleDeg` exist on both slam types; `HitObject.Direction` (`RotationalDirection`) exists on `GarbusSlamEdge`.
