// Tests for the shared beat-divisor colour/height palette.
using Garbus.Game.Edit;
using NUnit.Framework;
using osuTK.Graphics;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestBeatDivisorColours
    {
        [Test]
        public void TestDistinctColoursPerDivisor()
        {
            Assert.That(BeatDivisorColours.ColourFor(2), Is.Not.EqualTo(BeatDivisorColours.ColourFor(4)));
        }

        [Test]
        public void TestHeightDecreasesWithFinerDivision()
        {
            Assert.That(BeatDivisorColours.HeightFor(1), Is.EqualTo(1.0f));
            Assert.That(BeatDivisorColours.HeightFor(4), Is.LessThan(BeatDivisorColours.HeightFor(1)));
        }
    }
}
