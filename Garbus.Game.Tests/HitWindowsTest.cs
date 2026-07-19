using Garbus.Game.Gameplay.Scoring;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class HitWindowsTest
    {
        /// <summary>
        /// A minimal asymmetric window set: Perfect ±50, Great ±100, Miss early-only 200.
        /// </summary>
        private class TestWindows : HitWindows
        {
            public override bool IsHitResultAllowed(HitResult result)
                => result is HitResult.Perfect or HitResult.Great or HitResult.Miss;

            public override HitWindowRange WindowFor(HitResult result) => result switch
            {
                HitResult.Perfect => HitWindowRange.Symmetric(50),
                HitResult.Great => HitWindowRange.Symmetric(100),
                HitResult.Miss => new HitWindowRange(200, 0),
                _ => default,
            };
        }

        [Test]
        public void ResultForIsSignAwareAndNested()
        {
            var windows = new TestWindows();

            Assert.That(windows.ResultFor(0), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(50), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(-50), Is.EqualTo(HitResult.Perfect));
            Assert.That(windows.ResultFor(51), Is.EqualTo(HitResult.Great));
            Assert.That(windows.ResultFor(100), Is.EqualTo(HitResult.Great));
            Assert.That(windows.ResultFor(-100), Is.EqualTo(HitResult.Great));
        }

        [Test]
        public void EarlyOnlyMissWindowHasNoLateSide()
        {
            var windows = new TestWindows();

            // Early side: outside Great (100) but inside the early-miss extent (200) -> Miss.
            Assert.That(windows.ResultFor(-101), Is.EqualTo(HitResult.Miss));
            Assert.That(windows.ResultFor(-200), Is.EqualTo(HitResult.Miss));
            // Beyond the early-miss extent -> no interaction at all.
            Assert.That(windows.ResultFor(-201), Is.EqualTo(HitResult.None));
            // Late side: past Great there is NO Miss window -> no interaction.
            Assert.That(windows.ResultFor(101), Is.EqualTo(HitResult.None));
        }

        [Test]
        public void LateEligibilityEdgeIsLatestNonMissLateExtent()
        {
            var windows = new TestWindows();

            Assert.That(windows.LateEligibilityEdge, Is.EqualTo(100));
            Assert.That(windows.CanBeHit(100), Is.True);
            Assert.That(windows.CanBeHit(101), Is.False);
        }
    }
}
