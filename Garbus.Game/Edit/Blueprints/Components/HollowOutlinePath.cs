using System;
using osu.Framework.Graphics.Lines;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Blueprints.Components;

/// <summary>
/// A <see cref="SmoothPath"/> that paints only its outer rails, leaving a transparent channel down the
/// centre. Used for the slider selection outline so the coloured slider body underneath stays visible
/// (revealing which side — <c>Left</c>/<c>Right</c> — the slider belongs to) instead of being painted over.
///
/// The full <see cref="Path.PathRadius"/> is still used for hit-testing, so click tolerance is unchanged —
/// only the fill is hollowed. <paramref name="ColourAt"/>'s <c>position</c> runs 0 (outer edge) → 1 (centre);
/// rails stay opaque out to <see cref="rail_extent"/> of the radius, then fade to transparent.
/// </summary>
internal partial class HollowOutlinePath : SmoothPath
{
    // fraction of the radius (measured inward from the edge) kept opaque; the inner remainder is the
    // transparent channel the body shows through.
    private const float rail_extent = 0.5f;

    // width of the inner fade, in the same 0..1 texture space, so the rail's inner edge isn't a hard step.
    private const float inner_aa = 0.08f;

    protected override Color4 ColourAt(float position)
    {
        if (position <= rail_extent)
            return Color4.White;

        float alpha = 1 - Math.Clamp((position - rail_extent) / inner_aa, 0, 1);
        return new Color4(1f, 1f, 1f, alpha);
    }
}
