// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacSlamEdge.cs). BacSlamEdge →
// GarbusSlamEdge. No drawable representation yet (editor-only concept so far, as in the source repo).

using Garbus.Game.Core;

namespace Garbus.Game.Objects;

public class GarbusSlamEdge : GarbusHitObject, IHasMutableAngle
{
    public required int AngleDeg { get; set; }
    public HorizontalDirection Side = HorizontalDirection.Left;
    public RotationalDirection Direction = RotationalDirection.Clockwise;
}
