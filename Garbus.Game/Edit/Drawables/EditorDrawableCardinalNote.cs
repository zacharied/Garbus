// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Drawables/EditorDrawableCardinalNote.cs).
// Namespace only change.

using osu.Framework.Graphics;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

public partial class EditorDrawableCardinalNote : EditorDrawableGarbusHitObject<CardinalNote>
{
    public const float NOTE_SIZE = 36;

    public EditorDrawableCardinalNote(CardinalNote hitObject)
        : base(hitObject)
    {
        Size = new Vector2(NOTE_SIZE);
    }

    protected override Drawable CreateVisual() => new EditorSpritePiece("square");
}
