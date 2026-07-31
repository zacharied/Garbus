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
    public partial class GlobalSettingsContainer : CompositeDrawable, ISettingsOverlayControl
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

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The gear yields to the overlay it opens: it fades out while the overlay is up and
            // returns when the overlay is dismissed.
            overlay.State.BindValueChanged(_ => updateGearVisibility());
        }

        protected override void Update()
        {
            base.Update();

            var current = screenStack.CurrentScreen;
            if (ReferenceEquals(current, lastScreen))
                return;

            lastScreen = current;

            updateGearVisibility();

            if (current is not IAllowSettings)
                overlay.Hide();
        }

        /// <summary>
        /// The gear is shown only when the screen both allows settings AND wants the floating gear,
        /// and the overlay itself is closed. Screens that expose their own settings entry point
        /// (e.g. the editor's menu) opt the gear out via ShowSettingsGear while still permitting
        /// the overlay.
        /// </summary>
        private void updateGearVisibility()
        {
            bool showGear = screenStack.CurrentScreen is IAllowSettings settings
                            && settings.ShowSettingsGear
                            && overlay.State.Value == Visibility.Hidden;
            gear.FadeTo(showGear ? 1 : 0, 150, Easing.OutQuint);
        }

        private void toggle()
        {
            if (screenStack.CurrentScreen is IAllowSettings)
                overlay.ToggleVisibility();
        }

        /// <summary>
        /// <see cref="ISettingsOverlayControl.OpenSettings"/>: show the overlay if the current screen
        /// permits settings. Used by screens that trigger settings from their own chrome rather than
        /// the floating gear.
        /// </summary>
        public void OpenSettings()
        {
            if (screenStack.CurrentScreen is IAllowSettings)
                overlay.Show();
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
            if (e.Button == toggle_button && screenStack.CurrentScreen is IAllowSettings)
            {
                toggle();
                return true;
            }

            return base.OnJoystickPress(e);
        }
    }
}
