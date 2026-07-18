// A single clickable row in the song-select list: the chart's display name + level, highlighted
// when selected. Group headers reuse this with a bolder style.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Screens.SongSelect
{
    public partial class ChartRow : ClickableContainer
    {
        private readonly Box background;
        private bool selected;

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                background.FadeColour(value ? new Color4(70, 90, 140, 255) : new Color4(32, 32, 44, 255), 120, Easing.OutQuint);
            }
        }

        public ChartRow(string text, int? level, bool header = false)
        {
            RelativeSizeAxes = Axes.X;
            Height = header ? 34 : 30;

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = header ? new Color4(24, 24, 32, 255) : new Color4(32, 32, 44, 255),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Padding = new MarginPadding { Left = header ? 12 : 28 },
                    Text = text,
                    Font = FontUsage.Default.With(size: header ? 22 : 18, weight: header ? "Bold" : null),
                    Colour = header ? Color4.White : new Color4(210, 210, 220, 255),
                },
                new SpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Padding = new MarginPadding { Right = 12 },
                    Text = level is > 0 ? $"Lv.{level}" : string.Empty,
                    Font = FontUsage.Default.With(size: 16),
                    Colour = new Color4(150, 160, 190, 255),
                },
            };
        }
    }
}
