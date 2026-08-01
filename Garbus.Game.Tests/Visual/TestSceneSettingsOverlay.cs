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
        // Short enough that the sections overflow the panel, so the scroll behaviour is exercised
        // rather than assumed. A full-height window would leave nothing to scroll.
        private const float panel_height = 320;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private SettingsOverlay overlay = null!;
        private ManualInputManager manual = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay", () => Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = panel_height,
                Child = manual = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = overlay = new SettingsOverlay(),
                },
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

        // Located by name rather than by glyph or by type: which icon the button wears is a cosmetic
        // detail, and matching on type would mean widening a nested type's visibility for the test.
        private Drawable headerButton =>
            overlay.ChildrenOfType<Drawable>().Single(d => d.Name == SettingsPanelHeader.ActionButtonName);

        private SettingsPanelHeader header => overlay.ChildrenOfType<SettingsPanelHeader>().Single();

        // Dropdown menus carry their own scroll containers, so match on the name too.
        private BasicScrollContainer settingsScroll =>
            overlay.ChildrenOfType<BasicScrollContainer>().Single(s => s.Name == SettingsOverlay.SettingsScrollName);

        private SettingsSection section(string title) =>
            overlay.ChildrenOfType<SettingsSection>().Single(s => s.Name == title);

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
        /// The rows are grouped into the three sections, in order.
        /// </summary>
        [Test]
        public void TestRowsGroupedIntoSections()
        {
            AddStep("show", () => overlay.Show());

            AddAssert("sections in order", () =>
                overlay.ChildrenOfType<SettingsSection>().Select(s => s.Name)
                       .SequenceEqual(new[] { "Audio", "Graphics", "Gameplay" }));

            AddAssert("audio section holds the volume rows", () =>
                section("Audio").ChildrenOfType<SettingsSlider>().Select(s => s.Name)
                                .SequenceEqual(new[] { "Master volume", "Music volume", "Hitsound volume" }));

            AddAssert("gameplay section holds scroll speed", () =>
                section("Gameplay").ChildrenOfType<SettingsSlider>().Select(s => s.Name)
                                   .SequenceEqual(new[] { "Scroll speed" }));
        }

        /// <summary>
        /// The header floats: scrolling moves the rows beneath it while the header itself stays put.
        /// </summary>
        [Test]
        public void TestHeaderStaysPutWhileContentScrolls()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("panel slid in", () => headerButton.ScreenSpaceDrawQuad.TopLeft.X > 0);

            AddAssert("content overflows the panel", () => settingsScroll.ScrollableExtent > 0);

            float headerY = 0;
            float audioY = 0;

            AddStep("record positions", () =>
            {
                headerY = header.ScreenSpaceDrawQuad.TopLeft.Y;
                audioY = section("Audio").ScreenSpaceDrawQuad.TopLeft.Y;
            });

            AddStep("scroll to end", () => settingsScroll.ScrollToEnd(false));

            AddUntilStep("rows moved up", () =>
                section("Audio").ScreenSpaceDrawQuad.TopLeft.Y < audioY);

            AddAssert("header did not move", () =>
                Precision.AlmostEquals(header.ScreenSpaceDrawQuad.TopLeft.Y, headerY, 0.5f));
        }

        /// <summary>
        /// The header button dismisses the overlay from the settings view, mirroring Escape /
        /// clicking outside the panel.
        /// </summary>
        [Test]
        public void TestLeaveButtonHidesOverlay()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);

            // Wait for the slide-in to bring the button onscreen — a click that misses it would also
            // dismiss the overlay (click-outside), which must not be what this test ends up exercising.
            AddUntilStep("leave button onscreen", () => headerButton.ScreenSpaceDrawQuad.TopLeft.X > 0);

            AddStep("click leave button", () =>
            {
                manual.MoveMouseTo(headerButton);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestControlsButtonShowsRebindPanel()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("panel slid in", () => headerButton.ScreenSpaceDrawQuad.TopLeft.X > 0);
            AddAssert("no controls panel yet", () => !overlay.ChildrenOfType<ControlsPanel>().Any());

            // The Controls row sits in the last section, below the fold of the shortened panel.
            AddStep("scroll to end", () => settingsScroll.ScrollToEnd(false));

            AddStep("click Controls", () =>
            {
                var controls = overlay.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "Controls…");
                manual.MoveMouseTo(controls);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("controls panel visible", () => overlay.ChildrenOfType<ControlsPanel>().Any());
            AddUntilStep("header retargeted", () => header.Title.ToString() == "Controls");

            AddStep("click header button", () =>
            {
                manual.MoveMouseTo(headerButton);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("controls panel gone", () => !overlay.ChildrenOfType<ControlsPanel>().Any());
            AddUntilStep("header back to Settings", () => header.Title.ToString() == "Settings");
        }
    }
}
