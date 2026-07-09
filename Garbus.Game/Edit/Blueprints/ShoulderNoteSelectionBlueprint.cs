// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/ShoulderNoteSelectionBlueprint.cs).
// BacEditorPlayfield → GarbusEditorPlayfield; EditorDrawableCardinalNote / EditSquarePiece from the Garbus editor.

using osu.Framework.Graphics;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Blueprints;

internal partial class ShoulderNoteSelectionBlueprint : GarbusSelectionBlueprint<ShoulderNote>
{
    public ShoulderNoteSelectionBlueprint(ShoulderNote note)
        : base(note)
    {
        Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE);
        InternalChild = new EditSquarePiece { RelativeSizeAxes = Axes.Both };
    }

    protected override float ComputeXFraction() => GarbusEditorPlayfield.ShoulderXFraction(HitObject.Side);

    // the shoulder lane strips sit well inside the grid; no wrap-around twin.
    protected override float? TwinXFraction() => null;
}
