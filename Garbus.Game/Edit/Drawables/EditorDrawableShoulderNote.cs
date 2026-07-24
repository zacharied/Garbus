using osu.Framework.Graphics;
using Garbus.Game.Objects;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Drawables;

/// <summary>
/// A shoulder note on the editor timeline: a purple square drawn in its side's dedicated lane strip at
/// the quadrant boundary (not at the note's actual in-game angle, which is fixed to West/East).
/// </summary>
public partial class EditorDrawableShoulderNote : EditorDrawableGarbusHitObject<ShoulderNote>
{
    public EditorDrawableShoulderNote(ShoulderNote hitObject)
        : base(hitObject)
    {
        Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE);
    }

    protected override Drawable CreateVisual() => new EditorSpritePiece("square") { Colour = Color4.MediumPurple };

    protected override float ComputeXFraction() => GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side);

    // the shoulder lane strips sit well inside the grid; no wrap-around twin.
    protected override float? TwinXFraction() => null;
}
