// Stub — filled by later tasks (Task 15+).

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace Garbus.Game.Edit.Screens
{
    public partial class ComposeTab : EditorTabScreen
    {
        public ComposeTab()
        {
            RelativeSizeAxes = Axes.Both;
            AddInternal(new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = "Compose",
            });
        }
    }
}
