using System.Collections.Generic;
using Garbus.Game.Edit;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestMultiValue
    {
        [Test]
        public void AllAgree_NotMixed_SharedValue()
        {
            var result = MultiValue.Aggregate(new[] { 5, 5, 5 }, x => x);
            Assert.That(result.IsMixed, Is.False);
            Assert.That(result.Value, Is.EqualTo(5));
        }

        [Test]
        public void Differing_IsMixed()
        {
            var result = MultiValue.Aggregate(new[] { 5, 6, 5 }, x => x);
            Assert.That(result.IsMixed, Is.True);
        }

        [Test]
        public void SingleElement_NotMixed()
        {
            var result = MultiValue.Aggregate(new[] { 42 }, x => x);
            Assert.That(result.IsMixed, Is.False);
            Assert.That(result.Value, Is.EqualTo(42));
        }

        [Test]
        public void ProjectsThroughGetter()
        {
            var result = MultiValue.Aggregate(new[] { (a: 1, b: 9), (a: 2, b: 9) }, t => t.b);
            Assert.That(result.IsMixed, Is.False);
            Assert.That(result.Value, Is.EqualTo(9));
        }
    }
}
