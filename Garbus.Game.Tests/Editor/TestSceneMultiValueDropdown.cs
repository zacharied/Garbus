using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneMultiValueDropdown : GarbusTestScene
    {
        private MultiValueEnumDropdown<HorizontalDirection> dropdown = null!;
        private HorizontalDirection? lastChange;

        private void build(MultiValue<HorizontalDirection> state)
        {
            lastChange = null;
            Child = dropdown = new MultiValueEnumDropdown<HorizontalDirection>(state, v => lastChange = v)
            {
                RelativeSizeAxes = Axes.X,
            };
        }

        [Test]
        public void Mixed_SelectsNullSentinel()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddAssert("current is null", () => dropdown.Current.Value == null);
            AddAssert("items include null", () => dropdown.Items.Any(i => i == null));
        }

        [Test]
        public void Shared_SelectsValue_NoSentinel()
        {
            AddStep("build shared Right",
                () => build(new MultiValue<HorizontalDirection>(isMixed: false, HorizontalDirection.Right)));
            AddAssert("current is Right", () => dropdown.Current.Value == HorizontalDirection.Right);
            AddAssert("no null item", () => dropdown.Items.All(i => i != null));
        }

        [Test]
        public void SelectingValue_FiresOnChange()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddStep("pick Right", () => dropdown.Current.Value = HorizontalDirection.Right);
            AddAssert("onChange got Right", () => lastChange == HorizontalDirection.Right);
        }

        [Test]
        public void MixedTextConstant()
        {
            Assert.That(MultiValueEnumDropdown<HorizontalDirection>.MixedText, Is.EqualTo("<multiple>"));
        }
    }
}
