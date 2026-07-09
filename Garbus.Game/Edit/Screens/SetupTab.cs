// Stub — filled by Task 20.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace Garbus.Game.Edit.Screens
{
    public partial class SetupTab : EditorTabScreen
    {
        public SetupTab()
        {
            RelativeSizeAxes = Axes.Both;
            AddInternal(new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = "Setup",
            });
        }
    }
}
