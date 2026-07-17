using System.Linq;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;
using Garbus.Game.Core;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ChordIndexTest
    {
        private static CardinalNote cardinal(double startTime, int angle) =>
            new CardinalNote { AngleDeg = angle, StartTime = startTime };

        private static CardinalHoldNote hold(double startTime, int angle, double duration = 500) =>
            new CardinalHoldNote { AngleDeg = angle, StartTime = startTime, Duration = duration };

        [Test]
        public void TwoCardinalsSameTimeAreAChord()
        {
            var a = cardinal(1000, 90);
            var b = cardinal(1000, 270);
            var index = new ChordIndex(new[] { a, b });

            Assert.That(index.IsInChord(a), Is.True);
            Assert.That(index.IsInChord(b), Is.True);
            Assert.That(index.Groups, Has.Count.EqualTo(1));
            Assert.That(index.Groups[0].Members.Select(m => m.Object), Is.EquivalentTo(new[] { a, b }));
        }

        [Test]
        public void CardinalAndHoldSameTimeGroupTogether()
        {
            var note = cardinal(1000, 0);
            var held = hold(1000, 180);
            var index = new ChordIndex(new HitObject[] { note, held });

            Assert.That(index.IsInChord(note), Is.True);
            Assert.That(index.IsInChord(held), Is.True);
            Assert.That(index.Groups, Has.Count.EqualTo(1));
        }

        [Test]
        public void LoneCardinalIsNotAChord()
        {
            var a = cardinal(1000, 90);
            var b = cardinal(2000, 90);
            var index = new ChordIndex(new[] { a, b });

            Assert.That(index.IsInChord(a), Is.False);
            Assert.That(index.IsInChord(b), Is.False);
            Assert.That(index.Groups, Is.Empty);
        }

        [Test]
        public void ArbitraryNMembersFormOneGroupSortedByAngle()
        {
            var members = new[] { cardinal(500, 200), cardinal(500, 10), cardinal(500, 300),
                                  cardinal(500, 90), cardinal(500, 45), cardinal(500, 250),
                                  cardinal(500, 150) };
            var index = new ChordIndex(members);

            Assert.That(index.Groups, Has.Count.EqualTo(1));
            var group = index.Groups[0];
            Assert.That(group.Members, Has.Count.EqualTo(7));
            Assert.That(group.Members.Select(m => m.AngleDeg), Is.Ordered);
        }

        [Test]
        public void ShoulderNoteAtSameTimeIsExcluded()
        {
            var cardinalA = cardinal(1000, 90);
            var cardinalB = cardinal(1000, 270);
            var shoulder = new ShoulderNote { Side = HorizontalDirection.Left, StartTime = 1000 };
            var index = new ChordIndex(new HitObject[] { cardinalA, cardinalB, shoulder });

            Assert.That(index.IsInChord(shoulder), Is.False);
            Assert.That(index.Groups[0].Members.Select(m => m.Object), Does.Not.Contain(shoulder));
        }
    }
}
