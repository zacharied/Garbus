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
using osu.Framework.Utils;

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
        public void TestMasterVolumeUsesLogarithmicTaper()
        {
            AddStep("show", () => overlay.Show());

            // Actual gain -> slider position: 3% gain sits at ~30% slider position.
            AddStep("set master gain 0.03", () => audio.Volume.Value = 0.03);
            AddAssert("first slider ~0.30", () =>
                Precision.AlmostEquals(overlay.ChildrenOfType<BasicSliderBar<double>>().ElementAt(0).Current.Value, 0.30, 0.01));

            // Slider position -> actual gain: dragging to 30% outputs ~3% gain.
            AddStep("set slider position 0.30", () =>
                overlay.ChildrenOfType<BasicSliderBar<double>>().ElementAt(0).Current.Value = 0.30);
            AddAssert("master gain ~0.03", () => Precision.AlmostEquals(audio.Volume.Value, 0.03, 0.01));
        }

        [Test]
        public void TestAllVolumeRowsUseTaper()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set all gains 0.03", () =>
            {
                audio.Volume.Value = 0.03;
                audio.VolumeTrack.Value = 0.03;
                audio.VolumeSample.Value = 0.03;
            });
            AddAssert("all three sliders ~0.30", () =>
            {
                var bars = overlay.ChildrenOfType<BasicSliderBar<double>>().ToList();
                return Precision.AlmostEquals(bars[0].Current.Value, 0.30, 0.01)
                       && Precision.AlmostEquals(bars[1].Current.Value, 0.30, 0.01)
                       && Precision.AlmostEquals(bars[2].Current.Value, 0.30, 0.01);
            });
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
