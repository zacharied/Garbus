// Modal overlay for confirmation dialogs. Reusable: pass any message + button set.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// A modal overlay that blocks input behind a dim and shows a message with configurable buttons.
    /// </summary>
    public partial class ConfirmDialog : VisibilityContainer
    {
        private readonly Container panel;

        public ConfirmDialog(string message, params (string label, Action action)[] buttons)
        {
            RelativeSizeAxes = Axes.Both;

            // dim background
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
                Alpha = 0.5f,
            });

            var buttonRow = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                // Must use the same anchor as the message text (TopCentre = Y:0) to satisfy
                // FillFlowContainer's requirement that all children share the same
                // RelativeAnchorPosition for the flow direction.
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
            };

            foreach (var (label, action) in buttons)
            {
                var capturedAction = action;
                var capturedLabel = label;
                buttonRow.Add(new BasicButton
                {
                    Text = capturedLabel,
                    Size = new Vector2(100, 30),
                    Action = () =>
                    {
                        Hide();
                        capturedAction();
                    },
                });
            }

            AddInternal(panel = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 400,
                AutoSizeAxes = Axes.Y,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(40, 40, 50, 255),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(16),
                        Spacing = new Vector2(0, 12),
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Text = message,
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Font = FontUsage.Default.With(size: 18),
                            },
                            buttonRow,
                        },
                    },
                },
            });
        }

        /// <summary>
        /// Creates a Save / Discard / Cancel confirmation dialog.
        /// </summary>
        public static ConfirmDialog SaveDiscardCancel(Action save, Action discard) =>
            new ConfirmDialog(
                "You have unsaved changes.",
                ("Save", save),
                ("Discard", discard),
                ("Cancel", () => { })
            );

        protected override void PopIn() => this.FadeIn(150);
        protected override void PopOut() => this.FadeOut(150);
    }
}
