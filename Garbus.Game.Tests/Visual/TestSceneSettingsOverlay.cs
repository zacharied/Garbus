using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSettingsOverlay : GarbusTestScene
    {
        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private SettingsOverlay overlay = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay());
        }

        [Test]
        public void TestShowHide()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("hide", () => overlay.Hide());
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestVolumeRowBoundToMaster()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set master 0.3", () => audio.Volume.Value = 0.3);
            AddAssert("first slider tracks master", () =>
                overlay.ChildrenOfType<BasicSliderBar<double>>().ElementAt(0).Current.Value == 0.3);
        }

        [Test]
        public void TestScrollSpeedRowBoundToConfig()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set speed 15", () => config.SetValue(GarbusSetting.ScrollSpeed, 15.0));
            AddAssert("last slider tracks speed", () =>
                overlay.ChildrenOfType<BasicSliderBar<double>>().ElementAt(3).Current.Value == 15.0);
        }
    }
}
