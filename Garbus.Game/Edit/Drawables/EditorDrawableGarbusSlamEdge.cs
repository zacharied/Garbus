using System.Collections.Generic;
using osu.Framework.Graphics;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// An edge slam on the editor timeline: the arrow sprite pointed sideways. On the unrolled axis, angle
/// increases to the right (counter-clockwise in the normal view, clockwise in the reversed view), so in
/// the normal view a clockwise slam points left and an anticlockwise slam points right — and the reversed
/// view mirrors that. Rotation tracks <see cref="GarbusSlamEdge.Direction"/> live since it's editable, and
/// <see cref="EditorAngleMapping.Direction"/> so it flips with the view reflection.
/// </summary>
public partial class EditorDrawableGarbusSlamEdge : EditorDrawableGarbusHitObject<GarbusSlamEdge>
{
    private readonly List<EditorSpritePiece> arrows = new List<EditorSpritePiece>();

    public EditorDrawableGarbusSlamEdge(GarbusSlamEdge hitObject)
        : base(hitObject)
    {
        Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE);
    }

    protected override Drawable CreateVisual()
    {
        var arrow = new EditorSpritePiece("arrow")
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = HitObject.Side.ToColour()
        };
        arrows.Add(arrow);
        return arrow;
    }

    protected override void Update()
    {
        base.Update();

        // Read Side/Direction live (not once in CreateVisual): the editor mutates them in place via
        // EditorChart.Update, which re-Apply()s the drawable rather than recreating it, so changes must
        // be reflected here.
        var sideColour = HitObject.Side.ToColour();
        var rotation = (HitObject.Direction == RotationalDirection.Clockwise ? -90 : 90) * EditorAngleMapping.Direction;

        foreach (var arrow in arrows)
        {
            arrow.Colour = sideColour;
            arrow.Rotation = rotation;
        }
    }
}
