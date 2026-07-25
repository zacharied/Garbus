using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
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

        AddStep("rewind below slider end boundary", () => preview.Apply(transportBatch(3, 2999, false, 1, 0)));
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
            Garbus.Game.Charts.Format.GarbusChartSerializer.EncodeHitObject(
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
}
