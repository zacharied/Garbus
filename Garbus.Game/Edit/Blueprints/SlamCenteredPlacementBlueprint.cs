using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Blueprints;

internal partial class SlamCenteredPlacementBlueprint : InstantPlacementBlueprint<GarbusSlamCentered>
{
    public SlamCenteredPlacementBlueprint()
        : base(new GarbusSlamCentered { AngleDeg = 0 })
    {
    }
}
