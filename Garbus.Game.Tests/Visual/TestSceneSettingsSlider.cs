using System.Linq;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSettingsSlider : GarbusTestScene
    {
        [Test]
        public void TestSliderTracksBindable()
        {
            var value = new BindableDouble(0.5) { MinValue = 0, MaxValue = 1 };
            SettingsSlider slider = null!;

            AddStep("create slider", () => Child = new Container
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Child = slider = new SettingsSlider("Test", value, v => v.ToString("0.00")),
            });

            AddStep("set bindable to 0.8", () => value.Value = 0.8);
            AddAssert("slider current updated", () =>
                slider.ChildrenOfType<BasicSliderBar<double>>().Single().Current.Value == 0.8);
        }
    }
}
