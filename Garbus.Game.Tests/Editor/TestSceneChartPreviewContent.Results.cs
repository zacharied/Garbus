using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Core;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;

namespace Garbus.Game.Tests.Editor;

public partial class TestSceneChartPreviewContent
{
    [Test]
    public void TestResultTimelineUsesStableRootIdAtEqualTimes()
    {
        var applied = new List<DrawableHitObject>();
        DrawableHitObject lowerId = null!;
        DrawableHitObject higherId = null!;

        AddStep("apply equal-time roots in reverse id order", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 1000, AngleDeg = 90 }), [20, 10], 999, 700)));
        AddUntilStep("equal-time roots loaded", () => preview.PlayfieldForTests.AllHitObjects.Count(d => d.IsLoaded) == 2);
        AddStep("capture roots and result order", () =>
        {
            lowerId = preview.DrawableForTests(new PreviewObjectId(10));
            higherId = preview.DrawableForTests(new PreviewObjectId(20));
            preview.PlayfieldForTests.NewResult += (drawable, _) => applied.Add(drawable);
        });

        AddStep("seek to exact result boundary", () => preview.Apply(transportBatch(2, 1000, false, 1, 0)));
        AddUntilStep("both equal-time results apply", () => applied.Count == 2);
        AddAssert("equal-time roots use stable id order", () => applied,
            () => Is.EqualTo(new[] { lowerId, higherId }));
    }

    [Test]
    public void TestResultTimelineUsesPostOrderAndExactReverseAtSliderEnd()
    {
        var applied = new List<DrawableHitObject>();
        var reverted = new List<JudgementResult>();
        DrawableSliderBody slider = null!;
        DrawableSliderChild finalChild = null!;
        JudgementResult sliderResult = null!;
        JudgementResult finalChildResult = null!;

        AddStep("apply slider immediately before end", () => preview.Replace(fullState(1, chartWith(new SliderBody
        {
            StartTime = 2000,
            AngleDeg = 0,
            Side = Core.HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 },
                    new GarbusPathControlPoint { TimeOffset = 1000, RotationOffset = 90 },
                },
            },
        }), [7], 2999, 700)));
        AddUntilStep("slider tree loaded before end", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault() is { IsLoaded: true } loaded
            && loaded.NestedHitObjects.All(d => d.IsLoaded)
            && (slider = loaded) != null
            && slider.NestedHitObjects.OfType<DrawableSliderChild>().Count() == 2);
        AddStep("capture equal-time slider results", () =>
        {
            finalChild = slider.NestedHitObjects.OfType<DrawableSliderChild>()
                               .Single(child => child.HitObject.GetEndTime() == slider.HitObject.GetEndTime());
            sliderResult = slider.Result;
            finalChildResult = finalChild.Result;
            preview.PlayfieldForTests.NewResult += (drawable, _) => applied.Add(drawable);
            preview.PlayfieldForTests.ResultReverted += reverted.Add;
        });

        AddStep("seek to slider end boundary", () => preview.Apply(transportBatch(2, 3000, false, 1, 0)));
        AddUntilStep("slider end results apply", () => applied.Count == 2);
        AddAssert("slider child applies before body", () => applied,
            () => Is.EqualTo(new DrawableHitObject[] { finalChild, slider }));
        AddAssert("slider body receives ignored maximum", () => slider.Result.Type,
            () => Is.EqualTo(slider.HitObject.Judgement.MaxResult));

        AddStep("advance past slider result fade", () => preview.Apply(transportBatch(3, 3401, false, 1, 0)));
        AddRepeatStep("allow slider result transforms", () => { }, 2);
        AddAssert("externally-resulted slider remains rewindable", () => slider.IsAlive && slider.IsPresent);

        AddStep("rewind below slider end boundary", () => preview.Apply(transportBatch(4, 2999, false, 1, 0)));
        AddUntilStep("slider end results revert", () => reverted.Count == 2);
        AddAssert("slider end reverts exact reverse order", () => reverted,
            () => Is.EqualTo(new[] { sliderResult, finalChildResult }));
        AddAssert("slider end results become idle", () => !slider.Judged && !finalChild.Judged);
    }

    [Test]
    public void TestResultTimelineVisitsOnlyCrossedEntries()
    {
        const int object_count = 100;
        long visitsBeforeIdleFrames = 0;
        long visitsBeforeJump = 0;
        CardinalNote[] notes = Enumerable.Range(0, object_count)
                                         .Select(index => new CardinalNote
                                         {
                                             StartTime = 1000 + index * 10,
                                             AngleDeg = index % 4 * 90,
                                         })
                                         .ToArray();
        long[] ids = Enumerable.Range(1, object_count).Select(index => (long)index).ToArray();

        AddStep("apply large future chart", () => preview.Replace(fullState(1, chartWith(notes), ids, 900, 700)));
        AddUntilStep("large chart and near roots ready", () =>
            preview.ObjectCountForTests == object_count
            && Enumerable.Range(1, 5).All(id => preview.DrawableForTests(new PreviewObjectId(id)).IsLoaded));
        AddStep("capture visits before idle frames", () => visitsBeforeIdleFrames = preview.ResultEntriesVisitedForTests);
        AddWaitStep("render stationary no-crossing frames", 5);
        AddAssert("no-crossing frames visit zero entries", () => preview.ResultEntriesVisitedForTests - visitsBeforeIdleFrames,
            () => Is.Zero);

        AddStep("capture visits before five-result jump", () => visitsBeforeJump = preview.ResultEntriesVisitedForTests);
        AddStep("jump across five entries", () => preview.Apply(transportBatch(2, 1040, false, 1, 0)));
        AddUntilStep("exactly five roots judged", () =>
            preview.PlayfieldForTests.AllHitObjects.Count(drawable => drawable.Judged) == 5);
        AddAssert("jump visits only crossed entries", () => preview.ResultEntriesVisitedForTests - visitsBeforeJump,
            () => Is.EqualTo(5));
    }

    [Test]
    public void TestDueUnreadyGenerationBlocksLaterResults()
    {
        var generations = new List<IGatedDrawable>();
        var applied = new List<DrawableHitObject>();
        IGatedDrawable earlier = null!;
        IGatedDrawable later = null!;

        useGatedDrawables(generations);
        AddStep("apply due gated roots", () => preview.Replace(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 100 },
            new ShoulderNote { StartTime = 1200, Side = Core.HorizontalDirection.Left }), [7, 8], 1300, 700)));
        AddUntilStep("both gated trees load", () => generations.Count == 2
                                                        && generations.All(generation => generation.Drawable.IsLoaded)
                                                        && generations.SelectMany(generation => withNested(generation.Drawable)).All(drawable => drawable.IsLoaded));
        AddStep("capture gates and result order", () =>
        {
            earlier = generations.Single(generation => generation.Drawable.HitObject.StartTime == 1000);
            later = generations.Single(generation => generation.Drawable.HitObject.StartTime == 1200);
            preview.PlayfieldForTests.NewResult += (drawable, _) => applied.Add(drawable);
        });

        AddStep("release only later due generation", () => later.ReleaseLoad());
        AddWaitStep("process blocked result frames", 3);
        AddAssert("later result cannot overtake earlier gate", () => applied, () => Is.Empty);

        AddStep("release earlier due generation", () => earlier.ReleaseLoad());
        AddUntilStep("both due generations catch up", () => applied.Count == 3);
        AddAssert("nested blocked results preserve post-order timeline", () => applied,
            () => Is.EqualTo(new[]
            {
                earlier.Drawable.NestedHitObjects.Single(),
                earlier.Drawable,
                later.Drawable,
            }));
    }

    [Test]
    public void TestRemovedNestedGenerationCannotProcessResults()
    {
        var generations = new List<IGatedDrawable>();
        var applied = new List<DrawableHitObject>();
        IGatedDrawable removed = null!;
        DrawableHitObject[] removedTree = null!;

        useGatedDrawables(generations);
        AddStep("apply blocked hold generation", () => preview.Replace(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 200 }), [7], 1500, 700)));
        AddUntilStep("blocked hold tree loads", () => generations.Count == 1
                                                     && withNested(generations[0].Drawable).Count() == 2
                                                     && withNested(generations[0].Drawable).All(drawable => drawable.IsLoaded));
        AddStep("capture removed nested generation", () =>
        {
            removed = generations.Single();
            removedTree = withNested(removed.Drawable).ToArray();
            preview.PlayfieldForTests.NewResult += (drawable, _) => applied.Add(drawable);
        });
        AddStep("remove blocked generation", () => Assert.That(preview.Apply(removeBatch(2, 7)), Is.True));
        AddStep("release removed generation", () => removed.ReleaseLoad());
        AddWaitStep("process after removed release", 3);

        AddAssert("removed generation applies no results", () => applied, () => Is.Empty);
        AddAssert("removed tree remains unjudged", () => removedTree.All(drawable => !drawable.Judged));
        AddAssert("removed nested generation is disposed", () => removedTree.All(isDisposed));
    }

    [Test]
    public void TestTypeReplacedNestedGenerationCannotProcessResults()
    {
        var generations = new List<IGatedDrawable>();
        var applied = new List<DrawableHitObject>();
        IGatedDrawable replaced = null!;
        IGatedDrawable current = null!;
        DrawableHitObject[] replacedTree = null!;

        useGatedDrawables(generations);
        AddStep("apply blocked hold generation", () => preview.Replace(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 200 }), [7], 1500, 700)));
        AddUntilStep("blocked hold tree loads", () => generations.Count == 1
                                                     && withNested(generations[0].Drawable).Count() == 2
                                                     && withNested(generations[0].Drawable).All(drawable => drawable.IsLoaded));
        AddStep("capture replaced nested generation", () =>
        {
            replaced = generations.Single();
            replacedTree = withNested(replaced.Drawable).ToArray();
            preview.PlayfieldForTests.NewResult += (drawable, _) => applied.Add(drawable);
        });
        AddStep("replace blocked hold with note", () => Assert.That(preview.Apply(upsertBatch(
            2,
            7,
            GarbusChartCloner.CloneHitObject(
                new CardinalNote { StartTime = 1100, AngleDeg = 90 }))), Is.True));
        AddUntilStep("replacement note loads blocked", () => generations.Count == 2 && generations[1].Drawable.IsLoaded);
        AddStep("capture current generation", () => current = generations[1]);

        AddStep("release only replaced generation", () => replaced.ReleaseLoad());
        AddWaitStep("process after replaced release", 3);
        AddAssert("replaced generation applies no results", () => applied, () => Is.Empty);
        AddAssert("replaced tree remains unjudged", () => replacedTree.All(drawable => !drawable.Judged));
        AddAssert("replaced nested generation is disposed", () => replacedTree.All(isDisposed));

        AddStep("release current generation", () => current.ReleaseLoad());
        AddUntilStep("only current generation catches up", () => applied.Count == 1);
        AddAssert("replacement result belongs to current generation", () => applied.Single(), () => Is.SameAs(current.Drawable));
    }

    [Test]
    public void TestAuthoritativeReplacementNestedGenerationCannotProcessResults()
    {
        var generations = new List<IGatedDrawable>();
        var applied = new List<DrawableHitObject>();
        IGatedDrawable replaced = null!;
        IGatedDrawable current = null!;
        DrawableHitObject[] replacedTree = null!;

        useGatedDrawables(generations);
        AddStep("apply blocked hold snapshot", () => preview.Replace(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 200 }), [7], 1500, 700)));
        AddUntilStep("blocked snapshot tree loads", () => generations.Count == 1
                                                         && withNested(generations[0].Drawable).Count() == 2
                                                         && withNested(generations[0].Drawable).All(drawable => drawable.IsLoaded));
        AddStep("capture snapshot nested generation", () =>
        {
            replaced = generations.Single();
            replacedTree = withNested(replaced.Drawable).ToArray();
            preview.PlayfieldForTests.NewResult += (drawable, _) => applied.Add(drawable);
        });
        AddStep("replace authoritative snapshot", () => Assert.That(preview.Replace(fullState(2, chartWith(
            new ShoulderNote { StartTime = 1100, Side = Core.HorizontalDirection.Right }), [8], 1500, 700)), Is.True));
        AddUntilStep("authoritative generation loads blocked", () => generations.Count == 2 && generations[1].Drawable.IsLoaded);
        AddStep("capture authoritative generation", () => current = generations[1]);

        AddStep("release replaced snapshot generation", () => replaced.ReleaseLoad());
        AddWaitStep("process after snapshot replacement", 3);
        AddAssert("replaced snapshot applies no results", () => applied, () => Is.Empty);
        AddAssert("replaced snapshot tree remains unjudged", () => replacedTree.All(drawable => !drawable.Judged));
        AddAssert("replaced snapshot nested generation is disposed", () => replacedTree.All(isDisposed));

        AddStep("release authoritative generation", () => current.ReleaseLoad());
        AddUntilStep("only authoritative generation catches up", () => applied.Count == 1);
        AddAssert("snapshot result belongs to authoritative generation", () => applied.Single(), () => Is.SameAs(current.Drawable));
    }

    [Test]
    public void TestDisposedNestedGenerationCannotProcessResults()
    {
        var generations = new List<IGatedDrawable>();
        var applied = new List<DrawableHitObject>();
        ChartPreviewContent disposedPreview = null!;
        IGatedDrawable disposed = null!;
        DrawableHitObject[] disposedTree = null!;

        useGatedDrawables(generations);
        AddStep("apply blocked hold before disposal", () => preview.Replace(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 200 }), [7], 1500, 700)));
        AddUntilStep("blocked disposal tree loads", () => generations.Count == 1
                                                         && withNested(generations[0].Drawable).Count() == 2
                                                         && withNested(generations[0].Drawable).All(drawable => drawable.IsLoaded));
        AddStep("capture disposal nested generation", () =>
        {
            disposed = generations.Single();
            disposedTree = withNested(disposed.Drawable).ToArray();
            preview.PlayfieldForTests.NewResult += (drawable, _) => applied.Add(drawable);
        });
        AddStep("remove preview content for disposal", () =>
        {
            disposedPreview = preview;
            Child = new Container();
        });
        AddUntilStep("preview content disposed", () => isDisposed(disposedPreview));
        AddStep("release disposed generation", () => disposed.ReleaseLoad());
        AddStep("create update sentinel", () => Child = preview = new ChartPreviewContent
        {
            Size = new osuTK.Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
        });
        AddUntilStep("update sentinel loaded", () => preview.IsLoaded);
        AddWaitStep("process after content disposal", 3);

        AddAssert("disposed generation applies no results", () => applied, () => Is.Empty);
        AddAssert("disposed tree remains unjudged", () => disposedTree.All(drawable => !drawable.Judged));
        AddAssert("disposed nested generation remains disposed", () => disposedTree.All(isDisposed));
    }

    [Test]
    public void TestOrdinaryPlayfieldRewindsMultipleResultsLifoAndRejectsOutOfOrderSeam()
    {
        var clock = new ManualClock();
        var reverted = new List<JudgementResult>();
        TestResultPlayfield ordinaryPlayfield = null!;
        TestResultDrawable first = null!;
        TestResultDrawable second = null!;
        JudgementResult firstResult = null!;
        JudgementResult secondResult = null!;

        AddStep("create isolated ordinary playfield", () =>
        {
            var firstObject = new HitObject { StartTime = 1000 };
            var secondObject = new HitObject { StartTime = 1200 };
            firstObject.ApplyDefaults();
            secondObject.ApplyDefaults();
            first = new TestResultDrawable(firstObject);
            second = new TestResultDrawable(secondObject);
            ordinaryPlayfield = new TestResultPlayfield { RelativeSizeAxes = Axes.Both };
            ordinaryPlayfield.Add(first);
            ordinaryPlayfield.Add(second);
            ordinaryPlayfield.ResultReverted += reverted.Add;
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = new FramedClock(clock),
                Child = ordinaryPlayfield,
            };
        });
        AddUntilStep("ordinary result drawables loaded", () => first.IsLoaded && second.IsLoaded);

        AddStep("move ordinary clock to first result", () => clock.CurrentTime = 1000);
        AddUntilStep("first drawable sees result time", () => first.Clock.CurrentTime == 1000);
        AddStep("apply first ordinary result", () =>
        {
            first.ApplyMaximumForTest();
            firstResult = first.Result;
        });
        AddStep("move ordinary clock to second result", () => clock.CurrentTime = 1200);
        AddUntilStep("second drawable sees result time", () => second.Clock.CurrentTime == 1200);
        AddStep("apply second ordinary result", () =>
        {
            second.ApplyMaximumForTest();
            secondResult = second.Result;
        });

        AddStep("reject non-top exact revert", () =>
            Assert.Throws<InvalidOperationException>(() => ordinaryPlayfield.RevertResult(firstResult)));
        AddAssert("rejected seam preserves both results", () => first.Judged && second.Judged);

        AddStep("rewind ordinary clock below both results", () => clock.CurrentTime = 900);
        AddUntilStep("ordinary stack reverts both results", () => reverted.Count == 2);
        AddAssert("ordinary results revert exact LIFO", () => reverted,
            () => Is.EqualTo(new[] { secondResult, firstResult }));
        AddAssert("ordinary results reset after rewind", () => !first.Judged && !second.Judged);
    }

    private partial class TestResultDrawable : DrawableHitObject
    {
        public TestResultDrawable(HitObject hitObject)
            : base(hitObject)
        {
        }

        public void ApplyMaximumForTest() => ApplyMaxResult();
    }

    private partial class TestResultPlayfield : Garbus.Game.Gameplay.UI.Playfield
    {
    }

    [Test]
    public void TestPreviewPolicyAppliesSilentMaximumResultWhileOrdinaryGameplayStillMisses()
    {
        ManualClock gameplayClock = null!;
        DrawableCardinalNote ordinary = null!;
        DrawableCardinalNote previewNote = null!;
        long transportRevision = 2;

        AddStep("create preview and ordinary note", () =>
        {
            preview.Replace(fullState(1, chartWith(
                new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700));

            gameplayClock = new ManualClock { CurrentTime = 900 };
            var ordinaryNote = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            ordinaryNote.ApplyDefaults();
            ordinary = new DrawableCardinalNote(ordinaryNote);
            var ordinaryPlayfield = new GarbusPlayfield { RelativeSizeAxes = Axes.Both };
            ordinaryPlayfield.Add(ordinary);
            Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = new FramedClock(gameplayClock),
                Child = ordinaryPlayfield,
            });
        });
        AddUntilStep("preview note loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && (previewNote = loaded) != null);
        AddUntilStep("advance both beyond miss window", () =>
        {
            gameplayClock.CurrentTime += 100;
            preview.Apply(transportBatch(transportRevision++, gameplayClock.CurrentTime, false, 1, 0));
            return gameplayClock.CurrentTime >= 3000;
        });

        AddUntilStep("ordinary gameplay note misses", () => ordinary.Judged);
        AddAssert("ordinary gets miss result", () => ordinary.Result.Type, () => Is.EqualTo(HitResult.Miss));
        AddUntilStep("preview note hits", () => previewNote.State.Value == ArmedState.Hit);
        AddAssert("preview gets maximum result", () => previewNote.Result.Type,
            () => Is.EqualTo(HitResult.CriticalPerfect));
        AddAssert("preview result occurs exactly at start", () => previewNote.Result.RawTime,
            () => Is.EqualTo(previewNote.HitObject.StartTime));
        AddAssert("preview hit remains silent", () => previewNote.SamplesPlayCount, () => Is.Zero);
    }

    [Test]
    public void TestPreviewHitLifecycleRewindsAndReappliesResult()
    {
        DrawableCardinalNote drawable = null!;
        double hitLifetimeEnd = 0;

        AddStep("apply preview note", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700)));
        AddUntilStep("preview note loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && (drawable = loaded) != null);

        AddStep("seek to note start", () => preview.Apply(transportBatch(2, 1000, false, 1, 0)));
        AddUntilStep("preview note hits", () => drawable.State.Value == ArmedState.Hit);
        AddAssert("first result is exact", () => drawable.Result.RawTime, () => Is.EqualTo(1000));
        AddStep("capture hit lifetime", () => hitLifetimeEnd = drawable.LifetimeEnd);

        AddStep("seek beyond hit lifetime", () => preview.Apply(transportBatch(3, hitLifetimeEnd + 1, false, 1, 0)));
        AddUntilStep("preview hit no longer alive", () => !drawable.IsAlive && !drawable.IsPresent);
        AddAssert("expired preview note remains hit", () => drawable.Judged && drawable.State.Value == ArmedState.Hit);

        AddStep("seek backward into hit lifetime", () => preview.Apply(transportBatch(4, 1200, false, 1, 0)));
        AddWaitStep("process backward seek", 2);
        AddAssert("preview clock rewinds", () => drawable.Clock.CurrentTime, () => Is.EqualTo(1200).Within(0.001));
        AddAssert("rewound time is inside hit lifetime", () => drawable.LifetimeEnd, () => Is.GreaterThan(1200));
        AddAssert("same preview hit is alive", () => drawable.IsAlive, () => Is.True);
        AddAssert("same preview hit is present", () => drawable.IsPresent, () => Is.True);
        AddAssert("preview result lifetime covers maximum animation", () => hitLifetimeEnd - drawable.HitStateUpdateTime,
            () => Is.EqualTo(1000));
        AddAssert("revived drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Single(), () => Is.SameAs(drawable));
        AddAssert("revived result remains hit", () => drawable.Judged && drawable.State.Value == ArmedState.Hit);

        AddStep("seek before result", () => preview.Apply(transportBatch(5, 900, false, 1, 0)));
        AddUntilStep("preview result rewinds", () => !drawable.Judged && drawable.State.Value == ArmedState.Idle);

        AddStep("seek forward to note start again", () => preview.Apply(transportBatch(6, 1000, false, 1, 0)));
        AddUntilStep("preview note hits again", () => drawable.State.Value == ArmedState.Hit);
        AddAssert("reapplied result is exact", () => drawable.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("reapplied result remains silent", () => drawable.SamplesPlayCount, () => Is.Zero);
    }

    [Test]
    public void TestPreviewResultsApplyChronologicallyAcrossForwardAndBackwardSeeks()
    {
        DrawableCardinalNote earlier = null!;
        DrawableCardinalNote later = null!;
        int laterResultCount = 0;

        AddStep("apply same-lane preview notes", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [7, 8], 900, 700)));
        AddUntilStep("same-lane preview notes loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().Count(d => d.IsLoaded) == 2);
        AddStep("capture preview notes and results", () =>
        {
            DrawableCardinalNote[] notes = preview.PlayfieldForTests.AllHitObjects
                                                      .OfType<DrawableCardinalNote>()
                                                      .OrderBy(d => d.HitObject.StartTime)
                                                      .ToArray();
            earlier = notes[0];
            later = notes[1];
            preview.PlayfieldForTests.NewResult += (drawable, _) =>
            {
                if (ReferenceEquals(drawable, later))
                    laterResultCount++;
            };
        });

        AddStep("jump forward across both notes", () => preview.Apply(transportBatch(2, 1300, false, 1, 0)));
        AddUntilStep("both preview notes hit", () =>
            earlier.Judged && earlier.State.Value == ArmedState.Hit
                           && later.Judged && later.State.Value == ArmedState.Hit);
        AddAssert("earlier result time is exact", () => earlier.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddAssert("later result applied once", () => laterResultCount, () => Is.EqualTo(1));

        AddStep("rewind between notes", () => preview.Apply(transportBatch(3, 1100, false, 1, 0)));
        AddUntilStep("rewind transport applied", () => preview.ClockTimeForTests, () => Is.EqualTo(1100));
        AddAssert("later result becomes unjudged", () => later.Judged, () => Is.False);
        AddAssert("later state becomes idle", () => later.State.Value, () => Is.EqualTo(ArmedState.Idle));
        AddAssert("earlier result remains applied", () => earlier.Judged && earlier.State.Value == ArmedState.Hit);

        AddStep("move forward across later note", () => preview.Apply(transportBatch(4, 1300, false, 1, 0)));
        AddUntilStep("later result reapplies", () => later.Judged && later.State.Value == ArmedState.Hit);
        AddAssert("reapplied later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddWaitStep("hold after later result", 3);
        AddAssert("later result reapplied exactly once", () => laterResultCount, () => Is.EqualTo(2));
    }

    [Test]
    public void TestPreviewResultsRemainChronologicalAcrossLiveUpserts()
    {
        DrawableCardinalNote earlier = null!;
        DrawableCardinalNote later = null!;
        int laterResultCount = 0;

        AddStep("listen for later results", () => preview.PlayfieldForTests.NewResult += (_, result) =>
        {
            if (result.RawTime == 1200)
                laterResultCount++;
        });
        AddStep("apply only later note at 1300", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [8], 1300, 700)));
        AddUntilStep("later preview note loads and hits", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true, Judged: true } loaded
            && loaded.State.Value == ArmedState.Hit
            && (later = loaded) != null);
        AddAssert("initial later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddAssert("later result initially applied once", () => laterResultCount, () => Is.EqualTo(1));

        AddStep("upsert earlier note while still at 1300", () => Assert.That(preview.Apply(upsertBatch(
            2,
            7,
            GarbusChartCloner.CloneHitObject(new CardinalNote { StartTime = 1000, AngleDeg = 0 }))), Is.True));
        AddUntilStep("both live-upserted notes load and hit", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>() is var notes
            && notes.Count(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit) == 2);
        AddStep("capture earlier note", () => earlier = preview.PlayfieldForTests.AllHitObjects
                                                                    .OfType<DrawableCardinalNote>()
                                                                    .Single(d => d.HitObject.StartTime == 1000));
        AddAssert("live-upserted earlier result time is exact", () => earlier.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("live-upserted later result time remains exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddAssert("timeline rebuild reapplies later result once", () => laterResultCount, () => Is.EqualTo(2));

        AddStep("rewind between live-upserted notes", () => preview.Apply(transportBatch(3, 1100, false, 1, 0)));
        AddUntilStep("live-upsert rewind applied", () => preview.ClockTimeForTests, () => Is.EqualTo(1100));
        AddAssert("earlier live-upserted result remains hit", () => earlier.Judged && earlier.State.Value == ArmedState.Hit);
        AddAssert("later live-upserted result becomes idle and unjudged", () =>
            !later.Judged && later.State.Value == ArmedState.Idle);

        AddStep("move forward across later live-upserted note", () => preview.Apply(transportBatch(4, 1300, false, 1, 0)));
        AddUntilStep("later live-upserted result reapplies", () => later.Judged && later.State.Value == ArmedState.Hit);
        AddAssert("reapplied live-upserted later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddWaitStep("hold after live-upserted later result", 3);
        AddAssert("explicit replay reapplies later result once", () => laterResultCount, () => Is.EqualTo(3));
    }

    [Test]
    public void TestPreviewReordersEditedResultTimesBeforeRewind()
    {
        DrawableCardinalNote edited = null!;
        DrawableCardinalNote unchanged = null!;

        AddStep("apply judged preview notes", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [7, 8], 1300, 700)));
        AddUntilStep("both preview notes load and hit", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>() is var notes
            && notes.Count(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit) == 2);
        AddStep("capture judged preview notes", () =>
        {
            edited = (DrawableCardinalNote)preview.DrawableForTests(new PreviewObjectId(7));
            unchanged = (DrawableCardinalNote)preview.DrawableForTests(new PreviewObjectId(8));
        });

        AddStep("move judged result after unchanged result", () => Assert.That(preview.Apply(upsertBatch(
            2,
            7,
            GarbusChartCloner.CloneHitObject(new CardinalNote { StartTime = 1250, AngleDeg = 0 }))), Is.True));
        AddUntilStep("judged result time updates in place", () =>
            edited.Judged && edited.HitObject.StartTime == 1250 && edited.Result.RawTime == 1250);
        AddAssert("unchanged result stays exact", () => unchanged.Result.RawTime, () => Is.EqualTo(1200));

        AddStep("rewind between edited result times", () => preview.Apply(transportBatch(3, 1225, false, 1, 0)));
        AddUntilStep("edited-result rewind applied", () => preview.ClockTimeForTests, () => Is.EqualTo(1225));
        AddAssert("unchanged earlier result remains hit", () => unchanged.Judged && unchanged.State.Value == ArmedState.Hit);
        AddAssert("edited later result becomes idle and unjudged", () =>
            !edited.Judged && edited.State.Value == ArmedState.Idle);
    }

    [Test]
    public void TestPreviewRevertsEditedResultMovedBeyondStationaryTime()
    {
        DrawableCardinalNote edited = null!;
        DrawableCardinalNote unchanged = null!;
        int reappliedResultCount = 0;

        AddStep("apply judged preview notes at 1300", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [7, 8], 1300, 700)));
        AddUntilStep("both preview notes load and hit", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>() is var notes
            && notes.Count(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit) == 2);
        AddStep("capture judged preview notes", () =>
        {
            edited = (DrawableCardinalNote)preview.DrawableForTests(new PreviewObjectId(7));
            unchanged = (DrawableCardinalNote)preview.DrawableForTests(new PreviewObjectId(8));
            preview.PlayfieldForTests.NewResult += (drawable, _) =>
            {
                if (ReferenceEquals(drawable, edited))
                    reappliedResultCount++;
            };
        });

        AddStep("move judged result beyond stationary time", () =>
        {
            Assert.That(preview.Apply(upsertBatch(
                2,
                7,
                GarbusChartCloner.CloneHitObject(new CardinalNote { StartTime = 1400, AngleDeg = 0 }))), Is.True);
            Assert.That(edited.HitObject.StartTime, Is.EqualTo(1400));
            Assert.That(edited.Result.RawTime, Is.Null);
        });
        AddAssert("preview time remains stationary", () => preview.ClockTimeForTests, () => Is.EqualTo(1300));
        AddUntilStep("future edited result promptly reverts", () =>
            !edited.Judged && edited.State.Value == ArmedState.Idle);
        AddAssert("unchanged earlier result remains hit", () =>
            unchanged.Judged && unchanged.State.Value == ArmedState.Hit);

        AddStep("move to edited result time", () => preview.Apply(transportBatch(3, 1400, false, 1, 0)));
        AddUntilStep("edited maximum result reapplies", () =>
            edited.Judged && edited.State.Value == ArmedState.Hit
                          && edited.Result.Type == HitResult.CriticalPerfect);
        AddAssert("reapplied edited result time is exact", () => edited.Result.RawTime, () => Is.EqualTo(1400));
        AddWaitStep("hold after edited result reapplies", 3);
        AddAssert("edited result reapplies exactly once", () => reappliedResultCount, () => Is.EqualTo(1));
    }

    [Test]
    public void TestPreviewJudgedHoldUpsertsRefreshResultTreeTracking()
    {
        DrawableCardinalHoldNote hold = null!;
        DrawableHitObject[] originalNested = null!;
        DrawableHitObject[] firstReplacementNested = null!;
        DrawableHitObject[] currentTree = null!;
        Dictionary<Drawable, int> holdDisposals = null!;
        Dictionary<Drawable, int> originalNestedDisposals = null!;
        Dictionary<Drawable, int> firstReplacementNestedDisposals = null!;
        JudgementResult rootResult = null!;
        JudgementResult[] currentResults = null!;
        JudgementResult[] judgedResultsBeforeRewind = null!;
        double[] updatedTimes = null!;
        var revertedResults = new List<JudgementResult>();
        var replayedResults = new List<(DrawableHitObject Drawable, JudgementResult Result)>();

        AddStep("apply preview hold", () => preview.Replace(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 1000 }), [7], 900, 700)));
        AddUntilStep("preview hold tree loads", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalHoldNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && withNested(loaded).Count() == 2
            && withNested(loaded).All(d => d.IsLoaded)
            && (hold = loaded) != null);
        AddStep("judge preview hold tree", () => preview.Apply(transportBatch(2, 2000, false, 1, 0)));
        AddUntilStep("preview hold tree hits", () =>
            withNested(hold).All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture original hold result tree", () =>
        {
            originalNested = hold.NestedHitObjects.ToArray();
            rootResult = hold.Result;
            holdDisposals = disposalCountsFor([hold]);
            originalNestedDisposals = disposalCountsFor(originalNested);
        });

        AddStep("upsert first judged hold rebuild", () => Assert.That(preview.Apply(upsertBatch(
            3,
            7,
            GarbusChartCloner.CloneHitObject(new CardinalHoldNote
            {
                StartTime = 1100,
                AngleDeg = 0,
                Duration = 900,
            }))), Is.True));
        AddStep("capture first replacement hold generation", () =>
        {
            firstReplacementNested = hold.NestedHitObjects.ToArray();
            firstReplacementNestedDisposals = disposalCountsFor(firstReplacementNested);
        });
        AddAssert("original hold generation disposed exactly once", () => originalNestedDisposals.Values, () => Is.All.EqualTo(1));
        AddAssert("retained hold root remains alive", () => holdDisposals.Values, () => Is.All.Zero);
        AddAssert("first replacement hold generation remains alive", () => firstReplacementNested.All(d => !isDisposed(d)));

        AddStep("upsert second judged hold rebuild", () => Assert.That(preview.Apply(upsertBatch(
            4,
            7,
            GarbusChartCloner.CloneHitObject(new CardinalHoldNote
            {
                StartTime = 1200,
                AngleDeg = 0,
                Duration = 800,
            }))), Is.True));
        AddUntilStep("current hold result tree loads and catches up", () =>
            withNested(hold).Count() == 2
            && withNested(hold).All(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture current hold result tree", () =>
        {
            currentTree = withNested(hold).ToArray();
            currentResults = currentTree.Select(d => d.Result).ToArray();
            judgedResultsBeforeRewind = currentTree.Where(d => d.Judged).Select(d => d.Result).ToArray();
            updatedTimes = currentTree.Select(d => d.HitObject.GetEndTime()).ToArray();

            Assert.That(preview.DrawableForTests(new PreviewObjectId(7)), Is.SameAs(hold));
            Assert.That(hold.Result, Is.Not.SameAs(rootResult));
            Assert.That(originalNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(firstReplacementNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(originalNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(firstReplacementNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(holdDisposals.Values, Is.All.Zero);
            Assert.That(currentTree.All(d => !isDisposed(d)), Is.True);
            Assert.That(currentTree, Has.Length.EqualTo(2));
            Assert.That(judgedResultsBeforeRewind, Is.EqualTo(currentResults));
            Assert.That(currentTree.Select(d => d.Result.RawTime), Is.EqualTo(updatedTimes));

            preview.PlayfieldForTests.ResultReverted += revertedResults.Add;
            preview.PlayfieldForTests.NewResult += (drawable, result) => replayedResults.Add((drawable, result));
        });

        AddStep("rewind before current hold tree", () => preview.Apply(transportBatch(5, 900, false, 1, 0)));
        AddUntilStep("current hold tree rewinds", () =>
            preview.ClockTimeForTests == 900
            && currentTree.All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddAssert("only judged current hold results revert", () => revertedResults.Count, () => Is.EqualTo(judgedResultsBeforeRewind.Length));
        AddAssert("judged current hold result identities revert once", () => revertedResults, () => Is.EquivalentTo(judgedResultsBeforeRewind));
        AddAssert("current hold nested objects are idle and unjudged", () =>
            currentTree.Skip(1).All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddUntilStep("current hold nested objects load idle", () => currentTree.Skip(1).All(d => d.IsLoaded && !d.Judged));

        AddStep("replay current hold result tree", () => preview.Apply(transportBatch(6, 2000, false, 1, 0)));
        AddUntilStep("current hold tree reapplies", () =>
            currentTree.All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddWaitStep("hold after current hold replay", 3);
        AddAssert("only current hold results reapply", () => replayedResults.Count, () => Is.EqualTo(currentResults.Length));
        AddAssert("current hold result identities reapply once", () => replayedResults.Select(e => e.Result), () => Is.EquivalentTo(currentResults));
        AddAssert("current hold results reapply at updated exact times", () =>
            currentTree.Select(d => d.Result.RawTime), () => Is.EqualTo(updatedTimes));
    }

    [Test]
    public void TestPreviewJudgedSliderUpsertsRefreshResultTreeTracking()
    {
        DrawableSliderBody slider = null!;
        DrawableHitObject[] originalNested = null!;
        DrawableHitObject[] firstReplacementNested = null!;
        DrawableHitObject[] currentTree = null!;
        Dictionary<Drawable, int> sliderDisposals = null!;
        Dictionary<Drawable, int> originalNestedDisposals = null!;
        Dictionary<Drawable, int> firstReplacementNestedDisposals = null!;
        JudgementResult rootResult = null!;
        JudgementResult[] currentResults = null!;
        JudgementResult[] judgedResultsBeforeRewind = null!;
        double[] updatedTimes = null!;
        var revertedResults = new List<JudgementResult>();
        var replayedResults = new List<(DrawableHitObject Drawable, JudgementResult Result)>();

        AddStep("apply preview slider", () => preview.Replace(fullState(1, chartWith(new SliderBody
        {
            StartTime = 1000,
            AngleDeg = 0,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 800, RotationOffset = 45 },
                    new GarbusPathControlPoint { TimeOffset = 1600, RotationOffset = 90 },
                },
            },
        }), [7], 900, 700)));
        AddUntilStep("preview slider tree loads", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault() is { IsLoaded: true } loaded
            && withNested(loaded).Count() == 4
            && withNested(loaded).All(d => d.IsLoaded)
            && (slider = loaded) != null);
        AddStep("judge preview slider tree", () => preview.Apply(transportBatch(2, 2600, false, 1, 0)));
        AddUntilStep("preview slider tree hits", () =>
            withNested(slider).All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture original slider result tree", () =>
        {
            originalNested = slider.NestedHitObjects.ToArray();
            rootResult = slider.Result;
            sliderDisposals = disposalCountsFor([slider]);
            originalNestedDisposals = disposalCountsFor(originalNested);
        });

        AddStep("upsert first judged slider rebuild", () => Assert.That(preview.Apply(upsertBatch(
            3,
            7,
            GarbusChartCloner.CloneHitObject(new SliderBody
            {
                StartTime = 1100,
                AngleDeg = 45,
                Side = HorizontalDirection.Right,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 700, RotationOffset = 45 },
                        new GarbusPathControlPoint { TimeOffset = 1500, RotationOffset = 90 },
                    },
                },
            }))), Is.True));
        AddStep("capture first replacement slider generation", () =>
        {
            firstReplacementNested = slider.NestedHitObjects.ToArray();
            firstReplacementNestedDisposals = disposalCountsFor(firstReplacementNested);
        });
        AddAssert("original slider generation disposed exactly once", () => originalNestedDisposals.Values, () => Is.All.EqualTo(1));
        AddAssert("retained slider root remains alive", () => sliderDisposals.Values, () => Is.All.Zero);
        AddAssert("first replacement slider generation remains alive", () => firstReplacementNested.All(d => !isDisposed(d)));

        AddStep("upsert second judged slider rebuild", () => Assert.That(preview.Apply(upsertBatch(
            4,
            7,
            GarbusChartCloner.CloneHitObject(new SliderBody
            {
                StartTime = 1200,
                AngleDeg = 90,
                Side = HorizontalDirection.Left,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 700, RotationOffset = 45 },
                        new GarbusPathControlPoint { TimeOffset = 1400, RotationOffset = 90 },
                    },
                },
            }))), Is.True));
        AddUntilStep("current slider result tree loads and catches up", () =>
            withNested(slider).Count() == 4
            && withNested(slider).All(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture current slider result tree", () =>
        {
            currentTree = withNested(slider).ToArray();
            currentResults = currentTree.Select(d => d.Result).ToArray();
            judgedResultsBeforeRewind = currentTree.Where(d => d.Judged).Select(d => d.Result).ToArray();
            updatedTimes = currentTree.Select(d => d.HitObject.GetEndTime()).ToArray();

            Assert.That(preview.DrawableForTests(new PreviewObjectId(7)), Is.SameAs(slider));
            Assert.That(slider.Result, Is.Not.SameAs(rootResult));
            Assert.That(originalNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(firstReplacementNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(originalNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(firstReplacementNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(sliderDisposals.Values, Is.All.Zero);
            Assert.That(currentTree.All(d => !isDisposed(d)), Is.True);
            Assert.That(currentTree, Has.Length.EqualTo(4));
            Assert.That(judgedResultsBeforeRewind, Is.EqualTo(currentResults));
            Assert.That(currentTree.Select(d => d.Result.RawTime), Is.EqualTo(updatedTimes));

            preview.PlayfieldForTests.ResultReverted += revertedResults.Add;
            preview.PlayfieldForTests.NewResult += (drawable, result) => replayedResults.Add((drawable, result));
        });

        AddStep("rewind before current slider tree", () => preview.Apply(transportBatch(5, 900, false, 1, 0)));
        AddUntilStep("current slider tree rewinds", () =>
            preview.ClockTimeForTests == 900
            && currentTree.All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddAssert("only judged current slider results revert", () => revertedResults.Count, () => Is.EqualTo(judgedResultsBeforeRewind.Length));
        AddAssert("judged current slider result identities revert once", () => revertedResults, () => Is.EquivalentTo(judgedResultsBeforeRewind));
        AddAssert("current slider nested objects are idle and unjudged", () =>
            currentTree.Skip(1).All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddUntilStep("current slider nested objects load idle", () => currentTree.Skip(1).All(d => d.IsLoaded && !d.Judged));

        AddStep("replay current slider result tree", () => preview.Apply(transportBatch(6, 2600, false, 1, 0)));
        AddUntilStep("current slider tree reapplies", () =>
            currentTree.All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddWaitStep("hold after current slider replay", 3);
        AddAssert("only current slider results reapply", () => replayedResults.Count, () => Is.EqualTo(currentResults.Length));
        AddAssert("current slider result identities reapply once", () => replayedResults.Select(e => e.Result), () => Is.EquivalentTo(currentResults));
        AddAssert("current slider results reapply at updated exact times", () =>
            currentTree.Select(d => d.Result.RawTime), () => Is.EqualTo(updatedTimes));
    }

}
