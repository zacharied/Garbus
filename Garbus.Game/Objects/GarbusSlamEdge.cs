// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacSlamEdge.cs). BacSlamEdge →
// GarbusSlamEdge. No drawable representation yet (editor-only concept so far, as in the source repo).

using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects;

public class GarbusSlamEdge : GarbusHitObject, IHasMutableAngle, IHasSide
{
    public required int AngleDeg { get; set; }
    public HorizontalDirection Side { get; set; } = HorizontalDirection.Left;
    public RotationalDirection Direction = RotationalDirection.Clockwise;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SlamEdge;

    public override Gameplay.Judgements.Judgement CreateJudgement() => new PerfectJudgement();

    // Interim lifetime headroom matching the drawable's ±200ms first-cut window (the slam cycle
    // replaces both with real early-permissive windows including the late Near extent).
    public override double MaximumJudgementOffset => 200;
}
