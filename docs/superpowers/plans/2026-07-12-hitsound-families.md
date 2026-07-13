# Judgement-keyed Hitsound Families Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make each concrete hit object type own a per-type hitsound family keyed by earned judgement, so the sound played on a hit is chosen by the judgement, falling back to the nearest better (then worse) defined member when a judgement has no sound of its own.

**Architecture:** A new immutable `HitsoundFamily` value maps `HitResult → HitSampleInfo` and resolves an earned judgement to a sample (better-first, then worse). Each concrete `GarbusHitObject` exposes its family and seeds `HitObject.Samples` from it (so the existing vendored preload path loads every member ahead of time). The Garbus drawable layer overrides `PlaySamples()` to play only the resolved member; misses stay silent because playback is gated on `ArmedState.Hit`.

**Tech Stack:** C# (.NET), osu-framework, NUnit. Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL fields use `= null!`.
- No backwards-compatibility layers, version bumps, or historical notes (experimental project).
- Vendored osu.Game files keep their ppy MIT header + "Adapted for Garbus:" line. `HitObject.cs` and
  `DrawableHitObject.cs` are vendored — do not restructure them; the only vendored edit here is one new
  `PlaySamples()` override site is added in *Garbus* subclasses, not in the vendored files.
- The single existing gameplay sample is `Samples/Gameplay/soft-hitnormal.wav`, looked up as
  `HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT)`.

---

## File Structure

- **Create** `Garbus.Game/Gameplay/Audio/HitsoundFamily.cs` — the family value + resolution logic.
- **Create** `Garbus.Game/Objects/HitsoundFamilies.cs` — the per-type family instances (distinct fields).
- **Create** `Garbus.Game/Objects/Drawables/GarbusHitSoundPlayback.cs` — shared static play helper.
- **Modify** `Garbus.Game/Gameplay/Audio/HitSoundContainer.cs` — add `Play(HitSampleInfo?)` + `LastPlayed`.
- **Modify** `Garbus.Game/Objects/GarbusHitObject.cs` — abstract `Hitsounds`, seed `Samples` from it.
- **Modify** the 10 concrete `GarbusHitObject` subclasses — add `Hitsounds` override.
- **Modify** `Garbus.Game/Objects/Drawables/DrawableGarbusHitObject.cs` — `PlaySamples()` override.
- **Modify** `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs` — `PlaySamples()` override.
- **Create** `Garbus.Game.Tests/HitsoundFamilyTest.cs` — unit tests for resolution.
- **Create** `Garbus.Game.Tests/Visual/TestSceneHitSoundContainer.cs` — container selection test.
- **Modify** `Garbus.Game.Tests/Visual/TestSceneGameplay.cs` — integration assertion (one member played).

---

## Task 1: `HitsoundFamily` value + resolution

**Files:**
- Create: `Garbus.Game/Gameplay/Audio/HitsoundFamily.cs`
- Test: `Garbus.Game.Tests/HitsoundFamilyTest.cs`

**Interfaces:**
- Produces:
  - `class HitsoundFamily` with an object-initializer indexer `HitSampleInfo this[HitResult] { set; }`,
    `IEnumerable<HitSampleInfo> AllSamples { get; }`, `HitSampleInfo? Resolve(HitResult earned)`,
    and `static HitsoundFamily Single(HitSampleInfo sample, HitResult key = HitResult.Perfect)`.
  - Resolution: order candidate keys by `HitResultExtensions.GetIndexForOrderedDisplay` (Perfect = 0 …
    Miss = 5 … None = 13). Return the defined key with the greatest index `<= earned`'s index (nearest
    at-least-as-good). If none, the defined key with the smallest index `> earned`'s index (nearest
    worse). Otherwise null.

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests/HitsoundFamilyTest.cs`:

```csharp
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class HitsoundFamilyTest
    {
        private static HitSampleInfo sample(string name) => new HitSampleInfo(name);

        [Test]
        public void SingleTopEntryResolvesEveryHitJudgement()
        {
            var perfect = sample("perfect");
            var family = HitsoundFamily.Single(perfect); // keyed at Perfect

            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(perfect));
            Assert.That(family.Resolve(HitResult.Great), Is.EqualTo(perfect));
            Assert.That(family.Resolve(HitResult.Meh), Is.EqualTo(perfect));
        }

        [Test]
        public void MidLadderEntryIsReachedFromBothSides()
        {
            var good = sample("good");
            var family = new HitsoundFamily { [HitResult.Good] = good };

            // worse-than-Good earned -> walk up to Good
            Assert.That(family.Resolve(HitResult.Meh), Is.EqualTo(good));
            // better-than-Good earned, nothing better defined -> fall down to Good
            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(good));
        }

        [Test]
        public void BetterSideIsPreferredOverWorseSide()
        {
            var perfect = sample("perfect");
            var meh = sample("meh");
            var family = new HitsoundFamily
            {
                [HitResult.Perfect] = perfect,
                [HitResult.Meh] = meh,
            };

            // Good is between the two; better-first prefers Perfect.
            Assert.That(family.Resolve(HitResult.Good), Is.EqualTo(perfect));
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
                [HitResult.Perfect] = a,
                [HitResult.Great] = a,
            };

            Assert.That(family.AllSamples, Is.EquivalentTo(new[] { a }));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~HitsoundFamilyTest`
Expected: FAIL to compile ("HitsoundFamily could not be found").

- [ ] **Step 3: Write the implementation**

Create `Garbus.Game/Gameplay/Audio/HitsoundFamily.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Gameplay.Audio
{
    /// <summary>
    /// A per-hit-object-type set of hitsounds, one per earnable judgement. The sound played on a hit is
    /// chosen by the earned <see cref="HitResult"/>; a judgement with no member of its own falls back to
    /// the nearest better member, then the nearest worse.
    /// </summary>
    public class HitsoundFamily
    {
        private readonly Dictionary<HitResult, HitSampleInfo> members = new Dictionary<HitResult, HitSampleInfo>();

        /// <summary>
        /// Assigns a member for object-initializer construction: <c>new HitsoundFamily { [HitResult.Perfect] = sample }</c>.
        /// </summary>
        public HitSampleInfo this[HitResult result]
        {
            set => members[result] = value;
        }

        /// <summary>
        /// Every distinct member, for preloading.
        /// </summary>
        public IEnumerable<HitSampleInfo> AllSamples => members.Values.Distinct();

        /// <summary>
        /// Builds a family with a single member, keyed at the type's best judgement by default.
        /// </summary>
        public static HitsoundFamily Single(HitSampleInfo sample, HitResult key = HitResult.Perfect)
            => new HitsoundFamily { [key] = sample };

        /// <summary>
        /// Resolves the member to play for an earned judgement: nearest at-least-as-good member first,
        /// then nearest worse. Null if the family is empty.
        /// </summary>
        public HitSampleInfo? Resolve(HitResult earned)
        {
            int earnedIndex = earned.GetIndexForOrderedDisplay();

            HitResult? better = members.Keys
                                       .Where(k => k.GetIndexForOrderedDisplay() <= earnedIndex)
                                       .OrderByDescending(k => k.GetIndexForOrderedDisplay())
                                       .Cast<HitResult?>()
                                       .FirstOrDefault();

            if (better is HitResult b)
                return members[b];

            HitResult? worse = members.Keys
                                      .Where(k => k.GetIndexForOrderedDisplay() > earnedIndex)
                                      .OrderBy(k => k.GetIndexForOrderedDisplay())
                                      .Cast<HitResult?>()
                                      .FirstOrDefault();

            return worse is HitResult w ? members[w] : null;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~HitsoundFamilyTest`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Gameplay/Audio/HitsoundFamily.cs Garbus.Game.Tests/HitsoundFamilyTest.cs
git commit -m "feat: add HitsoundFamily with judgement-based resolution"
```

---

## Task 2: `HitSoundContainer` plays a chosen member

**Files:**
- Modify: `Garbus.Game/Gameplay/Audio/HitSoundContainer.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneHitSoundContainer.cs`

**Interfaces:**
- Consumes: `HitsoundFamily` (Task 1), `HitSampleInfo`.
- Produces on `HitSoundContainer`:
  - `void Play(HitSampleInfo? info)` — plays the preloaded channel whose originating `HitSampleInfo`
    equals `info`; a null or unmatched `info` is a no-op (does not increment `PlayCount`).
  - `HitSampleInfo? LastPlayed { get; }` — the info last matched and played (test seam).
  - Existing `void Play()` and `int PlayCount` are retained.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneHitSoundContainer.cs`:

```csharp
using Garbus.Game.Gameplay.Audio;
using NUnit.Framework;
using osu.Framework.Graphics;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneHitSoundContainer : GarbusTestScene
    {
        private HitSoundContainer container = null!;

        private static readonly HitSampleInfo real = new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT);
        private static readonly HitSampleInfo unloaded = new HitSampleInfo("does-not-exist");

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create container with the soft-hitnormal sample", () =>
            {
                Child = container = new HitSoundContainer { RelativeSizeAxes = Axes.Both };
                container.Samples = new[] { real };
            });
        }

        [Test]
        public void PlayingAMatchingInfoPlaysExactlyThatMember()
        {
            AddStep("play the loaded info", () => container.Play(real));
            AddAssert("play count is 1", () => container.PlayCount, () => Is.EqualTo(1));
            AddAssert("last played is the loaded info", () => container.LastPlayed, () => Is.EqualTo(real));
        }

        [Test]
        public void PlayingAnUnloadedInfoIsSilent()
        {
            AddStep("play an info that was never loaded", () => container.Play(unloaded));
            AddAssert("play count stays 0", () => container.PlayCount, () => Is.EqualTo(0));
            AddAssert("last played stays null", () => container.LastPlayed, () => Is.Null);
        }

        [Test]
        public void PlayingNullIsSilent()
        {
            AddStep("play null", () => container.Play((HitSampleInfo?)null));
            AddAssert("play count stays 0", () => container.PlayCount, () => Is.EqualTo(0));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneHitSoundContainer`
Expected: FAIL to compile ("no overload for Play takes HitSampleInfo" / "LastPlayed not found").

- [ ] **Step 3: Implement the overload**

In `Garbus.Game/Gameplay/Audio/HitSoundContainer.cs`, change the sample bookkeeping to track each
sample's originating info, and add the overload + seam.

Replace the `drawableSamples` field and the `Samples` setter body so each loaded sample remembers its
info. First, replace:

```csharp
        private readonly List<DrawableSample> drawableSamples = new List<DrawableSample>();
```

with:

```csharp
        private readonly List<(HitSampleInfo info, DrawableSample sample)> drawableSamples = new List<(HitSampleInfo, DrawableSample)>();
```

Update `Length` (it referenced `drawableSamples`):

```csharp
        public double Length => drawableSamples.Count == 0 ? 0 : drawableSamples.Max(s => s.sample.Length);
```

In the `Samples` setter, change the add line from `drawableSamples.Add(drawableSample);` to:

```csharp
                    drawableSamples.Add((info, drawableSample));
```

In `ClearSamples`, change the disposal loop from `foreach (var sample in drawableSamples) RemoveInternal(sample, true);` to:

```csharp
            foreach (var (_, sample) in drawableSamples)
                RemoveInternal(sample, true);
```

Change the existing wholesale `Play()` loop from `foreach (var sample in drawableSamples) playingChannels.Add(sample.Play());` to:

```csharp
            foreach (var (_, sample) in drawableSamples)
                playingChannels.Add(sample.Play());
```

Add the seam field near `PlayCount`:

```csharp
        /// <summary>The info last matched and played by <see cref="Play(HitSampleInfo?)"/>. Test seam.</summary>
        public HitSampleInfo? LastPlayed { get; private set; }
```

Add the new overload directly below the existing `Play()`:

```csharp
        /// <summary>
        /// Plays the single preloaded member whose originating <see cref="HitSampleInfo"/> equals <paramref name="info"/>.
        /// A null or unmatched info is a no-op.
        /// </summary>
        public void Play(HitSampleInfo? info)
        {
            if (info == null)
                return;

            foreach (var (loadedInfo, sample) in drawableSamples)
            {
                if (!loadedInfo.Equals(info))
                    continue;

                PlayCount++;
                LastPlayed = info;

                playingChannels.RemoveAll(c => !c.Playing);
                playingChannels.Add(sample.Play());
                return;
            }
        }
```

Note: `HitSoundContainer.cs` is a Garbus original (the SkinnableSound replacement), so this is not a
vendored-file edit. The file already `using`s `Garbus.Game.Gameplay.Audio`'s own namespace; no new
usings are required (`HitSampleInfo` is in the same namespace).

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneHitSoundContainer`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Gameplay/Audio/HitSoundContainer.cs Garbus.Game.Tests/Visual/TestSceneHitSoundContainer.cs
git commit -m "feat: play a single hitsound member by info"
```

---

## Task 3: `GarbusHitObject` declares and seeds its family

**Files:**
- Create: `Garbus.Game/Objects/HitsoundFamilies.cs`
- Modify: `Garbus.Game/Objects/GarbusHitObject.cs`
- Modify: `Garbus.Game/Objects/CardinalNote.cs`, `ShoulderNote.cs`, `CardinalHoldNote.cs`,
  `ShoulderHoldNote.cs`, `HoldNoteHead.cs`, `SliderHead.cs`, `SliderChild.cs`, `SliderBody.cs`,
  `GarbusSlamCentered.cs`, `GarbusSlamEdge.cs`
- Test: `Garbus.Game.Tests/HitsoundFamilyTest.cs` (add a wiring test)

**Interfaces:**
- Consumes: `HitsoundFamily` (Task 1), `HitSampleInfo`.
- Produces:
  - `static class HitsoundFamilies` with ten distinct `public static readonly HitsoundFamily` fields:
    `CardinalNote`, `ShoulderNote`, `CardinalHoldNote`, `ShoulderHoldNote`, `HoldNoteHead`,
    `SliderHead`, `SliderChild`, `SliderBody`, `SlamCentered`, `SlamEdge` — each a single
    soft-hitnormal member for now.
  - `GarbusHitObject.Hitsounds` — `public abstract HitsoundFamily Hitsounds { get; }`.
  - `GarbusHitObject` seeds `Samples = Hitsounds.AllSamples.ToList()` in `ApplyDefaultsToSelf`.

- [ ] **Step 1: Write the failing wiring test**

Append to `Garbus.Game.Tests/HitsoundFamilyTest.cs` (inside the existing class):

```csharp
        [Test]
        public void ConcreteTypesSeedSamplesFromTheirFamily()
        {
            var note = new Garbus.Game.Objects.CardinalNote { AngleDeg = 0 };
            note.ApplyDefaults();

            var expected = HitsoundFamilies.CardinalNote.AllSamples.ToArray();

            Assert.That(note.Samples, Is.EquivalentTo(expected));
            Assert.That(note.Hitsounds.Resolve(HitResult.Meh), Is.Not.Null);
        }
```

Add the required usings at the top of the file:

```csharp
using System.Linq;
using Garbus.Game.Objects;
```

(Note: `CardinalNote`'s required members — confirm the property name/kind by opening
`Garbus.Game/Objects/CardinalNote.cs`; it has `AngleDeg`. If `CardinalNote` requires other `required`
members, set them here too.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~HitsoundFamilyTest`
Expected: FAIL to compile ("HitsoundFamilies not found" / "Hitsounds not found").

- [ ] **Step 3a: Create the family holder**

Create `Garbus.Game/Objects/HitsoundFamilies.cs`:

```csharp
// The per-hit-object-type hitsound families. Each type owns a distinct field so its sound set can
// diverge independently; today every one is the single soft-hitnormal member.

using Garbus.Game.Gameplay.Audio;

namespace Garbus.Game.Objects
{
    public static class HitsoundFamilies
    {
        private static HitsoundFamily softNormal()
            => HitsoundFamily.Single(new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT));

        public static readonly HitsoundFamily CardinalNote = softNormal();
        public static readonly HitsoundFamily ShoulderNote = softNormal();
        public static readonly HitsoundFamily CardinalHoldNote = softNormal();
        public static readonly HitsoundFamily ShoulderHoldNote = softNormal();
        public static readonly HitsoundFamily HoldNoteHead = softNormal();
        public static readonly HitsoundFamily SliderHead = softNormal();
        public static readonly HitsoundFamily SliderChild = softNormal();
        public static readonly HitsoundFamily SliderBody = softNormal();
        public static readonly HitsoundFamily SlamCentered = softNormal();
        public static readonly HitsoundFamily SlamEdge = softNormal();
    }
}
```

- [ ] **Step 3b: Make `GarbusHitObject` abstract-declare and seed the family**

Replace the whole body of `Garbus.Game/Objects/GarbusHitObject.cs` with:

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacHitObject.cs). BacHitObject →
// GarbusHitObject.

using System.Linq;
using System.Threading;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects;

public abstract class GarbusHitObject : HitObject
{
    /// <summary>
    /// This type's hitsound family: the set of sounds, one per earnable judgement, from which the
    /// judged sound is chosen at hit time.
    /// </summary>
    public abstract HitsoundFamily Hitsounds { get; }

    protected override void ApplyDefaultsToSelf(CancellationToken cancellationToken = default)
    {
        base.ApplyDefaultsToSelf();
        Samples = Hitsounds.AllSamples.ToList();
    }

    public override Gameplay.Judgements.Judgement CreateJudgement() => new();
}
```

IMPORTANT: `HitObject.ApplyDefaultsToSelf` in the vendored base is `protected virtual void
ApplyDefaultsToSelf()` and takes **no** parameter. Match its exact signature — override
`protected override void ApplyDefaultsToSelf()` (no `CancellationToken`). Corrected body:

```csharp
    protected override void ApplyDefaultsToSelf()
    {
        base.ApplyDefaultsToSelf();
        Samples = Hitsounds.AllSamples.ToList();
    }
```

(Drop the `using System.Threading;`. Verify the base signature in
`Garbus.Game/Gameplay/Objects/HitObject.cs` before writing — it is `protected virtual void ApplyDefaultsToSelf()`.)

- [ ] **Step 3c: Add the `Hitsounds` override to each concrete type**

For each of the ten files, add the property to the class body (returning its distinct family field).
Add `using Garbus.Game.Gameplay.Audio;` to any file that does not already import it. Exact additions:

`CardinalNote.cs` (class `CardinalNote`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.CardinalNote;
```
`ShoulderNote.cs` (class `ShoulderNote`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.ShoulderNote;
```
`CardinalHoldNote.cs` (class `CardinalHoldNote`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.CardinalHoldNote;
```
`ShoulderHoldNote.cs` (class `ShoulderHoldNote`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.ShoulderHoldNote;
```
`HoldNoteHead.cs` (class `HoldNoteHead<TParent>`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.HoldNoteHead;
```
`SliderHead.cs` (class `SliderHead`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderHead;
```
`SliderChild.cs` (class `SliderChild`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderChild;
```
`SliderBody.cs` (class `SliderBody`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderBody;
```
`GarbusSlamCentered.cs` (class `GarbusSlamCentered`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.SlamCentered;
```
`GarbusSlamEdge.cs` (class `GarbusSlamEdge`):
```csharp
    public override HitsoundFamily Hitsounds => HitsoundFamilies.SlamEdge;
```

The `Note` abstract class in `Note.cs` needs no change — it stays abstract and does not implement
`Hitsounds` (its concrete descendants do).

- [ ] **Step 4: Build, then run the test**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: SUCCESS (every concrete `GarbusHitObject` implements `Hitsounds`; a missed one is a compile
error naming the type).

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~HitsoundFamilyTest`
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Objects/HitsoundFamilies.cs Garbus.Game/Objects/GarbusHitObject.cs Garbus.Game/Objects/*.cs Garbus.Game.Tests/HitsoundFamilyTest.cs
git commit -m "feat: give each hit object type a hitsound family"
```

---

## Task 4: Play the judged member (drawable overrides) + integration

**Files:**
- Create: `Garbus.Game/Objects/Drawables/GarbusHitSoundPlayback.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableGarbusHitObject.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`

**Interfaces:**
- Consumes: `HitSoundContainer.Play(HitSampleInfo?)` (Task 2), `GarbusHitObject.Hitsounds` (Task 3),
  `JudgementResult`.
- Produces:
  - `static class GarbusHitSoundPlayback` with
    `void Play(HitSoundContainer samples, GarbusHitObject hitObject, JudgementResult? result)` —
    no-op unless `result?.IsHit == true`; otherwise plays `hitObject.Hitsounds.Resolve(result.Type)`.
  - `PlaySamples()` overrides on `DrawableGarbusHitObject<T>` and `DrawableSliderChild` delegating to it.

- [ ] **Step 1: Write the failing integration test**

In `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`, add a test that a landed key-press hit plays
exactly one family member on that object's drawable. Add this test method inside the class (mirror the
existing key-press-hit test's setup for acquiring a `DrawableHitObject` and pressing its key — reuse
the scene's `playfield`, `manualClock`, and `input` fields):

```csharp
        [Test]
        public void HittingAnObjectPlaysExactlyOneFamilyMember()
        {
            Garbus.Game.Gameplay.Objects.Drawables.DrawableHitObject drawable = null!;

            AddUntilStep("wait for a cardinal note drawable", () =>
            {
                drawable = playfield.AllHitObjects
                                    .FirstOrDefault(d => d.HitObject is CardinalNote);
                return drawable != null;
            });

            // Seek into the note's hit window and press its key (see the existing key-press hit test
            // for the exact seek/press mechanics used in this scene).
            AddStep("seek to note and press its key", () =>
            {
                var note = (CardinalNote)drawable.HitObject;
                manualClock.CurrentTime = note.StartTime;
                // press the note's bound key via `input` exactly as the existing hit test does
            });

            AddUntilStep("note is hit", () => drawable.IsHit);
            AddAssert("exactly one member played", () =>
                ((Garbus.Game.Objects.Drawables.DrawableGarbusHitObject<CardinalNote>)drawable).SamplesPlayCount,
                () => Is.EqualTo(1));
        }
```

To make `SamplesPlayCount` observable, expose the container's count on the Garbus drawable base in the
implementation step (the vendored `Samples` container is `protected`). If mirroring the existing hit
test's press mechanics is non-trivial, instead assert against the already-hit objects the scene's
`playThrough` helper drives — the key invariant is `PlayCount == 1` per hit object, never the length of
`Samples`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~HittingAnObjectPlaysExactlyOneFamilyMember`
Expected: FAIL to compile ("SamplesPlayCount not found").

- [ ] **Step 3a: Create the shared play helper**

Create `Garbus.Game/Objects/Drawables/GarbusHitSoundPlayback.cs`:

```csharp
// Shared judgement-based hitsound playback for Garbus drawables. Both DrawableGarbusHitObject<T> and
// DrawableSliderChild (which derives from the vendored DrawableHitObject directly) route PlaySamples
// through here so the resolution logic lives in one place.

using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Judgements;

namespace Garbus.Game.Objects.Drawables
{
    internal static class GarbusHitSoundPlayback
    {
        public static void Play(HitSoundContainer? samples, GarbusHitObject hitObject, JudgementResult? result)
        {
            // Playback is gated to hits; misses (and unjudged states) stay silent.
            if (samples == null || result?.IsHit != true)
                return;

            samples.Play(hitObject.Hitsounds.Resolve(result.Type));
        }
    }
}
```

- [ ] **Step 3b: Override `PlaySamples` on `DrawableGarbusHitObject<T>`**

Replace `Garbus.Game/Objects/Drawables/DrawableGarbusHitObject.cs` with:

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableBacHitObject.cs).
// DrawableBacHitObject → DrawableGarbusHitObject.

using Garbus.Game.Gameplay.Objects.Drawables;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableGarbusHitObject<T> : DrawableHitObject<GarbusHitObject>
    where T : GarbusHitObject
{
    public new T HitObject => (T)base.HitObject;

    public DrawableGarbusHitObject(T hitObject)
        : base(hitObject)
    {
    }

    /// <summary>Number of family members this object has played. Test seam.</summary>
    public int SamplesPlayCount => Samples?.PlayCount ?? 0;

    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);
}
```

(`Samples` and `Result` are the vendored `protected`/`public` members on `DrawableHitObject`; `HitObject`
here is `T : GarbusHitObject`.)

- [ ] **Step 3c: Override `PlaySamples` on `DrawableSliderChild`**

In `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs`, add the override to the class body (its
`HitObject` is `SliderChild`, a `GarbusHitObject`):

```csharp
    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);
```

No new usings are needed — `GarbusHitSoundPlayback` is in the same `Garbus.Game.Objects.Drawables`
namespace as this file.

- [ ] **Step 4: Run the test (and the full suite)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~HittingAnObjectPlaysExactlyOneFamilyMember`
Expected: PASS.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all prior tests still green; no object plays its whole sample list).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Objects/Drawables/GarbusHitSoundPlayback.cs Garbus.Game/Objects/Drawables/DrawableGarbusHitObject.cs Garbus.Game/Objects/Drawables/DrawableSliderChild.cs Garbus.Game.Tests/Visual/TestSceneGameplay.cs
git commit -m "feat: play the judged hitsound member on hit"
```

---

## Self-Review Notes

- **Spec coverage:** `HitsoundFamily` + `Resolve` (Task 1); per-type families incl. Cardinal/Shoulder
  hold split (Task 3); `HitSoundContainer.Play(info)` (Task 2); `PlaySamples` override on both drawable
  bases incl. the `DrawableSliderChild` exception + shared helper (Task 4); misses silent (gated on
  `IsHit` in the helper and on `ArmedState.Hit` upstream — no code needed); preload via seeded
  `Samples` (Task 3). All spec sections map to a task.
- **Fallback direction:** implemented as better-first-then-worse in `Resolve` (Task 1), matching the
  "toward better only, then fall down" decision.
- **Vendored files:** `HitObject.cs` / `DrawableHitObject.cs` are not edited; overrides live in Garbus
  subclasses. `HitSoundContainer.cs` is a Garbus original.
- **Verify-before-write reminders embedded:** the exact `ApplyDefaultsToSelf` base signature (no
  parameter) and `CardinalNote`'s `required` members must be confirmed against the source files during
  Task 3.
