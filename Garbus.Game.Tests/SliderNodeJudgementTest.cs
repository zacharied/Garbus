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
