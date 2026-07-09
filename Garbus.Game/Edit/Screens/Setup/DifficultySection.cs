// Stub difficulty section — Garbus has no per-chart difficulty settings yet.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup
{
    public partial class DifficultySection : FillFlowContainer
    {
        public DifficultySection()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 8);
            Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };

            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = "Difficulty",
                    Font = FontUsage.Default.With(size: 20),
                },
                new SpriteText
                {
                    Text = "No per-chart difficulty settings yet.",
                    Font = FontUsage.Default.With(size: 16),
                    Colour = new osuTK.Graphics.Color4(180, 180, 180, 255),
                },
            };
        }
    }
}
