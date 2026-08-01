using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// A node dot on the editor slider polyline: a filled circle for a judged node, a hollow ring for a
/// shape-only control point — the fill/ring distinction is the compose view's only judged-vs-shape
/// signal, since gameplay renders shape-only points seamlessly.
///
/// The polyline draws beneath the markers and is wider than the ring's interior, so a transparent
/// interior would show the (same-coloured) line straight through the hole and read as filled. The
/// hollow look is therefore a punch-out: an opaque near-black interior that masks the line. The
/// parent visual tints everything by the side colour multiplicatively, which leaves the dark
/// interior dark while the ring picks up the side colour like the line does.
/// </summary>
public partial class SliderNodeMarker : CircularContainer
{
    // Near-black punch-out for the hollow interior. Not pure black so it still reads as part of
    // the slider's UI over the playfield backdrop rather than a literal hole in the screen.
    private static readonly Colour4 punch_out_colour = new Colour4(25, 25, 32, 255);

    private readonly Box fill;

    /// <summary>Largest RGB component of the fill (test seam for the darker-than relation).</summary>
    public float FillMaxRgb => System.Math.Max(fill.Colour.TopLeft.Linear.R, System.Math.Max(fill.Colour.TopLeft.Linear.G, fill.Colour.TopLeft.Linear.B));

    /// <summary>Smallest RGB component of the fill (test seam for the darker-than relation).</summary>
    public float FillMinRgb => System.Math.Min(fill.Colour.TopLeft.Linear.R, System.Math.Min(fill.Colour.TopLeft.Linear.G, fill.Colour.TopLeft.Linear.B));

    /// <summary>Whether the fill hides what is behind it — the punch-out only works when opaque.</summary>
    public bool FillOpaque => fill.Alpha >= 1 && fill.Colour.TopLeft.Linear.A >= 1;

    public SliderNodeMarker()
    {
        Size = new Vector2(10);
        Origin = Anchor.Centre;
        Masking = true;
        BorderColour = Colour4.White;
        InternalChild = fill = new Box { RelativeSizeAxes = Axes.Both };
    }

    public bool ShapeOnly
    {
        set
        {
            BorderThickness = value ? 2.5f : 0;
            fill.Colour = value ? punch_out_colour : Colour4.White;
        }
    }
}
