using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit;

public partial class GarbusBlueprintContainer : ComposeBlueprintContainer
{
    public new GarbusHitObjectComposer Composer => (GarbusHitObjectComposer)base.Composer!;

    // While a Shift+drag box is active it selects slider nodes/heads instead of whole objects. Both flags
    // are latched once at drag start (never re-read per frame) so mid-drag modifier changes don't flip mode,
    // matching how the whole-object box captures Ctrl into selectionBeforeDrag at OnDragStart.
    private bool nodeDragBoxActive;
    private bool nodeDragBoxCombine;

    public GarbusBlueprintContainer(GarbusHitObjectComposer composer)
        : base(composer)
    {
    }

    /// <summary>The selected slider blueprints that own node targets — the ones a node drag-box acts on.</summary>
    private IEnumerable<SliderSelectionBlueprint> selectedNodeSliders() =>
        SelectionBlueprints.OfType<SliderSelectionBlueprint>().Where(b => b.IsSelected && b.HasNodeTargets);

    protected override bool OnDragStart(DragStartEvent e)
    {
        bool result = base.OnDragStart(e);

        // A Shift-held box that the base engaged (a real box, not a blueprint move) selects slider nodes.
        if (e.ShiftPressed && DragBox.State == Visibility.Visible && CurrentTool is SelectTool)
        {
            nodeDragBoxActive = true;
            nodeDragBoxCombine = e.ControlPressed;

            foreach (var slider in selectedNodeSliders())
                slider.BeginNodeDragBox(nodeDragBoxCombine);
        }

        return result;
    }

    protected override void UpdateSelectionFromDragBox(HashSet<GarbusHitObject> selectionBeforeDrag)
    {
        if (nodeDragBoxActive)
        {
            var quad = DragBox.Box.ScreenSpaceDrawQuad;

            foreach (var slider in selectedNodeSliders())
                slider.UpdateNodeDragBox(quad);

            return;
        }

        base.UpdateSelectionFromDragBox(selectionBeforeDrag);
    }

    protected override void OnDragEnd(DragEndEvent e)
    {
        bool wasNodeDrag = nodeDragBoxActive;
        bool replace = !nodeDragBoxCombine;
        var sliders = wasNodeDrag ? selectedNodeSliders().ToList() : null;

        base.OnDragEnd(e);

        if (wasNodeDrag)
        {
            foreach (var slider in sliders!)
                slider.EndNodeDragBox();

            // Replace mode narrows the selection to the boxed sliders: any selected slider left without a
            // node/head drops out of the whole-object selection. Combine mode only adds, so it never prunes.
            if (replace)
            {
                foreach (var slider in sliders!)
                {
                    if (!slider.HasNodeSelection)
                        SelectedItems.Remove(slider.Item);
                }
            }

            nodeDragBoxActive = false;
            nodeDragBoxCombine = false;
        }
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
