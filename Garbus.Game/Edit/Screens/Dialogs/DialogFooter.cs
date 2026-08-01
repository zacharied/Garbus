// The bottom strip of a picker dialog: setting checkboxes bottom-left, Cancel/Confirm bottom-right.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osuTK;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// A fixed-height footer for a modal picker. Settings stack upward from the bottom-left; the
    /// action buttons sit at the bottom-right with cancel to the left of confirm.
    /// </summary>
    public partial class DialogFooter : Container
    {
        public const string CancelButtonName = "dialog footer cancel";
        public const string ConfirmButtonName = "dialog footer confirm";
        public const string SettingsFlowName = "dialog footer settings";

        /// <summary>Height reserved for the footer inside a dialog panel.</summary>
        public const float HEIGHT = 56;

        private readonly FillFlowContainer settingsFlow;
        private readonly FillFlowContainer buttonFlow;
        private readonly BasicButton cancelButton;
        private readonly BasicButton confirmButton;

        public DialogFooter(LocalisableString confirmText, Action onConfirm, Action onCancel)
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
            Padding = new MarginPadding(8);

            Children = new Drawable[]
            {
                settingsFlow = new FillFlowContainer
                {
                    Name = SettingsFlowName,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                },
                buttonFlow = new FillFlowContainer
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new Drawable[]
                    {
                        cancelButton = new BasicButton
                        {
                            Name = CancelButtonName,
                            Text = "Cancel",
                            Size = new Vector2(100, 36),
                            Action = onCancel,
                        },
                        confirmButton = new BasicButton
                        {
                            Name = ConfirmButtonName,
                            Text = confirmText,
                            Size = new Vector2(100, 36),
                            Action = onConfirm,
                        },
                    },
                },
            };
        }

        /// <summary>Size of each action button. Settable so tuning scenes can drive it live.</summary>
        public Vector2 ButtonSize
        {
            get => confirmButton.Size;
            set => cancelButton.Size = confirmButton.Size = value;
        }

        /// <summary>Gap between the action buttons. Settable so tuning scenes can drive it live.</summary>
        public float ButtonSpacing
        {
            get => buttonFlow.Spacing.X;
            set => buttonFlow.Spacing = new Vector2(value, 0);
        }

        /// <summary>
        /// Adds a labelled checkbox to the settings column, bound two-way to <paramref name="current"/>.
        /// </summary>
        public BasicCheckbox AddSetting(string name, LocalisableString label, BindableBool current)
        {
            var checkbox = new BasicCheckbox
            {
                Name = name,
                LabelText = label,
                LabelSpacing = 6,
            };

            checkbox.Current.BindTo(current);
            settingsFlow.Add(checkbox);

            return checkbox;
        }
    }
}
