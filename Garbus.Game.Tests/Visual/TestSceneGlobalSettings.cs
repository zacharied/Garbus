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
using osu.Framework.Utils;

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
        private SettingsEnumDropdown<FrameSync> frameLimiterRow => overlay.ChildrenOfType<SettingsEnumDropdown<FrameSync>>().Single();
        private SettingsEnumDropdown<WindowMode> screenModeRow => overlay.ChildrenOfType<SettingsEnumDropdown<WindowMode>>().Single();

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
        /// An open dropdown menu behaves like a desktop combo box: it pops over the rows below it
        /// rather than growing its own row. Later rows hold their position, the open menu's bounds
        /// overlap the next row, and the settings flow draws earlier rows in front of later ones so
        /// the menu is actually visible above them.
        /// </summary>
        [Test]
        public void TestDropdownMenuPopsOverLaterRows()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("open overlay", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);

            float screenModeY = 0;
            AddStep("capture screen-mode row Y", () => screenModeY = screenModeRow.ScreenSpaceDrawQuad.TopLeft.Y);

            AddStep("open frame limiter menu", () => frameLimiterMenu.Open());
            AddUntilStep("menu open", () => frameLimiterMenu.State == MenuState.Open);

            AddUntilStep("menu overlaps screen-mode row", () =>
                frameLimiterMenu.ScreenSpaceDrawQuad.AABBFloat.IntersectsWith(screenModeRow.ScreenSpaceDrawQuad.AABBFloat));
            AddAssert("screen-mode row did not move", () =>
                Precision.AlmostEquals(screenModeY, screenModeRow.ScreenSpaceDrawQuad.TopLeft.Y));
            AddAssert("rows draw front-to-back down the panel", () =>
            {
                // Draw order in the settings flow (list order, back-to-front) must be the reverse of
                // layout order (top-to-bottom) so an open menu on any row covers everything below it.
                var rows = settingsFlow.Children.Where(c => c.IsPresent).ToList();
                var topToBottom = rows.OrderBy(r => r.ScreenSpaceDrawQuad.TopLeft.Y).ToList();
                return topToBottom.SequenceEqual(rows.AsEnumerable().Reverse());
            });
        }

        private Menu frameLimiterMenu => frameLimiter.ChildrenOfType<Menu>().Single();

        // The flow the dropdown rows actually sit in — the Graphics section's row flow, now that the
        // panel is sectioned. Taken from the row's own Parent rather than by searching for a flow
        // that Contains() it: a flow's child list is depth-sorted, so Contains() binary-searches by
        // Depth and reports a match for any child sharing that depth, not for this instance.
        private FillFlowContainer settingsFlow => (FillFlowContainer)frameLimiterRow.Parent!;

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
