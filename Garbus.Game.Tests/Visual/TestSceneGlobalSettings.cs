using System.Linq;
using Garbus.Game.Screens;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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

        private SettingsGearButton gear => global.ChildrenOfType<SettingsGearButton>().Single();
        private SettingsOverlay overlay => global.ChildrenOfType<SettingsOverlay>().Single();

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

        [Test]
        public void TestGearClickTogglesOverlay()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("click gear", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("click gear again", () => gear.TriggerClick());
            AddUntilStep("overlay hidden", () => overlay.State.Value == Visibility.Hidden);
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
