using System.Collections.Generic;
using Garbus.Game.Core;
using osu.Framework.Graphics;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>A centered slam on the editor timeline: the arrow sprite (natively up) rotated to point down.</summary>
public partial class EditorDrawableGarbusSlamCentered : EditorDrawableGarbusHitObject<GarbusSlamCentered>
{
    private readonly List<EditorSpritePiece> arrows = new List<EditorSpritePiece>();

    public EditorDrawableGarbusSlamCentered(GarbusSlamCentered hitObject)
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
            Rotation = 180,
            Colour = HitObject.Side.ToColour()
        };
        arrows.Add(arrow);
        return arrow;
    }

    protected override void Update()
    {
        base.Update();

        // Read Side live (not once in CreateVisual): the editor mutates it in place via EditorChart.Update,
        // which re-Apply()s the drawable rather than recreating it, so a Side change must recolour here.
        var sideColour = HitObject.Side.ToColour();
        foreach (var arrow in arrows)
            arrow.Colour = sideColour;
    }
}
