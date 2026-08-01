// The top strip of a picker dialog: icon actions at the top-right. Mirror of DialogFooter.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osuTK;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// A fixed-height header for a modal picker, holding icon actions at the top-right. Actions flow
    /// left to right in the order they are added, so the last one added is rightmost.
    /// </summary>
    public partial class DialogHeader : Container
    {
        /// <summary>Height reserved for the header inside a dialog panel.</summary>
        public const float HEIGHT = 44;

        private readonly FillFlowContainer<DialogIconButton> actionFlow;

        private Vector2 actionSize = new Vector2(32);
        private float iconScale = 0.5f;

        public DialogHeader()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;
            RelativeSizeAxes = Axes.X;
            Height = HEIGHT;
            Padding = new MarginPadding(8);

            Child = actionFlow = new FillFlowContainer<DialogIconButton>
            {
                Name = "dialog header actions",
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
            };
        }

        /// <summary>Size of each action button. Settable so tuning scenes can drive it live.</summary>
        public Vector2 ActionSize
        {
            get => actionSize;
            set
            {
                actionSize = value;

                foreach (var button in actionFlow.Children)
                    button.Size = value;
            }
        }

        /// <summary>Gap between action buttons. Settable so tuning scenes can drive it live.</summary>
        public float ActionSpacing
        {
            get => actionFlow.Spacing.X;
            set => actionFlow.Spacing = new Vector2(value, 0);
        }

        /// <summary>
        /// Icon edge length as a fraction of the button. Settable so tuning scenes can drive it live.
        /// </summary>
        public float IconScale
        {
            get => iconScale;
            set
            {
                iconScale = value;

                foreach (var button in actionFlow.Children)
                    button.IconScale = value;
            }
        }

        /// <summary>Appends an icon action to the right-hand group.</summary>
        public DialogIconButton AddAction(string name, IconUsage icon, LocalisableString tooltip, Action action)
        {
            var button = new DialogIconButton(icon)
            {
                Name = name,
                TooltipText = tooltip,
                Size = actionSize,
                IconScale = iconScale,
                Action = action,
            };

            actionFlow.Add(button);

            return button;
        }
    }
}
