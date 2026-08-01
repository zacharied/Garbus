// A square, icon-only dialog button. Icon-only means the tooltip is the only label, so it is required.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// A <see cref="BasicButton"/> that displays a centred icon instead of text, with a tooltip
    /// standing in for the label it does not have.
    /// </summary>
    /// <remarks>
    /// Tooltips only appear inside a <see cref="TooltipContainer"/>; <see cref="ModalOverlay"/>
    /// provides one for every modal dialog.
    /// </remarks>
    public partial class DialogIconButton : BasicButton, IHasTooltip
    {
        public LocalisableString TooltipText { get; init; }

        private readonly SpriteIcon spriteIcon;

        /// <summary>
        /// The icon's edge length as a fraction of the button. Relative so the glyph tracks a button
        /// resized live by a tuning scene.
        /// </summary>
        public float IconScale
        {
            get => spriteIcon.Size.X;
            set => spriteIcon.Size = new Vector2(value);
        }

        public DialogIconButton(IconUsage icon)
        {
            Add(spriteIcon = new SpriteIcon
            {
                Depth = -1,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Size = new Vector2(0.5f),
                Icon = icon,
                Colour = Color4.White,
            });
        }
    }
}
