// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/OutlineSelectionBlueprint.cs).
// BacHitObject → GarbusHitObject; EditorDrawableCardinalNote / EditSquarePiece from the Garbus editor.

using osu.Framework.Graphics;
using Garbus.Game.Edit.Blueprints.Components;
using Garbus.Game.Edit.Drawables;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Blueprints;

/// <summary>Selection blueprint for any single-press object drawn as a note-sized sprite (cardinal notes, slams).</summary>
internal partial class OutlineSelectionBlueprint<T> : GarbusSelectionBlueprint<T>
    where T : GarbusHitObject, IHasAngle
{
    public OutlineSelectionBlueprint(T hitObject)
        : base(hitObject)
    {
        Size = new Vector2(EditorDrawableCardinalNote.NOTE_SIZE);
        InternalChild = new EditSquarePiece { RelativeSizeAxes = Axes.Both };
    }
}
