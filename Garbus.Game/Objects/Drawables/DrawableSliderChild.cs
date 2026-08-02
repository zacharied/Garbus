// Each child is a hold-family duration judgement, while HeadStyleHit is the
// distinct catch-style pseudo-judgement used only as the next segment's head reference.

using System;
using System.Linq;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;
using Garbus.Game.UI;
using osu.Framework.Allocation;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableSliderChild : DrawableHitObject<SliderChild>, ISelfPosition, IHasActivationProgress
{
    /// <summary>
    /// A child grades how much of its segment was caught. Its application time at the node is not a
    /// player timing offset and must not be labelled early or late.
    /// </summary>
    public override bool DisplayTimingOffset => false;

    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    [Resolved]
    private SlamCoincidenceIndex slamCoincidenceIndex { get; set; } = null!;

    private double lastActivationUpdate = double.NaN;
    private double activatedAfterOpeningGrace;
    private bool activatedDuringEndGrace;
    private bool? activatedAtSegmentEnd;

    /// <summary>
    /// The proportion of this segment credited as activated. This is the same activation duration
    /// passed to <see cref="DurationJudgement.Resolve"/> when the child is judged.
    /// </summary>
    public double ActivationProgress
    {
        get
        {
            double duration = HitObject.StartTime - HitObject.Parent.GetSegmentStartTime(HitObject);
            if (duration <= 0)
                return 1;

            double creditedOpeningGrace = Math.Min(duration, SliderNodeHitWindows.NODE_WINDOW);
            return Math.Clamp((creditedOpeningGrace + activatedAfterOpeningGrace) / duration, 0, 1);
        }
    }

    /// <summary>The catch-style pseudo-judgement at this node; never applied as a real result.</summary>
    public bool? HeadStyleHit { get; private set; }

    public DrawableSliderChild(SliderChild hitObject)
        : base(hitObject)
    {
    }

    /// <summary>Number of family members this child has played. Test seam.</summary>
    public int SamplesPlayCount => Samples?.PlayCount ?? 0;

    // Duration children play once when hit; GarbusHitSoundPlayback keeps Miss results silent.
    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);

    protected internal override JudgementResult CreateResult(Gameplay.Judgements.Judgement judgement)
        => new SliderJudgementResult(HitObject, judgement);

    protected override void OnFree()
    {
        base.OnFree();

        lastActivationUpdate = double.NaN;
        activatedAfterOpeningGrace = 0;
        activatedDuringEndGrace = false;
        activatedAtSegmentEnd = null;
        HeadStyleHit = null;
    }

    protected override void Update()
    {
        updateActivation();
        updateHeadStyleJudgement();
        base.Update();
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < 0 || HeadStyleHit is null)
            return;

        bool? referenceWasHit = headReferenceWasHit();
        if (referenceWasHit is null)
            return;

        double duration = HitObject.StartTime - HitObject.Parent.GetSegmentStartTime(HitObject);
        double openingGrace = Math.Min(duration, SliderNodeHitWindows.NODE_WINDOW);

        HitResult result = DurationJudgement.Resolve(
            duration,
            openingGrace + activatedAfterOpeningGrace,
            SliderNodeHitWindows.NODE_WINDOW,
            referenceWasHit.Value,
            activatedAtSegmentEnd == true,
            activatedDuringEndGrace || activatedAtSegmentEnd == true,
            bestThreshold: 0.95,
            perfectThreshold: 0.90,
            badThreshold: 0.50);

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

    private bool? headReferenceWasHit()
    {
        var reference = ParentHitObject!.NestedHitObjects
                                        .Single(d => ReferenceEquals(d.HitObject, HitObject.HeadReference));

        return reference switch
        {
            DrawableSliderHead head when head.Judged => head.IsHit,
            DrawableSliderChild child => child.HeadStyleHit,
            _ => null,
        };
    }

    private void updateHeadStyleJudgement()
    {
        if (HeadStyleHit is not null || Time.Current < HitObject.StartTime)
            return;

        if (isCatchingNode())
            HeadStyleHit = true;
        else if (Time.Current > HitObject.StartTime + SliderNodeHitWindows.NODE_WINDOW)
            HeadStyleHit = false;
    }

    private void updateActivation()
    {
        var body = (DrawableSliderBody)ParentHitObject!;
        double now = Time.Current;
        double previous = double.IsNaN(lastActivationUpdate) ? now - Time.Elapsed : lastActivationUpdate;
        lastActivationUpdate = now;

        double segmentStart = HitObject.Parent.GetSegmentStartTime(HitObject);
        double segmentEnd = HitObject.StartTime;
        if (now < segmentStart || previous > segmentEnd)
            return;

        bool catching = analogInput.SliderCatchers[HitObject.Parent.Side]
                                   .IsCatchingAt((int)body.AngleDegAt(Math.Min(now, segmentEnd)));

        if (now >= segmentEnd && activatedAtSegmentEnd is null)
            activatedAtSegmentEnd = catching;

        if (!catching)
            return;

        double intervalStart = Math.Max(previous, segmentStart + SliderNodeHitWindows.NODE_WINDOW);
        double intervalEnd = Math.Min(now, segmentEnd);
        if (intervalEnd > intervalStart)
            activatedAfterOpeningGrace += intervalEnd - intervalStart;

        double endingGraceStart = Math.Max(segmentStart, segmentEnd - SliderNodeHitWindows.NODE_WINDOW);
        if (now >= endingGraceStart && previous <= segmentEnd)
            activatedDuringEndGrace = true;
    }

    private bool isCatchingNode()
        => analogInput.SliderCatchers[HitObject.Parent.Side].IsCatchingAt(HitObject.AngleDeg);
}
