using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Blueprints;

internal partial class SlamEdgePlacementBlueprint : InstantPlacementBlueprint<GarbusSlamEdge>
{
    public SlamEdgePlacementBlueprint()
        : base(new GarbusSlamEdge { AngleDeg = 0 })
    {
    }
}
