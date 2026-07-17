using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class GarbusSliderPathTest
    {
        [Test]
        public void StrictlyIncreasingIsValid()
        {
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 100, 200, 300 }), Is.True);
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 100, 200, 300 }), Is.True);
        }

        [Test]
        public void SingleZeroLengthLinkIsValid()
        {
            // two consecutive nodes at the same time (a mid horizontal arc), then a later node.
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 100, 100, 200 }), Is.True);
            // a trailing zero-length link (the last two nodes share a time).
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 100, 100 }), Is.True);
        }

        [Test]
        public void LoneZeroNodeIsAValidZeroDurationArc()
        {
            // child at offset 0 right after the head, followed by a real node: valid.
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 0, 200 }), Is.True);
            // a lone node at 0 (head + one node at the same instant) is a zero-duration arc: now valid.
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 0 }), Is.True);
            Assert.That(GarbusSliderPath.AreTimesValid(new double[] { 0 }), Is.True);
        }

        [Test]
        public void TwoConsecutiveZeroLengthLinksRejected()
        {
            // three nodes at one non-zero time (head→100 non-zero, then 100,100 = double zero link).
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 100, 100, 100 }), Is.False);
            // head + two children at 0 = a double zero link at the start.
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 0, 0 }), Is.False);
        }

        [Test]
        public void DecreasingRejected()
        {
            Assert.That(GarbusSliderPath.AreTimesOrdered(new double[] { 200, 100 }), Is.False);
        }

        [Test]
        public void EmptyIsNotValid()
        {
            Assert.That(GarbusSliderPath.AreTimesValid(new double[0]), Is.False);
        }
    }
}
