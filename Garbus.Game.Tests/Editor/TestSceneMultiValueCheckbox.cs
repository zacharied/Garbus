using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestSceneMultiValueCheckbox
    {
        [Test]
        public void Mixed_MapsToIndeterminate()
        {
            var cb = new MultiValueCheckbox("On", new MultiValue<bool>(isMixed: true, default), _ => { });
            Assert.That(cb.State, Is.EqualTo(TernaryState.Indeterminate));
        }

        [Test]
        public void True_MapsToTrue()
        {
            var cb = new MultiValueCheckbox("On", new MultiValue<bool>(isMixed: false, true), _ => { });
            Assert.That(cb.State, Is.EqualTo(TernaryState.True));
        }

        [Test]
        public void False_MapsToFalse()
        {
            var cb = new MultiValueCheckbox("On", new MultiValue<bool>(isMixed: false, false), _ => { });
            Assert.That(cb.State, Is.EqualTo(TernaryState.False));
        }

        [Test]
        public void NextValue_TrueGoesFalse_OthersGoTrue()
        {
            Assert.That(MultiValueCheckbox.NextValue(TernaryState.True), Is.False);
            Assert.That(MultiValueCheckbox.NextValue(TernaryState.False), Is.True);
            Assert.That(MultiValueCheckbox.NextValue(TernaryState.Indeterminate), Is.True);
        }
    }
}
