using System.Linq;
using Garbus.Game.Screens;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGlobalSettings : GarbusTestScene
    {
        private ScreenStack stack = null!;
        private GlobalSettingsContainer global = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        private SettingsGearButton gear => global.ChildrenOfType<SettingsGearButton>().Single();
        private SettingsOverlay overlay => global.ChildrenOfType<SettingsOverlay>().Single();
        private BasicDropdown<FrameSync> frameLimiter => overlay.ChildrenOfType<BasicDropdown<FrameSync>>().Single();
        private BasicDropdown<WindowMode> screenMode => overlay.ChildrenOfType<BasicDropdown<WindowMode>>().Single();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create host", () => Children = new Drawable[]
            {
                stack = new ScreenStack { RelativeSizeAxes = Axes.Both },
                global = new GlobalSettingsContainer(stack) { RelativeSizeAxes = Axes.Both },
            });
        }

        [Test]
        public void TestGearGatedByScreen()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("push disallowed screen", () => stack.Push(new DisallowedScreen()));
            AddUntilStep("gear hidden", () => gear.Alpha == 0);
        }

        /// <summary>
        /// The gear opens the overlay and yields to it: it fades out while the overlay is up (the
        /// overlay owns dismissal via its leave button / Escape) and returns once the overlay closes.
        /// </summary>
        [Test]
        public void TestGearOpensOverlayAndYieldsToIt()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("click gear", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddUntilStep("gear hidden while overlay open", () => gear.Alpha == 0);
            AddStep("close overlay", () => overlay.Hide());
            AddUntilStep("overlay hidden", () => overlay.State.Value == Visibility.Hidden);
            AddUntilStep("gear returns", () => gear.Alpha == 1);
        }

        [Test]
        public void TestButton9TogglesOverlay()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("press button 9", () =>
                global.TriggerEvent(new JoystickPressEvent(new InputState(), JoystickButton.Button9)));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestDisallowedScreenForcesOverlayClosed()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("open overlay", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("push disallowed screen", () => stack.Push(new DisallowedScreen()));
            AddUntilStep("overlay force-closed", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestFrameLimiterDropdownDrivesConfig()
        {
            AddStep("set frame sync to 2x", () => frameworkConfig.SetValue(FrameworkSetting.FrameSync, FrameSync.Limit2x));
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("open overlay", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddStep("select unlimited", () => frameLimiter.Current.Value = FrameSync.Unlimited);
            AddAssert("config updated", () => frameworkConfig.Get<FrameSync>(FrameworkSetting.FrameSync) == FrameSync.Unlimited);
        }

        /// <summary>
        /// The screen-mode row offers exclusive <see cref="WindowMode.Fullscreen"/> alongside
        /// <see cref="WindowMode.Borderless"/>, and drives the same
        /// <see cref="FrameworkSetting.WindowMode"/> bindable that Alt+Enter cycles.
        /// </summary>
        [Test]
        public void TestScreenModeDropdownDrivesConfig()
        {
            AddStep("set windowed", () => frameworkConfig.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed));
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("open overlay", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            AddAssert("offers exclusive fullscreen", () => screenMode.Items.Contains(WindowMode.Fullscreen));
            AddAssert("offers borderless", () => screenMode.Items.Contains(WindowMode.Borderless));

            AddStep("select fullscreen", () => screenMode.Current.Value = WindowMode.Fullscreen);
            AddAssert("config updated", () => frameworkConfig.Get<WindowMode>(FrameworkSetting.WindowMode) == WindowMode.Fullscreen);
        }

        /// <summary>
        /// A screen that allows settings but sets <see cref="IAllowSettings.ShowSettingsGear"/> to
        /// false hides the floating gear, yet the overlay can still be opened programmatically (via
        /// <see cref="ISettingsOverlayControl.OpenSettings"/>) and is NOT force-closed on that screen.
        /// </summary>
        [Test]
        public void TestGearlessScreenHidesGearButAllowsOverlay()
        {
            AddStep("push gearless screen", () => stack.Push(new GearlessScreen()));
            AddUntilStep("gear hidden", () => gear.Alpha == 0);
            AddStep("open settings programmatically", () => global.OpenSettings());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            // Stays open across frames — the screen permits settings, so Update() must not force-close it.
            AddWaitStep("wait a few frames", 5);
            AddAssert("overlay still visible", () => overlay.State.Value == Visibility.Visible);
        }

        private partial class AllowedScreen : Screen, IAllowSettings
        {
        }

        private partial class GearlessScreen : Screen, IAllowSettings
        {
            bool IAllowSettings.ShowSettingsGear => false;
        }

        private partial class DisallowedScreen : Screen
        {
        }
    }
}
