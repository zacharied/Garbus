using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Blueprints;

internal partial class CardinalNotePlacementBlueprint : InstantPlacementBlueprint<CardinalNote>
{
    public CardinalNotePlacementBlueprint()
        : base(new CardinalNote { AngleDeg = 0 })
    {
    }
}
