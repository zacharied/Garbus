// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacSlamCentered.cs). BacSlamCentered →
// GarbusSlamCentered. No drawable representation yet (editor-only concept so far, as in the source repo).

using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;

namespace Garbus.Game.Objects;

public class GarbusSlamCentered : GarbusHitObject, IHasMutableAngle, IHasSide
{
    public required int AngleDeg { get; set; }
    public HorizontalDirection Side { get; set; } = HorizontalDirection.Left;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SlamCentered;
}
