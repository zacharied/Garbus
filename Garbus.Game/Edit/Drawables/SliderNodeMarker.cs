using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// A node dot on the editor slider polyline: a filled circle for a judged node, a hollow ring for a
/// shape-only control point — the fill/ring distinction is the compose view's only judged-vs-shape
/// signal, since gameplay renders shape-only points seamlessly.
/// </summary>
public partial class SliderNodeMarker : CircularContainer
{
    private readonly Box fill;

    /// <summary>Whether the fill box is currently visible (test seam for the filled/hollow relation).</summary>
    public bool FillVisible => fill.Alpha > 0;

    public SliderNodeMarker()
    {
        Size = new Vector2(10);
        Origin = Anchor.Centre;
        Masking = true;
        BorderColour = Colour4.White;
        // AlwaysPresent keeps the masked content drawn when the fill is invisible, so the border
        // ring still renders on hollow markers.
        InternalChild = fill = new Box { RelativeSizeAxes = Axes.Both, AlwaysPresent = true };
    }

    public bool ShapeOnly
    {
        set
        {
            BorderThickness = value ? 2.5f : 0;
            fill.Alpha = value ? 0 : 1;
        }
    }
}
