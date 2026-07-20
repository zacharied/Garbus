using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;
using NUnit.Framework;

namespace Garbus.Game.Tests;

[TestFixture]
public class DurationJudgementTest
{
    [TestCase(1000, 1000, true, true, true, HitResult.CriticalPerfect)]
    [TestCase(1000, 950, true, true, true, HitResult.Perfect)]
    [TestCase(1000, 600, true, true, true, HitResult.Bad)]
    [TestCase(1000, 599, true, false, true, HitResult.Miss)]
    [TestCase(1000, 1000, true, false, false, HitResult.Perfect)]
    public void HoldThresholdsAndEndingGrace(
        double duration,
        double activated,
        bool headHit,
        bool activeAtEnd,
        bool activeInEndingGrace,
        HitResult expected)
    {
        Assert.That(resolveHold(duration, activated, headHit, activeAtEnd, activeInEndingGrace), Is.EqualTo(expected));
    }

    [TestCase(true, false, HitResult.CriticalPerfect)]
    [TestCase(false, false, HitResult.Miss)]
    [TestCase(false, true, HitResult.Bad)]
    public void ShortHoldResolvesFromHeadHitAndEndFloor(bool headHit, bool activeAtEnd, HitResult expected)
    {
        Assert.That(resolveHold(109, 0, headHit, activeAtEnd, activeAtEnd), Is.EqualTo(expected));
    }

    [Test]
    public void DurationEqualToHeadWindowUsesActivationProportion()
    {
        Assert.That(resolveHold(110, 0, true, false, false), Is.EqualTo(HitResult.Miss));
    }

    private static HitResult resolveHold(double duration, double activated, bool headHit, bool activeAtEnd, bool activeInEndingGrace)
        => DurationJudgement.Resolve(duration, activated, 110, headHit, activeAtEnd, activeInEndingGrace, 1, 0.95, 0.60);
}
