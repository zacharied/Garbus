// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/EditorDrawableSlamEdge.cs).
// BacSlamEdge → GarbusSlamEdge; RotationalDirection from Garbus.Game.Core.

using System.Collections.Generic;
using osu.Framework.Graphics;
using Garbus.Game.Core;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// An edge slam on the editor timeline: the arrow sprite pointed sideways. On the unrolled axis, angle
/// increases (counter-clockwise) to the right, so a clockwise slam points left and an anticlockwise slam
/// points right. Rotation tracks <see cref="GarbusSlamEdge.Direction"/> live since it's editable.
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
        };
        arrows.Add(arrow);
        return arrow;
    }

    protected override void Update()
    {
        base.Update();

        foreach (var arrow in arrows)
            arrow.Rotation = HitObject.Direction == RotationalDirection.Clockwise ? -90 : 90;
    }
}
