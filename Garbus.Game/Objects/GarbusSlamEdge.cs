// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/BacSlamEdge.cs). BacSlamEdge →
// GarbusSlamEdge. No drawable representation yet (editor-only concept so far, as in the source repo).

using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects;

public class GarbusSlamEdge : GarbusHitObject, IHasMutableAngle, IHasSide
{
    public required int AngleDeg { get; set; }
    public HorizontalDirection Side { get; set; } = HorizontalDirection.Left;
    public RotationalDirection Direction = RotationalDirection.Clockwise;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SlamEdge;

    public override Gameplay.Judgements.Judgement CreateJudgement() => new PerfectJudgement();

    protected override HitWindows CreateHitWindows() => new SlamHitWindows();
}
