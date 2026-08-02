# Slider Node Judgement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Judge every Slider node — head and children alike — by a symmetric catch window, and credit a
duration object's opening grace period only when the player actually activated it.

**Architecture:** A Slider stops being "a catch-timed head plus duration tails referencing a pseudo-head
chain" and becomes a chain of nodes joined by segments. A new `SliderNodeJudgement` tracker folds
per-frame catch state into one node judgement (Perfect / Bad / Miss). A child whose segment has zero
duration takes that node judgement directly; a child with a real segment takes the segment's duration
judgement. `DurationJudgement` gains a `CreditedActivation` helper that gates the opening grace on real
activation, and loses both the head-reference parameter and the short-duration special case, which the
gate makes redundant.

**Tech Stack:** C# / .NET, osu-framework, NUnit (headless) + osu-framework visual test scenes.

## Global Constraints

- The authority for behaviour is `docs/rules-specs/Judgement.md`. It has already been updated for this
  work; implement what it says and do not re-litigate it here.
- No historical context in docs. Write present tense, no phase numbers, no version bumps.
- Do not add new warnings, including in tests. Build and test output stays warning-clean.
- Test expectations are independent and spec-anchored: pinned constants trace to the spec, expected
  values are hand-derived and never computed with the implementation's own constants or functions, and
  no test is a strict subset of a sibling.
- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- Do not run the app. Tests are how you verify.
- Build: `dotnet build Garbus.Desktop.slnf`
- Test: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
- The node window is **200 ms**, spec-anchored to `docs/rules-specs/Judgement.md` → Slider → Timing.
- The hold grace period is the parent `*Note` type's worst non-Miss late window: **110 ms** for
  CardinalNote, **150 ms** for ShoulderNote.

---

### Task 1: Symmetric node windows and the node-judgement tracker

Pure, headless building blocks. No gameplay behaviour changes in this task — the drawables are only
updated to reference the renamed window type.

**Files:**
- Create: `Garbus.Game/Objects/Judgement/SliderNodeHitWindows.cs`
- Create: `Garbus.Game/Objects/Judgement/SliderNodeJudgement.cs`
- Delete: `Garbus.Game/Objects/Judgement/SliderCatchHitWindows.cs`
- Modify: `Garbus.Game/Objects/SliderHead.cs:18,24`
- Modify: `Garbus.Game/Objects/SliderChild.cs:22`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderHead.cs:32`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs:47,97,102,143,168,173`
- Modify: `Garbus.Game.Tests/CompositeJudgementTest.cs:25-30`
- Test: `Garbus.Game.Tests/SliderNodeJudgementTest.cs` (create)

**Interfaces:**
- Consumes: `HitWindows`, `HitWindowRange`, `HitResult` from `Garbus.Game.Gameplay.Scoring`.
- Produces:
  - `SliderNodeHitWindows.NODE_WINDOW` — `const double` = 200.
  - `SliderNodeJudgement` with `HitResult? Result { get; }`, `void Reset()`, and
    `HitResult? Update(double previousTime, double time, double startTime, bool covered)`.

- [ ] **Step 1: Write the failing tracker test**

Create `Garbus.Game.Tests/SliderNodeJudgementTest.cs`:

```csharp
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;
using NUnit.Framework;

namespace Garbus.Game.Tests;

[TestFixture]
public class SliderNodeJudgementTest
{
    // Node StartTime and window are hand-anchored to docs/rules-specs/Judgement.md -> Slider -> Timing:
    // a 200 ms window either side of StartTime, Perfect only for coverage as StartTime is reached.
    private const double start = 1000;

    [Test]
    public void CoveringAcrossStartTimeIsPerfect()
        => Assert.That(play((984, true), (1000, true), (1016, true)), Is.EqualTo(HitResult.Perfect));

    [Test]
    public void CoveringOnlyEarlyInsideTheWindowIsBad()
        => Assert.That(play((900, true), (950, false), (1000, false)), Is.EqualTo(HitResult.Bad));

    [Test]
    public void CoveringOnlyLateInsideTheWindowIsBad()
        => Assert.That(play((1000, false), (1050, true)), Is.EqualTo(HitResult.Bad));

    [Test]
    public void EarlyAndLateCoverageAtTheSameDistanceGradeAlike()
    {
        Assert.That(play((850, true), (1000, false), (1201, false)), Is.EqualTo(HitResult.Bad));
        Assert.That(play((1000, false), (1150, true), (1201, false)), Is.EqualTo(HitResult.Bad));
    }

    [Test]
    public void CoveringOnlyOutsideTheEarlyWindowIsMiss()
        => Assert.That(play((780, true), (1000, false), (1201, false)), Is.EqualTo(HitResult.Miss));

    [Test]
    public void CoveringOnlyOutsideTheLateWindowIsMiss()
        => Assert.That(play((1000, false), (1250, true)), Is.EqualTo(HitResult.Miss));

    [Test]
    public void NeverCoveringIsMiss()
        => Assert.That(play((1000, false), (1201, false)), Is.EqualTo(HitResult.Miss));

    [Test]
    public void StaysUndecidedWhileTheLateWindowIsStillOpen()
        => Assert.That(play((1000, false), (1100, false)), Is.Null);

    [Test]
    public void ResetClearsADecidedResult()
    {
        var node = new SliderNodeJudgement();
        node.Update(984, 1000, start, true);
        Assert.That(node.Result, Is.EqualTo(HitResult.Perfect));

        node.Reset();
        Assert.That(node.Result, Is.Null);
    }

    /// <summary>Feed frames to a fresh tracker, 16 ms before the first frame standing in for its predecessor.</summary>
    private static HitResult? play(params (double time, bool covered)[] frames)
    {
        var node = new SliderNodeJudgement();
        double previous = frames[0].time - 16;

        foreach (var (time, covered) in frames)
        {
            node.Update(previous, time, start, covered);
            previous = time;
        }

        return node.Result;
    }
}
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter SliderNodeJudgementTest`
Expected: build failure — `SliderNodeJudgement` does not exist.

- [ ] **Step 3: Add the windows type**

Create `Garbus.Game/Objects/Judgement/SliderNodeHitWindows.cs`:

```csharp
// Catch timing for slider nodes: a symmetric window either side of StartTime, per
// docs/rules-specs/Judgement.md. Perfect is a state check at StartTime itself, so its window has no
// extent; Bad spans the rest of the node window.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class SliderNodeHitWindows : HitWindows
{
    /// <summary>How far either side of a node's StartTime its angle may be covered and still count.</summary>
    public const double NODE_WINDOW = 200;

    public override bool IsHitResultAllowed(HitResult result)
        => result is HitResult.Perfect or HitResult.Bad or HitResult.Miss;

    public override HitWindowRange WindowFor(HitResult result) => result switch
    {
        HitResult.Perfect => default,
        HitResult.Bad => HitWindowRange.Symmetric(NODE_WINDOW),
        HitResult.Miss => default,
        _ => throw new System.ArgumentOutOfRangeException(nameof(result), result, null),
    };
}
```

- [ ] **Step 4: Add the tracker**

Create `Garbus.Game/Objects/Judgement/SliderNodeJudgement.cs`:

```csharp
// The catch-timed judgement of one slider node, per docs/rules-specs/Judgement.md ("Catch timing").
// Perfect requires the node's angle to be covered as StartTime is reached; coverage anywhere else
// inside the node window is a Bad; no coverage inside it is a Miss.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class SliderNodeJudgement
{
    private bool coveredInWindow;

    /// <summary>The node's judgement, or null while it is still undecided.</summary>
    public HitResult? Result { get; private set; }

    public void Reset()
    {
        coveredInWindow = false;
        Result = null;
    }

    /// <summary>
    /// Fold one frame of catch state in. <paramref name="covered"/> is whether the input covers the
    /// node's angle now; <paramref name="previousTime"/> is the previous frame's time, used to spot
    /// the frame that crosses StartTime.
    /// </summary>
    public HitResult? Update(double previousTime, double time, double startTime, bool covered)
    {
        if (Result is not null)
            return Result;

        bool inWindow = time >= startTime - SliderNodeHitWindows.NODE_WINDOW
                        && time <= startTime + SliderNodeHitWindows.NODE_WINDOW;

        if (covered && inWindow)
            coveredInWindow = true;

        if (inWindow && covered && previousTime < startTime && time >= startTime)
            Result = HitResult.Perfect;
        else if (time >= startTime && coveredInWindow)
            Result = HitResult.Bad;
        else if (time > startTime + SliderNodeHitWindows.NODE_WINDOW)
            Result = HitResult.Miss;

        return Result;
    }
}
```

- [ ] **Step 5: Run the tracker test**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter SliderNodeJudgementTest`
Expected: 9 tests PASS.

- [ ] **Step 6: Retire `SliderCatchHitWindows`**

Delete `Garbus.Game/Objects/Judgement/SliderCatchHitWindows.cs`, then replace every reference to
`SliderCatchHitWindows.PERFECT_WINDOW` with `SliderNodeHitWindows.NODE_WINDOW` and every
`new SliderCatchHitWindows()` with `new SliderNodeHitWindows()`. The call sites are:

- `Garbus.Game/Objects/SliderHead.cs` lines 18 and 24
- `Garbus.Game/Objects/SliderChild.cs` line 22 (drop the `global::` prefix while you are here — add
  `using Garbus.Game.Objects.Judgement;` and write `SliderNodeHitWindows.NODE_WINDOW`)
- `Garbus.Game/Objects/Drawables/DrawableSliderHead.cs` line 32
- `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs` lines 47, 97, 102, 143, 168, 173

- [ ] **Step 7: Update the window test in `CompositeJudgementTest`**

Replace `SliderCatchWindowIsLateOnly` (lines 25-30) with:

```csharp
    // Nodes grade on catch state, not on a signed offset: only offset 0 maps to Perfect, and the Bad
    // window reaches 200 ms to each side (docs/rules-specs/Judgement.md -> Slider -> Timing).
    [TestCase(-201, HitResult.None)]
    [TestCase(-200, HitResult.Bad)]
    [TestCase(-1, HitResult.Bad)]
    [TestCase(0, HitResult.Perfect)]
    [TestCase(1, HitResult.Bad)]
    [TestCase(200, HitResult.Bad)]
    [TestCase(201, HitResult.None)]
    public void SliderNodeWindowIsSymmetric(double offset, HitResult expected)
        => Assert.That(new SliderNodeHitWindows().ResultFor(offset), Is.EqualTo(expected));
```

- [ ] **Step 8: Build and run the full suite**

Run: `dotnet build Garbus.Desktop.slnf` then
`dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: build clean with no new warnings; all tests PASS. Behaviour is unchanged so far —
`LateEligibilityEdge` is still 200 ms because Bad is now the lowest successful result.

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/Objects/Judgement Garbus.Game/Objects/SliderHead.cs Garbus.Game/Objects/SliderChild.cs Garbus.Game/Objects/Drawables/DrawableSliderHead.cs Garbus.Game/Objects/Drawables/DrawableSliderChild.cs Garbus.Game.Tests/SliderNodeJudgementTest.cs Garbus.Game.Tests/CompositeJudgementTest.cs
git commit -m "feat: add symmetric slider node windows and judgement tracker"
```

---

### Task 2: Judge slider nodes as nodes

Wire the tracker into both node drawables. The head's Judgement becomes its node judgement, and a
child whose segment has zero duration takes its own node judgement instead of inheriting from a head
reference. Children with a real segment keep today's duration path for now.

**Files:**
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderHead.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs`
- Modify: `Garbus.Game/Objects/SliderChild.cs`
- Test: `Garbus.Game.Tests/CompositeJudgementTest.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneGameplay.cs:624-671`

**Interfaces:**
- Consumes: `SliderNodeJudgement`, `SliderNodeHitWindows.NODE_WINDOW` from Task 1;
  `PerfectJudgement` (`Garbus.Game/Objects/Judgement/PerfectJudgement.cs`, `MaxResult => Perfect`).
- Produces: `DrawableSliderChild.NodeResult` — `HitResult?`, the child's node judgement, null while
  undecided. Replaces the removed `HeadStyleHit`.

- [ ] **Step 1: Write the failing MaxResult test**

A zero-duration child can never take Critical Perfect, because its Judgement is a node judgement. Add
to `Garbus.Game.Tests/CompositeJudgementTest.cs`:

```csharp
    [Test]
    public void ZeroDurationChildCannotTakeCriticalPerfect()
    {
        var slider = createSlider(0, 300);
        slider.ApplyDefaults();

        var children = slider.NestedHitObjects.OfType<SliderChild>().OrderBy(c => c.StartTime).ToArray();

        // Child 0 sits at TimeOffset 0 — a jump, graded as a node, so Perfect is its ceiling.
        Assert.That(children[0].Judgement.MaxResult, Is.EqualTo(HitResult.Perfect));
        // Child 1 ends a 300 ms segment, graded on activation, so the hold family's ceiling applies.
        Assert.That(children[1].Judgement.MaxResult, Is.EqualTo(HitResult.CriticalPerfect));
    }
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter ZeroDurationChildCannotTakeCriticalPerfect`
Expected: FAIL — child 0 reports `CriticalPerfect`.

- [ ] **Step 3: Give zero-duration children a Perfect-capped Judgement**

In `Garbus.Game/Objects/SliderChild.cs`, add:

```csharp
    /// <summary>The duration of the segment ending at this child. Zero at a jump.</summary>
    public double SegmentDuration => StartTime - Parent.GetSegmentStartTime(this);

    // A jump has no segment to grade, so the child is judged as a node — Perfect, Bad or Miss.
    public override Gameplay.Judgements.Judgement CreateJudgement()
        => SegmentDuration <= 0 ? new PerfectJudgement() : new();
```

Add `using Garbus.Game.Objects.Judgement;` if it is not already present.

- [ ] **Step 4: Run the test**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter ZeroDurationChildCannotTakeCriticalPerfect`
Expected: PASS. If it fails because `StartTime` is still 0 when `CreateJudgement` runs, move the
judgement selection to `ApplyDefaultsToSelf` instead and re-run.

- [ ] **Step 5: Judge the head from its node tracker**

Rewrite `Garbus.Game/Objects/Drawables/DrawableSliderHead.cs` to:

```csharp
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;
using Garbus.Game.UI;
using osu.Framework.Allocation;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableSliderHead : DrawableGarbusHitObject<SliderHead>, ISelfPosition
{
    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    [Resolved]
    private SlamCoincidenceIndex slamCoincidenceIndex { get; set; } = null!;

    private readonly SliderNodeJudgement node = new();

    public DrawableSliderHead(SliderHead hitObject)
        : base(hitObject)
    {
    }

    protected override void OnFree()
    {
        base.OnFree();

        node.Reset();
    }

    protected override void Update()
    {
        node.Update(Time.Current - Time.Elapsed, Time.Current, HitObject.StartTime, isCoveringNode());
        base.Update();
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < 0 || node.Result is null)
            return;

        var result = node.Result.Value;

        if (result == HitResult.Miss)
        {
            bool? coincidentSlamHit = slamCoincidenceIndex.SlamHitAt(HitObject.StartTime, HitObject.Parent.Side);
            if (coincidentSlamHit is null)
                return;

            if (coincidentSlamHit.Value)
                result = HitResult.Bad;
        }

        ApplyResult(result);
    }

    private bool isCoveringNode()
        => analogInput.SliderCatchers[HitObject.Parent.Side].IsCatchingAt(HitObject.AngleDeg);
}
```

Note the coincident-slam branch now floors to `Bad`, matching the child and the spec's "the node cannot
be a Miss either" — it previously awarded the head a full Perfect.

- [ ] **Step 6: Replace the child's pseudo-head with a node judgement**

In `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs`:

1. Change the file header comment to:
   `// A child grades the segment ending at it, or — at a jump, where that segment has no duration —`
   `// its own node judgement. See docs/rules-specs/Judgement.md.`
2. Replace the `HeadStyleHit` property and field with:

```csharp
    private readonly SliderNodeJudgement node = new();

    /// <summary>This child's node judgement, or null while it is still undecided.</summary>
    public HitResult? NodeResult => node.Result;
```

3. Delete `updateHeadStyleJudgement()` and its call in `Update()`, replacing the call with:

```csharp
        node.Update(Time.Current - Time.Elapsed, Time.Current, HitObject.StartTime, isCatchingNode());
```

   so `Update()` reads:

```csharp
    protected override void Update()
    {
        updateActivation();
        node.Update(Time.Current - Time.Elapsed, Time.Current, HitObject.StartTime, isCatchingNode());
        base.Update();
    }
```

4. In `OnFree()`, replace `HeadStyleHit = null;` with `node.Reset();`.
5. Rewrite `CheckForResult` to route on segment duration:

```csharp
    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < 0)
            return;

        double duration = HitObject.SegmentDuration;
        HitResult result;

        if (duration <= 0)
        {
            if (node.Result is null)
                return;

            result = node.Result.Value;
        }
        else
        {
            bool? referenceWasHit = headReferenceWasHit();
            if (referenceWasHit is null)
                return;

            double openingGrace = Math.Min(duration, SliderNodeHitWindows.NODE_WINDOW);

            result = DurationJudgement.Resolve(
                duration,
                openingGrace + activatedAfterOpeningGrace,
                SliderNodeHitWindows.NODE_WINDOW,
                referenceWasHit.Value,
                activatedAtSegmentEnd == true,
                activatedDuringEndGrace || activatedAtSegmentEnd == true,
                bestThreshold: 0.95,
                perfectThreshold: 0.90,
                badThreshold: 0.50);
        }

        if (result == HitResult.Miss)
        {
            bool? coincidentSlamHit = slamCoincidenceIndex.SlamHitAt(HitObject.StartTime, HitObject.Parent.Side);
            if (coincidentSlamHit is null)
                return;

            if (coincidentSlamHit.Value)
                result = HitResult.Bad;
        }

        ApplyResult(result);
    }
```

6. Update `headReferenceWasHit()` to read the new property:

```csharp
            DrawableSliderChild child => child.NodeResult is null ? null : child.NodeResult != HitResult.Miss,
```

7. Replace the two `HitObject.StartTime - HitObject.Parent.GetSegmentStartTime(HitObject)` expressions
   (in `ActivationProgress` and `updateActivation`) with `HitObject.SegmentDuration` — `updateActivation`
   still needs `segmentStart` for its interval maths, so keep that local.

- [ ] **Step 7: Retarget the zero-duration gameplay test**

In `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`, rename
`TestZeroDurationSliderResolvesFromMissedHead` to `TestZeroDurationSliderNodesMissWithoutInput` and
replace its comment block (lines 629-632) with:

```csharp
            // A slider whose only child sits at TimeOffset 0 — a jump. The segment has no duration, so
            // the child is graded purely as a node. With no input at all, neither node's angle is ever
            // covered inside its window, so head and child both miss.
```

Change the `"child inherits missed head"` assertion label to `"untouched child node misses"`; the
assertion body is unchanged.

- [ ] **Step 8: Build and run the full suite**

Run: `dotnet build Garbus.Desktop.slnf` then
`dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: build clean with no new warnings; all tests PASS.

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/Objects Garbus.Game.Tests/CompositeJudgementTest.cs Garbus.Game.Tests/Visual/TestSceneGameplay.cs
git commit -m "feat: judge slider heads and jump children as nodes"
```

---

### Task 3: Gate the opening grace and delete the head reference

The opening grace is credited only when the object was Activated inside it. That makes the
short-duration head-reference rule redundant, so it goes, and with it the whole head-reference chain.

**Files:**
- Modify: `Garbus.Game/Objects/Judgement/DurationJudgement.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs:58-68,128-151,178-197`
- Modify: `Garbus.Game/Objects/SliderChild.cs`
- Modify: `Garbus.Game/Objects/SliderBody.cs` (`CreateNestedHitObjects`)
- Test: `Garbus.Game.Tests/DurationJudgementTest.cs`

**Interfaces:**
- Consumes: `SliderNodeHitWindows.NODE_WINDOW` (Task 1), `HitObject.SegmentDuration` (Task 2).
- Produces:
  - `DurationJudgement.CreditedActivation(double duration, double gracePeriod, bool activatedDuringGrace, double activatedAfterGrace)` → `double`
  - `DurationJudgement.Resolve(double duration, double activatedDuration, bool activatedAtEnd, bool activatedDuringEndGrace, double bestThreshold, double perfectThreshold, double badThreshold)` → `HitResult`
  - `SliderChild(SliderBody parent, GarbusPathControlPoint controlPoint)` — the `headReference`
    constructor parameter is gone.

- [ ] **Step 1: Write the failing grace-gate tests**

Replace the whole body of `Garbus.Game.Tests/DurationJudgementTest.cs` with:

```csharp
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;
using NUnit.Framework;

namespace Garbus.Game.Tests;

[TestFixture]
public class DurationJudgementTest
{
    // Hold thresholds are hand-anchored to docs/rules-specs/Judgement.md -> HoldNote -> Duration:
    // Critical Perfect 100%, Perfect 95%, Bad 60%, Miss 0%.
    [TestCase(1000, 1000, true, true, HitResult.CriticalPerfect)]
    [TestCase(1000, 950, true, true, HitResult.Perfect)]
    [TestCase(1000, 600, true, true, HitResult.Bad)]
    [TestCase(1000, 599, false, true, HitResult.Miss)]
    [TestCase(1000, 1000, false, false, HitResult.Perfect)]
    public void HoldThresholdsAndEndingGrace(
        double duration,
        double activated,
        bool activeAtEnd,
        bool activeInEndingGrace,
        HitResult expected)
    {
        Assert.That(resolveHold(duration, activated, activeAtEnd, activeInEndingGrace), Is.EqualTo(expected));
    }

    [Test]
    public void ActivationAtEndFloorsAMissToBad()
        => Assert.That(resolveHold(1000, 0, true, true), Is.EqualTo(HitResult.Bad));

    [Test]
    public void ShortDurationsGetNoSpecialCase()
    {
        // A 109 ms hold with no credited activation is a Miss like any other, rather than inheriting a hit head.
        Assert.That(resolveHold(109, 0, false, false), Is.EqualTo(HitResult.Miss));
        // Credited in full, it takes the best judgement on its own merits.
        Assert.That(resolveHold(109, 109, true, true), Is.EqualTo(HitResult.CriticalPerfect));
    }

    // The grace period is credited only when the object was Activated inside it
    // (docs/rules-specs/Judgement.md -> Duration -> Grace period).
    [Test]
    public void GraceIsCreditedOnlyWhenActivatedWithinIt()
    {
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, true, 0), Is.EqualTo(200));
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, false, 0), Is.EqualTo(0));
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, true, 750), Is.EqualTo(950));
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, false, 750), Is.EqualTo(750));
    }

    [Test]
    public void GraceIsCappedAtTheDuration()
        => Assert.That(DurationJudgement.CreditedActivation(150, 200, true, 0), Is.EqualTo(150));

    [Test]
    public void AnUntouchedShortSegmentIsAMiss()
    {
        // A 250 ms slider segment with no input at all: no grace credit, so nothing to grade.
        // Slider thresholds are hand-anchored to docs/rules-specs/Judgement.md -> Slider -> Duration.
        double credited = DurationJudgement.CreditedActivation(250, 200, false, 0);

        Assert.That(DurationJudgement.Resolve(250, credited, false, false, 0.95, 0.90, 0.50),
                    Is.EqualTo(HitResult.Miss));
    }

    [Test]
    public void ATouchedShortSegmentTakesTheBestJudgement()
    {
        // The same segment, entered 50 ms in and held: 200 ms grace covers the whole opening, and the
        // remaining 50 ms is real activation.
        double credited = DurationJudgement.CreditedActivation(250, 200, true, 50);

        Assert.That(DurationJudgement.Resolve(250, credited, true, true, 0.95, 0.90, 0.50),
                    Is.EqualTo(HitResult.CriticalPerfect));
    }

    private static HitResult resolveHold(double duration, double activated, bool activeAtEnd, bool activeInEndingGrace)
        => DurationJudgement.Resolve(duration, activated, activeAtEnd, activeInEndingGrace, 1, 0.95, 0.60);
}
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter DurationJudgementTest`
Expected: build failure — `Resolve` has a different arity and `CreditedActivation` does not exist.

- [ ] **Step 3: Rewrite `DurationJudgement`**

Replace `Garbus.Game/Objects/Judgement/DurationJudgement.cs` with:

```csharp
// Duration-tail resolution shared by hold notes and slider segments, per
// docs/rules-specs/Judgement.md ("Grace period", "Final judgement").

using System;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public static class DurationJudgement
{
    /// <summary>
    /// The activation credited to a duration, in milliseconds: the opening grace period — capped at
    /// the duration, and credited only when the object was Activated inside it — plus the activation
    /// measured after the grace period.
    /// </summary>
    public static double CreditedActivation(double duration, double gracePeriod, bool activatedDuringGrace, double activatedAfterGrace)
        => (activatedDuringGrace ? Math.Min(duration, gracePeriod) : 0) + activatedAfterGrace;

    public static HitResult Resolve(
        double duration,
        double activatedDuration,
        bool activatedAtEnd,
        bool activatedDuringEndGrace,
        double bestThreshold,
        double perfectThreshold,
        double badThreshold)
    {
        double fraction = duration > 0 ? activatedDuration / duration : 0;

        HitResult result = fraction >= bestThreshold ? HitResult.CriticalPerfect
            : fraction >= perfectThreshold ? HitResult.Perfect
            : fraction >= badThreshold ? HitResult.Bad
            : HitResult.Miss;

        if (!activatedDuringEndGrace && result == HitResult.CriticalPerfect)
            result = HitResult.Perfect;

        if (activatedAtEnd && result == HitResult.Miss)
            result = HitResult.Bad;

        return result;
    }
}
```

- [ ] **Step 4: Track opening-grace activation on the slider child**

In `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs`:

1. Add the field next to `activatedDuringEndGrace`:

```csharp
    private bool activatedDuringOpeningGrace;
```

2. Reset it in `OnFree()` alongside the others.
3. In `updateActivation()`, after the `if (!catching) return;` guard, add the opening-grace record
   above the existing interval maths:

```csharp
        double openingGraceEnd = segmentStart + Math.Min(segmentEnd - segmentStart, SliderNodeHitWindows.NODE_WINDOW);
        if (now >= segmentStart && previous <= openingGraceEnd)
            activatedDuringOpeningGrace = true;
```

4. Replace the body of `ActivationProgress` with:

```csharp
        get
        {
            double duration = HitObject.SegmentDuration;
            if (duration <= 0)
                return 1;

            double credited = DurationJudgement.CreditedActivation(
                duration, SliderNodeHitWindows.NODE_WINDOW, activatedDuringOpeningGrace, activatedAfterOpeningGrace);

            return Math.Clamp(credited / duration, 0, 1);
        }
```

5. Replace the `else` branch of `CheckForResult` (everything between `else {` and its closing brace)
   with:

```csharp
            result = DurationJudgement.Resolve(
                duration,
                DurationJudgement.CreditedActivation(
                    duration, SliderNodeHitWindows.NODE_WINDOW, activatedDuringOpeningGrace, activatedAfterOpeningGrace),
                activatedAtSegmentEnd == true,
                activatedDuringEndGrace || activatedAtSegmentEnd == true,
                bestThreshold: 0.95,
                perfectThreshold: 0.90,
                badThreshold: 0.50);
```

6. Delete the `headReferenceWasHit()` method and its `using System.Linq;` if nothing else needs it.

- [ ] **Step 5: Track opening-grace activation on the hold note**

In `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs`:

1. Add `private bool activatedDuringOpeningGrace;` next to `activatedDuringEndGrace`, and reset it in
   `OnFree()`.
2. In `updateActivation()`, after the existing `double grace = ...` line, add:

```csharp
        double openingGraceEnd = HitObject.StartTime + Math.Min(HitObject.Duration, grace);
        if (now >= HitObject.StartTime && previous <= openingGraceEnd)
            activatedDuringOpeningGrace = true;
```

3. Replace the `ActivationProgress` credit lines (currently `creditedOpeningGrace` + clamp) with:

```csharp
            double credited = DurationJudgement.CreditedActivation(
                HitObject.Duration,
                HitObject.HitWindows.LateEligibilityEdge,
                activatedDuringOpeningGrace,
                activatedAfterOpeningGrace);

            return Math.Clamp(credited / HitObject.Duration, 0, 1);
```

4. Replace the `CheckForResult` call with:

```csharp
        double grace = Head.HitObject.HitWindows.LateEligibilityEdge;

        ApplyResult(DurationJudgement.Resolve(
            HitObject.Duration,
            DurationJudgement.CreditedActivation(HitObject.Duration, grace, activatedDuringOpeningGrace, activatedAfterOpeningGrace),
            activatedAtEnd == true,
            activatedDuringEndGrace,
            bestThreshold: 1,
            perfectThreshold: 0.95,
            badThreshold: 0.60));
```

- [ ] **Step 6: Delete the head reference**

1. In `Garbus.Game/Objects/SliderChild.cs`, remove the `HeadReference` property and the
   `headReference` constructor parameter, leaving
   `public SliderChild(SliderBody parent, GarbusPathControlPoint controlPoint)`.
2. In `Garbus.Game/Objects/SliderBody.cs`, `CreateNestedHitObjects`, drop the `previousNode` local and
   its per-iteration reassignment, and construct children as
   `new SliderChild(this, controlPoint)`.
3. In `Garbus.Game.Tests/CompositeJudgementTest.cs`, delete
   `SliderChildrenFormAHeadReferenceChain` — `ZeroDurationChildCannotTakeCriticalPerfect` from Task 2
   already covers per-child judgement construction.

- [ ] **Step 7: Run the judgement tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter DurationJudgementTest`
Expected: all PASS.

- [ ] **Step 8: Build and run the full suite**

Run: `dotnet build Garbus.Desktop.slnf` then
`dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: build clean with no new warnings; all tests PASS.

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game Garbus.Game.Tests
git commit -m "feat: credit the opening grace only when activation happened"
```

---

### Task 4: Pin the reported bugs and refresh the domain doc

Two gameplay-level regression tests for the exact reports that started this work, plus the domain doc.

**Files:**
- Test: `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`
- Modify: `docs/agents/gameplay.md:95-107`

**Interfaces:**
- Consumes: everything from Tasks 1-3. Uses the scene's existing `playfield`, `playThrough(double)`,
  and `PlayScreen.CreateDrawableRepresentation` helpers, and the `SliderBody` / `GarbusPath` /
  `GarbusPathControlPoint` construction pattern already used by
  `TestZeroDurationSliderNodesMissWithoutInput`.

- [ ] **Step 1: Write the failing short-segment regression test**

Add to `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`, next to the other slider tests:

```csharp
        [Test]
        public void TestShortSegmentWithoutInputMisses()
        {
            Objects.Drawables.DrawableSliderBody body = null!;

            // A slider ending in a 250 ms segment. The opening grace is 200 ms, so an ungated grace
            // would credit 200/250 = 80% and hand the last child a Bad with no input whatsoever
            // (docs/rules-specs/Judgement.md -> Duration -> Grace period).
            AddStep("add slider with a short tail segment", () =>
            {
                var slider = new SliderBody
                {
                    StartTime = 5050,
                    AngleDeg = 0,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath
                    {
                        ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = 600, RotationOffset = 30 },
                            new GarbusPathControlPoint { TimeOffset = 850, RotationOffset = 60 },
                        },
                    },
                };
                slider.ApplyDefaults();
                playfield.Add(PlayScreen.CreateDrawableRepresentation(slider));
            });

            AddUntilStep("slider body present", () =>
            {
                body = playfield.AllHitObjects
                                .OfType<Objects.Drawables.DrawableSliderBody>()
                                .FirstOrDefault(b => b.HitObject.StartTime == 5050)!;
                return body != null;
            });

            playThrough(20000);

            AddUntilStep("children judged", () => body.NestedHitObjects
                                                      .OfType<Objects.Drawables.DrawableSliderChild>()
                                                      .All(c => c.Judged));
            AddAssert("untouched short segment misses", () => body.NestedHitObjects
                                                                  .OfType<Objects.Drawables.DrawableSliderChild>()
                                                                  .All(c => c.Result?.Type == HitResult.Miss));
        }
```

- [ ] **Step 2: Run it**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestShortSegmentWithoutInputMisses`
Expected: PASS — Task 3 already fixed the behaviour; this test exists to keep it fixed. If it fails,
the grace gate is not reaching the segment path; debug Task 3 Step 4 before continuing.

- [ ] **Step 3: Refresh the gameplay domain doc**

In `docs/agents/gameplay.md`, replace the two sentences in the "Judgement (summary — see the spec)"
section that currently read

> `Objects/Judgement/DurationJudgement.cs` applies hold/slider grace, short-duration, end-floor, and
> ending-grace rules. Slider heads use a late-only 200 ms catch window (`SliderCatchHitWindows`);
> children use hold-family segment proportions plus a catch-style pseudo-head chain with
> same-time/same-side slam floors.

with

> `Objects/Judgement/DurationJudgement.cs` applies the gated opening grace (`CreditedActivation`),
> end-floor, and ending-grace rules — there is no short-duration special case and no head reference.
> Every slider node is judged by `SliderNodeJudgement` over a symmetric 200 ms window
> (`SliderNodeHitWindows`): Perfect for covering the node's angle as StartTime is reached, Bad for
> covering it anywhere else inside the window, Miss otherwise. A child takes that node judgement when
> its segment has no duration, and the hold-family segment proportion otherwise; same-time/same-side
> slams floor either away from a Miss.

- [ ] **Step 4: Build and run the full suite one last time**

Run: `dotnet build Garbus.Desktop.slnf` then
`dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: build clean with no new warnings; all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game.Tests/Visual/TestSceneGameplay.cs docs/agents/gameplay.md
git commit -m "test: pin ungraded-without-input segments and refresh the gameplay doc"
```
