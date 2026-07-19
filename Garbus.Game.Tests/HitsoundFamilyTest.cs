using System.Linq;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class HitsoundFamilyTest
    {
        private static GarbusHitSample sample(string name) => new GarbusHitSample(name);

        [Test]
        public void SingleTopEntryResolvesEveryHitJudgement()
        {
            var best = sample("best");
            var family = HitsoundFamily.Single(best); // keyed at CriticalPerfect

            Assert.That(family.Resolve(HitResult.CriticalPerfect), Is.EqualTo(best));
            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(best));
            Assert.That(family.Resolve(HitResult.Bad), Is.EqualTo(best));
        }

        [Test]
        public void MidLadderEntryIsReachedFromBothSides()
        {
            var near = sample("near");
            var family = new HitsoundFamily { [HitResult.Near] = near };

            // worse-than-Near earned -> walk up to Near
            Assert.That(family.Resolve(HitResult.Bad), Is.EqualTo(near));
            // better-than-Near earned, nothing better defined -> fall down to Near
            Assert.That(family.Resolve(HitResult.CriticalPerfect), Is.EqualTo(near));
        }

        [Test]
        public void BetterSideIsPreferredOverWorseSide()
        {
            var best = sample("best");
            var bad = sample("bad");
            var family = new HitsoundFamily
            {
                [HitResult.CriticalPerfect] = best,
                [HitResult.Bad] = bad,
            };

            // Perfect is between the two; better-first prefers CriticalPerfect.
            Assert.That(family.Resolve(HitResult.Perfect), Is.EqualTo(best));
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
                [HitResult.CriticalPerfect] = a,
                [HitResult.Perfect] = a,
            };

            Assert.That(family.AllSamples, Is.EquivalentTo(new[] { a }));
        }

        [Test]
        public void ConcreteTypesSeedSamplesFromTheirFamily()
        {
            var note = new Garbus.Game.Objects.CardinalNote { AngleDeg = 0 };
            note.ApplyDefaults();

            var expected = HitsoundFamilies.CardinalNote.AllSamples.ToArray();

            Assert.That(note.Samples, Is.EquivalentTo(expected));
            Assert.That(note.Hitsounds.Resolve(HitResult.Bad), Is.Not.Null);
        }
    }
}
