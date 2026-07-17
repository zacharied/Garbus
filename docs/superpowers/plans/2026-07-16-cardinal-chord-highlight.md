# Cardinal Chord Highlight Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When two or more cardinal-directed notes share an exact start time, colour them yellow (editor + gameplay) and, in gameplay only, join them with a thin semi-transparent yellow polygon connector.

**Architecture:** A pure `ChordIndex` groups cardinal-directed notes by exact `StartTime` (size ≥ 2). A resolvable `ChordHighlighter` holds the current index (rebuilt once in gameplay, on every edit in the editor). Cardinal note drawables tint themselves from it; a Ring-level `ChordConnectorOverlay` (drawn below all hit objects) reads the groups and draws one angle-sorted `n`-gon per group at the group's shared, co-radial distance, kept alive until every member has despawned.

**Tech Stack:** C# 12, osu-framework (DI via `[Cached]`/`[Resolved]`, `Drawable`/`CompositeDrawable`, `SmoothPath`), NUnit visual test scenes run headless.

## Global Constraints

- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- New files here are Garbus-original (not vendored) — no ppy MIT header needed; a one-line summary comment is enough. Do **not** add historical/compat notes (experimental project).
- Terminology: "chart" not "beatmap"; `Garbus*` prefixes.
- No serialized chart fields, no scoring/difficulty impact — chord state is derived at runtime only.
- Do not increment any version numbers.
- Build: `dotnet build Garbus.Desktop.slnf`. Test: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`. Filter a single fixture with `--filter "FullyQualifiedName~<Name>"`.

## File Structure

**Create:**
- `Garbus.Game/Objects/ChordColours.cs` — shared colour constants (note highlight, connector).
- `Garbus.Game/Objects/ChordIndex.cs` — pure grouping of cardinal-directed notes by exact StartTime.
- `Garbus.Game/Objects/ChordHighlighter.cs` — resolvable holder of the current `ChordIndex`; `Rebuild(...)`.
- `Garbus.Game/UI/ChordConnectorOverlay.cs` — Ring-level gameplay overlay drawing the connectors.
- `Garbus.Game.Tests/ChordIndexTest.cs` — pure grouping tests (headless, no scene).
- `Garbus.Game.Tests/Visual/HitObjects/TestSceneChordHighlight.cs` — gameplay coloring + connector.
- `Garbus.Game.Tests/Editor/TestSceneChordHighlightEditor.cs` — editor coloring, no connector.

**Modify:**
- `Garbus.Game/UI/GarbusPlayfield.cs` — cache a `ChordHighlighter`; rebuild it in `SetHitObjects`.
- `Garbus.Game/UI/Ring.cs` — add `ChordConnectorOverlay` below the hit objects; expose alive hit objects.
- `Garbus.Game/Objects/Drawables/DrawableCardinalNote.cs` — tint in `PrepareForUse`.
- `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNote.cs` — tint in `PrepareForUse`.
- `Garbus.Game/Edit/Drawables/EditorDrawableCardinalNote.cs` — tint each frame in `Update`.
- `Garbus.Game/Edit/Drawables/EditorDrawableCardinalHoldNote.cs` — tint each frame in `Update`.
- `Garbus.Game/Edit/GarbusHitObjectComposer.cs` — cache a `ChordHighlighter`; rebuild on chart events.

---

### Task 1: `ChordIndex` + `ChordColours` (pure grouping)

**Files:**
- Create: `Garbus.Game/Objects/ChordColours.cs`
- Create: `Garbus.Game/Objects/ChordIndex.cs`
- Test: `Garbus.Game.Tests/ChordIndexTest.cs`

**Interfaces:**
- Consumes: `Garbus.Game.Gameplay.Objects.HitObject`, `Garbus.Game.Objects.{CardinalNote, CardinalHoldNote, IHasAngle}`.
- Produces:
  - `static class ChordColours { static readonly Colour4 Highlight; static readonly Colour4 Connector; }`
  - `sealed class ChordIndex`:
    - `ChordIndex(IEnumerable<HitObject> hitObjects)`
    - `bool IsInChord(HitObject hitObject)`
    - `IReadOnlyList<ChordIndex.ChordGroup> Groups` (ordered by `StartTime`)
    - `sealed class ChordGroup { double StartTime; IReadOnlyList<ChordMember> Members; }` (members ordered by `AngleDeg`)
    - `readonly record struct ChordMember(HitObject Object, int AngleDeg)`

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/ChordIndexTest.cs`:

```csharp
using System.Linq;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ChordIndexTest
    {
        private static CardinalNote cardinal(double startTime, int angle) =>
            new CardinalNote { AngleDeg = angle, StartTime = startTime };

        private static CardinalHoldNote hold(double startTime, int angle, double duration = 500) =>
            new CardinalHoldNote { AngleDeg = angle, StartTime = startTime, Duration = duration };

        [Test]
        public void TwoCardinalsSameTimeAreAChord()
        {
            var a = cardinal(1000, 90);
            var b = cardinal(1000, 270);
            var index = new ChordIndex(new[] { a, b });

            Assert.That(index.IsInChord(a), Is.True);
            Assert.That(index.IsInChord(b), Is.True);
            Assert.That(index.Groups, Has.Count.EqualTo(1));
            Assert.That(index.Groups[0].Members.Select(m => m.Object), Is.EquivalentTo(new[] { a, b }));
        }

        [Test]
        public void CardinalAndHoldSameTimeGroupTogether()
        {
            var note = cardinal(1000, 0);
            var held = hold(1000, 180);
            var index = new ChordIndex(new HitObject[] { note, held });

            Assert.That(index.IsInChord(note), Is.True);
            Assert.That(index.IsInChord(held), Is.True);
            Assert.That(index.Groups, Has.Count.EqualTo(1));
        }

        [Test]
        public void LoneCardinalIsNotAChord()
        {
            var a = cardinal(1000, 90);
            var b = cardinal(2000, 90);
            var index = new ChordIndex(new[] { a, b });

            Assert.That(index.IsInChord(a), Is.False);
            Assert.That(index.IsInChord(b), Is.False);
            Assert.That(index.Groups, Is.Empty);
        }

        [Test]
        public void ArbitraryNMembersFormOneGroupSortedByAngle()
        {
            var members = new[] { cardinal(500, 200), cardinal(500, 10), cardinal(500, 300),
                                  cardinal(500, 90), cardinal(500, 45), cardinal(500, 250),
                                  cardinal(500, 150) };
            var index = new ChordIndex(members);

            Assert.That(index.Groups, Has.Count.EqualTo(1));
            var group = index.Groups[0];
            Assert.That(group.Members, Has.Count.EqualTo(7));
            Assert.That(group.Members.Select(m => m.AngleDeg), Is.Ordered);
        }

        [Test]
        public void ShoulderNoteAtSameTimeIsExcluded()
        {
            var cardinalA = cardinal(1000, 90);
            var cardinalB = cardinal(1000, 270);
            var shoulder = new ShoulderNote { Side = Core.HorizontalDirection.Left, StartTime = 1000 };
            var index = new ChordIndex(new HitObject[] { cardinalA, cardinalB, shoulder });

            Assert.That(index.IsInChord(shoulder), Is.False);
            Assert.That(index.Groups[0].Members.Select(m => m.Object), Does.Not.Contain(shoulder));
        }
    }
}
```

Note: `HitObject` is `Garbus.Game.Gameplay.Objects.HitObject`; add `using Garbus.Game.Gameplay.Objects;` and `using Garbus.Game.Core;` as the compiler requires.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~ChordIndexTest"`
Expected: FAIL to compile — `ChordIndex` / `ChordColours` do not exist.

- [ ] **Step 3: Write `ChordColours`**

Create `Garbus.Game/Objects/ChordColours.cs`:

```csharp
// Shared colours for the same-start-time cardinal "chord" highlight (notes + gameplay connector).

using osu.Framework.Graphics;

namespace Garbus.Game.Objects;

public static class ChordColours
{
    /// <summary>The tint applied to every cardinal note that shares its start time with another.</summary>
    public static readonly Colour4 Highlight = Colour4.Yellow;

    /// <summary>The thin, semi-transparent yellow of the gameplay connector.</summary>
    public static readonly Colour4 Connector = new Colour4(1f, 1f, 0f, 0.35f);

    /// <summary>Connector line half-thickness in local px (2px total).</summary>
    public const float ConnectorPathRadius = 1f;
}
```

- [ ] **Step 4: Write `ChordIndex`**

Create `Garbus.Game/Objects/ChordIndex.cs`:

```csharp
// Groups cardinal-directed notes (CardinalNote + CardinalHoldNote) that share an exact StartTime into
// "chords" of size >= 2. Pure and immutable: built from a hit-object snapshot, no drawing/framework types.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects;

public sealed class ChordIndex
{
    public readonly record struct ChordMember(HitObject Object, int AngleDeg);

    public sealed class ChordGroup
    {
        public double StartTime { get; }
        public IReadOnlyList<ChordMember> Members { get; }

        public ChordGroup(double startTime, IReadOnlyList<ChordMember> members)
        {
            StartTime = startTime;
            Members = members;
        }
    }

    private readonly IReadOnlyList<ChordGroup> groups;
    private readonly HashSet<HitObject> members;

    public ChordIndex(IEnumerable<HitObject> hitObjects)
    {
        var buckets = new Dictionary<double, List<ChordMember>>();

        foreach (var h in hitObjects)
        {
            if (!isCardinalDirected(h, out int angle))
                continue;

            if (!buckets.TryGetValue(h.StartTime, out var list))
                buckets[h.StartTime] = list = new List<ChordMember>();

            list.Add(new ChordMember(h, angle));
        }

        var kept = buckets
                   .Where(kvp => kvp.Value.Count >= 2)
                   .OrderBy(kvp => kvp.Key)
                   .Select(kvp => new ChordGroup(
                       kvp.Key,
                       kvp.Value.OrderBy(m => m.AngleDeg).ToArray()))
                   .ToArray();

        groups = kept;
        members = new HashSet<HitObject>(kept.SelectMany(g => g.Members).Select(m => m.Object));
    }

    public IReadOnlyList<ChordGroup> Groups => groups;

    public bool IsInChord(HitObject hitObject) => members.Contains(hitObject);

    // Cardinal-directed = CardinalNote or CardinalHoldNote specifically. ShoulderNote also carries a
    // cardinal Direction but is deliberately excluded, so match the concrete types, not IHasCardinalDirection.
    private static bool isCardinalDirected(HitObject h, out int angleDeg)
    {
        switch (h)
        {
            case CardinalNote c:
                angleDeg = c.AngleDeg;
                return true;
            case CardinalHoldNote hold:
                angleDeg = hold.AngleDeg;
                return true;
            default:
                angleDeg = 0;
                return false;
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~ChordIndexTest"`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Objects/ChordColours.cs Garbus.Game/Objects/ChordIndex.cs Garbus.Game.Tests/ChordIndexTest.cs
git commit -m "feat: add ChordIndex grouping for coincident cardinal notes"
```

---

### Task 2: `ChordHighlighter` holder

**Files:**
- Create: `Garbus.Game/Objects/ChordHighlighter.cs`
- Test: `Garbus.Game.Tests/ChordIndexTest.cs` (add a fixture below; or a new `ChordHighlighterTest.cs`)

**Interfaces:**
- Consumes: `ChordIndex`, `HitObject`.
- Produces:
  - `sealed class ChordHighlighter`:
    - `void Rebuild(IEnumerable<HitObject> hitObjects)`
    - `bool IsInChord(HitObject hitObject)`
    - `IReadOnlyList<ChordIndex.ChordGroup> Groups`

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/ChordHighlighterTest.cs`:

```csharp
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ChordHighlighterTest
    {
        private static CardinalNote cardinal(double startTime, int angle) =>
            new CardinalNote { AngleDeg = angle, StartTime = startTime };

        [Test]
        public void RebuildReflectsCurrentObjects()
        {
            var highlighter = new ChordHighlighter();
            var a = cardinal(1000, 90);
            var b = cardinal(1000, 270);

            Assert.That(highlighter.IsInChord(a), Is.False, "empty before rebuild");

            highlighter.Rebuild(new[] { a, b });
            Assert.That(highlighter.IsInChord(a), Is.True);
            Assert.That(highlighter.Groups, Has.Count.EqualTo(1));

            // Move b off a's time: the chord dissolves after the next rebuild.
            b.StartTime = 2000;
            highlighter.Rebuild(new[] { a, b });
            Assert.That(highlighter.IsInChord(a), Is.False);
            Assert.That(highlighter.Groups, Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~ChordHighlighterTest"`
Expected: FAIL to compile — `ChordHighlighter` does not exist.

- [ ] **Step 3: Write `ChordHighlighter`**

Create `Garbus.Game/Objects/ChordHighlighter.cs`:

```csharp
// A resolvable holder for the current ChordIndex. Gameplay rebuilds it once from the static chart; the
// editor rebuilds it on every chart mutation. Cardinal note drawables and the connector overlay read it.

using System;
using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects;

namespace Garbus.Game.Objects;

public sealed class ChordHighlighter
{
    private ChordIndex index = new ChordIndex(Array.Empty<HitObject>());

    public void Rebuild(IEnumerable<HitObject> hitObjects) => index = new ChordIndex(hitObjects);

    public bool IsInChord(HitObject hitObject) => index.IsInChord(hitObject);

    public IReadOnlyList<ChordIndex.ChordGroup> Groups => index.Groups;
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~ChordHighlighterTest"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Objects/ChordHighlighter.cs Garbus.Game.Tests/ChordHighlighterTest.cs
git commit -m "feat: add ChordHighlighter holder for live chord state"
```

---

### Task 3: Gameplay note coloring

**Files:**
- Modify: `Garbus.Game/UI/GarbusPlayfield.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableCardinalNote.cs`
- Modify: `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNote.cs`
- Test: `Garbus.Game.Tests/Visual/HitObjects/TestSceneChordHighlight.cs`

**Interfaces:**
- Consumes: `ChordHighlighter` (Task 2), `ChordColours` (Task 1).
- Produces: `GarbusPlayfield` caches a `ChordHighlighter` and rebuilds it in `SetHitObjects(...)`. Cardinal gameplay drawables resolve it and set `this.Colour` in `PrepareForUse`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/HitObjects/TestSceneChordHighlight.cs`:

```csharp
using System.Linq;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Testing.Input;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    [TestFixture]
    public partial class TestSceneChordHighlight : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private GarbusPlayfield playfield = null!;

        private void buildScene(params GarbusHitObject[] hitObjects)
        {
            manualClock = new ManualClock { Rate = 1 };

            foreach (var h in hitObjects)
                h.ApplyDefaults();

            Child = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = new GarbusInputManager
                    {
                        Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                    },
                },
            };

            foreach (var h in hitObjects)
                playfield.Add(PlayScreen.CreateDrawableRepresentation(h));

            playfield.SetHitObjects(hitObjects);
        }

        private DrawableCardinalNote cardinalAt(int angle) =>
            playfield.AllHitObjects.OfType<DrawableCardinalNote>().Single(d => d.HitObject.AngleDeg == angle);

        [Test]
        public void CoincidentPairIsYellow()
        {
            AddStep("two cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));

            AddAssert("north yellow", () => cardinalAt(90).Colour, () => Is.EqualTo((ColourInfo)ChordColours.Highlight));
            AddAssert("south yellow", () => cardinalAt(270).Colour, () => Is.EqualTo((ColourInfo)ChordColours.Highlight));
        }

        [Test]
        public void LoneCardinalIsWhite()
        {
            AddStep("single cardinal", () => buildScene(new CardinalNote { AngleDeg = 90, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);
            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().Any(d => d.IsAlive));

            AddAssert("white", () => cardinalAt(90).Colour, () => Is.EqualTo((ColourInfo)Colour4.White));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChordHighlight"`
Expected: FAIL — the notes are white (no coloring yet) so `CoincidentPairIsYellow` fails its assert. (`LoneCardinalIsWhite` may already pass.)

- [ ] **Step 3: Cache `ChordHighlighter` in `GarbusPlayfield` and rebuild in `SetHitObjects`**

In `Garbus.Game/UI/GarbusPlayfield.cs`, add the `using`:

```csharp
using System.Linq;
using Garbus.Game.Objects;
```

Add a cached field (place it beside the other `[Cached]` member, `analogInputManager`):

```csharp
[Cached]
private ChordHighlighter chordHighlighter { get; set; } = new ChordHighlighter();
```

Replace `SetHitObjects` so it also rebuilds the chord index:

```csharp
/// <summary>
/// Hand the full set of chart hit objects to the warning-indicator display (approaching heads/slams)
/// and rebuild the chord highlight index. Call once after adding drawables.
/// </summary>
public void SetHitObjects(IEnumerable<GarbusHitObject> hitObjects)
{
    var list = hitObjects.ToList();
    warningIndicators.SetHitObjects(list);
    chordHighlighter.Rebuild(list);
}
```

- [ ] **Step 4: Tint `DrawableCardinalNote` in `PrepareForUse`**

In `Garbus.Game/Objects/Drawables/DrawableCardinalNote.cs`, add usings:

```csharp
using osu.Framework.Allocation;
using Garbus.Game.Objects; // ChordColours, ChordHighlighter
```

Add a resolved field inside the class:

```csharp
[Resolved]
private ChordHighlighter chords { get; set; } = null!;
```

Replace `PrepareForUse`:

```csharp
protected override void PrepareForUse()
{
    // Pooled/reused drawables must set an explicit colour every time (yellow if this note shares its
    // start time with another cardinal note, else reset to white).
    Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : osuTK.Graphics.Color4.White;

    // Apply note spawn effect
    sprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
}
```

(`osuTK.Graphics.Color4` is already imported in this file as `Color4`; you may write `Color4.White`.)

- [ ] **Step 5: Tint `DrawableCardinalHoldNote` in `PrepareForUse`**

In `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNote.cs`, add usings:

```csharp
using Garbus.Game.Objects; // ChordColours, ChordHighlighter
```

Add a resolved field:

```csharp
[Resolved]
private ChordHighlighter chords { get; set; } = null!;
```

Update `PrepareForUse` (tint the whole drawable so head **and** body colour; the body's held/dropped
and miss transforms still modulate on top):

```csharp
protected override void PrepareForUse()
{
    base.PrepareForUse();

    Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Colour4.White;

    headSprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
    body.FadeInFromZero(100, Easing.In);
}
```

(`Color4`/`Colour4` are already imported here as `osuTK.Graphics.Color4` and `osu.Framework.Graphics.Colour4`. `ChordColours.Highlight` is a `Colour4`; assigning it to `Colour` is fine. Use `Colour4.White` for the reset.)

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChordHighlight"`
Expected: PASS (2 tests).

- [ ] **Step 7: Build to confirm nothing else broke**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/UI/GarbusPlayfield.cs Garbus.Game/Objects/Drawables/DrawableCardinalNote.cs Garbus.Game/Objects/Drawables/DrawableCardinalHoldNote.cs Garbus.Game.Tests/Visual/HitObjects/TestSceneChordHighlight.cs
git commit -m "feat: colour coincident cardinal notes yellow in gameplay"
```

---

### Task 4: `ChordConnectorOverlay` (gameplay connector)

**Files:**
- Create: `Garbus.Game/UI/ChordConnectorOverlay.cs`
- Modify: `Garbus.Game/UI/Ring.cs`
- Test: `Garbus.Game.Tests/Visual/HitObjects/TestSceneChordHighlight.cs` (add cases)

**Interfaces:**
- Consumes: `ChordHighlighter` (resolved from `GarbusPlayfield`), `Ring` (resolved parent), `GarbusScrollingHitObjectContainer.ProgressAtTime`, `ChordColours`.
- Produces: `ChordConnectorOverlay` (a `CompositeDrawable`) added to `Ring` **below** the hit objects; `Ring` exposes `IEnumerable<DrawableHitObject> AliveHitObjects` and `GarbusScrollingHitObjectContainer ScrollingContainer`.

- [ ] **Step 1: Write the failing test**

Append these cases to `TestSceneChordHighlight` (inside the class):

```csharp
        private ChordConnectorOverlay overlay =>
            playfield.ChildrenOfType<ChordConnectorOverlay>().Single();

        private System.Collections.Generic.IEnumerable<osu.Framework.Graphics.Lines.SmoothPath> visiblePaths() =>
            overlay.ChildrenOfType<osu.Framework.Graphics.Lines.SmoothPath>().Where(p => p.IsPresent);

        [Test]
        public void ConnectorAppearsForAlivePairAndClearsAfterDespawn()
        {
            AddStep("two cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 90, StartTime = 2000 },
                new CardinalNote { AngleDeg = 270, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);

            AddAssert("no connector before spawn", () => !visiblePaths().Any());

            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("both alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));
            AddUntilStep("connector visible", () => visiblePaths().Count() == 1);

            // Walk well past the notes so they auto-miss and despawn; the connector must clear.
            AddUntilStep("play past despawn", () =>
            {
                manualClock.CurrentTime = System.Math.Min(6000, manualClock.CurrentTime + 200);
                return manualClock.CurrentTime >= 6000
                       && playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => !d.IsAlive);
            });
            AddUntilStep("connector cleared", () => !visiblePaths().Any());
        }

        [Test]
        public void ConnectorHasVertexPerMemberForThreeNoteChord()
        {
            AddStep("three cardinals at 2000ms", () => buildScene(
                new CardinalNote { AngleDeg = 0, StartTime = 2000 },
                new CardinalNote { AngleDeg = 120, StartTime = 2000 },
                new CardinalNote { AngleDeg = 240, StartTime = 2000 }));
            AddUntilStep("loaded", () => playfield.IsLoaded);
            AddStep("seek to make alive", () => manualClock.CurrentTime = 2000);
            AddUntilStep("all alive", () => playfield.AllHitObjects.OfType<DrawableCardinalNote>().All(d => d.IsAlive));

            // Closed triangle: 3 members + repeat of the first vertex = 4 points.
            AddUntilStep("closed triangle", () => visiblePaths().Any()
                && visiblePaths().Single().Vertices.Count == 4);
        }
```

Also ensure the file has `using osu.Framework.Testing;` (for `ChildrenOfType`).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChordHighlight"`
Expected: FAIL to compile — `ChordConnectorOverlay` does not exist.

- [ ] **Step 3: Expose alive objects + scrolling container on `Ring`**

In `Garbus.Game/UI/Ring.cs`, add `using System.Linq;` (if absent) and add these members to the class:

```csharp
/// <summary>The ring's own scrolling container (paths live here); also the shared radius source
/// (ProgressAtTime) for the chord connector — all containers share the same size, so the radius matches.</summary>
public GarbusScrollingHitObjectContainer ScrollingContainer => (GarbusScrollingHitObjectContainer)HitObjectContainer;

/// <summary>Every hit object drawable in the ring and its lanes (used by the chord connector's
/// presence check).</summary>
public IEnumerable<DrawableHitObject> AliveHitObjects => AllHitObjects.Where(h => h.IsAlive);
```

Add `using System.Collections.Generic;` if not present (Ring already imports framework graphics; `DrawableHitObject` is in `Garbus.Game.Gameplay.Objects.Drawables`, already imported).

- [ ] **Step 4: Add the connector to `Ring` below the hit objects**

In `Garbus.Game/UI/Ring.cs`, change the `AddRangeInternal` block in the constructor so a `ChordConnectorOverlay` sits **after** the radial lines and **before** `HitObjectContainer`:

```csharp
// Back-to-front: radial spokes, chord connectors (under all notes), cross-lane paths, the lanes, the ring.
AddRangeInternal([
    new PlayfieldRadialLines(),
    new ChordConnectorOverlay(),
    HitObjectContainer,
    laneContainer,
    new Arc(0, 2 * MathF.PI)
    {
        Resolution = 128,
        Colour = Colour4.White,
    },
]);
```

- [ ] **Step 5: Write `ChordConnectorOverlay`**

Create `Garbus.Game/UI/ChordConnectorOverlay.cs`:

```csharp
// Gameplay-only overlay: draws one thin, semi-transparent yellow polygon per same-start-time cardinal
// chord, inscribed at the chord's shared (co-radial) distance from centre. Lives in Ring below the hit
// objects. Geometry comes from ChordHighlighter + ProgressAtTime (never from live note positions), so it
// keeps its full shape until the last member of the chord has despawned.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;
using Garbus.Game.Utils;
using osuTK;

namespace Garbus.Game.UI;

public partial class ChordConnectorOverlay : CompositeDrawable
{
    [Resolved]
    private ChordHighlighter chords { get; set; } = null!;

    [Resolved]
    private Ring ring { get; set; } = null!;

    // One reusable path per chord, keyed by the chord's shared start time. Hidden when the chord is not
    // currently present, rather than removed, to avoid per-frame allocation churn.
    private readonly Dictionary<double, SmoothPath> pathsByStartTime = new Dictionary<double, SmoothPath>();

    public ChordConnectorOverlay()
    {
        RelativeSizeAxes = Axes.Both;
    }

    protected override void Update()
    {
        base.Update();

        var alive = new HashSet<HitObject>(ring.AliveHitObjects.Select(d => d.HitObject));
        var present = new HashSet<double>();

        foreach (var group in chords.Groups)
        {
            // Present while ANY member still has an alive drawable (covers the whole hit/miss fade-out).
            if (!group.Members.Any(m => alive.Contains(m.Object)))
                continue;

            present.Add(group.StartTime);

            float radius = ring.ScrollingContainer.ProgressAtTime(group.StartTime);

            var vertices = group.Members.Select(m => polar(m.AngleDeg, radius)).ToList();
            if (vertices.Count >= 3)
                vertices.Add(vertices[0]); // close the loop

            if (!pathsByStartTime.TryGetValue(group.StartTime, out var path))
            {
                path = new SmoothPath
                {
                    Anchor = Anchor.Centre,
                    PathRadius = ChordColours.ConnectorPathRadius,
                    Colour = ChordColours.Connector,
                };
                pathsByStartTime[group.StartTime] = path;
                AddInternal(path);
            }

            path.Vertices = vertices;
            path.Position = -path.PositionInBoundingBox(Vector2.Zero);
            path.Show();
        }

        foreach (var kvp in pathsByStartTime)
        {
            if (!present.Contains(kvp.Key))
                kvp.Value.Hide();
        }
    }

    // Matches GarbusScrollingHitObjectContainer.PositionAtTime: +x right, -y up (screen y grows downward).
    private static Vector2 polar(int angleDeg, float radius)
    {
        float radians = MathUtils.DegToRad(angleDeg);
        return new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
    }
}
```

Add `using System;` for `MathF` (or use `osuTK`'s). Confirm `MathUtils.DegToRad` is the same helper used in `GarbusScrollingHitObjectContainer` (namespace `Garbus.Game.Utils`).

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChordHighlight"`
Expected: PASS (4 tests total — the 2 from Task 3 plus the 2 new connector cases).

- [ ] **Step 7: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/UI/ChordConnectorOverlay.cs Garbus.Game/UI/Ring.cs Garbus.Game.Tests/Visual/HitObjects/TestSceneChordHighlight.cs
git commit -m "feat: draw yellow connector under coincident cardinal chords"
```

---

### Task 5: Editor note coloring (no connector)

**Files:**
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs`
- Modify: `Garbus.Game/Edit/Drawables/EditorDrawableCardinalNote.cs`
- Modify: `Garbus.Game/Edit/Drawables/EditorDrawableCardinalHoldNote.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneChordHighlightEditor.cs`

**Interfaces:**
- Consumes: `ChordHighlighter`, `ChordColours`, `EditorChart.HitObject{Added,Removed,Updated}`, `EditorChart.HitObjects`.
- Produces: `GarbusHitObjectComposer` caches a `ChordHighlighter`, rebuilt on load and on every chart event. Editor cardinal drawables resolve it and set `this.Colour` each frame in `Update` (so the ±360° ghost twin inherits it).

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneChordHighlightEditor.cs`. `TestSceneComposePlacement`'s harness
is a **private nested class**, so this file embeds its own copy of the same DI harness (a private nested
class) rather than referencing it.

```csharp
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneChordHighlightEditor : GarbusTestScene
    {
        private ChordEditorHarness harness = null!;
        private EditorChart editorChart = null!;

        private GarbusEditorPlayfield playfield => harness.Composer.Playfield;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            editorChart = new EditorChart(chart);
            Child = harness = new ChordEditorHarness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        private void waitForComposer() => AddUntilStep("wait for composer", () => harness.Composer?.IsLoaded == true);

        private EditorDrawableCardinalNote drawableFor(CardinalNote note) =>
            playfield.ChildrenOfType<EditorDrawableCardinalNote>().Single(d => d.HitObject == note);

        [Test]
        public void CoincidentPairColouredYellowLoneStaysWhite()
        {
            var a = new CardinalNote { AngleDeg = 90, StartTime = 1000 };
            var b = new CardinalNote { AngleDeg = 270, StartTime = 1000 };
            var lone = new CardinalNote { AngleDeg = 0, StartTime = 2000 };

            waitForComposer();
            AddStep("add three notes (two coincident)", () => editorChart.AddRange(new[] { a, b, lone }));

            AddUntilStep("a yellow", () => drawableFor(a).Colour.Equals((ColourInfo)ChordColours.Highlight));
            AddUntilStep("b yellow", () => drawableFor(b).Colour.Equals((ColourInfo)ChordColours.Highlight));
            AddUntilStep("lone white", () => drawableFor(lone).Colour.Equals((ColourInfo)Colour4.White));
        }

        [Test]
        public void MovingNoteOffChordReturnsItToWhite()
        {
            var a = new CardinalNote { AngleDeg = 90, StartTime = 1000 };
            var b = new CardinalNote { AngleDeg = 270, StartTime = 1000 };

            waitForComposer();
            AddStep("add coincident pair", () => editorChart.AddRange(new[] { a, b }));
            AddUntilStep("both yellow", () =>
                drawableFor(a).Colour.Equals((ColourInfo)ChordColours.Highlight) &&
                drawableFor(b).Colour.Equals((ColourInfo)ChordColours.Highlight));

            AddStep("move b to a new time", () =>
            {
                b.StartTime = 3000;
                editorChart.Update(b);
            });

            AddUntilStep("a back to white", () => drawableFor(a).Colour.Equals((ColourInfo)Colour4.White));
            AddUntilStep("b white", () => drawableFor(b).Colour.Equals((ColourInfo)Colour4.White));
        }

        // Self-contained copy of TestSceneComposePlacement's DI harness (that one is private/nested).
        // Caches the deps the composer tree resolves, wires the composer subtree to the EditorClock, and
        // hosts the real GarbusHitObjectComposer.
        private partial class ChordEditorHarness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;

            public GarbusHitObjectComposer Composer { get; private set; } = null!;

            public ChordEditorHarness(EditorChart editorChart)
            {
                this.editorChart = editorChart;
            }

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
                Child = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = dependencies.Get<EditorClock>(),
                        Child = Composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                    },
                };
                AddInternal(dependencies.Get<EditorClock>());
            }
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChordHighlightEditor"`
Expected: FAIL — editor notes stay white (no coloring wired yet).

- [ ] **Step 3: Cache + rebuild `ChordHighlighter` in the composer**

In `Garbus.Game/Edit/GarbusHitObjectComposer.cs`, add usings:

```csharp
using Garbus.Game.Objects; // ChordHighlighter
```

Add a cached field (next to the other `[Cached]` member `AutoSeekOnPlacement`):

```csharp
[Cached]
private readonly ChordHighlighter chordHighlighter = new ChordHighlighter();
```

Add lifecycle wiring. `GarbusHitObjectComposer` currently has no `LoadComplete`/`Dispose` override, so add them:

```csharp
protected override void LoadComplete()
{
    base.LoadComplete();

    EditorChart.HitObjectAdded += onChartChanged;
    EditorChart.HitObjectRemoved += onChartChanged;
    EditorChart.HitObjectUpdated += onChartChanged;

    rebuildChords();
}

private void onChartChanged(GarbusHitObject _) => rebuildChords();

private void rebuildChords() => chordHighlighter.Rebuild(EditorChart.HitObjects);

protected override void Dispose(bool isDisposing)
{
    if (EditorChart != null)
    {
        EditorChart.HitObjectAdded -= onChartChanged;
        EditorChart.HitObjectRemoved -= onChartChanged;
        EditorChart.HitObjectUpdated -= onChartChanged;
    }

    base.Dispose(isDisposing);
}
```

(`GarbusHitObject` is already imported in this file; `EditorChart` is the protected `[Resolved]` member from `HitObjectComposer`.)

- [ ] **Step 4: Tint `EditorDrawableCardinalNote` each frame**

Replace `Garbus.Game/Edit/Drawables/EditorDrawableCardinalNote.cs` body to add resolved highlighter and an `Update` override:

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/EditorDrawableCardinalNote.cs).
// Namespace only change; adds the same-start-time chord tint (Garbus addition).

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

public partial class EditorDrawableCardinalNote : EditorDrawableGarbusHitObject<CardinalNote>
{
    public const float NOTE_SIZE = 36;

    [Resolved]
    private ChordHighlighter chords { get; set; } = null!;

    public EditorDrawableCardinalNote(CardinalNote hitObject)
        : base(hitObject)
    {
        Size = new Vector2(NOTE_SIZE);
    }

    protected override Drawable CreateVisual() => new EditorSpritePiece("square");

    protected override void Update()
    {
        base.Update();

        // Set on the whole drawable so the ±360° ghost twin (an InternalChild) inherits the tint.
        Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Colour4.White;
    }
}
```

- [ ] **Step 5: Tint `EditorDrawableCardinalHoldNote` each frame**

In `Garbus.Game/Edit/Drawables/EditorDrawableCardinalHoldNote.cs`, add usings:

```csharp
using osu.Framework.Allocation;
using Garbus.Game.Objects; // ChordColours, ChordHighlighter
```

Add a resolved field inside the class:

```csharp
[Resolved]
private ChordHighlighter chords { get; set; } = null!;
```

Add an `Update` override (the class currently has none):

```csharp
protected override void Update()
{
    base.Update();

    // Whole-drawable tint covers head + body and the ghost twin.
    Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Color4.White;
}
```

(`Color4` is already imported here from `osuTK.Graphics`; `ChordColours.Highlight` is a `Colour4` and assigns to `Colour` fine.)

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneChordHighlightEditor"`
Expected: PASS (2 tests).

- [ ] **Step 7: Full test + build**

Run: `dotnet build Garbus.Desktop.slnf`
Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: Build succeeded; all tests green (existing + new).

- [ ] **Step 8: Commit**

```bash
git add Garbus.Game/Edit/GarbusHitObjectComposer.cs Garbus.Game/Edit/Drawables/EditorDrawableCardinalNote.cs Garbus.Game/Edit/Drawables/EditorDrawableCardinalHoldNote.cs Garbus.Game.Tests/Editor/TestSceneChordHighlightEditor.cs
git commit -m "feat: colour coincident cardinal notes yellow in the editor"
```

---

## Self-Review

**Spec coverage:**
- Chord membership (CardinalNote + CardinalHoldNote, exact StartTime, size ≥ 2, shoulders excluded) → Task 1 `ChordIndex` + tests.
- Exact-equality match rule → Task 1 (dictionary keyed by `StartTime`).
- Arbitrary `n` (7+) → Task 1 test `ArbitraryNMembersFormOneGroupSortedByAngle`; Task 4 3-member connector test.
- Coloring editor + gameplay, whole note incl. hold body → Tasks 3 (gameplay, `this.Colour`) & 5 (editor, `this.Colour`).
- Pooled reset to white → Task 3 `PrepareForUse` sets white for non-members; `LoneCardinalIsWhite` test.
- Ghost twin inherits tint → Task 5 sets `this.Colour` (twin is InternalChild).
- Connector gameplay-only, below all hit objects → Task 4 Ring ordering (after radial lines, before HitObjectContainer/lanes).
- Connector geometry from ChordIndex + ProgressAtTime, angle-sorted closed `n`-gon → Task 4 overlay; `ConnectorHasVertexPerMemberForThreeNoteChord`.
- Visible while any member alive; clears when all despawned → Task 4 presence check; `ConnectorAppearsForAlivePairAndClearsAfterDespawn`.
- Style: yellow, ~2px, ~0.35 alpha → Task 1 `ChordColours`.
- No serialized fields / no scoring impact → nothing added to chart format or scoring; derived at runtime.

**Placeholder scan:** No TBD/TODO; every code step has complete code and exact commands.

**Type consistency:** `ChordHighlighter.{Rebuild, IsInChord, Groups}`, `ChordIndex.{Groups, IsInChord}`, `ChordIndex.ChordGroup.{StartTime, Members}`, `ChordIndex.ChordMember.{Object, AngleDeg}`, `ChordColours.{Highlight, Connector, ConnectorPathRadius}`, `Ring.{ScrollingContainer, AliveHitObjects}`, `GarbusScrollingHitObjectContainer.ProgressAtTime(double)` — used consistently across Tasks 1–5.

**Note on the "keeps full shape after an early hit" property:** this is structural — the overlay computes vertices for *all* members from `ChordIndex` + `ProgressAtTime` and gates visibility on *any* member being alive, so a member hit and despawned early neither removes its vertex nor hides the connector while others remain. The 3-member vertex-count assert plus the alive→despawn clearing assert pin the observable behaviour; forcing one-of-two co-timed notes to despawn early requires live input and is out of scope for the headless asserts.
