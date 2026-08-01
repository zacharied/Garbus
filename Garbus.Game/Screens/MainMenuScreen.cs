// Main menu — entry point for the game. Three actions: Play, New Chart, Open Chart.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Dialogs;
using Garbus.Game.Screens.SongSelect;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Screens
{
    public partial class MainMenuScreen : Screen, IAllowSettings
    {
        // The menu buttons carry no highlight of their own, so gamepad navigation drives an explicit
        // selection tint: the unselected fill matches BasicButton's default dark background, the selected
        // fill is a brighter blue so the current d-pad target reads at a glance.
        private static readonly Color4 button_unselected = new Color4(51, 51, 51, 255);
        private static readonly Color4 button_selected = new Color4(80, 110, 200, 255);

        private Container dialogOverlay = null!;
        private BasicButton[] menuButtons = Array.Empty<BasicButton>();
        private int selectedIndex;

        /// <summary>The index of the currently gamepad-selected menu button. Exposed for tests.</summary>
        public int SelectedIndex => selectedIndex;

        /// <summary>The number of navigable menu buttons. Exposed for tests.</summary>
        public int MenuButtonCount => menuButtons.Length;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            RelativeSizeAxes = Axes.Both;

            var play = new BasicButton
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = "Play",
                Size = new Vector2(200, 40),
                Action = onPlay,
            };

            var newChart = new BasicButton
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = "New Song",
                Size = new Vector2(200, 40),
                Action = onNewChart,
            };

            var openChart = new BasicButton
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = "Open Song",
                Size = new Vector2(200, 40),
                Action = onOpenChart,
            };

            menuButtons = new[] { play, newChart, openChart };

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(18, 18, 26, 255),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "Garbus",
                            Font = FontUsage.Default.With(size: 48),
                            Colour = Color4.White,
                        },
                        play,
                        newChart,
                        openChart,
                    },
                },
                dialogOverlay = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };

            updateSelection();
        }

        // Gamepad: d-pad up/down move the highlight, face button south (A / Joystick2) activates the
        // highlighted item. The controller is opened as an SDL gamepad, so the d-pad arrives as
        // JoystickHat1 (GamePadDPad*) and A as Button2 (GamePadA); see GarbusInputManager.DefaultBindings.
        protected override bool OnJoystickPress(JoystickPressEvent e)
        {
            // A modal dialog (e.g. Open Song's file picker) owns input while visible; the dim box it draws
            // only blocks positional input, so guard non-positional joystick presses here explicitly.
            if (isDialogOpen)
                return base.OnJoystickPress(e);

            switch (e.Button)
            {
                case JoystickButton.GamePadDPadDown:
                    moveSelection(1);
                    return true;

                case JoystickButton.GamePadDPadUp:
                    moveSelection(-1);
                    return true;

                case JoystickButton.GamePadA:
                    menuButtons[selectedIndex].TriggerClick();
                    return true;
            }

            return base.OnJoystickPress(e);
        }

        private bool isDialogOpen =>
            dialogOverlay.Children.OfType<VisibilityContainer>().Any(d => d.State.Value == Visibility.Visible);

        private void moveSelection(int delta)
        {
            if (menuButtons.Length == 0)
                return;

            selectedIndex = Math.Clamp(selectedIndex + delta, 0, menuButtons.Length - 1);
            updateSelection();
        }

        private void updateSelection()
        {
            for (int i = 0; i < menuButtons.Length; i++)
                menuButtons[i].BackgroundColour = i == selectedIndex ? button_selected : button_unselected;
        }

        private void onPlay()
        {
            this.Push(new SongSelectScreen());
        }

        private void onNewChart()
        {
            this.Push(new GarbusEditor(new SongFile(GarbusSong.CreateDefault())));
        }

        private void onOpenChart()
        {
            var dialog = new FileSelectDialog(new[] { ".garbus" }, "Open", path =>
            {
                try
                {
                    this.Push(new GarbusEditor(SongFile.Load(path)));
                }
                catch (Exception ex)
                {
                    showError($"Failed to open song:\n{ex.Message}");
                }
            });

            dialogOverlay.Child = dialog;
            dialog.Show();
        }

        private void showError(string message)
        {
            var dialog = new ConfirmDialog(message, ("OK", () => { }));
            dialogOverlay.Child = dialog;
            dialog.Show();
        }
    }
}
