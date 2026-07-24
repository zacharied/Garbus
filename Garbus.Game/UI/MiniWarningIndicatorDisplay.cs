using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using Garbus.Game.Core;
using osuTK;

namespace Garbus.Game.UI;

/// <summary>
/// The editor mini-preview presentation of the slider/slam warning indicator. Reveal timing and the
/// "breathe" pulse are inherited from <see cref="WarningIndicatorDisplay"/>; only the drawing differs:
/// instead of a blurred glow clipped to just outside the ring, this draws a plain, unblurred arc of the
/// side's colour sitting directly on the ring. The small preview loses fine detail, so the crisp arc reads
/// far more clearly there than the soft under-ring glow.
/// </summary>
public sealed partial class MiniWarningIndicatorDisplay : WarningIndicatorDisplay
{
    // Sits on the ring (radius_scale 1.0 → arc centreline == ring radius) and is thick enough to read on top
    // of the ring's thin white stroke once the whole preview is scaled down.
    private const float thickness = 16f;

    protected override SideArc CreateSideArc(HorizontalDirection side, ColourInfo colour)
    {
        // A single plain arc is both the fade target (its alpha breathes) and the arc whose radians track the
        // revealed angle — no blur buffer, no clip mask (so the base per-frame mask layout is a no-op here).
        var arc = new Arc(thickness: thickness)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
            Size = Vector2.One,
            Colour = colour,
            Alpha = 0,
        };

        return new SideArc(arc, arc);
    }
}
