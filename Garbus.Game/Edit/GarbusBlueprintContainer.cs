// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/BacBlueprintContainer.cs).
// BacBlueprintContainer → GarbusBlueprintContainer; BigAssCircleHitObjectComposer → GarbusHitObjectComposer;
// ComposeBlueprintContainer / SelectionBlueprint / DragBox / ScrollingDragBox from Edit.Compose.
//
// Task 15 scope: the placement half. CreateHitObjectBlueprintFor (per-type SELECTION blueprints) and the
// custom BacSelectionHandler are Task 16 — until then this returns the base's null (no per-object
// selection blueprint) and inherits the base EditorSelectionHandler. TryMoveBlueprints / CreateDragBox
// (both placement/selection movement) are ported now so drag-move snapping works.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Events;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit;

public partial class GarbusBlueprintContainer : ComposeBlueprintContainer
{
    public new GarbusHitObjectComposer Composer => (GarbusHitObjectComposer)base.Composer!;

    public GarbusBlueprintContainer(GarbusHitObjectComposer composer)
        : base(composer)
    {
    }

    // Per-type selection blueprints arrive in Task 16; until then no per-object selection blueprint.
    public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject) => null;

    protected sealed override DragBox CreateDragBox() => new ScrollingDragBox(Composer.Playfield);

    protected override bool TryMoveBlueprints(DragEvent e, IList<(SelectionBlueprint<GarbusHitObject> blueprint, Vector2[] originalSnapPositions)> blueprints)
    {
        Vector2 distanceTravelled = e.ScreenSpaceMousePosition - e.ScreenSpaceMouseDownPosition;

        // The final movement position, relative to movementBlueprintOriginalPosition.
        Vector2 movePosition = blueprints.First().originalSnapPositions.First() + distanceTravelled;

        // Retrieve a snapped position.
        var result = Composer.FindSnappedAngleTimeAndPosition(movePosition);

        var referenceBlueprint = blueprints.First().blueprint;
        bool moved = SelectionHandler.HandleMovement(new MoveSelectionEvent<GarbusHitObject>(referenceBlueprint, result.ScreenSpacePosition - referenceBlueprint.ScreenSpaceSelectionPoint));
        if (moved)
            ApplySnapResultTime(result, referenceBlueprint.Item.StartTime);
        return moved;
    }
}
