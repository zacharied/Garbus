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
        public void Mixed_SelectsMixedSentinel()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddAssert("current is Mixed", () => dropdown.Current.Value.IsMixed);
            AddAssert("items include Mixed", () => dropdown.Items.Any(i => i.IsMixed));
        }

        [Test]
        public void Shared_SelectsValue_NoSentinel()
        {
            AddStep("build shared Right",
                () => build(new MultiValue<HorizontalDirection>(isMixed: false, HorizontalDirection.Right)));
            AddAssert("current is Right",
                () => !dropdown.Current.Value.IsMixed && dropdown.Current.Value.Value == HorizontalDirection.Right);
            AddAssert("no Mixed item", () => dropdown.Items.All(i => !i.IsMixed));
        }

        [Test]
        public void SelectingValue_FiresOnChange()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddStep("pick Right",
                () => dropdown.Current.Value = new MultiValueEnumDropdown<HorizontalDirection>.Choice(HorizontalDirection.Right));
            AddAssert("onChange got Right", () => lastChange == HorizontalDirection.Right);
        }

        [Test]
        public void MixedEntryRendersAsMultipleText()
        {
            Assert.That(
                MultiValueEnumDropdown<HorizontalDirection>.FormatChoice(MultiValueEnumDropdown<HorizontalDirection>.Choice.Mixed).ToString(),
                Is.EqualTo("<multiple>"));
        }

        [Test]
        public void MixedTextConstant()
        {
            Assert.That(MultiValueEnumDropdown<HorizontalDirection>.MixedText, Is.EqualTo("<multiple>"));
        }
    }
}
