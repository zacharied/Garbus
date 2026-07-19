using System;
using Garbus.Game.Gameplay.Scoring;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    /// <summary>
    /// Pins the range-based <see cref="HitResultExtensions"/> semantics against the
    /// <see cref="HitResult"/> enum being reordered — every extension here relies on the declared
    /// ordinal ladder (None &lt; Miss &lt; Bad &lt; Near &lt; Perfect &lt; CriticalPerfect &lt;
    /// IgnoreMiss &lt; IgnoreHit).
    /// </summary>
    [TestFixture]
    public class HitResultExtensionsTest
    {
        [TestCase(HitResult.Miss, true)]
        [TestCase(HitResult.Bad, true)]
        [TestCase(HitResult.Near, true)]
        [TestCase(HitResult.Perfect, true)]
        [TestCase(HitResult.CriticalPerfect, true)]
        [TestCase(HitResult.None, false)]
        [TestCase(HitResult.IgnoreMiss, false)]
        [TestCase(HitResult.IgnoreHit, false)]
        public void IsScorable(HitResult result, bool expected)
            => Assert.That(result.IsScorable(), Is.EqualTo(expected));

        [TestCase(HitResult.Miss, true)]
        [TestCase(HitResult.Bad, true)]
        [TestCase(HitResult.Near, true)]
        [TestCase(HitResult.Perfect, true)]
        [TestCase(HitResult.CriticalPerfect, true)]
        [TestCase(HitResult.None, false)]
        [TestCase(HitResult.IgnoreMiss, false)]
        [TestCase(HitResult.IgnoreHit, false)]
        public void AffectsCombo(HitResult result, bool expected)
            => Assert.That(result.AffectsCombo(), Is.EqualTo(expected));

        [TestCase(HitResult.None, false)]
        [TestCase(HitResult.Miss, false)]
        [TestCase(HitResult.IgnoreMiss, false)]
        [TestCase(HitResult.Bad, true)]
        [TestCase(HitResult.Near, true)]
        [TestCase(HitResult.Perfect, true)]
        [TestCase(HitResult.CriticalPerfect, true)]
        [TestCase(HitResult.IgnoreHit, true)]
        public void IsHit(HitResult result, bool expected)
            => Assert.That(result.IsHit(), Is.EqualTo(expected));

        [TestCase(HitResult.Miss, true)]
        [TestCase(HitResult.IgnoreMiss, true)]
        [TestCase(HitResult.None, false)]
        [TestCase(HitResult.Bad, false)]
        [TestCase(HitResult.Near, false)]
        [TestCase(HitResult.Perfect, false)]
        [TestCase(HitResult.CriticalPerfect, false)]
        [TestCase(HitResult.IgnoreHit, false)]
        public void IsMiss(HitResult result, bool expected)
            => Assert.That(result.IsMiss(), Is.EqualTo(expected));

        [TestCase(HitResult.Bad, true)]
        [TestCase(HitResult.Near, true)]
        [TestCase(HitResult.Perfect, true)]
        [TestCase(HitResult.CriticalPerfect, true)]
        [TestCase(HitResult.None, false)]
        [TestCase(HitResult.Miss, false)]
        [TestCase(HitResult.IgnoreMiss, false)]
        [TestCase(HitResult.IgnoreHit, false)]
        public void IncreasesCombo(HitResult result, bool expected)
            => Assert.That(result.IncreasesCombo(), Is.EqualTo(expected));

        [TestCase(HitResult.Miss, true)]
        [TestCase(HitResult.None, false)]
        [TestCase(HitResult.Bad, false)]
        [TestCase(HitResult.Near, false)]
        [TestCase(HitResult.Perfect, false)]
        [TestCase(HitResult.CriticalPerfect, false)]
        [TestCase(HitResult.IgnoreMiss, false)]
        [TestCase(HitResult.IgnoreHit, false)]
        public void BreaksCombo(HitResult result, bool expected)
            => Assert.That(result.BreaksCombo(), Is.EqualTo(expected));

        [Test]
        public void IsValidHitResult()
        {
            Assert.That(HitResult.Near.IsValidHitResult(HitResult.Miss, HitResult.CriticalPerfect), Is.True);
            Assert.That(HitResult.Bad.IsValidHitResult(HitResult.Miss, HitResult.Perfect), Is.True);
            Assert.That(HitResult.None.IsValidHitResult(HitResult.Miss, HitResult.CriticalPerfect), Is.False);
            Assert.That(HitResult.IgnoreHit.IsValidHitResult(HitResult.Miss, HitResult.CriticalPerfect), Is.False);
        }

        [Test]
        public void ValidateHitResultPairAcceptsValidBasicAndIgnorePairs()
        {
            Assert.DoesNotThrow(() => HitResultExtensions.ValidateHitResultPair(HitResult.CriticalPerfect, HitResult.Miss));
            Assert.DoesNotThrow(() => HitResultExtensions.ValidateHitResultPair(HitResult.Perfect, HitResult.Miss));
            Assert.DoesNotThrow(() => HitResultExtensions.ValidateHitResultPair(HitResult.IgnoreHit, HitResult.IgnoreMiss));
        }

        [Test]
        public void ValidateHitResultPairRejectsNonMissMinimumForABasicMaximum()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HitResultExtensions.ValidateHitResultPair(HitResult.CriticalPerfect, HitResult.Bad));
        }

        [Test]
        public void ValidateHitResultPairRejectsNonIgnoreMissMinimumForAnIgnoreHitMaximum()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HitResultExtensions.ValidateHitResultPair(HitResult.IgnoreHit, HitResult.Miss));
        }

        [Test]
        public void ValidateHitResultPairRejectsAMissMaximum()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                HitResultExtensions.ValidateHitResultPair(HitResult.Miss, HitResult.Miss));
        }
    }
}
