using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;
using Garbus.Game.UI;
using osu.Framework.Allocation;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableSliderHead : DrawableGarbusHitObject<SliderHead>, ISelfPosition
{
    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    [Resolved]
    private SlamCoincidenceIndex slamCoincidenceIndex { get; set; } = null!;

    private readonly SliderNodeJudgement node = new();

    public DrawableSliderHead(SliderHead hitObject)
        : base(hitObject)
    {
    }

    protected override void OnFree()
    {
        base.OnFree();

        node.Reset();
    }

    protected override void Update()
    {
        node.Update(Time.Current - Time.Elapsed, Time.Current, HitObject.StartTime, isCoveringNode());
        base.Update();
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < 0 || node.Result is null)
            return;

        var result = node.Result.Value;

        if (result == HitResult.Miss)
        {
            bool? coincidentSlamHit = slamCoincidenceIndex.SlamHitAt(HitObject.StartTime, HitObject.Parent.Side);
            if (coincidentSlamHit is null)
                return;

            if (coincidentSlamHit.Value)
                result = HitResult.Bad;
        }

        ApplyResult(result);
    }

    private bool isCoveringNode()
        => analogInput.SliderCatchers[HitObject.Parent.Side].IsCatchingAt(HitObject.AngleDeg);
}
