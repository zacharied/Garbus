using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ChordHighlighterTest
    {
        private static CardinalNote cardinal(double startTime, int angle) =>
            new CardinalNote { AngleDeg = angle, StartTime = startTime };

        [Test]
        public void RebuildReflectsCurrentObjects()
        {
            var highlighter = new ChordHighlighter();
            var a = cardinal(1000, 90);
            var b = cardinal(1000, 270);

            Assert.That(highlighter.IsInChord(a), Is.False, "empty before rebuild");

            highlighter.Rebuild(new[] { a, b });
            Assert.That(highlighter.IsInChord(a), Is.True);
            Assert.That(highlighter.Groups, Has.Count.EqualTo(1));

            // Move b off a's time: the chord dissolves after the next rebuild.
            b.StartTime = 2000;
            highlighter.Rebuild(new[] { a, b });
            Assert.That(highlighter.IsInChord(a), Is.False);
            Assert.That(highlighter.Groups, Is.Empty);
        }

        [Test]
        public void RebuildNotifiesAfterPublishingNewIndex()
        {
            var highlighter = new ChordHighlighter();
            var a = cardinal(1000, 90);
            var b = cardinal(1000, 270);
            var observedGroupCounts = new List<int>();

            highlighter.IndexChanged += () => observedGroupCounts.Add(highlighter.Groups.Count);

            highlighter.Rebuild(new[] { a, b });
            b.StartTime = 2000;
            highlighter.Rebuild(new[] { a, b });

            Assert.That(observedGroupCounts, Is.EqualTo(new[] { 1, 0 }));
        }
    }
}
