using Garbus.Game.Screens;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// Hosts the settings gear + overlay above the screen stack. The gear is shown (and toggling
    /// enabled) only when the current screen implements <see cref="IAllowSettings"/>. Toggled by the
    /// gear, the Escape key, or gamepad button 9.
    /// </summary>
    public partial class GlobalSettingsContainer : CompositeDrawable
    {
        /// <summary>The gamepad button which toggles settings — button 9 on the target controller.</summary>
        private const JoystickButton toggle_button = JoystickButton.Button9;

        private readonly ScreenStack screenStack;

        private SettingsGearButton gear = null!;
        private SettingsOverlay overlay = null!;

        private IScreen? lastScreen;

        public GlobalSettingsContainer(ScreenStack screenStack)
        {
            this.screenStack = screenStack;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                overlay = new SettingsOverlay(),
                gear = new SettingsGearButton
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(10, 10),
                    Alpha = 0,
                    Action = toggle,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            var current = screenStack.CurrentScreen;
            if (ReferenceEquals(current, lastScreen))
                return;

            lastScreen = current;

            bool allowed = current is IAllowSettings;
            gear.FadeTo(allowed ? 1 : 0, 150, Easing.OutQuint);

            if (!allowed)
                overlay.Hide();
        }

        private void toggle()
        {
            if (screenStack.CurrentScreen is IAllowSettings)
                overlay.ToggleVisibility();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape && overlay.State.Value == Visibility.Visible)
            {
                overlay.Hide();
                return true;
            }

            return base.OnKeyDown(e);
        }

        protected override bool OnJoystickPress(JoystickPressEvent e)
        {
            if (e.Button == toggle_button)
            {
                toggle();
                return true;
            }

            return base.OnJoystickPress(e);
        }
    }
}
