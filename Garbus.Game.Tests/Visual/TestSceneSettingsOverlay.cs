using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Utils;
using osuTK.Input;

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
        private ManualInputManager manual = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay", () => Child = manual = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = overlay = new SettingsOverlay(),
            });
        }

        [Test]
        public void TestShowHide()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("hide", () => overlay.Hide());
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        private SpriteIcon leaveButton =>
            overlay.ChildrenOfType<SpriteIcon>().Single(i => i.Icon.Equals(FontAwesome.Solid.SignOutAlt));

        private BasicSliderBar<double> sliderFor(string label) =>
            overlay.ChildrenOfType<SettingsSlider>().Single(s => s.Name == label)
                   .ChildrenOfType<BasicSliderBar<double>>().Single();

        [Test]
        public void TestMasterVolumeUsesLogarithmicTaper()
        {
            AddStep("show", () => overlay.Show());

            // Actual gain -> slider position: 3% gain sits at ~30% slider position.
            AddStep("set master gain 0.03", () => audio.Volume.Value = 0.03);
            AddAssert("master slider ~0.30", () =>
                Precision.AlmostEquals(sliderFor("Master volume").Current.Value, 0.30, 0.01));

            // Slider position -> actual gain: dragging to 30% outputs ~3% gain.
            AddStep("set slider position 0.30", () =>
                sliderFor("Master volume").Current.Value = 0.30);
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
            AddAssert("all three volume sliders ~0.30", () =>
                Precision.AlmostEquals(sliderFor("Master volume").Current.Value, 0.30, 0.01)
                && Precision.AlmostEquals(sliderFor("Music volume").Current.Value, 0.30, 0.01)
                && Precision.AlmostEquals(sliderFor("Hitsound volume").Current.Value, 0.30, 0.01));
        }

        [Test]
        public void TestScrollSpeedRowBoundToConfig()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set speed 15", () => config.SetValue(GarbusSetting.ScrollSpeed, 15.0));
            AddAssert("scroll speed slider tracks speed", () =>
                sliderFor("Scroll speed").Current.Value == 15.0);
        }

        /// <summary>
        /// The leave button beside the "Settings" title dismisses the overlay, mirroring Escape /
        /// clicking outside the panel.
        /// </summary>
        [Test]
        public void TestLeaveButtonHidesOverlay()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);

            // Wait for the slide-in to bring the button onscreen — a click that misses it would also
            // dismiss the overlay (click-outside), which must not be what this test ends up exercising.
            AddUntilStep("leave button onscreen", () => leaveButton.ScreenSpaceDrawQuad.TopLeft.X > 0);

            AddStep("click leave button", () =>
            {
                manual.MoveMouseTo(leaveButton);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestControlsButtonShowsRebindPanel()
        {
            AddStep("show", () => overlay.Show());
            AddAssert("no controls panel yet", () => !overlay.ChildrenOfType<ControlsPanel>().Any());

            AddStep("click Controls", () =>
            {
                var controls = overlay.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "Controls…");
                manual.MoveMouseTo(controls);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("controls panel visible", () => overlay.ChildrenOfType<ControlsPanel>().Any());

            AddStep("click Back", () =>
            {
                var back = overlay.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "‹ Back");
                manual.MoveMouseTo(back);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("controls panel gone", () => !overlay.ChildrenOfType<ControlsPanel>().Any());
        }
    }
}
