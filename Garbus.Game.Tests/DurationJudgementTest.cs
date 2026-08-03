using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;
using NUnit.Framework;

namespace Garbus.Game.Tests;

[TestFixture]
public class DurationJudgementTest
{
    // Hold thresholds are hand-anchored to docs/rules-specs/Judgement.md -> HoldNote -> Duration:
    // Critical Perfect 100%, Perfect 95%, Bad 60%, Miss 0%.
    [TestCase(1000, 1000, true, true, HitResult.CriticalPerfect)]
    [TestCase(1000, 950, true, true, HitResult.Perfect)]
    [TestCase(1000, 600, true, true, HitResult.Bad)]
    [TestCase(1000, 599, false, true, HitResult.Miss)]
    [TestCase(1000, 1000, false, false, HitResult.Perfect)]
    public void HoldThresholdsAndEndingGrace(
        double duration,
        double activated,
        bool activeAtEnd,
        bool activeInEndingGrace,
        HitResult expected)
    {
        Assert.That(resolveHold(duration, activated, activeAtEnd, activeInEndingGrace), Is.EqualTo(expected));
    }

    [Test]
    public void ActivationAtEndFloorsAMissToBad()
        => Assert.That(resolveHold(1000, 0, true, true), Is.EqualTo(HitResult.Bad));

    [Test]
    public void ShortDurationsGetNoSpecialCase()
    {
        // A 109 ms hold with no credited activation is a Miss like any other, rather than inheriting a hit head.
        Assert.That(resolveHold(109, 0, false, false), Is.EqualTo(HitResult.Miss));
        // Credited in full, it takes the best judgement on its own merits.
        Assert.That(resolveHold(109, 109, true, true), Is.EqualTo(HitResult.CriticalPerfect));
    }

    // The grace period is credited only when the object was Activated inside it
    // (docs/rules-specs/Judgement.md -> Duration -> Grace period).
    [Test]
    public void GraceIsCreditedOnlyWhenActivatedWithinIt()
    {
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, true, 0), Is.EqualTo(200));
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, false, 0), Is.EqualTo(0));
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, true, 750), Is.EqualTo(950));
        Assert.That(DurationJudgement.CreditedActivation(1000, 200, false, 750), Is.EqualTo(750));
    }

    [Test]
    public void GraceIsCappedAtTheDuration()
        => Assert.That(DurationJudgement.CreditedActivation(150, 200, true, 0), Is.EqualTo(150));

    [Test]
    public void AnUntouchedShortSegmentIsAMiss()
    {
        // A 250 ms slider segment with no input at all: no grace credit, so nothing to grade.
        // Slider thresholds are hand-anchored to docs/rules-specs/Judgement.md -> Slider -> Duration.
        double credited = DurationJudgement.CreditedActivation(250, 200, false, 0);

        Assert.That(DurationJudgement.Resolve(250, credited, false, false, 0.95, 0.90, 0.50),
                    Is.EqualTo(HitResult.Miss));
    }

    [Test]
    public void ATouchedShortSegmentTakesTheBestJudgement()
    {
        // The same segment, entered 50 ms in and held: 200 ms grace covers the whole opening, and the
        // remaining 50 ms is real activation.
        double credited = DurationJudgement.CreditedActivation(250, 200, true, 50);

        Assert.That(DurationJudgement.Resolve(250, credited, true, true, 0.95, 0.90, 0.50),
                    Is.EqualTo(HitResult.CriticalPerfect));
    }

    private static HitResult resolveHold(double duration, double activated, bool activeAtEnd, bool activeInEndingGrace)
        => DurationJudgement.Resolve(duration, activated, activeAtEnd, activeInEndingGrace, 1, 0.95, 0.60);
}
