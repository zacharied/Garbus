// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/EditorDrawableSlamCentered.cs).
// BacSlamCentered → GarbusSlamCentered.

using Garbus.Game.Core;
using osu.Framework.Graphics;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

/// <summary>A centered slam on the editor timeline: the arrow sprite (natively up) rotated to point down.</summary>
public partial class EditorDrawableGarbusSlamCentered : EditorDrawableGarbusHitObject<GarbusSlamCentered>
{
    public EditorDrawableGarbusSlamCentered(GarbusSlamCentered hitObject)
        : base(hitObject)
    {
        Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE);
    }

    protected override Drawable CreateVisual() => new EditorSpritePiece("arrow")
    {
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Rotation = 180,
        Colour = HitObject.Side.ToColour()
    };
}
