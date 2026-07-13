using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class HitsoundFamilyTest
    {
        private static HitSampleInfo sample(string name) => new HitSampleInfo(name);

        [Test]
        public void SingleTopEntryResolvesEveryHitJudgement()
        {
            var perfect = sample("perfect");
            var family = HitsoundFamily.Single(perfect); // keyed at Perfect

            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(perfect));
            Assert.That(family.Resolve(HitResult.Great), Is.EqualTo(perfect));
            Assert.That(family.Resolve(HitResult.Meh), Is.EqualTo(perfect));
        }

        [Test]
        public void MidLadderEntryIsReachedFromBothSides()
        {
            var good = sample("good");
            var family = new HitsoundFamily { [HitResult.Good] = good };

            // worse-than-Good earned -> walk up to Good
            Assert.That(family.Resolve(HitResult.Meh), Is.EqualTo(good));
            // better-than-Good earned, nothing better defined -> fall down to Good
            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(good));
        }

        [Test]
        public void BetterSideIsPreferredOverWorseSide()
        {
            var perfect = sample("perfect");
            var meh = sample("meh");
            var family = new HitsoundFamily
            {
                [HitResult.Perfect] = perfect,
                [HitResult.Meh] = meh,
            };

            // Good is between the two; better-first prefers Perfect.
            Assert.That(family.Resolve(HitResult.Good), Is.EqualTo(perfect));
        }

        [Test]
        public void EmptyFamilyResolvesToNull()
        {
            var family = new HitsoundFamily();
            Assert.That(family.Resolve(HitResult.Perfect), Is.Null);
        }

        [Test]
        public void AllSamplesReturnsDistinctMembers()
        {
            var a = sample("a");
            var family = new HitsoundFamily
            {
                [HitResult.Perfect] = a,
                [HitResult.Great] = a,
            };

            Assert.That(family.AllSamples, Is.EquivalentTo(new[] { a }));
        }
    }
}
