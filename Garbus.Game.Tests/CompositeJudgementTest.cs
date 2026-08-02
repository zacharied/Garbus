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
