// No drawable representation yet (editor-only concept so far).

using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects;

public class GarbusSlamCentered : GarbusHitObject, IHasMutableAngle, IHasSide
{
    public required int AngleDeg { get; set; }
    public HorizontalDirection Side { get; set; } = HorizontalDirection.Left;

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SlamCentered;

    public override Gameplay.Judgements.Judgement CreateJudgement() => new PerfectJudgement();

    protected override HitWindows CreateHitWindows() => new SlamHitWindows();
}
