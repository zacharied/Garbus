// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/Blueprints/SlamCenteredPlacementBlueprint.cs).
// BacSlamCentered → GarbusSlamCentered.

using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Blueprints;

internal partial class SlamCenteredPlacementBlueprint : InstantPlacementBlueprint<GarbusSlamCentered>
{
    public SlamCenteredPlacementBlueprint()
        : base(new GarbusSlamCentered { AngleDeg = 0 })
    {
    }
}
