# Judgement Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace osu's vendored scoring core with the spec-native judgement foundation from `docs/superpowers/specs/2026-07-19-judgement-foundation-design.md`: a Garbus `HitResult` ladder, asymmetric per-note-type hit windows, the spec auto-miss edge, and oldest-eligible note-lock.

**Architecture:** The vendored `HitResult`/`HitWindows`/`Judgement` files are rewritten in place (they are owned copies, not a package). Work is sequenced so the solution builds and all tests pass after every task: first the asymmetric-window mechanics under the old enum, then the enum cutover with all mechanical fallout, then the real per-type window tables and wiring, then the note-lock rewrite, then docs.

**Tech Stack:** C# / .NET (osu-framework), NUnit headless tests via `Garbus.Game.Tests`.

## Global Constraints

- Read the spec first: `docs/superpowers/specs/2026-07-19-judgement-foundation-design.md`. The gameplay-rules source of truth is `docs/rules-specs/Judgement.md`.
- Nullability is enabled solution-wide; DI/BDL fields use `= null!`.
- Vendored osu.Game files keep the ppy MIT header plus an "Adapted for Garbus:" line summarising the trims. Update that line when you change what was adapted.
- No backwards compatibility concerns anywhere (explicit project policy in CLAUDE.md). Delete dead members outright; never add shims for their own sake.
- Build: `dotnet build Garbus.Desktop.slnf` (run from the repo root, `C:\Users\zachd\Code\Garbus_worktrees\earthy-dove`).
- Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj` (all), `--filter "FullyQualifiedName~<Name>"` for one fixture.
- Window-table constants (32/64/110, 40/80/150, 200 early-miss) are provisional per the spec — keep them as named constants in the two window classes, nowhere else.

---

### Task 1: Asymmetric `HitWindows` core (old enum, behaviour-preserving)

Rework the vendored `HitWindows` to per-result `(Early, Late)` ranges with a sign-aware `ResultFor` and a `LateEligibilityEdge`, while still using the osu enum members and symmetric osu values — so behaviour is unchanged except that `MaximumJudgementOffset` / the hold-carry threshold move from the Miss window (173) to the late eligibility edge (136), which nothing observable depends on yet.

**Files:**
- Modify: `Garbus.Game/Gameplay/Scoring/HitWindows.cs` (full rewrite below)
- Modify: `Garbus.Game/Gameplay/Scoring/DefaultHitWindows.cs` (return ranges)
- Modify: `Garbus.Game/Gameplay/Objects/HitObject.cs:151` (`MaximumJudgementOffset`)
- Modify: `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs:140` (`headCarries`)
- Test: `Garbus.Game.Tests/HitWindowsTest.cs` (new)

**Interfaces:**
- Consumes: existing `HitResult` (osu members, unchanged this task).
- Produces (later tasks rely on these exact names):
  - `readonly struct HitWindowRange { double Early; double Late; static HitWindowRange Symmetric(double); bool Contains(double timeOffset); }`
  - `abstract HitWindowRange HitWindows.WindowFor(HitResult result)` (replaces the `double` version)
  - `HitResult HitWindows.ResultFor(double timeOffset)` — now sign-aware, negative = early
  - `double HitWindows.LateEligibilityEdge` — late extent of the latest non-Miss allowed window
  - `bool HitWindows.CanBeHit(double timeOffset)` — `timeOffset <= LateEligibilityEdge`
  - `IEnumerable<(HitResult result, HitWindowRange window)> GetAllAvailableWindows()`

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/HitWindowsTest.cs`:

```csharp
using Garbus.Game.Gameplay.Scoring;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class HitWindowsTest
    {
        /// <summary>
        /// A minimal asymmetric window set: Perfect ±50, Great ±100, Miss early-only 200.
        /// </summary>
        private class TestWindows : HitWindows
        {
            public override bool IsHitResultAllowed(HitResult result)
                => result is HitResult.Perfect or HitResult.Great or HitResult.Miss;

            public override HitWindowRange WindowFor(HitResult result) => result switch
            {
                HitResult.Perfect => HitWindowRange.Symmetric(50),
                HitResult.Great => HitWindowRange.Symmetric(100),
                HitResult.Miss => new HitWindowRange(200, 0),
                _ => default,
            };
        }

        [Test]
        public void ResultForIsSignAwareAndNested()
        {
            var windows = new TestWindows();

            Assert.That(windows.ResultFor(0), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(50), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(-50), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(51), Is.EqualTo(HitResult.Great));
            Assert.That(windows.ResultFor(100), Is.EqualTo(HitResult.Great));
            Assert.That(windows.ResultFor(-100), Is.EqualTo(HitResult.Great));
        }

        [Test]
        public void EarlyOnlyMissWindowHasNoLateSide()
        {
            var windows = new TestWindows();

            // Early side: outside Great (100) but inside the early-miss extent (200) -> Miss.
            Assert.That(windows.ResultFor(-101), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.ResultFor(-200), Is.EqualTo(HitResult.Miss));
            // Beyond the early-miss extent -> no interaction at all.
            Assert.That(windows.ResultFor(-201), Is.EqualTo(HitResult.None));
            // Late side: past Great there is NO Miss window -> no interaction.
            Assert.That(windows.ResultFor(101), Is.EqualTo(HitResult.None));
        }

        [Test]
        public void LateEligibilityEdgeIsLatestNonMissLateExtent()
        {
            var windows = new TestWindows();

            Assert.That(windows.LateEligibilityEdge, Is.EqualTo(100));
            Assert.That(windows.CanBeHit(100), Is.True);
            Assert.That(windows.CanBeHit(101), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~HitWindowsTest"`
Expected: compile errors (`HitWindowRange` does not exist, `WindowFor` returns `double`). A compile failure is this step's "failing test".

- [ ] **Step 3: Rewrite `HitWindows.cs`**

Replace the entire contents of `Garbus.Game/Gameplay/Scoring/HitWindows.cs` with:

```csharp
// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Scoring/HitWindows.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: SetDifficulty removed (windows are fixed), and windows are asymmetric
// (early, late) ranges per docs/rules-specs/Judgement.md — ResultFor is sign-aware, a zero side
// means "no window on that side" (the note-family Miss window is early-only), and hittability keys
// off LateEligibilityEdge (the late extent of the latest non-Miss window), not the Miss window.

using System.Collections.Generic;
using System.Diagnostics;

namespace Garbus.Game.Gameplay.Scoring
{
    /// <summary>
    /// An asymmetric timing window: how far before (<see cref="Early"/>) and after (<see cref="Late"/>)
    /// an object's time an input still falls within the window. Both bounds are inclusive. A zero side
    /// means the window does not extend to that side.
    /// </summary>
    public readonly struct HitWindowRange
    {
        public double Early { get; }
        public double Late { get; }

        public HitWindowRange(double early, double late)
        {
            Early = early;
            Late = late;
        }

        public static HitWindowRange Symmetric(double extent) => new HitWindowRange(extent, extent);

        /// <summary>
        /// Whether a signed time offset (negative = early) falls inside this window.
        /// </summary>
        public bool Contains(double timeOffset) => timeOffset < 0 ? -timeOffset <= Early : timeOffset <= Late;
    }

    /// <summary>
    /// A structure containing timing data for hit window based gameplay.
    /// </summary>
    public abstract class HitWindows
    {
        /// <summary>
        /// An empty <see cref="HitWindows"/> whose windows are all zero-width. Used by objects that
        /// have no timed button input.
        /// </summary>
        public static HitWindows Empty { get; } = new EmptyHitWindows();

        protected HitWindows()
        {
            ensureValidHitWindows();
        }

        [Conditional("DEBUG")]
        private void ensureValidHitWindows()
        {
            bool anyMiss = false;
            bool anyNonMiss = false;

            // Windows must nest: walking worst -> best, each present side must shrink or stay equal.
            // A zero side is "absent" (e.g. the early-only Miss window has Late == 0) and exempt.
            double lastEarly = double.PositiveInfinity;
            double lastLate = double.PositiveInfinity;

            foreach (var (result, window) in GetAllAvailableWindows())
            {
                anyMiss |= result == HitResult.Miss;
                anyNonMiss |= result != HitResult.Miss;

                if (window.Early > 0)
                {
                    Debug.Assert(window.Early <= lastEarly, $"{GetType().Name}: early extents must not grow toward better judgements.");
                    lastEarly = window.Early;
                }

                if (window.Late > 0)
                {
                    Debug.Assert(window.Late <= lastLate, $"{GetType().Name}: late extents must not grow toward better judgements.");
                    lastLate = window.Late;
                }
            }

            Debug.Assert(anyMiss, $"{nameof(GetAllAvailableWindows)} should always contain {nameof(HitResult.Miss)}");
            Debug.Assert(anyNonMiss, $"{nameof(GetAllAvailableWindows)} should always contain at least one result type other than {nameof(HitResult.Miss)}.");
        }

        /// <summary>
        /// Retrieves the <see cref="HitResult"/> with the largest hit window that produces a successful hit.
        /// </summary>
        /// <returns>The lowest allowed successful <see cref="HitResult"/>.</returns>
        protected HitResult LowestSuccessfulHitResult()
        {
            for (var result = HitResult.Meh; result <= HitResult.Perfect; ++result)
            {
                if (IsHitResultAllowed(result))
                    return result;
            }

            return HitResult.None;
        }

        /// <summary>
        /// Retrieves a mapping of <see cref="HitResult"/>s to their timing windows for all allowed
        /// <see cref="HitResult"/>s, worst (Miss) first.
        /// </summary>
        public IEnumerable<(HitResult result, HitWindowRange window)> GetAllAvailableWindows()
        {
            for (var result = HitResult.Miss; result <= HitResult.Perfect; ++result)
            {
                if (IsHitResultAllowed(result))
                    yield return (result, WindowFor(result));
            }
        }

        /// <summary>
        /// Check whether it is possible to achieve the provided <see cref="HitResult"/>.
        /// </summary>
        public virtual bool IsHitResultAllowed(HitResult result) => true;

        /// <summary>
        /// Retrieves the <see cref="HitResult"/> for a signed time offset (negative = early).
        /// </summary>
        /// <returns>The innermost (best) containing window's result, or <see cref="HitResult.None"/>
        /// if no window contains the offset — the input does not interact with the object.</returns>
        public HitResult ResultFor(double timeOffset)
        {
            for (var result = HitResult.Perfect; result >= HitResult.Miss; --result)
            {
                if (IsHitResultAllowed(result) && WindowFor(result).Contains(timeOffset))
                    return result;
            }

            return HitResult.None;
        }

        /// <summary>
        /// Retrieves the (early, late) hit window for a <see cref="HitResult"/>.
        /// </summary>
        public abstract HitWindowRange WindowFor(HitResult result);

        /// <summary>
        /// The late extent of the latest non-Miss window: how long after an object's time it stays
        /// hittable. Once this elapses the object is Missed automatically — there is no late Miss
        /// window (see the judgement spec).
        /// </summary>
        public double LateEligibilityEdge
        {
            get
            {
                var lowest = LowestSuccessfulHitResult();
                return lowest == HitResult.None ? 0 : WindowFor(lowest).Late;
            }
        }

        /// <summary>
        /// Given a time offset, whether the <see cref="Objects.HitObject"/> can ever be hit in the
        /// future with a non-<see cref="HitResult.Miss"/> result.
        /// </summary>
        public bool CanBeHit(double timeOffset) => timeOffset <= LateEligibilityEdge;

        private class EmptyHitWindows : HitWindows
        {
            public override bool IsHitResultAllowed(HitResult result) => true;

            public override HitWindowRange WindowFor(HitResult result) => default;
        }
    }
}
```

- [ ] **Step 4: Update `DefaultHitWindows.cs` to return ranges**

In `Garbus.Game/Gameplay/Scoring/DefaultHitWindows.cs`, replace the `WindowFor` override (keep the header, constants, and class shell):

```csharp
        public override HitWindowRange WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return HitWindowRange.Symmetric(perfect_window);

                case HitResult.Great:
                    return HitWindowRange.Symmetric(great_window);

                case HitResult.Good:
                    return HitWindowRange.Symmetric(good_window);

                case HitResult.Ok:
                    return HitWindowRange.Symmetric(ok_window);

                case HitResult.Meh:
                    return HitWindowRange.Symmetric(meh_window);

                case HitResult.Miss:
                    return HitWindowRange.Symmetric(miss_window);

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
```

- [ ] **Step 5: Update the two `WindowFor(HitResult.Miss)` call sites**

`Garbus.Game/Gameplay/Objects/HitObject.cs:151` — replace:

```csharp
        public virtual double MaximumJudgementOffset => HitWindows?.WindowFor(HitResult.Miss) ?? 0;
```

with (also update the `<para>` doc line "Defaults to the miss window." to "Defaults to the late eligibility edge of the hit windows."; the `using Garbus.Game.Gameplay.Scoring;` import stays — `HitWindows` the type is still referenced):

```csharp
        public virtual double MaximumJudgementOffset => HitWindows?.LateEligibilityEdge ?? 0;
```

`Garbus.Game/Objects/Drawables/DrawableHoldNote.cs:140` — replace:

```csharp
        bool headCarries = HitObject.Duration < Head.HitObject.HitWindows.WindowFor(HitResult.Miss);
```

with:

```csharp
        bool headCarries = HitObject.Duration < Head.HitObject.HitWindows.LateEligibilityEdge;
```

- [ ] **Step 6: Build and run the new tests**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: success, no warnings introduced.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~HitWindowsTest"`
Expected: 3 passed.

- [ ] **Step 7: Run the full suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green (behaviour unchanged — symmetric values, sign-aware checks give identical results).

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/Gameplay/Scoring/HitWindows.cs Garbus.Game/Gameplay/Scoring/DefaultHitWindows.cs Garbus.Game/Gameplay/Objects/HitObject.cs Garbus.Game/Objects/Drawables/DrawableHoldNote.cs Garbus.Game.Tests/HitWindowsTest.cs
git commit -m "refactor: asymmetric (early, late) hit windows with a late eligibility edge"
```

---

### Task 2: Native `HitResult` cutover

Replace the osu enum with the spec ladder and update every consumer mechanically. The interim `DefaultHitWindows` adopts the cardinal table (all notes share it until Task 3 splits per type).

**Files:**
- Modify: `Garbus.Game/Gameplay/Scoring/HitResult.cs` (full rewrite below)
- Modify: `Garbus.Game/Gameplay/Judgements/Judgement.cs` (MaxResult/MinResult/health)
- Modify: `Garbus.Game/Gameplay/Scoring/HitWindows.cs` (iteration bounds)
- Modify: `Garbus.Game/Gameplay/Scoring/DefaultHitWindows.cs` (interim cardinal table)
- Modify: `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs` (`resultFor` table)
- Modify: `Garbus.Game/Gameplay/Audio/HitsoundFamily.cs` (`Single` default key)
- Modify: `Garbus.Game/Screens/PlayScreen.cs:327-339` (`scoreFor`)
- Create: `Garbus.Game/Objects/Judgement/PerfectJudgement.cs`
- Modify: `Garbus.Game/Objects/GarbusSlamCentered.cs`, `Garbus.Game/Objects/GarbusSlamEdge.cs`, `Garbus.Game/Objects/SliderHead.cs`, `Garbus.Game/Objects/SliderChild.cs` (judgement overrides)
- Modify: `Garbus.Game.Tests/HitsoundFamilyTest.cs`, `Garbus.Game.Tests/HitWindowsTest.cs`, `Garbus.Game.Tests/Visual/TestSceneGameplay.cs` (re-key / comments)

**Interfaces:**
- Consumes: Task 1's `HitWindowRange` / `LateEligibilityEdge`.
- Produces: `enum HitResult { None, Miss, Bad, Near, Perfect, CriticalPerfect, IgnoreMiss, IgnoreHit }` (this ordinal order — everything ordinal-dependent relies on it); `Judgement.MaxResult => HitResult.CriticalPerfect` default; `class PerfectJudgement : Judgement` with `MaxResult => HitResult.Perfect` in namespace `Garbus.Game.Objects.Judgement`.

- [ ] **Step 1: Rewrite `HitResult.cs`**

Replace the entire contents of `Garbus.Game/Gameplay/Scoring/HitResult.cs` with:

```csharp
// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Scoring/HitResult.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: osu's members are replaced by the Garbus judgement ladder
// (docs/rules-specs/Judgement.md) — one shared ordinal ladder whose subsets form the note, hold and
// early-permissive families. Ticks, bonuses, ComboBreak and the family-specific osu grades are gone;
// the Ignore pair remains for unscored expiry judgements (the slider body).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Utils;

namespace Garbus.Game.Gameplay.Scoring
{
    [HasOrderedElements]
    public enum HitResult
    {
        /// <summary>
        /// Indicates that the object has not been judged yet.
        /// </summary>
        [Description(@"")]
        [Order(5)]
        None,

        /// <summary>
        /// The object was missed — by its windows elapsing with no qualifying input, or by an
        /// early-miss press. Shared by every judgement family.
        /// </summary>
        [Description(@"Miss")]
        [Order(4)]
        Miss,

        /// <summary>
        /// The hold-family intermediate judgement (hold tails, slider children).
        /// </summary>
        [Description(@"Bad")]
        [Order(3)]
        Bad,

        /// <summary>
        /// The note- and early-permissive-family intermediate judgement.
        /// </summary>
        [Description(@"Near")]
        [Order(2)]
        Near,

        /// <summary>
        /// Shared by every judgement family; the best result for catch-timed and early-permissive
        /// objects, whose families have no Critical Perfect.
        /// </summary>
        [Description(@"Perfect")]
        [Order(1)]
        Perfect,

        /// <summary>
        /// The best judgement of the note and hold families.
        /// </summary>
        [Description(@"Critical Perfect")]
        [Order(0)]
        CriticalPerfect,

        /// <summary>
        /// Indicates a miss that should be ignored for scoring purposes.
        /// </summary>
        [Order(6)]
        IgnoreMiss,

        /// <summary>
        /// Indicates a hit that should be ignored for scoring purposes.
        /// </summary>
        [Order(7)]
        IgnoreHit,
    }

    public static class HitResultExtensions
    {
        private static readonly IList<HitResult> order = EnumExtensions.GetValuesInOrder<HitResult>().ToList();

        /// <summary>
        /// Whether a <see cref="HitResult"/> increases the combo.
        /// </summary>
        public static bool IncreasesCombo(this HitResult result)
            => AffectsCombo(result) && IsHit(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> breaks the combo and resets it back to zero.
        /// </summary>
        public static bool BreaksCombo(this HitResult result)
            => AffectsCombo(result) && !IsHit(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> increases or breaks the combo: every basic non-Miss
        /// result increases it, Miss breaks it, the Ignore pair does neither.
        /// </summary>
        public static bool AffectsCombo(this HitResult result)
            => result >= HitResult.Miss && result <= HitResult.CriticalPerfect;

        /// <summary>
        /// Whether a <see cref="HitResult"/> affects the accuracy portion of the score.
        /// </summary>
        public static bool AffectsAccuracy(this HitResult result) => IsScorable(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> is a basic (scorable, non-ignore) result.
        /// </summary>
        public static bool IsBasic(this HitResult result) => IsScorable(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> represents a miss of any type.
        /// </summary>
        /// <remarks>
        /// Of note, both <see cref="IsMiss"/> and <see cref="IsHit"/> return <see langword="false"/> for <see cref="HitResult.None"/>.
        /// </remarks>
        public static bool IsMiss(this HitResult result)
            => result is HitResult.Miss or HitResult.IgnoreMiss;

        /// <summary>
        /// Whether a <see cref="HitResult"/> represents a successful hit.
        /// </summary>
        /// <remarks>
        /// Of note, both <see cref="IsMiss"/> and <see cref="IsHit"/> return <see langword="false"/> for <see cref="HitResult.None"/>.
        /// </remarks>
        public static bool IsHit(this HitResult result)
        {
            switch (result)
            {
                case HitResult.None:
                case HitResult.Miss:
                case HitResult.IgnoreMiss:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Whether a <see cref="HitResult"/> is scorable.
        /// </summary>
        public static bool IsScorable(this HitResult result)
            => result >= HitResult.Miss && result < HitResult.IgnoreMiss;

        /// <summary>
        /// An array of all <see cref="HitResult"/>s.
        /// </summary>
        public static readonly HitResult[] ALL_TYPES = Enum.GetValues<HitResult>().ToArray();

        /// <summary>
        /// Whether a <see cref="HitResult"/> is valid within a given <see cref="HitResult"/> range.
        /// </summary>
        /// <param name="result">The <see cref="HitResult"/> to check.</param>
        /// <param name="minResult">The minimum <see cref="HitResult"/>.</param>
        /// <param name="maxResult">The maximum <see cref="HitResult"/>.</param>
        /// <returns>Whether <see cref="HitResult"/> falls between <paramref name="minResult"/> and <paramref name="maxResult"/>.</returns>
        public static bool IsValidHitResult(this HitResult result, HitResult minResult, HitResult maxResult)
        {
            if (result == HitResult.None)
                return false;

            if (result == minResult || result == maxResult)
                return true;

            Debug.Assert(minResult <= maxResult);
            return result > minResult && result < maxResult;
        }

        /// <summary>
        /// Ordered index of a <see cref="HitResult"/>. Used for consistent order when displaying hit results to the user.
        /// </summary>
        public static int GetIndexForOrderedDisplay(this HitResult result) => order.IndexOf(result);

        public static void ValidateHitResultPair(HitResult maxResult, HitResult minResult)
        {
            if (maxResult == HitResult.None || !IsHit(maxResult))
                throw new ArgumentOutOfRangeException(nameof(maxResult), $"{maxResult} is not a valid maximum judgement result.");

            if (minResult == HitResult.None || IsHit(minResult))
                throw new ArgumentOutOfRangeException(nameof(minResult), $"{minResult} is not a valid minimum judgement result.");

            if (maxResult == HitResult.IgnoreHit && minResult != HitResult.IgnoreMiss)
                throw new ArgumentOutOfRangeException(nameof(minResult), $"{minResult} is not a valid minimum result for a {maxResult} judgement.");

            if (maxResult.IsBasic() && minResult != HitResult.Miss)
                throw new ArgumentOutOfRangeException(nameof(minResult), $"{HitResult.Miss} is the only valid minimum result for a {maxResult} judgement.");
        }
    }
}
```

- [ ] **Step 2: Update `Judgement.cs`**

In `Garbus.Game/Gameplay/Judgements/Judgement.cs` (append to the "Adapted for Garbus:" header line: "MaxResult defaults to CriticalPerfect; tick/bonus branches removed."), replace `MaxResult`, `MinResult`, and `HealthIncreaseFor(HitResult)`:

```csharp
        /// <summary>
        /// The maximum <see cref="HitResult"/> that can be achieved.
        /// </summary>
        public virtual HitResult MaxResult => HitResult.CriticalPerfect;

        /// <summary>
        /// The minimum <see cref="HitResult"/> that can be achieved - the inverse of <see cref="MaxResult"/>.
        /// </summary>
        /// <remarks>
        /// Defaults to a sane value for the given <see cref="MaxResult"/>.
        /// </remarks>
        public virtual HitResult MinResult
        {
            get
            {
                switch (MaxResult)
                {
                    case HitResult.IgnoreHit:
                        return HitResult.IgnoreMiss;

                    default:
                        return HitResult.Miss;
                }
            }
        }
```

```csharp
        protected virtual double HealthIncreaseFor(HitResult result)
        {
            switch (result)
            {
                default:
                    return 0;

                case HitResult.Miss:
                    return -DEFAULT_MAX_HEALTH_INCREASE * 2;

                case HitResult.Bad:
                    return DEFAULT_MAX_HEALTH_INCREASE * 0.5;

                case HitResult.Near:
                    return DEFAULT_MAX_HEALTH_INCREASE * 0.75;

                case HitResult.Perfect:
                    return DEFAULT_MAX_HEALTH_INCREASE;

                case HitResult.CriticalPerfect:
                    return DEFAULT_MAX_HEALTH_INCREASE * 1.05;
            }
        }
```

- [ ] **Step 3: Update `HitWindows.cs` iteration bounds**

In `Garbus.Game/Gameplay/Scoring/HitWindows.cs`, three loop bounds change:

- `LowestSuccessfulHitResult`: `for (var result = HitResult.Bad; result <= HitResult.CriticalPerfect; ++result)`
- `GetAllAvailableWindows`: `for (var result = HitResult.Miss; result <= HitResult.CriticalPerfect; ++result)`
- `ResultFor`: `for (var result = HitResult.CriticalPerfect; result >= HitResult.Miss; --result)`

- [ ] **Step 4: Interim `DefaultHitWindows` on the new ladder**

Replace the constants and both overrides in `Garbus.Game/Gameplay/Scoring/DefaultHitWindows.cs` (class doc: "Interim shared note windows (the cardinal table) — replaced by per-type windows and deleted in the next task."):

```csharp
        private const double critical_perfect_window = 32;
        private const double perfect_window = 64;
        private const double near_window = 110;
        private const double early_miss_window = 200;

        public override bool IsHitResultAllowed(HitResult result)
            => result is HitResult.CriticalPerfect or HitResult.Perfect or HitResult.Near or HitResult.Miss;

        public override HitWindowRange WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.CriticalPerfect:
                    return HitWindowRange.Symmetric(critical_perfect_window);

                case HitResult.Perfect:
                    return HitWindowRange.Symmetric(perfect_window);

                case HitResult.Near:
                    return HitWindowRange.Symmetric(near_window);

                case HitResult.Miss:
                    return new HitWindowRange(early_miss_window, 0);

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
```

- [ ] **Step 5: Re-key the hold tail table**

In `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs`, replace `resultFor` (interim: spec hold proportions; grace periods and floors come in the hold cycle):

```csharp
    private static HitResult resultFor(double fraction)
    {
        if (fraction >= 1.0) return HitResult.CriticalPerfect;
        if (fraction >= 0.95) return HitResult.Perfect;
        if (fraction >= 0.60) return HitResult.Bad;

        return HitResult.Miss;
    }
```

- [ ] **Step 6: `PerfectJudgement` + the four judgement overrides**

Create `Garbus.Game/Objects/Judgement/PerfectJudgement.cs`:

```csharp
// A judgement capped at Perfect — for catch-timed slider parts and early-permissive slams, whose
// families have no Critical Perfect (see docs/rules-specs/Judgement.md, "Judgement families").

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class PerfectJudgement : Gameplay.Judgements.Judgement
{
    public override HitResult MaxResult => HitResult.Perfect;
}
```

In each of `Garbus.Game/Objects/GarbusSlamCentered.cs`, `Garbus.Game/Objects/GarbusSlamEdge.cs`, `Garbus.Game/Objects/SliderHead.cs`, `Garbus.Game/Objects/SliderChild.cs`, add `using Garbus.Game.Objects.Judgement;` and this member to the class body:

```csharp
    public override Gameplay.Judgements.Judgement CreateJudgement() => new PerfectJudgement();
```

- [ ] **Step 7: `HitsoundFamily.Single` default key + `PlayScreen.scoreFor`**

`Garbus.Game/Gameplay/Audio/HitsoundFamily.cs` — the `Single` factory's default key becomes the new best grade:

```csharp
        public static HitsoundFamily Single(GarbusHitSample sample, HitResult key = HitResult.CriticalPerfect)
            => new HitsoundFamily { [key] = sample };
```

`Garbus.Game/Screens/PlayScreen.cs:327-339` — replace `scoreFor` (values are placeholders, outside the judgement spec's scope):

```csharp
        private static long scoreFor(HitResult result) => result switch
        {
            HitResult.CriticalPerfect => 320,
            HitResult.Perfect => 300,
            HitResult.Near => 200,
            HitResult.Bad => 100,
            _ => 0,
        };
```

- [ ] **Step 8: Update the three test files**

`Garbus.Game.Tests/HitWindowsTest.cs` — re-key `TestWindows` onto the new ladder (same semantics: best ±50, intermediate ±100, Miss early-only 200):

- `IsHitResultAllowed`: `result is HitResult.Perfect or HitResult.Near or HitResult.Miss;`
- `WindowFor`: `HitResult.Perfect => HitWindowRange.Symmetric(50)`, `HitResult.Near => HitWindowRange.Symmetric(100)`, `HitResult.Miss => new HitWindowRange(200, 0)`.
- In assertions, replace every `HitResult.Great` with `HitResult.Near`.

`Garbus.Game.Tests/HitsoundFamilyTest.cs` — replace the whole file body's member keys (same five tests, new ladder):

```csharp
using System.Linq;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class HitsoundFamilyTest
    {
        private static GarbusHitSample sample(string name) => new GarbusHitSample(name);

        [Test]
        public void SingleTopEntryResolvesEveryHitJudgement()
        {
            var best = sample("best");
            var family = HitsoundFamily.Single(best); // keyed at CriticalPerfect

            Assert.That(family.Resolve(HitResult.CriticalPerfect), Is.EqualTo(best));
            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(best));
            Assert.That(family.Resolve(HitResult.Bad), Is.EqualTo(best));
        }

        [Test]
        public void MidLadderEntryIsReachedFromBothSides()
        {
            var near = sample("near");
            var family = new HitsoundFamily { [HitResult.Near] = near };

            // worse-than-Near earned -> walk up to Near
            Assert.That(family.Resolve(HitResult.Bad), Is.EqualTo(near));
            // better-than-Near earned, nothing better defined -> fall down to Near
            Assert.That(family.Resolve(HitResult.CriticalPerfect), Is.EqualTo(near));
        }

        [Test]
        public void BetterSideIsPreferredOverWorseSide()
        {
            var best = sample("best");
            var bad = sample("bad");
            var family = new HitsoundFamily
            {
                [HitResult.CriticalPerfect] = best,
                [HitResult.Bad] = bad,
            };

            // Perfect is between the two; better-first prefers CriticalPerfect.
            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(best));
        }

        [Test]
        public void EmptyFamilyResolvesToNull()
        {
            var family = new HitsoundFamily();
            Assert.That(family.Resolve(HitResult.Perfect), Is.Null);
        }

        [Test]
        public void AllSamplesReturnsDistinctMembers()
        {
            var a = sample("a");
            var family = new HitsoundFamily
            {
                [HitResult.CriticalPerfect] = a,
                [HitResult.Perfect] = a,
            };

            Assert.That(family.AllSamples, Is.EquivalentTo(new[] { a }));
        }

        [Test]
        public void ConcreteTypesSeedSamplesFromTheirFamily()
        {
            var note = new Garbus.Game.Objects.CardinalNote { AngleDeg = 0 };
            note.ApplyDefaults();

            var expected = HitsoundFamilies.CardinalNote.AllSamples.ToArray();

            Assert.That(note.Samples, Is.EquivalentTo(expected));
            Assert.That(note.Hitsounds.Resolve(HitResult.Bad), Is.Not.Null);
        }
    }
}
```

`Garbus.Game.Tests/Visual/TestSceneGameplay.cs` — two comment fixes only (assertions all use `Miss`/`IsHit`, which survive):

- Line 161: `// 100ms early falls inside the Ok window of the default hit windows.` → `// 100ms early falls inside the Near window.`
- Line 185: `...so the offset lands inside the Ok window), but` → `...so the offset lands inside the Near window), but`

- [ ] **Step 9: Build and run the full suite**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: success. If anything still references `Great`/`Good`/`Ok`/`Meh`/ticks/bonuses/`ComboBreak`, the compiler will list it — fix by mapping to the nearest new grade in the same spirit as the changes above (there should be none beyond the files in this task).

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green. (`TestCardinalNoteHitByButtonPress` presses 100 ms early → now `Near`, still a hit; `TestShortHoldInheritsMissedHead`'s 80 ms hold is still shorter than the 110 ms edge, so it still inherits the missed head.)

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "feat: replace osu HitResult ladder with the Garbus judgement ladder"
```

---

### Task 3: Per-type note windows + wiring

Real window tables per note type, hold heads inheriting the parent's windows, `HitWindows.Empty` as the base default, and slam lifetime headroom. Deletes `DefaultHitWindows` and the empty `SliderHitWindows` placeholder.

**Files:**
- Create: `Garbus.Game/Objects/Judgement/CardinalNoteHitWindows.cs`
- Create: `Garbus.Game/Objects/Judgement/ShoulderNoteHitWindows.cs`
- Delete: `Garbus.Game/Gameplay/Scoring/DefaultHitWindows.cs`
- Delete: `Garbus.Game/Objects/Judgement/SliderHitWindows.cs` (empty ported placeholder, superseded)
- Modify: `Garbus.Game/Gameplay/Objects/HitObject.cs:143` (default `CreateHitWindows`)
- Modify: `Garbus.Game/Objects/Note.cs` (remove placeholder override)
- Modify: `Garbus.Game/Objects/CardinalNote.cs`, `Garbus.Game/Objects/ShoulderNote.cs`, `Garbus.Game/Objects/CardinalHoldNote.cs`, `Garbus.Game/Objects/ShoulderHoldNote.cs` (window overrides)
- Modify: `Garbus.Game/Objects/HoldNoteHead.cs` (inherit parent windows)
- Modify: `Garbus.Game/Objects/GarbusSlamCentered.cs`, `Garbus.Game/Objects/GarbusSlamEdge.cs` (`MaximumJudgementOffset`)
- Test: `Garbus.Game.Tests/NoteHitWindowsTest.cs` (new)

**Interfaces:**
- Consumes: `HitWindowRange`, `LateEligibilityEdge`, the new `HitResult` ladder, `PerfectJudgement`.
- Produces: `class CardinalNoteHitWindows : HitWindows` and `class ShoulderNoteHitWindows : HitWindows` in namespace `Garbus.Game.Objects.Judgement` (Task 4's note-lock relies on note objects carrying these via `HitObject.HitWindows`).

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/NoteHitWindowsTest.cs`:

```csharp
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Judgement;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class NoteHitWindowsTest
    {
        [Test]
        public void CardinalTableMatchesSpec()
        {
            var windows = new CardinalNoteHitWindows();

            Assert.That(windows.ResultFor(0), Is.EqualTo(HitResult.CriticalPerfect));
            Assert.That(windows.ResultFor(-32), Is.EqualTo(HitResult.CriticalPerfect));
            Assert.That(windows.ResultFor(33), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(-64), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(65), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(110), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(111), Is.EqualTo(HitResult.None)); // no late Miss window
            Assert.That(windows.ResultFor(-111), Is.EqualTo(HitResult.Miss)); // early-miss
            Assert.That(windows.ResultFor(-200), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.ResultFor(-201), Is.EqualTo(HitResult.None));
            Assert.That(windows.LateEligibilityEdge, Is.EqualTo(110));
            Assert.That(windows.IsHitResultAllowed(HitResult.Bad), Is.False);
        }

        [Test]
        public void ShoulderTableMatchesSpec()
        {
            var windows = new ShoulderNoteHitWindows();

            Assert.That(windows.ResultFor(-40), Is.EqualTo(HitResult.CriticalPerfect));
            Assert.That(windows.ResultFor(41), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(80), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(-81), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(150), Is.EqualTo(HitResult.Near));
            Assert.That(windows.ResultFor(151), Is.EqualTo(HitResult.None));
            Assert.That(windows.ResultFor(-151), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.ResultFor(-200), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.LateEligibilityEdge, Is.EqualTo(150));
            Assert.That(windows.IsHitResultAllowed(HitResult.Bad), Is.False);
        }

        [Test]
        public void NoteTypesWireTheirWindows()
        {
            var cardinal = new CardinalNote { AngleDeg = 90 };
            cardinal.ApplyDefaults();
            Assert.That(cardinal.HitWindows, Is.InstanceOf<CardinalNoteHitWindows>());

            var shoulder = new ShoulderNote { Side = HorizontalDirection.Left };
            shoulder.ApplyDefaults();
            Assert.That(shoulder.HitWindows, Is.InstanceOf<ShoulderNoteHitWindows>());

            var cardinalHold = new CardinalHoldNote { AngleDeg = 90, Duration = 500 };
            cardinalHold.ApplyDefaults();
            Assert.That(cardinalHold.HitWindows, Is.InstanceOf<CardinalNoteHitWindows>());
        }

        [Test]
        public void HoldHeadInheritsParentWindows()
        {
            var hold = new ShoulderHoldNote { Side = HorizontalDirection.Right, Duration = 500 };
            hold.ApplyDefaults();

            Assert.That(hold.HitWindows, Is.InstanceOf<ShoulderNoteHitWindows>());
            Assert.That(hold.Head.HitWindows, Is.SameAs(hold.HitWindows));
        }

        [Test]
        public void NonNoteObjectsGetEmptyWindowsAndSlamsKeepLifetimeHeadroom()
        {
            var slam = new GarbusSlamCentered { AngleDeg = 0 };
            slam.ApplyDefaults();

            Assert.That(slam.HitWindows, Is.SameAs(HitWindows.Empty));
            Assert.That(slam.MaximumJudgementOffset, Is.EqualTo(200));
            Assert.That(slam.Judgement.MaxResult, Is.EqualTo(HitResult.Perfect));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~NoteHitWindowsTest"`
Expected: compile failure (`CardinalNoteHitWindows` does not exist).

- [ ] **Step 3: Create the two window classes**

Create `Garbus.Game/Objects/Judgement/CardinalNoteHitWindows.cs`:

```csharp
// The note-family timing windows for cardinal notes and holds, per docs/rules-specs/Judgement.md.
// The Miss window is early-only: pressing inside it registers an immediate Miss; late mistimes are
// instead handled by eligibility elapsing (LateEligibilityEdge). The spec marks these extents
// provisional — tune them here.

using System;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class CardinalNoteHitWindows : HitWindows
{
    private const double critical_perfect_window = 32;
    private const double perfect_window = 64;
    private const double near_window = 110;
    private const double early_miss_window = 200;

    public override bool IsHitResultAllowed(HitResult result)
        => result is HitResult.CriticalPerfect or HitResult.Perfect or HitResult.Near or HitResult.Miss;

    public override HitWindowRange WindowFor(HitResult result)
    {
        switch (result)
        {
            case HitResult.CriticalPerfect:
                return HitWindowRange.Symmetric(critical_perfect_window);

            case HitResult.Perfect:
                return HitWindowRange.Symmetric(perfect_window);

            case HitResult.Near:
                return HitWindowRange.Symmetric(near_window);

            case HitResult.Miss:
                return new HitWindowRange(early_miss_window, 0);

            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }
}
```

Create `Garbus.Game/Objects/Judgement/ShoulderNoteHitWindows.cs` — identical shape, constants `40`, `80`, `150`, `200`, class doc "The note-family timing windows for shoulder notes and holds":

```csharp
// The note-family timing windows for shoulder notes and holds, per docs/rules-specs/Judgement.md.
// The Miss window is early-only: pressing inside it registers an immediate Miss; late mistimes are
// instead handled by eligibility elapsing (LateEligibilityEdge). The spec marks these extents
// provisional — tune them here.

using System;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Judgement;

public class ShoulderNoteHitWindows : HitWindows
{
    private const double critical_perfect_window = 40;
    private const double perfect_window = 80;
    private const double near_window = 150;
    private const double early_miss_window = 200;

    public override bool IsHitResultAllowed(HitResult result)
        => result is HitResult.CriticalPerfect or HitResult.Perfect or HitResult.Near or HitResult.Miss;

    public override HitWindowRange WindowFor(HitResult result)
    {
        switch (result)
        {
            case HitResult.CriticalPerfect:
                return HitWindowRange.Symmetric(critical_perfect_window);

            case HitResult.Perfect:
                return HitWindowRange.Symmetric(perfect_window);

            case HitResult.Near:
                return HitWindowRange.Symmetric(near_window);

            case HitResult.Miss:
                return new HitWindowRange(early_miss_window, 0);

            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }
}
```

- [ ] **Step 4: Wire the note types**

In each of `CardinalNote.cs` and `CardinalHoldNote.cs`, add `using Garbus.Game.Gameplay.Scoring;` and `using Garbus.Game.Objects.Judgement;` (where missing) and this member:

```csharp
    protected override HitWindows CreateHitWindows() => new CardinalNoteHitWindows();
```

In each of `ShoulderNote.cs` and `ShoulderHoldNote.cs`, likewise:

```csharp
    protected override HitWindows CreateHitWindows() => new ShoulderNoteHitWindows();
```

In `Garbus.Game/Objects/Note.cs`, delete the `CreateHitWindows` override entirely (and the now-unused `using Garbus.Game.Gameplay.Scoring;`).

In `Garbus.Game/Objects/HoldNoteHead.cs`, add `using Garbus.Game.Gameplay.Scoring;` and (parent windows exist by the time nested defaults run — `ApplyDefaultsToSelf` precedes `CreateNestedHitObjects`; the classes are stateless so sharing the instance is fine):

```csharp
    protected override HitWindows CreateHitWindows() => Parent.HitWindows;
```

- [ ] **Step 5: Base default → `Empty`; delete the dead files; slam headroom**

`Garbus.Game/Gameplay/Objects/HitObject.cs:143` — replace:

```csharp
        protected virtual HitWindows CreateHitWindows() => new DefaultHitWindows();
```

with (also reword the XML summary to "Defaults to <see cref="Scoring.HitWindows.Empty"/> — objects without a timed button input have no windows."):

```csharp
        protected virtual HitWindows CreateHitWindows() => HitWindows.Empty;
```

Delete `Garbus.Game/Gameplay/Scoring/DefaultHitWindows.cs` and `Garbus.Game/Objects/Judgement/SliderHitWindows.cs`.

In each of `GarbusSlamCentered.cs` and `GarbusSlamEdge.cs`, add this member:

```csharp
    // Interim lifetime headroom matching the drawable's ±200ms first-cut window (the slam cycle
    // replaces both with real early-permissive windows including the late Near extent).
    public override double MaximumJudgementOffset => 200;
```

- [ ] **Step 6: Run the new tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~NoteHitWindowsTest"`
Expected: 5 passed.

- [ ] **Step 7: Run the full suite and build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: success (nothing references `DefaultHitWindows`/`SliderHitWindows` anymore).

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green. Shoulder assertions in `TestSceneGameplay` (hold at 13000, press at 12900 = −100 → inside shoulder Perfect 80? No — −100 is inside Near 150 → still a hit) remain valid.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: per-note-type asymmetric hit windows per the judgement spec"
```

---

### Task 4: Note-lock rewrite (oldest-eligible, no force-miss)

**Files:**
- Modify: `Garbus.Game/UI/GarbusOrderedHitPolicy.cs` (full rewrite below)
- Modify: `Garbus.Game/UI/Lane.cs` (drop the `NewResult` → `HandleHit` wiring)
- Modify: `Garbus.Game/Objects/Drawables/IHittableNote.cs` (`MissForcefully` → `PressJudged`)
- Modify: `Garbus.Game/Objects/Drawables/DrawableNote.cs`, `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs` (implement `PressJudged`, drop `MissForcefully`)
- Test: `Garbus.Game.Tests/Visual/TestSceneNoteLock.cs` (new)

**Interfaces:**
- Consumes: `HitWindows.ResultFor` (sign-aware, Task 1), note windows on `HitObject.HitWindows` (Task 3), `HitObjectContainer.AliveObjects` (ordered by StartTime ascending — see `Gameplay/UI/HitObjectContainer.cs:26`).
- Produces: `bool IHittableNote.PressJudged { get; }`; `GarbusOrderedHitPolicy.IsHittable(DrawableHitObject, double)` (unchanged signature, new semantics); `HandleHit` and `MissForcefully` no longer exist.

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/Visual/TestSceneNoteLock.cs` (harness mirrors `TestSceneGameplay`; the spec rules under test are called out per test):

```csharp
// Pins the judgement spec's note-lock (docs/rules-specs/Judgement.md): an input resolves against
// the oldest eligible object in its lane whose window contains it; eligibility ends only by
// judgement (including early-miss presses) or the object's own late window elapsing — hitting a
// later object never force-misses an earlier one.

using System;
using System.Linq;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Timing;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneNoteLock : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private ManualInputManager input = null!;
        private GarbusPlayfield playfield = null!;

        [Resolved]
        private Gameplay.UI.Scrolling.GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        private void createPlayfield(params GarbusHitObject[] hitObjects)
        {
            AddStep("create playfield", () =>
            {
                scrollingInfo.TimeRange.Value = 700;
                manualClock = new ManualClock { Rate = 1 };

                foreach (var hitObject in hitObjects)
                    hitObject.ApplyDefaults();

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = new FramedClock(manualClock),
                        Child = new Input.GarbusInputManager
                        {
                            Child = playfield = new GarbusPlayfield
                            {
                                Size = Vector2.One,
                            },
                        },
                    },
                };

                foreach (var hitObject in hitObjects)
                    playfield.Add(PlayScreen.CreateDrawableRepresentation(hitObject));
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
        }

        /// <summary>Walks the clock forward in sub-lifetime increments (see TestSceneGameplay.playThrough).</summary>
        private void seekTo(double target)
        {
            AddUntilStep($"seek to {target}", () =>
            {
                manualClock.CurrentTime = Math.Min(target, manualClock.CurrentTime + 200);
                return manualClock.CurrentTime >= target;
            });
        }

        private void pressNorth()
        {
            AddStep("press north", () => input.PressJoystickButton(JoystickButton.Hat1Up));
            AddStep("release north", () => input.ReleaseJoystickButton(JoystickButton.Hat1Up));
        }

        private Objects.Drawables.DrawableCardinalNote note(double startTime)
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableCardinalNote>().Single(h => h.HitObject.StartTime == startTime);

        [Test]
        public void TestOldestContainingObjectTakesThePress()
        {
            // Two same-lane (North) notes with overlapping windows.
            createPlayfield(
                new CardinalNote { StartTime = 2000, AngleDeg = 90 },
                new CardinalNote { StartTime = 2050, AngleDeg = 90 });

            // 2055 is inside BOTH windows: note1 offset +55 (Perfect), note2 offset +5 (CriticalPerfect).
            // Oldest-first: note1 must take the press; under the old mania policy note2 would take it
            // and note1 would be force-missed.
            seekTo(2055);
            pressNorth();

            AddUntilStep("older note judged", () => note(2000).Judged);
            AddAssert("older note took the press (Perfect)", () => note(2000).Result?.Type == HitResult.Perfect);
            AddAssert("newer note untouched", () => !note(2050).Judged);

            // The newer note stays eligible until its own edge: a second press at +100 lands Near.
            seekTo(2150);
            pressNorth();

            AddUntilStep("newer note judged", () => note(2050).Judged);
            AddAssert("newer note hit (Near)", () => note(2050).Result?.Type == HitResult.Near);
            AddAssert("older note was never force-missed", () => note(2000).IsHit);
        }

        [Test]
        public void TestEarlyMissPressJudgesImmediately()
        {
            createPlayfield(new CardinalNote { StartTime = 2000, AngleDeg = 90 });

            // -150: outside Near (110) but inside the early-only Miss window (200).
            seekTo(1850);
            pressNorth();

            AddUntilStep("note judged before its time", () => note(2000).Judged);
            AddAssert("early-miss press registered a Miss", () => note(2000).Result?.Type == HitResult.Miss);
            AddAssert("clock still before StartTime", () => manualClock.CurrentTime < 2000);
        }

        [Test]
        public void TestAutoMissAtLateEligibilityEdge()
        {
            createPlayfield(new CardinalNote { StartTime = 2000, AngleDeg = 90 });

            // Just inside the Near late edge (110): still eligible, unjudged.
            seekTo(2105);
            AddAssert("still eligible inside the Near edge", () => !note(2000).Judged);

            // Just past it: auto-missed (not at osu's 136/173).
            AddStep("step past the edge", () => manualClock.CurrentTime = 2115);
            AddUntilStep("auto-missed past the edge", () => note(2000).Result?.Type == HitResult.Miss);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify the new behaviour fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneNoteLock"`
Expected: `TestOldestContainingObjectTakesThePress` FAILS (the old policy gives the 2055 press to the newer note and force-misses the older). `TestEarlyMissPressJudgesImmediately` and `TestAutoMissAtLateEligibilityEdge` should already pass (they pin Task 1–3 behaviour); if they fail, stop and investigate before touching the policy.

- [ ] **Step 3: Rewrite the policy**

Replace the entire contents of `Garbus.Game/UI/GarbusOrderedHitPolicy.cs` with:

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/BacOrderedHitPolicy.cs).
// BacOrderedHitPolicy → GarbusOrderedHitPolicy. Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: rewritten from mania's ordered policy to the Garbus judgement spec's
// note-lock (docs/rules-specs/Judgement.md) — oldest-eligible resolution, no force-missing.

using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Objects.Drawables;

namespace Garbus.Game.UI;

/// <summary>
/// The spec's note-lock: an input interacts with the <b>oldest eligible object in the lane whose
/// window contains it</b>. <see cref="IsHittable"/> vetoes a candidate when an older, press-unjudged
/// object's window also contains the press; combined with notes declining presses their own window
/// doesn't contain (<c>ResultFor == None</c> applies nothing), exactly one object accepts any press
/// regardless of input-queue order. Objects leave eligibility only by being judged (including via an
/// early-miss press) or by their own late window elapsing — hitting a later object never affects an
/// earlier one.
/// </summary>
public class GarbusOrderedHitPolicy
{
    private readonly HitObjectContainer hitObjectContainer;

    public GarbusOrderedHitPolicy(HitObjectContainer hitObjectContainer)
    {
        this.hitObjectContainer = hitObjectContainer;
    }

    /// <summary>
    /// Determines whether a <see cref="DrawableHitObject"/> may accept a press at a point in time.
    /// </summary>
    public bool IsHittable(DrawableHitObject hitObject, double time)
    {
        // AliveObjects is ordered by start time (ascending).
        foreach (var obj in hitObjectContainer.AliveObjects)
        {
            if (obj.HitObject.StartTime >= hitObject.HitObject.StartTime)
                return true; // no older candidates remain

            if (obj is not IHittableNote older || older.PressJudged)
                continue;

            if (obj.HitObject.HitWindows.ResultFor(time - obj.HitObject.StartTime) != HitResult.None)
                return false; // the press belongs to this older object
        }

        return true;
    }
}
```

- [ ] **Step 4: Update `IHittableNote` and the two note drawables**

Replace the interface body in `Garbus.Game/Objects/Drawables/IHittableNote.cs`:

```csharp
/// <summary>
/// A non-generic view over a note drawable that participates in a lane's note-lock. Lets the
/// <see cref="UI.Lane"/> and <see cref="UI.GarbusOrderedHitPolicy"/> drive the policy without knowing the
/// concrete <see cref="DrawableNote{T}"/> type.
/// </summary>
public interface IHittableNote
{
    /// <summary>
    /// Note-lock gate installed by the owning lane: vetoes a press that belongs to an older
    /// eligible object in the lane.
    /// </summary>
    Func<DrawableHitObject, double, bool>? CheckHittable { get; set; }

    /// <summary>
    /// Whether this note's note-locked press has been judged — the head, for holds. Once true the
    /// object no longer competes for presses (its tail may still be pending).
    /// </summary>
    bool PressJudged { get; }
}
```

In `Garbus.Game/Objects/Drawables/DrawableNote.cs`: delete the `MissForcefully` method (and its XML doc), and add:

```csharp
    public virtual bool PressJudged => Judged;
```

In `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs`: delete the empty `MissForcefully` override, and add:

```csharp
    public override bool PressJudged => Head.Judged;
```

- [ ] **Step 5: Simplify `Lane`**

In `Garbus.Game/UI/Lane.cs`, delete the `LoadComplete` override, the `onNewResult` method, and the `Dispose` override (all three existed only for the `NewResult -= / += onNewResult` force-miss wiring). Remove the now-unused `using Garbus.Game.Gameplay.Judgements;`. Everything else (policy construction, `OnNewDrawableHitObject` installing `CheckHittable`) stays.

- [ ] **Step 6: Run the note-lock tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneNoteLock"`
Expected: 3 passed.

- [ ] **Step 7: Run the full suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green. `TestCardinalNoteHitByButtonPress` asserts the two later cardinals stay unjudged after hitting the first — still true (no force-miss ever fired there; the notes are seconds apart).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: oldest-eligible note-lock per the judgement spec, drop force-missing"
```

---

### Task 5: Documentation sweep + final verification

**Files:**
- Modify: `CLAUDE.md` (current-state note)
- Modify: `docs/superpowers/specs/2026-07-19-judgement-foundation-design.md` (status line)

- [ ] **Step 1: Record the new state in `CLAUDE.md`**

In `CLAUDE.md`, insert the following as a new paragraph at the end of the `## Current state (Phase 4 complete)` section's intro block (immediately before the `## Current state (Phase 3 complete)` heading):

```markdown
**Judgement foundation (first alignment cycle against `docs/rules-specs/Judgement.md`):** `HitResult`
is the Garbus ladder (`Miss < Bad < Near < Perfect < CriticalPerfect` + the Ignore pair), windows are
asymmetric `(Early, Late)` ranges (`HitWindowRange`) with an early-only Miss window and hittability
keyed off `HitWindows.LateEligibilityEdge`, notes carry `CardinalNoteHitWindows` /
`ShoulderNoteHitWindows` (hold heads share the parent instance), and note-lock is the spec's
oldest-eligible-containing rule (no force-missing — `GarbusOrderedHitPolicy`). Holds/sliders/slams
carry interim mappings only; their spec alig nment is the hold/slider/slam cycles (see
`docs/superpowers/specs/2026-07-19-judgement-foundation-design.md`).
```

- [ ] **Step 2: Mark the spec implemented**

In `docs/superpowers/specs/2026-07-19-judgement-foundation-design.md`, change `Status: approved` to `Status: implemented`.

- [ ] **Step 3: Full build + full suite**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: success, zero warnings introduced by this work.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md docs/superpowers/specs/2026-07-19-judgement-foundation-design.md
git commit -m "docs: record judgement foundation state"
```
