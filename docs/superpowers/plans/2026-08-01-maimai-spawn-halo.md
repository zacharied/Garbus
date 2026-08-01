# Spawn Halo and Stationary Spawn Phase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Hit objects appear on a small halo around the playfield centre instead of at the centre itself, hold still there while their spawn tween plays, and start travelling outward the instant it completes.

**Architecture:** The radius function becomes the existing linear time→radius map with a lower bound at the halo radius, so the stationary phase *is* the floor rather than a separate branch. The floor lives in `GarbusScrollingHitObjectContainer`, leaving the vendored `IScrollAlgorithm`/`ConstantScrollAlgorithm` untouched and the editor composer insulated. A single `SpawnDuration` on `GarbusScrollingInfo` owns both the hold length and every drawable's spawn tween duration, so the two cannot drift.

**Tech Stack:** C# 12, .NET, osu-framework 2026.629.0, NUnit visual test scenes.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-08-01-maimai-spawn-halo-design.md`. Read it before starting.
- Build: `dotnet build Garbus.Desktop.slnf` — the iOS slnf needs a workload that is not installed.
- Tests: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`
- **No new warnings, including in tests.** Build and test output stays warning-clean.
- **No historical context in docs.** Present tense, no phase numbers, no version bumps, no "previously…" framing.
- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- **Do not modify** `Garbus.Game/Gameplay/UI/Scrolling/IScrollAlgorithm.cs` or `ConstantScrollAlgorithm.cs`. They are vendored osu.Game files under the deviate-minimally rule, and keeping them halo-free is what guarantees the editor composer stays insulated.
- **Do not run the app.** Tests are how you verify.
- Test expectations are hand-derived and trace to a spec doc or a commented calibration anchor. Never compute an expected value using the implementation's own constants or functions. No test may be a strict subset of a sibling.
- Terminology: osu's "beatmap" is a **chart** here.

## Parameter reference (used throughout)

```
haloRadius = ScrollLength × SpawnHaloFraction
travelTime = TimeRange × (1 − SpawnHaloFraction)
leadTime   = travelTime + SpawnDuration

radius(Δ) = max(haloRadius, ScrollLength − Δ × ScrollLength / TimeRange)      where Δ = objectTime − currentTime
```

Defaults: `TimeRange` 700 ms, `SpawnHaloFraction` 0.12, `SpawnDuration` 125 ms.

## File Structure

**Created:**
- `Garbus.Game.Tests/Visual/TestSceneSpawnHalo.cs` — pins the radius map and entry lifetime at the container level.
- `Garbus.Game.Tests/Visual/TestSceneSpawnTween.cs` — pins that the spawn tween finishes exactly when motion begins.
- `Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs` — live sliders for halo fraction and spawn duration.

**Modified:**
- `docs/presentation-specs/Playfield.md` — the authoritative statement of the halo, the hold, and the radius function. Written first; the tests anchor to it.
- `docs/agents/gameplay.md` — its layout section states objects spawn at the centre.
- `Garbus.Game/Gameplay/UI/Scrolling/GarbusScrollingInfo.cs` — owns the three parameters and the derived timings.
- `Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs` — applies the floor; owns display start time; sheds dead `LengthAtTime`.
- `Garbus.Game/Objects/Drawables/DrawableGarbusHitObject.cs` — exposes `SpawnAnimationDuration`, re-anchors `InitialLifetimeOffset`.
- Eight spawn tween call sites (Task 4).

---

### Task 1: Write the presentation spec

The spec doc is the anchor every later test cites, so it lands first. `docs/presentation-specs/Playfield.md` currently says objects "emerge from the center of the circle", which this feature makes false.

**Files:**
- Modify: `docs/presentation-specs/Playfield.md:9` (the "Playfield model" paragraph)
- Modify: `docs/agents/gameplay.md:15-16` (the "Layout" opening sentence)

**Interfaces:**
- Consumes: nothing.
- Produces: the "Spawn halo" section that Task 3 and Task 4 tests cite by name in their calibration-anchor comments.

- [ ] **Step 1: Replace the playfield-model paragraph in `docs/presentation-specs/Playfield.md`**

Find this line (line 9):

```markdown
The gameplay is presented as a large circle (the playfield) on the player's display. Small visuals (hit objects) emerge from the center of the circle and move toward the outer circumference. The shape and color of a hit object represents a prompt that requests some action to be performed (see `../rules-specs/Inputs.md` for the physical mapping).
```

Replace it with:

```markdown
The gameplay is presented as a large circle (the playfield) on the player's display. Small visuals (hit objects) appear on the **spawn halo** — a small circle concentric with the playfield — and move outward toward the outer circumference. The shape and color of a hit object represents a prompt that requests some action to be performed (see `../rules-specs/Inputs.md` for the physical mapping).
```

- [ ] **Step 2: Add the "Spawn halo" section**

Insert this immediately after the "Playfield model" section, before the "Hit object presentation" heading:

```markdown
## Spawn halo and spawn phase

A hit object does not appear at the playfield centre. It appears on the **spawn halo**, a circle
concentric with the playfield whose radius is a fixed fraction of the playfield radius, at the
object's own Angle. It holds that position, motionless, while its spawn animation plays. The instant
that animation completes it begins travelling outward, reaching the ring at its StartTime.

Three parameters govern this:

| Parameter | Meaning |
| --- | --- |
| `TimeRange` | Sets radial velocity: one playfield radius per `TimeRange`. |
| `SpawnHaloFraction` | Halo radius as a fraction of the playfield radius. |
| `SpawnDuration` | How long an object holds on the halo — and how long its spawn animation runs. |

Writing `ScrollLength` for the playfield radius and `Δ` for the time remaining until an object's
StartTime, the derived quantities and the radius function are:

    haloRadius = ScrollLength × SpawnHaloFraction
    travelTime = TimeRange × (1 − SpawnHaloFraction)
    leadTime   = travelTime + SpawnDuration

    radius(Δ) = max(haloRadius, ScrollLength − Δ × ScrollLength / TimeRange)

An object appears at `Δ = leadTime` and holds at `haloRadius` until `Δ = travelTime`, where the floor
and the ramp meet without a seam. It reaches `ScrollLength` at `Δ = 0`. Radial velocity through the
travel phase is `ScrollLength / TimeRange`, independent of the halo.

The spawn animation's duration and the hold are the same quantity, so an object is never still
growing while it moves, and never fully grown while it is still.

The halo is not drawn. Objects simply appear at that radius.

An object with duration whose span falls entirely inside the hold window renders as a stub at the
halo before extending outward, because every point along it maps to `haloRadius`.
```

- [ ] **Step 3: Fix the layout sentence in `docs/agents/gameplay.md`**

Find this (lines 15-16, under "## Layout: playfield → ring → lanes"):

```markdown
Objects spawn at the **centre** of a circular playfield and travel **outward** to the ring, where
they are judged in time with the music.
```

Replace with:

```markdown
Objects spawn on a small **halo** around the centre of a circular playfield, hold still there while
their spawn animation plays, then travel **outward** to the ring, where they are judged in time with
the music. The halo radius, the hold duration, and the time→radius map are specified in
[`docs/presentation-specs/Playfield.md`](../presentation-specs/Playfield.md).
```

- [ ] **Step 4: Check the top-of-file staleness banner**

`docs/presentation-specs/Playfield.md:1` reads `*The contents of this document differ from the current implementation.*`. Leave it exactly as is — it is a pre-existing note about other sections, and this plan does not resolve it.

- [ ] **Step 5: Commit**

```bash
git add docs/presentation-specs/Playfield.md docs/agents/gameplay.md
git commit -m "docs: specify the spawn halo and stationary spawn phase"
```

---

### Task 2: Add the parameters to GarbusScrollingInfo

**Files:**
- Modify: `Garbus.Game/Gameplay/UI/Scrolling/GarbusScrollingInfo.cs` (whole file)

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `GarbusScrollingInfo.DEFAULT_TIME_RANGE` / `DEFAULT_SPAWN_HALO_FRACTION` / `DEFAULT_SPAWN_DURATION` — `public const double`
  - `GarbusScrollingInfo.SpawnHaloFraction` / `SpawnDuration` — `public readonly BindableDouble`
  - `GarbusScrollingInfo.TravelTime` / `LeadTime` — `public double` computed properties

This task has no test of its own: it introduces data with no behaviour, and Tasks 3 and 4 pin every value it produces. Adding a test here that reads back a default it just set would be a tautology.

- [ ] **Step 1: Rewrite `GarbusScrollingInfo.cs`**

Replace the whole file with:

```csharp
// Replaces osu.Game's IScrollingInfo/ScrollingTestContainer.TestScrollingInfo pair. Garbus's
// playfield is radial, so there is no scrolling direction — only the visible time range, the spawn
// halo an object appears on, and the algorithm mapping time to distance-from-centre.

using osu.Framework.Bindables;

namespace Garbus.Game.Gameplay.UI.Scrolling
{
    public class GarbusScrollingInfo
    {
        public const double DEFAULT_TIME_RANGE = 700;
        public const double DEFAULT_SPAWN_HALO_FRACTION = 0.12;
        public const double DEFAULT_SPAWN_DURATION = 125;

        /// <summary>
        /// Sets radial velocity: an object covers one playfield radius per <see cref="TimeRange"/>.
        /// </summary>
        public readonly BindableDouble TimeRange = new BindableDouble(DEFAULT_TIME_RANGE);

        /// <summary>
        /// The radius of the spawn halo objects appear on, as a fraction of the playfield radius.
        /// Dimensionless so the halo tracks the playfield through a resize.
        /// </summary>
        public readonly BindableDouble SpawnHaloFraction = new BindableDouble(DEFAULT_SPAWN_HALO_FRACTION);

        /// <summary>
        /// How long an object holds motionless on the halo — and how long its spawn animation runs.
        /// One quantity, so an object is never still growing while it moves.
        /// </summary>
        public readonly BindableDouble SpawnDuration = new BindableDouble(DEFAULT_SPAWN_DURATION);

        /// <summary>
        /// The algorithm which controls hit object positions and sizes.
        /// </summary>
        public readonly Bindable<IScrollAlgorithm> Algorithm = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());

        /// <summary>How long an object spends travelling from the halo to the ring (ms).</summary>
        public double TravelTime => TimeRange.Value * (1 - SpawnHaloFraction.Value);

        /// <summary>How long before its StartTime an object appears on the halo (ms).</summary>
        public double LeadTime => TravelTime + SpawnDuration.Value;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: succeeds, no new warnings. Nothing consumes the new members yet.

- [ ] **Step 3: Commit**

```bash
git add Garbus.Game/Gameplay/UI/Scrolling/GarbusScrollingInfo.cs
git commit -m "feat: add spawn halo and spawn duration to scrolling info"
```

---

### Task 3: Floor the radius map at the halo

**Files:**
- Modify: `Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneSpawnHalo.cs` (create)

**Interfaces:**
- Consumes: `GarbusScrollingInfo.SpawnHaloFraction`, `SpawnDuration` (Task 2).
- Produces:
  - `GarbusScrollingHitObjectContainer.HaloRadius` — `public float`
  - `GarbusScrollingHitObjectContainer.TravelTime` / `LeadTime` — `public double`
  - `ProgressAtTime` and `DistanceFromCentreAtTime` keep their existing signatures; only their lower bound changes.
  - `LengthAtTime` is **removed**.

**Calibration anchor for the tests.** Container sized 400×400 → `ScrollLength` = 200. `SpawnHaloFraction` 0.25, `TimeRange` 800 ms, `SpawnDuration` 100 ms — chosen for exact arithmetic and deliberately *not* the production defaults, so the tests pin the map rather than the defaults. Hand-derived from the spec's formulas:

```
haloRadius = 200 × 0.25       =  50 px
travelTime = 800 × (1 − 0.25) = 600 ms
leadTime   = 600 + 100        = 700 ms
velocity   = 200 / 800        = 0.25 px/ms
```

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneSpawnHalo.cs`:

```csharp
// Pins the spawn-halo radius map specified in docs/presentation-specs/Playfield.md ("Spawn halo and
// spawn phase").
//
// Calibration anchor — a 400x400 container gives ScrollLength 200; the scrolling parameters are set
// to values chosen for exact arithmetic, deliberately not the production defaults:
//   SpawnHaloFraction 0.25, TimeRange 800 ms, SpawnDuration 100 ms
// Hand-derived from the spec's formulas:
//   haloRadius = 200 * 0.25       =  50 px
//   travelTime = 800 * (1 - 0.25) = 600 ms
//   leadTime   = 600 + 100        = 700 ms
//   velocity   = 200 / 800        = 0.25 px/ms
// Every expected value below is derived by hand from those four numbers.

using Garbus.Game.Gameplay.Objects; // HitObjectLifetimeEntry
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSpawnHalo : GarbusTestScene
    {
        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private GarbusScrollingHitObjectContainer container = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            // Set TimeRange on the scrolling info directly rather than through the ScrollSpeed
            // config: the config->TimeRange binding only fires on config change, so a direct write
            // holds, and 800 ms is not reachable from the tenths-snapped speed slider anyway.
            AddStep("set scroll parameters", () =>
            {
                scrollingInfo.TimeRange.Value = 800;
                scrollingInfo.SpawnHaloFraction.Value = 0.25;
                scrollingInfo.SpawnDuration.Value = 100;
            });

            AddStep("create container", () => Child = container = new GarbusScrollingHitObjectContainer
            {
                RelativeSizeAxes = Axes.None,
                Size = new Vector2(400),
            });

            AddUntilStep("scroll length 200", () => Precision.AlmostEquals(container.ScrollLength, 200, 0.001));
        }

        // Δ is passed as `time` with currentTime 0, so `time` reads directly as time-until-ring.
        private float radiusAt(double delta) => container.ProgressAtTime(delta, 0);

        private float unclampedRadiusAt(double delta) => container.DistanceFromCentreAtTime(delta, 0);

        [Test]
        public void TestRadiusHoldsAtHaloThroughSpawnWindowThenLeaves()
        {
            AddAssert("halo at spawn (delta 700)", () => Precision.AlmostEquals(radiusAt(700), 50, 0.001));
            AddAssert("halo mid-hold (delta 650)", () => Precision.AlmostEquals(radiusAt(650), 50, 0.001));
            AddAssert("halo at travel start (delta 600)", () => Precision.AlmostEquals(radiusAt(600), 50, 0.001));
            // 200 - 599 * 0.25 = 50.25 — one millisecond past the boundary the ramp has taken over,
            // and it takes over exactly at the halo, with no seam.
            AddAssert("just past travel start (delta 599)", () => Precision.AlmostEquals(radiusAt(599), 50.25, 0.001));
        }

        [Test]
        public void TestTravelPhaseReachesRingAtStartTime()
        {
            // Halfway through the 600 ms travel, halfway between halo and ring: (50 + 200) / 2.
            AddAssert("midway (delta 300)", () => Precision.AlmostEquals(radiusAt(300), 125, 0.001));
            AddAssert("ring at start time (delta 0)", () => Precision.AlmostEquals(radiusAt(0), 200, 0.001));
        }

        [Test]
        public void TestDistanceFromCentreExtrapolatesPastRingWhileProgressClamps()
        {
            // 100 ms after the ring, at 0.25 px/ms: 200 + 25 = 225. The unclamped accessor keeps
            // extrapolating so callers can clip what the outer edge has consumed.
            AddAssert("unclamped overshoots (delta -100)", () => Precision.AlmostEquals(unclampedRadiusAt(-100), 225, 0.001));
            AddAssert("clamped pins at ring (delta -100)", () => Precision.AlmostEquals(radiusAt(-100), 200, 0.001));
        }

        [Test]
        public void TestRadialVelocityIsScrollLengthOverTimeRange()
        {
            // 200 - 200 * 0.25 = 150 and 200 - 100 * 0.25 = 175: 25 px covered in 100 ms.
            AddAssert("radius at delta 200", () => Precision.AlmostEquals(radiusAt(200), 150, 0.001));
            AddAssert("radius at delta 100", () => Precision.AlmostEquals(radiusAt(100), 175, 0.001));
            AddAssert("0.25 px per ms", () => Precision.AlmostEquals((radiusAt(100) - radiusAt(200)) / 100, 0.25, 0.001));
        }

        [Test]
        public void TestHoldWindowIsInvariantToTimeRange()
        {
            // Halving TimeRange to 400 halves travelTime to 300 ms and doubles velocity to 0.5 px/ms,
            // but SpawnDuration is a fixed constant, so the hold stays 100 ms: leadTime 400,
            // travel start still 100 ms after spawn. haloRadius is unchanged at 200 * 0.25 = 50.
            AddStep("halve time range", () => scrollingInfo.TimeRange.Value = 400);

            AddAssert("halo at spawn (delta 400)", () => Precision.AlmostEquals(radiusAt(400), 50, 0.001));
            AddAssert("halo at travel start (delta 300)", () => Precision.AlmostEquals(radiusAt(300), 50, 0.001));
            // 200 - 299 * 0.5 = 50.5
            AddAssert("just past travel start (delta 299)", () => Precision.AlmostEquals(radiusAt(299), 50.5, 0.001));
        }

        [Test]
        public void TestEntryLifetimeStartsAtLeadTime()
        {
            HitObjectLifetimeEntry entry = null!;

            AddStep("add note entry", () =>
            {
                var note = new CardinalNote { StartTime = 10_000, AngleDeg = 0 };
                note.ApplyDefaults();
                container.Add(entry = new HitObjectLifetimeEntry(note));
            });

            // 10000 - 700. A cardinal note's interaction lead is its 200 ms early-miss window
            // (CardinalNoteHitWindows.early_miss_window), so leadTime is the larger of the two and
            // decides when the entry goes alive.
            AddAssert("alive from 9300", () => Precision.AlmostEquals(entry.LifetimeStart, 9300, 0.001));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSpawnHalo"`
Expected: compile error — `GarbusScrollingHitObjectContainer` has no `HaloRadius`, and the map has no floor.

- [ ] **Step 3: Bind the new parameters in the container**

In `Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs`, add two bindables next to the existing pair (currently lines 25-26):

```csharp
    private readonly IBindable<double> timeRange = new BindableDouble();
    private readonly IBindable<IScrollAlgorithm> algorithm = new Bindable<IScrollAlgorithm>();
    private readonly IBindable<double> spawnHaloFraction = new BindableDouble();
    private readonly IBindable<double> spawnDuration = new BindableDouble();
```

Then extend `load()` so both bind and both invalidate the layout, exactly as `timeRange` does:

```csharp
    [BackgroundDependencyLoader]
    private void load()
    {
        var info = scrollingInfo ?? fallbackScrollingInfo;

        timeRange.BindTo(info.TimeRange);
        algorithm.BindTo(info.Algorithm);
        spawnHaloFraction.BindTo(info.SpawnHaloFraction);
        spawnDuration.BindTo(info.SpawnDuration);

        timeRange.ValueChanged += _ => layoutCache.Invalidate();
        algorithm.ValueChanged += _ => layoutCache.Invalidate();
        spawnHaloFraction.ValueChanged += _ => layoutCache.Invalidate();
        spawnDuration.ValueChanged += _ => layoutCache.Invalidate();
    }
```

- [ ] **Step 4: Add the derived map quantities and apply the floor**

Still in `GarbusScrollingHitObjectContainer.cs`, replace the `ProgressAtTime` / `ScrollLength` / `DistanceFromCentreAtTime` block (currently lines 82-108) with:

```csharp
    /// <summary>
    /// The radius of the spawn halo objects appear on, in local pixels. Objects hold here, motionless,
    /// while their spawn animation plays. Specified in docs/presentation-specs/Playfield.md.
    /// </summary>
    public float HaloRadius => scrollLength * (float)spawnHaloFraction.Value;

    /// <summary>How long an object spends travelling from the halo to the ring (ms).</summary>
    public double TravelTime => timeRange.Value * (1 - spawnHaloFraction.Value);

    /// <summary>
    /// How long before its own time an object appears on the halo (ms) — the hold plus the travel.
    /// </summary>
    public double LeadTime => TravelTime + spawnDuration.Value;

    public float ProgressAtTime(double time, double currentTime, double? originTime = null)
        => MathF.Min(scrollLength, DistanceFromCentreAtTime(time, currentTime, originTime));

    public float ProgressAtTime(double time) => ProgressAtTime(time, Time.Current);

    /// <summary>
    /// The distance from the playfield centre to the outer ring, in local pixels. An object reaches
    /// the ring exactly at its own time.
    /// </summary>
    public float ScrollLength => scrollLength;

    /// <summary>
    /// The distance from the centre at which an object with the given <paramref name="time"/> should be
    /// drawn, floored at <see cref="HaloRadius"/> — an object never appears inside the halo, and the floor
    /// is what holds it still through its spawn animation. Unlike <see cref="ProgressAtTime(double,double,double?)"/>
    /// this is not bounded above, so once the time has passed the ring it keeps extrapolating outward and
    /// callers can clip the portion of a shape the outer edge has consumed rather than pinning it there.
    /// </summary>
    public float DistanceFromCentreAtTime(double time, double currentTime, double? originTime = null)
    {
        float scrollPosition = algorithm.Value.PositionAt(time, currentTime, timeRange.Value, scrollLength, originTime);
        return MathF.Max(HaloRadius, scrollLength - scrollPosition);
    }

    public float DistanceFromCentreAtTime(double time) => DistanceFromCentreAtTime(time, Time.Current);
```

- [ ] **Step 5: Delete `LengthAtTime`**

Remove this method entirely (currently lines 119-122):

```csharp
    public float LengthAtTime(double startTime, double endTime)
    {
        return algorithm.Value.GetLength(startTime, endTime, timeRange.Value, scrollLength);
    }
```

It takes no `currentTime`, so it cannot express a length that now depends on when you look, and it has no callers — the editor blueprints that call `LengthAtTime` reach the editor's linear `ScrollingHitObjectContainer` through `GarbusSelectionBlueprint.HitObjectContainer` (`Garbus.Game/Edit/Blueprints/GarbusSelectionBlueprint.cs:34`), not this class.

- [ ] **Step 6: Anchor the display start time on lead time**

Replace `computeDisplayStartTime` (currently lines 237-240) with:

```csharp
    // The object appears on the halo one lead time before its own time: the travel, plus the hold it
    // spends motionless there while its spawn animation plays. Computed directly rather than through
    // IScrollAlgorithm.GetDisplayStartTime — the algorithm is deliberately halo-unaware, which is what
    // keeps the editor composer's own ConstantScrollAlgorithm insulated from this behaviour.
    private double computeDisplayStartTime(HitObjectLifetimeEntry entry) => entry.HitObject.StartTime - LeadTime;
```

Leave `setComputedLifetime`'s `entry.LifetimeEnd = entry.HitObject.GetEndTime() + timeRange.Value` alone. That pad covers the post-ring hit animation and has nothing to do with spawning.

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSpawnHalo"`
Expected: all six tests PASS.

- [ ] **Step 8: Run the whole suite to catch regressions**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`
Expected: PASS. If a scrolling or gameplay scene fails, read the failure before changing it — a position assertion written against the old centre-spawn map may be legitimately stale and need rebasing onto the halo, but a lifetime or judgement failure is a real regression in this task.

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs Garbus.Game.Tests/Visual/TestSceneSpawnHalo.cs
git commit -m "feat: floor the time-to-radius map at the spawn halo"
```

---

### Task 4: Couple the spawn tween to the hold

**Files:**
- Modify: `Garbus.Game/Objects/Drawables/DrawableGarbusHitObject.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableCardinalNote.cs:62`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSlamCentered.cs:50`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSlamEdge.cs:50`
- Modify: `Garbus.Game/Objects/Drawables/DrawableShoulderNote.cs:99`
- Modify: `Garbus.Game/Objects/Drawables/DrawableShoulderHoldNote.cs:131`
- Modify: `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNote.cs:84-85`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderBody.cs:349-351`
- Test: `Garbus.Game.Tests/Visual/TestSceneSpawnTween.cs` (create)

**Interfaces:**
- Consumes: `GarbusScrollingInfo.SpawnDuration`, `LeadTime` (Task 2).
- Produces: `DrawableGarbusHitObject<T>.SpawnAnimationDuration` — `protected double`. Every drawable with a spawn tween descends from `DrawableGarbusHitObject<T>`, so this one member reaches all eight call sites.

**Calibration anchor for the test.** `SpawnHaloFraction` 0.25, `TimeRange` 800 ms. Hand-derived: `travelTime` = 800 × 0.75 = 600 ms, so a note with StartTime 10000 starts moving at t = 9400 regardless of `SpawnDuration`. With `SpawnDuration` 100 it appears at t = 9300; with `SpawnDuration` 300 it appears at t = 9100. In both cases the tween must reach full scale exactly at t = 9400.

`DrawableShoulderNote` is the subject because its spawn tween targets the drawable itself (`this.ScaleTo`), so `Scale` is readable without reaching into a private child — no production member needs widening to observe it.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneSpawnTween.cs`:

```csharp
// Pins the coupling specified in docs/presentation-specs/Playfield.md ("Spawn halo and spawn phase"):
// the spawn animation's duration and the motionless hold are one quantity, so the tween reaches full
// scale exactly when the object starts moving — never still growing while it moves, never fully grown
// while still.
//
// Calibration anchor — SpawnHaloFraction 0.25 and TimeRange 800 ms give travelTime = 800 * 0.75 =
// 600 ms, so a note at StartTime 10000 leaves the halo at t = 9400 whatever SpawnDuration is. The
// spawn instant moves with it: leadTime = 600 + SpawnDuration, so 100 ms spawns at 9300 and 300 ms
// spawns at 9100, and both must land full scale on 9400.
//
// The subject is a shoulder note because its spawn tween targets the drawable itself, so Scale is
// observable without reaching into a private child.

using Garbus.Game.Core;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osuTK;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSpawnTween : GarbusTestScene
    {
        private const double note_start_time = 10_000;

        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private readonly ManualClock manualClock = new ManualClock { Rate = 0 };

        private Gameplay.Objects.Drawables.DrawableHitObject drawable = null!;

        private void setUpScene(double spawnDuration)
        {
            AddStep($"scroll parameters, spawn {spawnDuration}ms", () =>
            {
                scrollingInfo.TimeRange.Value = 800;
                scrollingInfo.SpawnHaloFraction.Value = 0.25;
                scrollingInfo.SpawnDuration.Value = spawnDuration;
            });

            AddStep("build playfield", () =>
            {
                var note = new ShoulderNote { StartTime = note_start_time, Side = HorizontalDirection.Right };
                note.ApplyDefaults();

                // Park the clock before the note exists so the drawable applies with the parameters above.
                manualClock.CurrentTime = note_start_time - 2000;

                GarbusPlayfield playfield;

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                    },
                };

                playfield.Add(drawable = PlayScreen.CreateDrawableRepresentation(note));
            });
        }

        private void seek(double time) => AddStep($"seek {time}", () => manualClock.CurrentTime = time);

        [Test]
        public void TestTweenReachesFullScaleWhenMotionBegins()
        {
            setUpScene(100);

            // leadTime = 600 + 100 = 700, so the note appears at 10000 - 700 = 9300.
            seek(9300);
            AddAssert("starts from nothing", () => Precision.AlmostEquals(drawable.Scale.X, 0, 0.01));

            seek(9350);
            AddAssert("growing mid-hold", () => drawable.Scale.X > 0 && drawable.Scale.X < 1);

            // travelTime = 600, so motion begins at 10000 - 600 = 9400 — and the tween ends there.
            seek(9400);
            AddAssert("full scale as motion begins", () => Precision.AlmostEquals(drawable.Scale.X, 1, 0.01));
        }

        [Test]
        public void TestLongerSpawnDurationMovesSpawnEarlierAndStillLandsOnMotion()
        {
            setUpScene(300);

            // leadTime = 600 + 300 = 900, so the note appears at 10000 - 900 = 9100.
            seek(9100);
            AddAssert("starts from nothing", () => Precision.AlmostEquals(drawable.Scale.X, 0, 0.01));

            // 9300 is the spawn instant of the 100 ms case; with a 300 ms tween it is only partway.
            seek(9300);
            AddAssert("still growing where the short tween would have started", () => drawable.Scale.X > 0 && drawable.Scale.X < 1);

            // Motion still begins at 9400, so the longer tween must still land exactly there.
            seek(9400);
            AddAssert("full scale as motion begins", () => Precision.AlmostEquals(drawable.Scale.X, 1, 0.01));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSpawnTween"`
Expected: FAIL. The tween is still a hardcoded 125 ms anchored at `StartTime − TimeRange` (t = 9200), so at t = 9400 it finished long ago in the first case and never started in the second.

- [ ] **Step 3: Give `DrawableGarbusHitObject` the shared duration**

Replace `Garbus.Game/Objects/Drawables/DrawableGarbusHitObject.cs` with:

```csharp
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI.Scrolling;
using osu.Framework.Allocation;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableGarbusHitObject<T> : DrawableHitObject<GarbusHitObject>
    where T : GarbusHitObject
{
    public new T HitObject => (T)base.HitObject;

    [Resolved(CanBeNull = true)]
    private GarbusScrollingInfo? scrollingInfo { get; set; }

    // Mirrors GarbusScrollingHitObjectContainer's fallback: bare test scenes without a cached
    // GarbusScrollingInfo still get the production defaults rather than a second set of literals.
    private readonly GarbusScrollingInfo fallbackScrollingInfo = new GarbusScrollingInfo();

    private GarbusScrollingInfo scrolling => scrollingInfo ?? fallbackScrollingInfo;

    /// <summary>
    /// How long this object's spawn animation runs. The same quantity as the motionless hold it spends
    /// on the spawn halo, so the animation finishes exactly as the object starts moving. Specified in
    /// docs/presentation-specs/Playfield.md.
    /// </summary>
    protected double SpawnAnimationDuration => scrolling.SpawnDuration.Value;

    // Anchor UpdateInitialTransforms at the note's halo-spawn time (StartTime − LeadTime) so the spawn
    // animation plays across exactly the window the note spends motionless on the halo and, being
    // absolute-sequenced, replays on rewind/restart and under editor-preview scrubbing. Base 10000
    // would fire it ~10s early / invisibly.
    protected override double InitialLifetimeOffset => scrolling.LeadTime;

    public DrawableGarbusHitObject(T hitObject)
        : base(hitObject)
    {
    }

    /// <summary>Number of family members this object has played. Test seam.</summary>
    public int SamplesPlayCount => Samples?.PlayCount ?? 0;

    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);
}
```

- [ ] **Step 4: Point all eight tween sites at the shared duration**

`DrawableCardinalNote.cs:62` — replace:

```csharp
            sprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
```

with:

```csharp
            sprite.ScaleTo(0).ScaleTo(1, SpawnAnimationDuration, Easing.In);
```

`DrawableSlamCentered.cs:50` and `DrawableSlamEdge.cs:50` — the same line appears in both; replace each:

```csharp
        sprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
```

with:

```csharp
        sprite.ScaleTo(0).ScaleTo(1, SpawnAnimationDuration, Easing.In);
```

`DrawableShoulderNote.cs:99` — replace:

```csharp
        this.ScaleTo(0).ScaleTo(1, 125, Easing.In);
```

with:

```csharp
        this.ScaleTo(0).ScaleTo(1, SpawnAnimationDuration, Easing.In);
```

`DrawableShoulderHoldNote.cs:131` — the same line; replace it the same way.

`DrawableCardinalHoldNote.cs:84-85` — replace:

```csharp
        headSprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
        body.FadeInFromZero(100, Easing.In);
```

with:

```csharp
        headSprite.ScaleTo(0).ScaleTo(1, SpawnAnimationDuration, Easing.In);
        body.FadeInFromZero(SpawnAnimationDuration, Easing.In);
```

`DrawableSliderBody.cs:349-351` — replace:

```csharp
        bodyVisual.FadeInFromZero(100, Easing.In);
        escapeVisual.FadeInFromZero(100, Easing.In);
        headContainer.FadeInFromZero(100, Easing.In);
```

with:

```csharp
        bodyVisual.FadeInFromZero(SpawnAnimationDuration, Easing.In);
        escapeVisual.FadeInFromZero(SpawnAnimationDuration, Easing.In);
        headContainer.FadeInFromZero(SpawnAnimationDuration, Easing.In);
```

The 100 ms body fades become 125 ms at the defaults. That is intended: a body that finishes fading before its object starts moving reads as a stutter.

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSpawnTween"`
Expected: both tests PASS.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`
Expected: PASS. Pay particular attention to `TestSceneMiniPreview` and `TestSceneAutoHit` — the editor preview reuses these drawables and its auto-hit path force-schedules the Hit exit at apply time, so a changed initial-transform anchor surfaces there first.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Objects/Drawables/ Garbus.Game.Tests/Visual/TestSceneSpawnTween.cs
git commit -m "feat: run the spawn animation across the halo hold"
```

---

### Task 5: Add the tuning scene

Required by the new-visual-elements rule in `AGENTS.md`: a visual element ships with a Tuning scene exposing its parameters as live controls, so the look can be eyeballed in the visual test browser. Settling the `SpawnHaloFraction` and `SpawnDuration` defaults is the point of this scene.

**Files:**
- Test: `Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs` (create)

**Interfaces:**
- Consumes: `GarbusScrollingInfo.SpawnHaloFraction`, `SpawnDuration`, `TimeRange` (Task 2). All three are live bindables, so no rebuild is needed on change — unlike `TestSceneSliderGlowTuning`, whose glow parameters bake into a texture at construction.

- [ ] **Step 1: Create the scene**

Create `Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs`:

```csharp
// Interactive tuning scene for the spawn halo: halo radius, spawn duration and scroll speed are
// sliders in the test browser's step sidebar, over a looping stream of mixed objects on all four
// cardinal angles plus both shoulders — so the hold reads on point notes and durationed objects at
// once. All three parameters are live bindables, so nothing rebuilds on change. [Explicit] so it
// never runs in a headless "run all"; pick it in the test browser.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects; // GetEndTime extension
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneSpawnHaloTuning : GarbusTestScene
    {
        private const double stream_start = 2000;
        private const double stream_end = 14_000;
        private const double note_gap = 400;
        private const double hold_length = 900;

        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };

        private double loopStart;
        private double loopEnd;
        private float playbackRate = 1;

        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        public TestSceneSpawnHaloTuning()
        {
            AddSliderStep("spawn halo fraction", 0f, 0.4f, (float)GarbusScrollingInfo.DEFAULT_SPAWN_HALO_FRACTION,
                v => { if (IsLoaded) scrollingInfo.SpawnHaloFraction.Value = v; });

            AddSliderStep("spawn duration (ms)", 0f, 500f, (float)GarbusScrollingInfo.DEFAULT_SPAWN_DURATION,
                v => { if (IsLoaded) scrollingInfo.SpawnDuration.Value = v; });

            AddSliderStep("scroll time range (ms)", 200f, 2000f, (float)GarbusScrollingInfo.DEFAULT_TIME_RANGE,
                v => { if (IsLoaded) scrollingInfo.TimeRange.Value = v; });

            AddSliderStep("playback rate", 0f, 2f, 1f, v => playbackRate = v);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var objects = buildStream().ToList();

            foreach (var hitObject in objects)
                hitObject.ApplyDefaults();

            // Loop from before the first object spawns to after the last one clears the ring.
            loopStart = objects.Min(o => o.StartTime) - 2000;
            loopEnd = objects.Max(o => o.GetEndTime()) + 1500;

            manualClock.CurrentTime = loopStart;

            GarbusPlayfield playfield;

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = new FramedClock(manualClock),
                Child = new GarbusInputManager
                {
                    Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                },
            };

            foreach (var hitObject in objects)
                playfield.Add(PlayScreen.CreateDrawableRepresentation(hitObject));
        }

        // Cardinal notes cycling the four angles, a shoulder note every fourth beat, and a cardinal
        // hold every eighth — enough variety to see the halo hold on point and durationed objects.
        private static IEnumerable<GarbusHitObject> buildStream()
        {
            int[] angles = { 0, 90, 180, 270 };
            int i = 0;

            for (double t = stream_start; t < stream_end; t += note_gap, i++)
            {
                if (i % 8 == 7)
                {
                    yield return new CardinalHoldNote { StartTime = t, AngleDeg = angles[i % 4], Duration = hold_length };
                    continue;
                }

                if (i % 4 == 3)
                {
                    yield return new ShoulderNote
                    {
                        StartTime = t,
                        Side = i % 8 == 3 ? HorizontalDirection.Left : HorizontalDirection.Right,
                    };
                    continue;
                }

                yield return new CardinalNote { StartTime = t, AngleDeg = angles[i % 4] };
            }
        }

        protected override void Update()
        {
            base.Update();

            manualClock.CurrentTime += Time.Elapsed * playbackRate;

            if (manualClock.CurrentTime > loopEnd)
                manualClock.CurrentTime = loopStart;
        }
    }
}
```

- [ ] **Step 2: Build and confirm the scene compiles warning-clean**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: succeeds with no new warnings.

`CardinalHoldNote` has a settable `double Duration` and a `required int AngleDeg`, and `ShoulderNote` has a `required HorizontalDirection Side` with a derived get-only `AngleDeg` — the object initialisers above match. Do not add a property to any production type to make this scene compile.

- [ ] **Step 3: Confirm the scene is excluded from headless runs**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneSpawnHaloTuning"`
Expected: zero tests run — `[Explicit]` keeps it out of automated runs. This confirms the attribute took.

- [ ] **Step 4: Commit**

```bash
git add Garbus.Game.Tests/Tuning/TestSceneSpawnHaloTuning.cs
git commit -m "test: add a spawn halo tuning scene"
```

---

### Task 6: Full verification sweep

**Files:** none created or modified unless a failure demands it.

**Interfaces:**
- Consumes: everything from Tasks 1-5.
- Produces: a warning-clean build and a green suite.

- [ ] **Step 1: Clean build**

Run: `dotnet build Garbus.Desktop.slnf --no-incremental`
Expected: `0 Warning(s)`. If any warning appears in a file this branch touched, fix it before continuing — `AGENTS.md` forbids adding warnings, including in tests.

- [ ] **Step 2: Full test suite**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj`
Expected: all tests PASS, no warnings in the test output.

- [ ] **Step 3: Confirm the editor stayed insulated**

Run: `dotnet test Garbus.Game.Tests/Garbus.Game.Tests.csproj --filter "FullyQualifiedName~Editor"`
Expected: PASS. The editor composer holds its own `EditorScrollingInfo` with a separate `ConstantScrollAlgorithm`, so its linear timeline must be completely unaffected. A failure here means halo logic leaked into the shared algorithm — check that `IScrollAlgorithm.cs` and `ConstantScrollAlgorithm.cs` are unmodified:

```bash
git diff --stat master...HEAD -- Garbus.Game/Gameplay/UI/Scrolling/IScrollAlgorithm.cs Garbus.Game/Gameplay/UI/Scrolling/ConstantScrollAlgorithm.cs
```

Expected: empty output.

- [ ] **Step 4: Confirm the spec and the code agree**

Re-read `docs/presentation-specs/Playfield.md`'s "Spawn halo and spawn phase" section against `GarbusScrollingHitObjectContainer.HaloRadius` / `TravelTime` / `LeadTime` and `DistanceFromCentreAtTime`. The formulas must match symbol for symbol. Fix whichever is wrong.

- [ ] **Step 5: Commit any fixes**

```bash
git add -A
git commit -m "fix: address verification sweep findings"
```

Skip this step if Steps 1-4 required no changes.
