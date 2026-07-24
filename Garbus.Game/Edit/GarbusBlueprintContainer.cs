using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.Events;
using Garbus.Game.Edit.Blueprints;
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

    public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject)
    {
        switch (hitObject)
        {
            case SliderBody slider:
                return new SliderSelectionBlueprint(slider);

            case CardinalHoldNote hold:
                return new CardinalHoldNoteSelectionBlueprint(hold);

            case CardinalNote note:
                return new OutlineSelectionBlueprint<CardinalNote>(note);

            case ShoulderHoldNote shoulderHold:
                return new ShoulderHoldNoteSelectionBlueprint(shoulderHold);

            case ShoulderNote shoulder:
                return new ShoulderNoteSelectionBlueprint(shoulder);

            case GarbusSlamCentered slam:
                return new OutlineSelectionBlueprint<GarbusSlamCentered>(slam);

            case GarbusSlamEdge slam:
                return new OutlineSelectionBlueprint<GarbusSlamEdge>(slam);
        }

        return base.CreateHitObjectBlueprintFor(hitObject);
    }

    protected override SelectionHandler<GarbusHitObject> CreateSelectionHandler() => new GarbusSelectionHandler();

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
