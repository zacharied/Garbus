// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacSlamCentered.cs). BacSlamCentered →
// GarbusSlamCentered. No drawable representation yet (editor-only concept so far, as in the source repo).

using Garbus.Game.Core;

namespace Garbus.Game.Objects;

public class GarbusSlamCentered : GarbusHitObject, IHasMutableAngle
{
    public required int AngleDeg { get; set; }
    public HorizontalDirection Side = HorizontalDirection.Left;
}
