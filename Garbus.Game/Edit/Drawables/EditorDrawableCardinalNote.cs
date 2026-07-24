using osu.Framework.Allocation;
using osu.Framework.Graphics;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Drawables;

public partial class EditorDrawableCardinalNote : EditorDrawableGarbusHitObject<CardinalNote>
{
    public const float NOTE_SIZE = 36;

    [Resolved]
    private ChordHighlighter chords { get; set; } = null!;

    public EditorDrawableCardinalNote(CardinalNote hitObject)
        : base(hitObject)
    {
        Size = new Vector2(NOTE_SIZE);
    }

    protected override Drawable CreateVisual() => new EditorSpritePiece("square");

    protected override void Update()
    {
        base.Update();

        // Set on the whole drawable so the ±360° ghost twin (an InternalChild) inherits the tint.
        Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Colour4.White;
    }
}
