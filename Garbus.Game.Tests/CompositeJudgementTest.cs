using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Judgement;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests;

[TestFixture]
public class CompositeJudgementTest
{
    [TestCase(-201, HitResult.None)]
    [TestCase(-200, HitResult.Perfect)]
    [TestCase(200, HitResult.Perfect)]
    [TestCase(201, HitResult.Near)]
    [TestCase(300, HitResult.Near)]
    [TestCase(301, HitResult.None)]
    public void SlamWindowsAreEarlyPermissive(double offset, HitResult expected)
        => Assert.That(new SlamHitWindows().ResultFor(offset), Is.EqualTo(expected));

    [TestCase(-1, HitResult.None)]
    [TestCase(0, HitResult.Perfect)]
    [TestCase(200, HitResult.Perfect)]
    [TestCase(201, HitResult.None)]
    public void SliderCatchWindowIsLateOnly(double offset, HitResult expected)
        => Assert.That(new SliderCatchHitWindows().ResultFor(offset), Is.EqualTo(expected));

    [Test]
    public void ShapeOnlyControlPointsSpawnNoChildren()
    {
        var slider = createSlider(100, 300, 500);
        slider.Path.ControlPoints[1].ShapeOnly = true;
        slider.ApplyDefaults();

        var head = slider.NestedHitObjects.OfType<SliderHead>().Single();
        var children = slider.NestedHitObjects.OfType<SliderChild>().OrderBy(c => c.StartTime).ToArray();

        // Judged nodes are CP[0] (offset 100) and CP[2] (offset 500); CP[1] shapes only.
        Assert.That(children, Has.Length.EqualTo(2));
        Assert.That(children[0].StartTime, Is.EqualTo(1100));
        Assert.That(children[1].StartTime, Is.EqualTo(1500));

        // The head-reference chain skips the shape-only point.
        Assert.That(children[0].HeadReference, Is.SameAs(head));
        Assert.That(children[1].HeadReference, Is.SameAs(children[0]));
    }

    [Test]
    public void SliderChildrenFormAHeadReferenceChain()
    {
        var slider = createSlider(100, 300);
        slider.ApplyDefaults();

        var head = slider.NestedHitObjects.OfType<SliderHead>().Single();
        var children = slider.NestedHitObjects.OfType<SliderChild>().OrderBy(c => c.StartTime).ToArray();

        Assert.That(children[0].HeadReference, Is.SameAs(head));
        Assert.That(children[1].HeadReference, Is.SameAs(children[0]));
        Assert.That(children.All(c => c.Judgement.MaxResult == HitResult.CriticalPerfect), Is.True);
    }

    [Test]
    public void CoincidentSlamOnlyFloorsSameTimeAndSide()
    {
        var index = new SlamCoincidenceIndex();
        var slam = new GarbusSlamCentered { StartTime = 1000, AngleDeg = 0, Side = HorizontalDirection.Left };
        slam.ApplyDefaults();
        index.Add(slam);

        Assert.That(index.SlamHitAt(1000, HorizontalDirection.Left), Is.Null);
        Assert.That(index.SlamHitAt(1000, HorizontalDirection.Right), Is.False);
        Assert.That(index.SlamHitAt(1001, HorizontalDirection.Left), Is.False);

        var result = new JudgementResult(slam, slam.Judgement) { Type = HitResult.Near };
        index.Record(result);
        Assert.That(index.SlamHitAt(1000, HorizontalDirection.Left), Is.True);

        index.Revert(result);
        Assert.That(index.SlamHitAt(1000, HorizontalDirection.Left), Is.Null);
    }

    [TestCase(1000, 950, HitResult.CriticalPerfect)]
    [TestCase(1000, 900, HitResult.Perfect)]
    [TestCase(1000, 500, HitResult.Bad)]
    [TestCase(1000, 499, HitResult.Miss)]
    public void SliderSegmentUsesHoldFamilyThresholds(double duration, double activated, HitResult expected)
        => Assert.That(DurationJudgement.Resolve(duration, activated, 200, true, false, true, 0.95, 0.90, 0.50), Is.EqualTo(expected));

    [Test]
    public void SegmentStartSkipsShapeOnlyPoints()
    {
        var slider = createSlider(100, 300, 500, 700);
        slider.Path.ControlPoints[1].ShapeOnly = true;
        slider.Path.ControlPoints[2].ShapeOnly = true;
        slider.ApplyDefaults();

        var children = slider.NestedHitObjects.OfType<SliderChild>().OrderBy(c => c.StartTime).ToArray();

        // First segment: head (1000) → CP[0] (1100).
        Assert.That(slider.GetSegmentStartTime(children[0]), Is.EqualTo(1000));
        // Merged segment: CP[0] (1100) → CP[3] (1700), spanning both shape-only points.
        Assert.That(slider.GetSegmentStartTime(children[1]), Is.EqualTo(1100));
    }

    [Test]
    public void ShapeOnlyPointsStillShapeTheSweep()
    {
        var slider = createSlider(200, 400);
        slider.Path.ControlPoints[0].RotationOffset = 90;
        slider.Path.ControlPoints[0].ShapeOnly = true;
        slider.Path.ControlPoints[1].RotationOffset = 0;
        slider.ApplyDefaults();

        Assert.That(slider.AngleDegAt(1100), Is.EqualTo(45f).Within(0.001f));
        Assert.That(slider.AngleDegAt(1200), Is.EqualTo(90f).Within(0.001f));
        Assert.That(slider.AngleDegAt(1300), Is.EqualTo(45f).Within(0.001f));
    }

    private static SliderBody createSlider(params double[] offsets)
        => new()
        {
            StartTime = 1000,
            AngleDeg = 0,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>(
                    offsets.Select(offset => new GarbusPathControlPoint { TimeOffset = offset }).ToList()),
            },
        };
}
