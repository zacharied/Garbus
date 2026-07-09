// Stub — filled by Task 21.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;

namespace Garbus.Game.Edit.Screens
{
    public partial class TimingTab : EditorTabScreen
    {
        public TimingTab()
        {
            RelativeSizeAxes = Axes.Both;
            AddInternal(new SpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = "Timing",
            });
        }
    }
}
