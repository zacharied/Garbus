# ShoulderHoldNote + CardinalHoldNote rename Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the existing `HoldNote` (a held cardinal note) to `CardinalHoldNote`, factor the hold judgement into shared generic bases, then add a parallel `ShoulderHoldNote` drawn with a transparent growing sector trail.

**Architecture:** Two hold hit-object types (`CardinalHoldNote`, `ShoulderHoldNote`) that share three generic bases — `HoldNoteHead<TParent>` (nested judgemental head object), `DrawableHoldNoteHead<THead>` (its drawable), and `DrawableHoldNote<THitObject, THead>` (all hold input/judgement logic). Each concrete hold drawable supplies only visuals. The shoulder hold reuses `ShoulderNoteGeometry` for its two-square-plus-arc head and a framework `CircularProgress` for the transparent sector body.

**Tech Stack:** C# (nullable enabled), osu-framework, System.Text.Json polymorphic DTOs, NUnit headless tests.

## Global Constraints

- Nullability enabled solution-wide; DI/BDL fields use `= null!`.
- Vendored osu.Game files keep the ppy MIT header + an "Adapted for Garbus:" line noting trims.
- Terminology: osu "beatmap" → "chart"; `Bac*` → `Garbus*`.
- No backwards-compatibility shims — the serializer discriminator may be renamed freely (no `.garbus` charts exist).
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- Commit after each task (frequent commits).
- `EditorChart` aliases `Chart.HitObjects` — never build a second list.
- Removed composer drawables must be `Dispose()`d; editor drawables must swallow drawable-side `LifetimeEnd` writes (already handled by the base classes being reused here).

---

### Task 1: Mechanical rename `HoldNote` → `CardinalHoldNote`

Pure rename, no behaviour change. Everything stays concrete (the generic split is Task 2).

**Files (rename the file, then the symbol inside, then all references):**
- Rename: `Garbus.Game/Objects/HoldNote.cs` → `CardinalHoldNote.cs` (type `HoldNote` → `CardinalHoldNote`)
- Rename: `Garbus.Game/Objects/HoldNoteHead.cs` → `CardinalHoldNoteHead.cs` (type + ctor + `HoldNote Parent` field type)
- Rename: `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs` → `DrawableCardinalHoldNote.cs`
- Rename: `Garbus.Game/Objects/Drawables/DrawableHoldNoteHead.cs` → `DrawableCardinalHoldNoteHead.cs`
- Rename: `Garbus.Game/Edit/Drawables/EditorDrawableHoldNote.cs` → `EditorDrawableCardinalHoldNote.cs`
- Rename: `Garbus.Game/Edit/Blueprints/HoldNotePlacementBlueprint.cs` → `CardinalHoldNotePlacementBlueprint.cs`
- Rename: `Garbus.Game/Edit/Blueprints/HoldNoteSelectionBlueprint.cs` → `CardinalHoldNoteSelectionBlueprint.cs`
- Rename: `Garbus.Game/Edit/Blueprints/Components/HoldNoteEndDragPiece.cs` → `HoldEndDragPiece.cs` (type `HoldNoteEndDragPiece` → `HoldEndDragPiece`; **neutral name**, shared by both hold selection blueprints)
- Modify: `Garbus.Game/Edit/Tools/GarbusCompositionTools.cs` (`HoldNoteCompositionTool` → `CardinalHoldNoteCompositionTool`, its `new HoldNotePlacementBlueprint()` → `new CardinalHoldNotePlacementBlueprint()`)
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs:60,75` (`HoldNote`/`EditorDrawableHoldNote`/`HoldNoteCompositionTool` renames)
- Modify: `Garbus.Game/Edit/GarbusBlueprintContainer.cs:32-33` (`HoldNote`/`HoldNoteSelectionBlueprint` renames)
- Modify: `Garbus.Game/UI/Ring.cs:113` (`HoldNote h` → `CardinalHoldNote h`)
- Modify: `Garbus.Game/Screens/PlayScreen.cs:225` (`HoldNote hold => new DrawableHoldNote(hold)` → `CardinalHoldNote hold => new DrawableCardinalHoldNote(hold)`)
- Modify: `Garbus.Game/Charts/Format/ChartFileDto.cs:55,70` (`HoldNoteDto` → `CardinalHoldNoteDto`; discriminator `"hold"` → `"cardinal-hold"`)
- Modify: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs:127,189` (`HoldNote`/`HoldNoteDto` → `CardinalHoldNote`/`CardinalHoldNoteDto`)
- Modify: `Garbus.Game/Charts/GarbusTestChartGenerator.cs:101,107,114` (`new HoldNote()` → `new CardinalHoldNote()`, 3 sites)
- Test files (rename `HoldNote` → `CardinalHoldNote`, `DrawableHoldNote` → `DrawableCardinalHoldNote`): `Garbus.Game.Tests/TestChartFormat.cs:139-142`, `Garbus.Game.Tests/Visual/TestSceneGameplay.cs:170-171`, `Garbus.Game.Tests/Editor/TestChecks.cs:226,230`, `Garbus.Game.Tests/Editor/TestEditorChart.cs:25,35`, `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs:97,106`, `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs:419,427,445`, `Garbus.Game.Tests/Editor/TestSceneEditorPlayfield.cs:128`, `Garbus.Game.Tests/Editor/TestSceneEditorIntegration.cs:73`, `Garbus.Game.Tests/Editor/TestTimingSectionAdjustments.cs:83`

**Interfaces:**
- Consumes: nothing new.
- Produces: `CardinalHoldNote` (was `HoldNote`), `CardinalHoldNoteHead`, `DrawableCardinalHoldNote`, `DrawableCardinalHoldNoteHead`, `HoldEndDragPiece`, discriminator `"cardinal-hold"`.

- [ ] **Step 1: Do the rename**

Rename each file and replace the symbols above. This is a token-level rename — `HoldNote` → `CardinalHoldNote`, `HoldNoteHead` → `CardinalHoldNoteHead`, `DrawableHoldNote` → `DrawableCardinalHoldNote`, `DrawableHoldNoteHead` → `DrawableCardinalHoldNoteHead`, `EditorDrawableHoldNote` → `EditorDrawableCardinalHoldNote`, `HoldNotePlacementBlueprint` → `CardinalHoldNotePlacementBlueprint`, `HoldNoteSelectionBlueprint` → `CardinalHoldNoteSelectionBlueprint`, `HoldNoteCompositionTool` → `CardinalHoldNoteCompositionTool`, `HoldNoteDto` → `CardinalHoldNoteDto`, `HoldNoteEndDragPiece` → `HoldEndDragPiece`, and the string `"hold"` → `"cardinal-hold"` in `ChartFileDto.cs`.

Use a search to confirm none remain (docs/comments outside the touch-list may keep the prose name; only rename code identifiers):

Run: `rg -n "\bHoldNote\b|\bHoldNoteHead\b|\bDrawableHoldNote\b|HoldNoteEndDragPiece" Garbus.Game Garbus.Game.Tests`
Expected: no matches under `Garbus.Game/` or `Garbus.Game.Tests/` code (comment/ported-from header lines mentioning the original BAC path may remain).

- [ ] **Step 2: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (same count as before the rename; behaviour unchanged).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor: rename HoldNote to CardinalHoldNote"
```

---

### Task 2: Extract shared generic hold bases

Factor the hold judgement/input out of the concrete cardinal classes into three generic bases. Cardinal keeps only its visuals.

**Files:**
- Create: `Garbus.Game/Objects/HoldNoteHead.cs` — generic `HoldNoteHead<TParent>`
- Delete: `Garbus.Game/Objects/CardinalHoldNoteHead.cs` (folded into the generic)
- Create: `Garbus.Game/Objects/Drawables/DrawableHoldNoteHead.cs` — generic `DrawableHoldNoteHead<THead>`
- Delete: `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNoteHead.cs`
- Create: `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs` — abstract `DrawableHoldNote<THitObject, THead>`
- Modify: `Garbus.Game/Objects/CardinalHoldNote.cs` — `Head` becomes `HoldNoteHead<CardinalHoldNote>`
- Modify: `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNote.cs` — extends the base, visuals only

**Interfaces:**
- Consumes: `CardinalHoldNote`, `Note`, `DrawableNote<T>`, `DrawableGarbusHitObject<T>` (`where T : GarbusHitObject`), `ISelfPosition`, `GarbusScrollingHitObjectContainer`, `HitResult`, `HitWindows`.
- Produces:
  - `HoldNoteHead<TParent> : Note, IHasAngle where TParent : Note, IHasAngle` (ctor `(TParent parent)`, `Parent` field).
  - `DrawableHoldNoteHead<THead> : DrawableGarbusHitObject<THead>, ISelfPosition where THead : Note` (public `bool UpdateResult()`).
  - `abstract DrawableHoldNote<THitObject, THead> : DrawableNote<THitObject>, ISelfPosition where THitObject : Note, IHasDuration where THead : Note`, with protected `DrawableHoldNoteHead<THead> Head`, protected `GarbusScrollingHitObjectContainer ScrollingContainer`, protected `bool Holding`, protected `bool HoldActive`, and abstract `void UpdateVisuals()` + virtual `void OnHeadHit()`.

- [ ] **Step 1: Write the generic head hit object**

Create `Garbus.Game/Objects/HoldNoteHead.cs`:

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/HoldNoteHead.cs).

using Garbus.Game.Input;

namespace Garbus.Game.Objects;

/// <summary>
/// The head of a hold note: a timed press judged exactly like a <see cref="CardinalNote"/>, nested inside
/// the hold at its start time. It takes its angle / button from the parent hold, so it shares the parent's
/// lane and direction. Its judgement is folded into the hold's final result at the tail
/// (see <see cref="Drawables.DrawableHoldNote{THitObject,THead}"/>).
/// </summary>
public class HoldNoteHead<TParent> : Note, IHasAngle
    where TParent : Note, IHasAngle
{
    public readonly TParent Parent;

    public HoldNoteHead(TParent parent)
    {
        Parent = parent;
    }

    public int AngleDeg => Parent.AngleDeg;

    public override GarbusButtonInput ButtonInput => Parent.ButtonInput;
}
```

Delete `Garbus.Game/Objects/CardinalHoldNoteHead.cs`.

- [ ] **Step 2: Point `CardinalHoldNote` at the generic head**

In `Garbus.Game/Objects/CardinalHoldNote.cs`, change the `Head` property and its creation:

```csharp
    public HoldNoteHead<CardinalHoldNote> Head { get; private set; } = null!;

    protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
    {
        base.CreateNestedHitObjects(cancellationToken);

        AddNested(Head = new HoldNoteHead<CardinalHoldNote>(this)
        {
            StartTime = StartTime,
        });
    }
```

- [ ] **Step 3: Write the generic head drawable**

Create `Garbus.Game/Objects/Drawables/DrawableHoldNoteHead.cs` (body copied from the deleted cardinal head drawable, retyped to `THead`):

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableHoldNoteHead.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: made generic over the head hit-object type so cardinal and shoulder holds share it.

using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// The head of a <see cref="DrawableHoldNote{THitObject,THead}"/>. Purely judgemental — the parent hold
/// draws the head and owns input — so this draws nothing and never handles input. Its result is applied when
/// the parent delegates a press via <see cref="UpdateResult"/>, or it auto-misses once its window elapses.
/// </summary>
public partial class DrawableHoldNoteHead<THead> : DrawableGarbusHitObject<THead>, ISelfPosition
    where THead : Note
{
    public override bool DisplayResult => false;

    public DrawableHoldNoteHead(THead hitObject)
        : base(hitObject)
    {
    }

    public bool UpdateResult() => base.UpdateResult(true);

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (!userTriggered)
        {
            if (!HitObject.HitWindows.CanBeHit(timeOffset))
                ApplyMinResult();
            return;
        }

        var result = HitObject.HitWindows.ResultFor(timeOffset);

        if (result == HitResult.None)
            return;

        ApplyResult(result);
    }
}
```

Delete `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNoteHead.cs`.

- [ ] **Step 4: Write the abstract hold-drawable base**

Create `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs`. This holds ALL the judgement/input logic (moved verbatim from `DrawableCardinalHoldNote`), with abstract visual hooks:

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableHoldNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: judgement/input/catch-record logic factored into this generic base so the cardinal
// and shoulder hold drawables share it; subclasses supply only visuals.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Allocation;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Input;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// Shared base for held notes: a nested judgemental head plus a time-accumulated ("catch record") tail,
/// judged deferred until the head resolves. Subclasses draw the head and body via <see cref="UpdateVisuals"/>.
/// </summary>
public abstract partial class DrawableHoldNote<THitObject, THead> : DrawableNote<THitObject>, ISelfPosition
    where THitObject : Note, IHasDuration
    where THead : Note
{
    [Resolved]
    protected GarbusScrollingHitObjectContainer ScrollingContainer { get; private set; } = null!;

    private readonly Container<DrawableHoldNoteHead<THead>> headContainer = new() { RelativeSizeAxes = Axes.Both };
    protected DrawableHoldNoteHead<THead> Head => headContainer.Child;

    private int holdPresses;
    private readonly List<CatchRecord> catchRecords = new();
    private CatchRecord? currentCatchRecord;
    private bool headPopPlayed;

    /// <summary>Whether the hold's button is currently held.</summary>
    protected bool Holding => holdPresses > 0;

    /// <summary>Whether the current time is within the hold body [StartTime, EndTime].</summary>
    protected bool HoldActive => Time.Current >= HitObject.StartTime && Time.Current <= HitObject.EndTime;

    protected DrawableHoldNote(THitObject hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(headContainer);
    }

    protected override void OnFree()
    {
        base.OnFree();

        holdPresses = 0;
        headPopPlayed = false;
        catchRecords.Clear();
        currentCatchRecord = null;
    }

    protected override void Update()
    {
        base.Update();

        if (Head.IsHit && !headPopPlayed)
        {
            headPopPlayed = true;
            OnHeadHit();
        }

        UpdateVisuals();
        updateCatchRecords();
    }

    /// <summary>Positions/builds the head and body for the frame. Subclasses draw everything here.</summary>
    protected abstract void UpdateVisuals();

    /// <summary>Called once when the head is hit, for the head-pop animation.</summary>
    protected virtual void OnHeadHit()
    {
    }

    private void updateCatchRecords()
    {
        double now = Time.Current;

        if (now < HitObject.StartTime || now > HitObject.EndTime)
            return;

        bool caught = holdPresses > 0;

        if (currentCatchRecord is null || currentCatchRecord.IsCatching != caught)
        {
            currentCatchRecord = new CatchRecord(caught, 0);
            catchRecords.Add(currentCatchRecord);
        }

        currentCatchRecord.Duration += Time.Elapsed;
    }

    public override bool OnPressed(KeyBindingPressEvent<GarbusAction> e)
    {
        if (e.Action.ToButtonInput() != HitObject.ButtonInput)
            return false;

        holdPresses++;

        if (!Head.Judged && CheckHittable?.Invoke(this, Time.Current) != false)
            return Head.UpdateResult();

        return false;
    }

    public override void OnReleased(KeyBindingReleaseEvent<GarbusAction> e)
    {
        if (e.Action.ToButtonInput() != HitObject.ButtonInput)
            return;

        holdPresses = Math.Max(0, holdPresses - 1);
    }

    public override void MissForcefully()
    {
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (!Head.Judged)
            return;

        bool headCarries = HitObject.Duration < Head.HitObject.HitWindows.WindowFor(HitResult.Miss);

        if (headCarries && !Head.IsHit)
        {
            ApplyMinResult();
            return;
        }

        if (timeOffset < 0)
            return;

        double total = 0, caught = 0;

        foreach (var record in catchRecords)
        {
            total += record.Duration;
            if (record.IsCatching)
                caught += record.Duration;
        }

        double fraction = total > 0 ? caught / total : 1.0;
        var result = resultFor(fraction);

        if (headCarries)
            result = (HitResult)Math.Min((int)result, (int)Head.Result.Type);

        ApplyResult(result);
    }

    private static HitResult resultFor(double fraction)
    {
        if (fraction >= 0.99) return HitResult.Perfect;
        if (fraction >= 0.90) return HitResult.Great;
        if (fraction >= 0.80) return HitResult.Good;
        if (fraction >= 0.65) return HitResult.Ok;
        if (fraction >= 0.50) return HitResult.Meh;

        return HitResult.Miss;
    }

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
    {
        return hitObject is THead head
            ? new DrawableHoldNoteHead<THead>(head)
            : throw new InvalidOperationException($"cannot create nested hit object for type {hitObject.GetType().Name}");
    }

    protected override void AddNestedHitObject(DrawableHitObject hitObject)
    {
        if (hitObject is not DrawableHoldNoteHead<THead> head)
            throw new InvalidOperationException($"cannot add child of type {hitObject.GetType()}");

        headContainer.Child = head;
    }

    protected override void ClearNestedHitObjects()
    {
        headContainer.Clear(false);
    }

    protected class CatchRecord(bool isCatching, double duration)
    {
        public bool IsCatching { get; } = isCatching;
        public double Duration { get; set; } = duration;
    }
}
```

- [ ] **Step 5: Reduce `DrawableCardinalHoldNote` to visuals**

Replace `Garbus.Game/Objects/Drawables/DrawableCardinalHoldNote.cs` with a subclass of the base carrying only the head sprite + trailing line (all `OnPressed`/`CheckForResult`/catch-record/nested code is now in the base):

```csharp
// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableHoldNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: judgement/input live in DrawableHoldNote<,>; this holds only the cardinal visuals.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using Garbus.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A held cardinal note: a square head sprite trailing a straight radial line (the hold body). The head
/// emerges from the centre and reaches the ring at StartTime; the trailing line runs inward toward the tail.
/// </summary>
public partial class DrawableCardinalHoldNote : DrawableHoldNote<CardinalHoldNote, HoldNoteHead<CardinalHoldNote>>
{
    private const float body_thickness = 20f;
    private const float head_size = 80f;

    private static readonly Colour4 held_colour = Colour4.White;
    private static readonly Colour4 dropped_colour = Colour4.Gray;

    private readonly Sprite headSprite;
    private readonly SmoothPath body;

    public DrawableCardinalHoldNote(CardinalHoldNote hitObject)
        : base(hitObject)
    {
        body = new SmoothPath
        {
            Anchor = Anchor.Centre,
            PathRadius = body_thickness / 2,
            Colour = held_colour,
        };

        headSprite = new Sprite
        {
            Size = new Vector2(head_size),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        headSprite.Texture = textures.Get("square");

        AddInternal(body);
        AddInternal(headSprite);
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();

        headSprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
        body.FadeInFromZero(100, Easing.In);
    }

    protected override void OnHeadHit()
    {
        headSprite.ScaleTo(1.2f, 80, Easing.OutQuint).Then().ScaleTo(1f, 120, Easing.OutQuint);
    }

    protected override void UpdateVisuals()
    {
        float ring = ScrollingContainer.ScrollLength;
        float radians = MathUtils.DegToRad(HitObject.AngleDeg);

        float headProgress = ScrollingContainer.ProgressAtTime(HitObject.StartTime);
        headSprite.Position = polarToCartesian(radians, headProgress);

        float outer = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, ring);
        float inner = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.EndTime), 0f, ring);

        if (outer - inner > 1f)
        {
            body.Vertices = new[]
            {
                polarToCartesian(radians, inner),
                polarToCartesian(radians, outer),
            };

            body.Position = -body.PositionInBoundingBox(Vector2.Zero);
        }
        else
        {
            body.Vertices = Array.Empty<Vector2>();
        }

        if (!Judged)
            body.Colour = HoldActive && !Holding ? dropped_colour : held_colour;
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                body.FadeOut(350, Easing.OutQuint);
                headSprite.Spin(700, RotationDirection.Clockwise)
                          .FadeOut(350, Easing.OutQuint)
                          .ScaleTo(new Vector2(2), 350, Easing.OutQuint)
                          .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                body.FadeColour(Color4.Red, duration);
                body.FadeOut(duration, Easing.InQuint);
                headSprite.FadeColour(Color4.Red, duration);
                headSprite.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }

    private static Vector2 polarToCartesian(float radians, float radius)
        => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
}
```

> Note: the original `updateVisuals` used `headProgress = ProgressAtTime(...)` then `polarToCartesian(radians, headProgress)` where the head sprite position was set from the *progress fraction* scaled by the container internally. Confirm against the pre-refactor file: if the original passed the raw `ProgressAtTime` result (a fraction) directly, keep it exactly as it was — do not introduce the `* ring` factor. Copy the head-position line verbatim from the Task-1 `DrawableCardinalHoldNote` to avoid a regression.

- [ ] **Step 6: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Run the gameplay + editor tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — `TestSceneGameplay` (short-hold-inherits-missed-head, untouched-miss), `TestSceneComposeSelection`/`Placement`, `TestChartFormat` all green. Cardinal behaviour is unchanged.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "refactor: factor shared generic hold-note bases"
```

---

### Task 3: `ShoulderHoldNote` object + serialization

**Files:**
- Create: `Garbus.Game/Objects/ShoulderHoldNote.cs`
- Modify: `Garbus.Game/Charts/Format/ChartFileDto.cs` (add `ShoulderHoldNoteDto` + discriminator)
- Modify: `Garbus.Game/Charts/Format/GarbusChartSerializer.cs` (add encode + decode cases)
- Test: `Garbus.Game.Tests/TestChartFormat.cs` (add a standalone roundtrip test + assert case)

**Interfaces:**
- Consumes: `Note`, `IHasCardinalDirection`, `IHasAngle`, `IHasDuration`, `HorizontalDirection`, `HoldNoteHead<TParent>`, `GarbusButtonInput`, `CardinalDirection`.
- Produces: `ShoulderHoldNote : Note, IHasCardinalDirection, IHasAngle, IHasDuration` with `required HorizontalDirection Side`, `double Duration`, `double EndTime`, `int AngleDeg` (derived), `CardinalDirection Direction`, `HoldNoteHead<ShoulderHoldNote> Head`; `ShoulderHoldNoteDto { string Side; double Duration }`; discriminator `"shoulder-hold"`.

- [ ] **Step 1: Write the failing roundtrip test**

In `Garbus.Game.Tests/TestChartFormat.cs`, add a test (place near the other format tests):

```csharp
        [Test]
        public void ShoulderHoldNoteRoundtrips()
        {
            var chart = new GarbusChart
            {
                HitObjects = new List<GarbusHitObject>
                {
                    new ShoulderHoldNote { StartTime = 1000, Duration = 750, Side = HorizontalDirection.Right },
                },
            };

            var decoded = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(chart));

            var hold = (ShoulderHoldNote)decoded.HitObjects.Single();
            Assert.That(hold.StartTime, Is.EqualTo(1000));
            Assert.That(hold.Duration, Is.EqualTo(750));
            Assert.That(hold.Side, Is.EqualTo(HorizontalDirection.Right));
        }
```

(Confirm the encode/decode entry-point names against the existing tests in this file — use whatever `TestChartFormat` already calls for its roundtrips, e.g. `GarbusChartSerializer.Encode`/`Decode`; match the existing usage exactly.)

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter ShoulderHoldNoteRoundtrips`
Expected: FAIL — `ShoulderHoldNote` does not exist (compile error).

- [ ] **Step 3: Write the object**

Create `Garbus.Game/Objects/ShoulderHoldNote.cs`:

```csharp
// Ported from BigAssCircle (shoulder counterpart of the held cardinal note).

using System;
using System.Threading;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Input;

namespace Garbus.Game.Objects;

/// <summary>
/// A held shoulder note — a <see cref="ShoulderNote"/> with a <see cref="Duration"/>. Its angle, button and
/// lane are derived from its <see cref="Side"/> (like <see cref="ShoulderNote"/>); it carries a nested head
/// judged like a shoulder press, with the tail judged on how much of the hold was held.
/// </summary>
public class ShoulderHoldNote : Note, IHasCardinalDirection, IHasAngle, IHasDuration
{
    public required HorizontalDirection Side { get; set; }

    public double Duration { get; set; }

    public double EndTime => StartTime + Duration;

    public int AngleDeg => Side.ToAngleDeg();

    public override GarbusButtonInput ButtonInput => Side switch
    {
        HorizontalDirection.Left => GarbusButtonInput.ButtonL,
        HorizontalDirection.Right => GarbusButtonInput.ButtonR,
        _ => throw new InvalidOperationException()
    };

    public CardinalDirection Direction => Side == HorizontalDirection.Left
        ? CardinalDirection.West
        : CardinalDirection.East;

    public HoldNoteHead<ShoulderHoldNote> Head { get; private set; } = null!;

    protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
    {
        base.CreateNestedHitObjects(cancellationToken);

        AddNested(Head = new HoldNoteHead<ShoulderHoldNote>(this)
        {
            StartTime = StartTime,
        });
    }
}
```

- [ ] **Step 4: Add the DTO + discriminator**

In `Garbus.Game/Charts/Format/ChartFileDto.cs`, add the derived-type attribute alongside the others (after the `"shoulder"` line):

```csharp
[JsonDerivedType(typeof(ShoulderHoldNoteDto), "shoulder-hold")]
```

and the DTO class (after `ShoulderNoteDto`):

```csharp
public class ShoulderHoldNoteDto : HitObjectDto
{
    public string Side { get; set; } = string.Empty;
    public double Duration { get; set; }
}
```

- [ ] **Step 5: Add the serializer cases**

In `Garbus.Game/Charts/Format/GarbusChartSerializer.cs`, in `toDto`'s switch add (after the `ShoulderNote` case):

```csharp
            ShoulderHoldNote shoulderHold => new ShoulderHoldNoteDto { Side = shoulderHold.Side.ToString(), Duration = shoulderHold.Duration },
```

and in `fromDto`'s switch add (after the `ShoulderNoteDto` case):

```csharp
            ShoulderHoldNoteDto shoulderHold => new ShoulderHoldNote { Side = parseEnum<HorizontalDirection>(shoulderHold.Side), Duration = shoulderHold.Duration },
```

- [ ] **Step 6: Add the assert case for future generator coverage**

In `TestChartFormat.assertHitObjectsEqual`'s switch, add (after the `ShoulderNote` case):

```csharp
                case ShoulderHoldNote e:
                    var actualShoulderHold = (ShoulderHoldNote)actual;
                    Assert.That(actualShoulderHold.Side, Is.EqualTo(e.Side));
                    Assert.That(actualShoulderHold.Duration, Is.EqualTo(e.Duration));
                    break;
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter ShoulderHoldNoteRoundtrips`
Expected: PASS.

- [ ] **Step 8: Build + full suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all green).

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat: add ShoulderHoldNote object and serialization"
```

---

### Task 4: `DrawableShoulderHoldNote` gameplay drawable + sector geometry + lane/factory wiring

**Files:**
- Modify: `Garbus.Game/Objects/Drawables/ShoulderNoteGeometry.cs` (add `SectorRotationDeg`)
- Create: `Garbus.Game/Objects/Drawables/DrawableShoulderHoldNote.cs`
- Modify: `Garbus.Game/UI/Ring.cs:110-116` (route `ShoulderHoldNote` to shoulder lane)
- Modify: `Garbus.Game/Screens/PlayScreen.cs:221-228` (gameplay drawable factory)
- Modify: `Garbus.Game/Charts/GarbusTestChartGenerator.cs` (add a `ShoulderHoldNote` instance)
- Test: `Garbus.Game.Tests/ShoulderNoteGeometryTest.cs` (rotation assertions)
- Test: `Garbus.Game.Tests/Visual/TestSceneGameplay.cs` (alive + miss + key-press hit)
- Regenerate: `Garbus.Resources/Charts/test-chart.garbus`

**Interfaces:**
- Consumes: `DrawableHoldNote<ShoulderHoldNote, HoldNoteHead<ShoulderHoldNote>>`, `ShoulderNoteGeometry`, `GarbusScrollingHitObjectContainer.ScrollLength`/`DistanceFromCentreAtTime`, `Arc`, framework `CircularProgress`.
- Produces: `DrawableShoulderHoldNote`; `ShoulderNoteGeometry.SectorRotationDeg(float baseAngleDeg) => 45f - baseAngleDeg`.

- [ ] **Step 1: Write the failing geometry test**

In `Garbus.Game.Tests/ShoulderNoteGeometryTest.cs`, add:

```csharp
        [Test]
        public void SectorRotationCentresOnCardinalDirection()
        {
            // CircularProgress fills a 0.25 (90°) wedge clockwise from local up; the rotation places the
            // wedge centre on the side's screen direction (east for a right note, west for a left one).
            Assert.That(ShoulderNoteGeometry.SectorRotationDeg(0f), Is.EqualTo(45f));    // right → east
            Assert.That(ShoulderNoteGeometry.SectorRotationDeg(180f), Is.EqualTo(-135f)); // left → west
        }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter SectorRotationCentresOnCardinalDirection`
Expected: FAIL — `SectorRotationDeg` not defined.

- [ ] **Step 3: Add the geometry helper**

In `Garbus.Game/Objects/Drawables/ShoulderNoteGeometry.cs`, add:

```csharp
    /// <summary>
    /// Rotation (degrees) for a <see cref="osu.Framework.Graphics.UserInterface.CircularProgress"/> whose
    /// 0.25 progress wedge should be centred on <paramref name="baseAngleDeg"/>'s screen direction, spanning
    /// ±45°. CircularProgress fills clockwise from local up; screen-clockwise angle for playfield angle θ is
    /// 90−θ, and the unrotated wedge centre sits at +45°, so rotation = (90−θ)−45 = 45−θ.
    /// </summary>
    public static float SectorRotationDeg(float baseAngleDeg) => 45f - baseAngleDeg;
```

- [ ] **Step 4: Run the geometry test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter SectorRotationCentresOnCardinalDirection`
Expected: PASS.

- [ ] **Step 5: Write the drawable**

Create `Garbus.Game/Objects/Drawables/DrawableShoulderHoldNote.cs`:

```csharp
// Ported from BigAssCircle (shoulder counterpart of DrawableHoldNote).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: two-square-plus-arc head (as DrawableShoulderNote) plus a transparent CircularProgress
// sector body; judgement/input come from DrawableHoldNote<,>.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using Garbus.Game.UI;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A held shoulder note: the two-square-plus-arc shoulder head plus a transparent sector body that grows
/// outward from the tail radius to the head radius over the 90° quadrant slice.
/// </summary>
public partial class DrawableShoulderHoldNote : DrawableHoldNote<ShoulderHoldNote, HoldNoteHead<ShoulderHoldNote>>
{
    private const float square_size = 80f;
    private const float arc_thickness = 15f;
    private const float sector_alpha = 0.35f;

    private static readonly Colour4 held_colour = Colour4.Purple;
    private static readonly Colour4 dropped_colour = Colour4.Gray;

    private Sprite squareA = null!;
    private Sprite squareB = null!;
    private Arc arc = null!;
    private CircularProgress sector = null!;

    public DrawableShoulderHoldNote(ShoulderHoldNote hitObject)
        : base(hitObject)
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var squareTexture = textures.Get("square");

        // Transparent body behind the head. CircularProgress fills a 90° wedge (Progress 0.25); Size/InnerRadius
        // are set each frame to grow the annulus between tail and head radii.
        AddInternal(sector = new CircularProgress
        {
            RelativeSizeAxes = Axes.None,
            Size = Vector2.Zero,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Progress = 0.25,
            InnerRadius = 0f,
            Colour = held_colour,
            Alpha = sector_alpha,
        });

        AddInternal(arc = new Arc(thickness: arc_thickness)
        {
            RelativeSizeAxes = Axes.None,
            Size = Vector2.Zero,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = held_colour,
        });

        AddInternal(squareA = createSquare(squareTexture));
        AddInternal(squareB = createSquare(squareTexture));
    }

    private static Sprite createSquare(Texture texture) => new Sprite
    {
        Texture = texture,
        Size = new Vector2(square_size),
        FillMode = FillMode.Fit,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Colour = held_colour,
    };

    protected override void UpdateVisuals()
    {
        float baseAngleDeg = HitObject.AngleDeg;
        float ring = ScrollingContainer.ScrollLength;
        float outer = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, ring);
        float inner = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.EndTime), 0f, ring);

        // Head: two squares on the ±45° diagonals + the growing arc, at the head radius.
        squareA.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, outer, +1f);
        squareB.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, outer, -1f);

        arc.Size = new Vector2(2f * outer);
        arc.StartRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg - ShoulderNoteGeometry.DiagonalOffsetDeg);
        arc.EndRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg + ShoulderNoteGeometry.DiagonalOffsetDeg);

        // Body: the transparent sector fills the annulus [inner, outer] over the 90° slice.
        sector.Size = new Vector2(2f * outer);
        sector.Rotation = ShoulderNoteGeometry.SectorRotationDeg(baseAngleDeg);
        sector.InnerRadius = outer > 0f ? inner / outer : 0f;

        if (!Judged)
        {
            var trailColour = HoldActive && !Holding ? dropped_colour : held_colour;
            arc.Colour = trailColour;
            sector.Colour = trailColour;
        }
    }

    protected override void OnHeadHit()
    {
        squareA.ScaleTo(1.2f, 80, Easing.OutQuint).Then().ScaleTo(1f, 120, Easing.OutQuint);
        squareB.ScaleTo(1.2f, 80, Easing.OutQuint).Then().ScaleTo(1f, 120, Easing.OutQuint);
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();
        this.ScaleTo(0).ScaleTo(1, 125, Easing.In);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                this.FadeOut(350, Easing.OutQuint)
                    .ScaleTo(new Vector2(1.4f), 350, Easing.OutQuint)
                    .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                this.FadeColour(Color4.Red, duration);
                this.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }
}
```

- [ ] **Step 6: Route the lane + gameplay factory**

In `Garbus.Game/UI/Ring.cs` `laneFor`, add (after the `ShoulderNote` case):

```csharp
        ShoulderHoldNote sh => shoulderLanes[shoulderIndex(sh.Side)],
```

In `Garbus.Game/Screens/PlayScreen.cs` `CreateDrawableRepresentation`, add (after the `ShoulderNote` case):

```csharp
            ShoulderHoldNote hold => new DrawableShoulderHoldNote(hold),
```

- [ ] **Step 7: Add a shoulder hold to the test chart generator**

In `Garbus.Game/Charts/GarbusTestChartGenerator.cs`, add inside the hit-object list (after the last hold):

```csharp
                new ShoulderHoldNote()
                {
                    StartTime = 13000,
                    Duration = 1000,
                    Side = HorizontalDirection.Right,
                },
```

- [ ] **Step 8: Write the failing gameplay tests**

In `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`, add a helper and two tests:

```csharp
        private Objects.Drawables.DrawableShoulderHoldNote? shoulderHold()
            => playfield.AllHitObjects.OfType<Objects.Drawables.DrawableShoulderHoldNote>().SingleOrDefault();

        [Test]
        public void TestShoulderHoldMissedWhenUntouched()
        {
            playThrough(20000);

            AddUntilStep("shoulder hold judged", () => shoulderHold()?.Judged == true);
            AddAssert("shoulder hold missed", () => shoulderHold()?.Result?.Type == HitResult.Miss);
        }

        [Test]
        public void TestShoulderHoldHeldByKeyPress()
        {
            // Right shoulder hold at 13000ms, 1000ms long. E maps to ButtonR.
            playThrough(12900);
            AddStep("press right shoulder", () => input.PressKey(Key.E));
            playThrough(14100);
            AddStep("release right shoulder", () => input.ReleaseKey(Key.E));

            AddUntilStep("shoulder hold judged", () => shoulderHold()?.Judged == true);
            AddAssert("shoulder hold hit", () => shoulderHold()?.IsHit == true);
        }
```

- [ ] **Step 9: Run the gameplay tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "TestShoulderHoldMissedWhenUntouched|TestShoulderHoldHeldByKeyPress"`
Expected: PASS. (If `TestShoulderHoldHeldByKeyPress` flakes on the manual clock, ensure `playThrough` steps in sub-window increments — it already advances 200ms per poll, smaller than the hit window.)

- [ ] **Step 10: Regenerate the bundled test chart**

The generator changed, so regenerate the bundled `.garbus`:

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter RegenerateBundledTestChart`
Expected: PASS (the `[Explicit]` test rewrites `Garbus.Resources/Charts/test-chart.garbus`).

- [ ] **Step 11: Build + full suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all green, including the roundtrip which now covers the generator's shoulder hold).

- [ ] **Step 12: Commit**

```bash
git add -A
git commit -m "feat: add ShoulderHoldNote gameplay drawable and lane wiring"
```

---

### Task 5: Editor drawable `EditorDrawableShoulderHoldNote` + composer factory

**Files:**
- Create: `Garbus.Game/Edit/Drawables/EditorDrawableShoulderHoldNote.cs`
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs:57-66` (drawable switch — add BEFORE the `ShoulderNote` case)
- Modify: `Garbus.Game.Tests/Editor/TestSceneEditorPlayfield.cs:126-130` (local factory switch)

**Interfaces:**
- Consumes: `EditorDrawableGarbusHitObject<ShoulderHoldNote>` (`where T : GarbusHitObject, IHasAngle` — satisfied, angle is derived), `GarbusEditorPlayfield.ShoulderXFraction`, `EditorSpritePiece`, `EditorDrawableCardinalNote.NOTE_SIZE`.
- Produces: `EditorDrawableShoulderHoldNote`.

- [ ] **Step 1: Write the editor drawable**

Create `Garbus.Game/Edit/Drawables/EditorDrawableShoulderHoldNote.cs` (a shoulder-lane head square + a translucent duration body, with head hit-testing — mirrors `EditorDrawableCardinalHoldNote` for the body and `EditorDrawableShoulderNote` for the x placement):

```csharp
// Editor timeline representation of a ShoulderHoldNote: a purple head square in its side's lane strip with
// a translucent body over the duration. Combines EditorDrawableShoulderNote (x from ShoulderXFraction) with
// EditorDrawableCardinalHoldNote (duration body + head hit-testing).

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Drawables;

public partial class EditorDrawableShoulderHoldNote : EditorDrawableGarbusHitObject<ShoulderHoldNote>
{
    private readonly Container nestedContainer;
    private readonly List<Drawable> headPieces = new List<Drawable>();

    public EditorDrawableShoulderHoldNote(ShoulderHoldNote hitObject)
        : base(hitObject)
    {
        Width = EditorDrawableCardinalNote.NOTE_SIZE;
        Origin = Anchor.BottomCentre;
        AddInternal(nestedContainer = new Container { RelativeSizeAxes = Axes.Both });
    }

    protected override Drawable CreateVisual()
    {
        EditorSpritePiece head;

        var visual = new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    Width = 12,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Colour = Color4.MediumPurple,
                    Alpha = 0.35f,
                },
                head = new EditorSpritePiece("square")
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Colour = Color4.MediumPurple,
                },
            },
        };

        headPieces.Add(head);
        return visual;
    }

    // Shoulder notes sit in their side's dedicated lane strip, not at their derived in-game angle.
    protected override float ComputeXFraction() => GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side);

    protected override float? TwinXFraction() => null;

    public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
    {
        if (base.ReceivePositionalInputAt(screenSpacePos))
            return true;

        foreach (var head in headPieces)
        {
            if (head.ScreenSpaceDrawQuad.Contains(screenSpacePos))
                return true;
        }

        return false;
    }

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject) =>
        new EditorDrawableNestedStub((GarbusHitObject)hitObject);

    protected override void AddNestedHitObject(DrawableHitObject hitObject) => nestedContainer.Add(hitObject);

    protected override void ClearNestedHitObjects() => nestedContainer.Clear(false);
}
```

> Check `EditorSpritePiece` accepts a `Colour` initializer (it derives from a framework drawable; if the constructor already forces a colour, drop the `Colour = Color4.MediumPurple` lines). Build will tell you.

- [ ] **Step 2: Register in the composer drawable factory**

In `Garbus.Game/Edit/GarbusHitObjectComposer.cs` `CreateDrawableRepresentation`, add BEFORE the `ShoulderNote` case (a `ShoulderHoldNote` is not a `ShoulderNote`, but keep the hold above the plain note for symmetry with the cardinal ordering):

```csharp
        ShoulderHoldNote shoulderHold => new EditorDrawableShoulderHoldNote(shoulderHold),
```

- [ ] **Step 3: Register in the test-scene local factory**

In `Garbus.Game.Tests/Editor/TestSceneEditorPlayfield.cs`, add to the local drawable switch (after the `ShoulderNote` case):

```csharp
                ShoulderHoldNote shn => new EditorDrawableShoulderHoldNote(shn),
```

- [ ] **Step 4: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run the editor playfield tests**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneEditorPlayfield`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add ShoulderHoldNote editor drawable"
```

---

### Task 6: `ShoulderHoldNotePlacementBlueprint` + composition tool

**Files:**
- Create: `Garbus.Game/Edit/Blueprints/ShoulderHoldNotePlacementBlueprint.cs`
- Modify: `Garbus.Game/Edit/Tools/GarbusCompositionTools.cs` (add `ShoulderHoldNoteCompositionTool`)
- Modify: `Garbus.Game/Edit/GarbusHitObjectComposer.cs:72-80` (add tool to the list — **order sets the number key**)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs` (drag-place a shoulder hold)

**Interfaces:**
- Consumes: `HitObjectPlacementBlueprint`, `SnapResult`, `PlacementState`, `Precision`, `EditSquarePiece`, `GarbusHitObjectComposer.FindSnappedAngleTimeAndPosition`, `GarbusEditorPlayfield.ShoulderXFraction`/`LEFT_SHOULDER_ANGLE_DEG`/`RIGHT_SHOULDER_ANGLE_DEG`, `EditorAngleMapping.ToAngle`/`NormalizeDeg`.
- Produces: `ShoulderHoldNotePlacementBlueprint`, `ShoulderHoldNoteCompositionTool`.

- [ ] **Step 1: Write the placement blueprint**

Create `Garbus.Game/Edit/Blueprints/ShoulderHoldNotePlacementBlueprint.cs` (merges `CardinalHoldNotePlacementBlueprint`'s drag-to-stretch with `ShoulderNotePlacementBlueprint`'s side-pick; note `ShoulderHoldNote` is not `IHasMutableAngle`, so it extends `HitObjectPlacementBlueprint` directly, not `GarbusPlacementBlueprint<T>`):

```csharp
// Shoulder hold placement: click begins, drag stretches the duration, release commits. The side is picked
// from the nearer shoulder lane strip while waiting (as ShoulderNotePlacementBlueprint); the duration drag
// mirrors CardinalHoldNotePlacementBlueprint.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Blueprints;

internal partial class ShoulderHoldNotePlacementBlueprint : HitObjectPlacementBlueprint
{
    protected new ShoulderHoldNote HitObject => (ShoulderHoldNote)base.HitObject;

    [Resolved]
    private GarbusHitObjectComposer? composer { get; set; }

    private readonly Box bodyPiece;
    private readonly EditSquarePiece headPiece;
    private readonly EditSquarePiece tailPiece;

    private double originalStartTime;

    protected override bool IsValidForPlacement => base.IsValidForPlacement
        && (PlacementActive == PlacementState.Waiting || Precision.DefinitelyBigger(HitObject.Duration, 0));

    public ShoulderHoldNotePlacementBlueprint()
        : base(new ShoulderHoldNote { Side = HorizontalDirection.Left })
    {
        RelativeSizeAxes = Axes.Both;

        InternalChildren = new Drawable[]
        {
            bodyPiece = new Box
            {
                Origin = Anchor.BottomCentre,
                Width = 12,
                Colour = Color4.MediumPurple,
                Alpha = 0.4f,
            },
            headPiece = new EditSquarePiece
            {
                Origin = Anchor.Centre,
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE),
                Colour = Color4.MediumPurple,
            },
            tailPiece = new EditSquarePiece
            {
                Origin = Anchor.Centre,
                Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE, 10),
                Colour = Color4.MediumPurple,
            },
        };
    }

    protected override void Update()
    {
        base.Update();

        if (composer == null)
            return;

        var container = composer.Playfield.HitObjectContainer;
        float x = GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side) * DrawWidth;

        headPiece.Position = new Vector2(x, ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.StartTime)).Y);
        tailPiece.Position = new Vector2(x, ToLocalSpace(container.ScreenSpacePositionAtTime(HitObject.EndTime)).Y);

        float bottom = Math.Max(headPiece.Y, tailPiece.Y);
        float top = Math.Min(headPiece.Y, tailPiece.Y);

        bodyPiece.Position = new Vector2(x, bottom);
        bodyPiece.Height = bottom - top;
    }

    protected override bool OnMouseDown(MouseDownEvent e)
    {
        if (e.Button != MouseButton.Left)
            return false;

        BeginPlacement(true);
        return true;
    }

    protected override void OnMouseUp(MouseUpEvent e)
    {
        if (e.Button != MouseButton.Left)
            return;

        base.OnMouseUp(e);
        EndPlacement(true);
    }

    public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
    {
        var result = composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition) ?? new SnapResult(screenSpacePosition, fallbackTime);

        base.UpdateTimeAndPosition(result.ScreenSpacePosition, result.Time ?? fallbackTime);

        if (PlacementActive == PlacementState.Active)
        {
            if (result.Time is double endTime)
            {
                HitObject.StartTime = endTime < originalStartTime ? endTime : originalStartTime;
                HitObject.Duration = Math.Abs(endTime - originalStartTime);
            }
        }
        else
        {
            if (result.Time is double startTime)
                originalStartTime = startTime;

            if (composer != null)
            {
                var playfield = composer.Playfield;
                float cursorAngle = EditorAngleMapping.ToAngle(playfield.ToLocalSpace(screenSpacePosition).X / playfield.DrawWidth);
                HitObject.Side = wrapDistance(cursorAngle, GarbusEditorPlayfield.LEFT_SHOULDER_ANGLE_DEG) <= wrapDistance(cursorAngle, GarbusEditorPlayfield.RIGHT_SHOULDER_ANGLE_DEG)
                    ? HorizontalDirection.Left
                    : HorizontalDirection.Right;
            }
        }

        return result;
    }

    private static float wrapDistance(float a, float b)
    {
        float d = Math.Abs(EditorAngleMapping.NormalizeDeg(a - b));
        return Math.Min(d, 360 - d);
    }

    public override bool ReplacesExistingObject(GarbusHitObject existing) =>
        base.ReplacesExistingObject(existing) && existing is ShoulderHoldNote shoulder && shoulder.Side == HitObject.Side;
}
```

- [ ] **Step 2: Add the composition tool**

In `Garbus.Game/Edit/Tools/GarbusCompositionTools.cs`, add after `ShoulderNoteCompositionTool`:

```csharp
public class ShoulderHoldNoteCompositionTool : CompositionTool
{
    public ShoulderHoldNoteCompositionTool()
        : base("Shoulder Hold")
    {
    }

    public override Drawable CreateIcon() => new SpriteText { Text = "ShH" };

    public override HitObjectPlacementBlueprint CreatePlacementBlueprint() => new ShoulderHoldNotePlacementBlueprint();
}
```

- [ ] **Step 3: Register the tool in the composer**

In `Garbus.Game/Edit/GarbusHitObjectComposer.cs` `CompositionTools`, add after `new ShoulderNoteCompositionTool()`:

```csharp
        new ShoulderHoldNoteCompositionTool(),
```

> The tool list order is the number-key order. `ShoulderHoldNoteCompositionTool` now sits at index 3 (0-based), so its hotkey is **Number5** (Cardinal=1, Hold=2, Shoulder=3, ShoulderHold=4? — count from the file: Cardinal, CardinalHold, Shoulder, ShoulderHold, SlamCentered, SlamEdge, Slider). Verify the resulting key in the test below; it is the (index+1) Number key.

- [ ] **Step 4: Write the failing placement test**

In `Garbus.Game.Tests/Editor/TestSceneComposePlacement.cs`, add (the shoulder-hold tool is the 4th tool → `Key.Number4`; if the earlier tools shift, use the actual index+1):

```csharp
        [Test]
        public void TestPlaceShoulderHoldWithDrag()
        {
            waitForComposer();
            AddStep("select shoulder hold tool", () => input.Key(Key.Number4));
            AddStep("move near right strip", () => input.MoveMouseTo(positionAtAngle(45, 0.6f)));
            AddStep("press", () => input.PressButton(MouseButton.Left));
            AddStep("drag upward", () => input.MoveMouseTo(positionAtAngle(45, 0.3f)));
            AddStep("release", () => input.ReleaseButton(MouseButton.Left));
            AddAssert("shoulder hold placed with duration", () => placedObject<ShoulderHoldNote>()?.Duration > 0);
            AddAssert("right side", () => placedObject<ShoulderHoldNote>()?.Side == HorizontalDirection.Right);
        }
```

- [ ] **Step 5: Run it (expect fail then pass)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestPlaceShoulderHoldWithDrag`
Expected: first FAIL if the number key is wrong (fix to the real index+1), then PASS. If `placedObject` finds nothing, confirm the tool index → number key mapping in `GarbusHitObjectComposer.CompositionTools`.

- [ ] **Step 6: Build + full suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: add ShoulderHoldNote placement tool"
```

---

### Task 7: `ShoulderHoldNoteSelectionBlueprint` + blueprint container

**Files:**
- Create: `Garbus.Game/Edit/Blueprints/ShoulderHoldNoteSelectionBlueprint.cs`
- Modify: `Garbus.Game/Edit/GarbusBlueprintContainer.cs:27-49` (add `ShoulderHoldNote` case BEFORE the `ShoulderNote` case)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (selectable by head; end-drag retimes)

**Interfaces:**
- Consumes: `GarbusSelectionBlueprint<ShoulderHoldNote>`, `HoldEndDragPiece`, `EditSquarePiece`, `IEditorChangeHandler`, `EditorChart`, `GarbusHitObjectComposer`, `GarbusEditorPlayfield.ShoulderXFraction`, `HitObjectContainer.LengthAtTime`.
- Produces: `ShoulderHoldNoteSelectionBlueprint`.

- [ ] **Step 1: Write the selection blueprint**

Create `Garbus.Game/Edit/Blueprints/ShoulderHoldNoteSelectionBlueprint.cs` (the cardinal-hold selection blueprint with x driven by `ShoulderXFraction` and no twin):

```csharp
// Shoulder hold selection: outline over the duration with draggable head/tail handles (as the cardinal hold
// selection), positioned in the side's shoulder lane strip.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Blueprints;

internal partial class ShoulderHoldNoteSelectionBlueprint : GarbusSelectionBlueprint<ShoulderHoldNote>
{
    [Resolved]
    private IEditorChangeHandler? changeHandler { get; set; }

    [Resolved]
    private EditorChart? editorChart { get; set; }

    [Resolved]
    private GarbusHitObjectComposer? composer { get; set; }

    private HoldEndDragPiece head = null!;

    public ShoulderHoldNoteSelectionBlueprint(ShoulderHoldNote hold)
        : base(hold)
    {
        Width = EditorDrawableCardinalNote.NOTE_SIZE;
        Origin = Anchor.BottomCentre;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        InternalChildren = new Drawable[]
        {
            new EditSquarePiece { RelativeSizeAxes = Axes.Both },
            head = new HoldEndDragPiece
            {
                RelativeSizeAxes = Axes.X,
                Height = EditorDrawableCardinalNote.NOTE_SIZE,
                Anchor = Anchor.BottomCentre,
                Origin = Anchor.Centre,
                DragStarted = () => changeHandler?.BeginChange(),
                Dragging = pos =>
                {
                    double endTime = HitObject.EndTime;
                    double proposedStartTime = timeAt(pos);

                    if (proposedStartTime >= endTime)
                        return;

                    HitObject.StartTime = proposedStartTime;
                    HitObject.Duration = endTime - proposedStartTime;
                    editorChart?.Update(HitObject);
                },
                DragEnded = () => changeHandler?.EndChange(),
            },
            new HoldEndDragPiece
            {
                RelativeSizeAxes = Axes.X,
                Height = 10,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                DragStarted = () => changeHandler?.BeginChange(),
                Dragging = pos =>
                {
                    double proposedEndTime = timeAt(pos);

                    if (HitObject.StartTime >= proposedEndTime)
                        return;

                    HitObject.Duration = proposedEndTime - HitObject.StartTime;
                    editorChart?.Update(HitObject);
                },
                DragEnded = () => changeHandler?.EndChange(),
            },
        };
    }

    private double timeAt(Vector2 screenSpacePosition) =>
        composer?.FindSnappedAngleTimeAndPosition(screenSpacePosition).Time ?? HitObjectContainer.TimeAtScreenSpacePosition(screenSpacePosition);

    protected override void Update()
    {
        base.Update();

        Height = HitObjectContainer.LengthAtTime(HitObject.StartTime, HitObject.EndTime);
    }

    protected override float ComputeXFraction() => GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side);

    protected override float? TwinXFraction() => null;

    public override Quad SelectionQuad => ScreenSpaceDrawQuad;

    public override Vector2 ScreenSpaceSelectionPoint => head.ScreenSpaceDrawQuad.Centre;
}
```

> `GarbusSelectionBlueprint<T>` drives `X` from `ComputeXFraction` (default angle-based). Confirm it exposes virtual `ComputeXFraction`/`TwinXFraction` (as used by `ShoulderNoteSelectionBlueprint`); the cardinal hold blueprint didn't override them, so if the base positions differently for BottomCentre-origin duration blueprints, mirror exactly what `ShoulderNoteSelectionBlueprint` + `CardinalHoldNoteSelectionBlueprint` do.

- [ ] **Step 2: Register in the blueprint container**

In `Garbus.Game/Edit/GarbusBlueprintContainer.cs` `CreateHitObjectBlueprintFor`, add BEFORE the `ShoulderNote` case:

```csharp
            case ShoulderHoldNote shoulderHold:
                return new ShoulderHoldNoteSelectionBlueprint(shoulderHold);
```

- [ ] **Step 3: Write the failing selection test**

In `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs`, add a test mirroring `TestHoldNoteSelectableByHead` (use the existing helpers in that file for adding an object + clicking its head; adapt the type):

```csharp
        [Test]
        public void TestShoulderHoldSelectableByHead()
        {
            AddStep("add shoulder hold", () => editorChart.Add(new ShoulderHoldNote
            {
                StartTime = 1000,
                Duration = 1000,
                Side = HorizontalDirection.Right,
            }));

            selectAll();

            AddAssert("shoulder hold selected", () => editorChart.SelectedHitObjects.OfType<ShoulderHoldNote>().Any());
        }
```

> Match this to the file's actual selection helpers (e.g. `selectAll()`, `editorChart.Add`, `SelectedHitObjects`). If the existing `TestHoldNoteSelectableByHead` clicks the head at a screen position, copy that pattern precisely, changing only the object type and the expected side.

- [ ] **Step 4: Run it (expect fail then pass)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestShoulderHoldSelectableByHead`
Expected: FAIL before Step 1-2 exist, PASS after.

- [ ] **Step 5: Build + full suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat: add ShoulderHoldNote selection blueprint"
```

---

### Task 8: Documentation

**Files:**
- Modify: `docs/presentation-specs/Presentation.md`

**Interfaces:** none (docs only).

- [ ] **Step 1: Update the presentation spec**

In `docs/presentation-specs/Presentation.md`, rename the `### HoldCardinalNote` heading to `### CardinalHoldNote` and `### HoldShoulderNote` to `### ShoulderHoldNote`, and rewrite the shoulder-hold body to describe the sector trail:

```markdown
### ShoulderHoldNote

A ShoulderHoldNote is presented as a shoulder note (two squares on the ±45° quadrant diagonals joined by
an arc) with a transparent sector trailing inward toward the center of the circle. The sector spans the
same 90° slice as the shoulder head and fills the band between the head (at StartTime) and the tail (at
EndTime), shrinking to nothing as the tail reaches the ring.
```

- [ ] **Step 2: Verify the doc reads consistently**

Run: `rg -n "HoldCardinalNote|HoldShoulderNote" docs/presentation-specs/Presentation.md`
Expected: no matches (both headings renamed).

- [ ] **Step 3: Commit**

```bash
git add docs/presentation-specs/Presentation.md
git commit -m "docs: update presentation spec for shoulder hold sector"
```

---

## Self-Review notes

- **Spec coverage:** rename (Task 1) + shared bases (Task 2) + object/serialization (Task 3) + gameplay drawable/sector/lane/factory/generator (Task 4) + editor drawable (Task 5) + placement/tool (Task 6) + selection (Task 7) + docs (Task 8). Every spec section maps to a task.
- **`IHasMutableAngle` trap:** `ShoulderHoldNote` deliberately does NOT implement it, so its placement blueprint extends `HitObjectPlacementBlueprint` directly (Task 6), not `GarbusPlacementBlueprint<T>` — mirrored from `ShoulderNotePlacementBlueprint`.
- **Discriminator ordering:** the `"type"` discriminator must stay the first serialized property; adding a `[JsonDerivedType]` does not change that.
- **Number-key hotkey:** the placement test's `Key.NumberN` depends on the tool list index; Task 6 flags verifying it.
- **Head-position line:** Task 2 Step 5 flags copying the cardinal head-position line verbatim to avoid a scaling regression.
