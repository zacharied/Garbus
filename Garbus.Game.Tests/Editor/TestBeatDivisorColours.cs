// Tests for the shared beat-divisor colour/height palette. The palette is hand-tuned, so exact
// values are never asserted — only the relations that must survive any retune.
using Garbus.Game.Edit;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestBeatDivisorColours
    {
        private static readonly int[] standard_divisors = { 1, 2, 3, 4, 6, 8 };

        [Test]
        public void TestDistinctColoursPerDivisor()
        {
            // Distinguishability is the invariant: every standard divisor must read as a different
            // colour from every other, whatever the palette is tuned to.
            for (int i = 0; i < standard_divisors.Length; i++)
            {
                for (int j = i + 1; j < standard_divisors.Length; j++)
                {
                    Assert.That(BeatDivisorColours.ColourFor(standard_divisors[i]),
                        Is.Not.EqualTo(BeatDivisorColours.ColourFor(standard_divisors[j])),
                        $"Divisors {standard_divisors[i]} and {standard_divisors[j]} must have distinct colours");
                }
            }
        }

        [Test]
        public void TestHeightDecreasesWithFinerDivision()
        {
            // Finer subdivisions render strictly shorter ticks — an ordering, not any exact height.
            for (int i = 1; i < standard_divisors.Length; i++)
            {
                Assert.That(BeatDivisorColours.HeightFor(standard_divisors[i]),
                    Is.LessThan(BeatDivisorColours.HeightFor(standard_divisors[i - 1])),
                    $"HeightFor({standard_divisors[i]}) must be shorter than HeightFor({standard_divisors[i - 1]})");
            }
        }
    }
}
