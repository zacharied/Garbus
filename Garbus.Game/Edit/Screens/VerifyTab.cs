// Stub — filled by Task 22.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace Garbus.Game.Edit.Screens
{
    public partial class VerifyTab : EditorTabScreen
    {
        public VerifyTab()
        {
            RelativeSizeAxes = Axes.Both;
            AddInternal(new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = "Verify",
            });
        }
    }
}
