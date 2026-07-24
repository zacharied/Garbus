using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Garbus.Game
{
    /// <summary>
    /// A small, always-visible build code pinned to the bottom-right corner (platform + commit hash).
    /// Lives at the game root so it persists across every screen. Purely informational; it never takes
    /// input, so it draws over everything without stealing clicks.
    /// </summary>
    public partial class BuildInfoOverlay : SpriteText
    {
        public BuildInfoOverlay()
        {
            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            Margin = new MarginPadding { Right = 6, Bottom = 4 };

            Text = BuildInfo.DisplayText;
            Font = new FontUsage(size: 16);
            Colour = new Color4(1f, 1f, 1f, 0.5f);
        }

        // Never intercept input — the overlay is a passive label drawn on top of every screen.
        public override bool ReceivePositionalInputAt(osuTK.Vector2 screenSpacePos) => false;
    }
}
