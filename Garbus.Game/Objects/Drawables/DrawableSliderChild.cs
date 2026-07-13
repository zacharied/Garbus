// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableSliderChild.cs).
// Adapted: the unused [Resolved] IGameplayClock was dropped.

using System.Collections.Generic;
using osu.Framework.Allocation;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;

namespace Garbus.Game.Objects.Drawables;

public partial class DrawableSliderChild : DrawableHitObject<SliderChild>, ISelfPosition
{
    [Resolved]
    private AnalogInputManager analogInput { get; set; } = null!;

    // Minimum fraction of the segment window that must be caught to count as a hit.
    private const double catch_threshold = 0.5;

    private readonly List<CatchRecord> catchRecords = new();
    private CatchRecord? currentCatchRecord;

    public DrawableSliderChild(SliderChild hitObject)
        : base(hitObject)
    {
    }

    public override void PlaySamples() => GarbusHitSoundPlayback.Play(Samples, HitObject, Result);

    protected internal override JudgementResult CreateResult(Gameplay.Judgements.Judgement judgement)
    {
        return new SliderJudgementResult(HitObject, judgement);
    }

    protected override void OnFree()
    {
        base.OnFree();

        catchRecords.Clear();
        currentCatchRecord = null;
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (timeOffset < 0)
            return;

        double total = 0, caught = 0;

        foreach (var record in catchRecords)
        {
            total += record.Duration;
            if (record.IsCatching)
                caught += record.Duration;
        }

        double fraction = total > 0 ? caught / total : 0;

        if (fraction >= catch_threshold)
            ApplyMaxResult();
        else
            ApplyMinResult();
    }

    protected override void Update()
    {
        base.Update();

        if (Result is null)
            return;

        updateCatchRecords();
    }

    /// <summary>
    /// Accumulates the current frame's elapsed time into runs of constant catch state, but only while
    /// within this child's segment window [previous node, this node]. The drawable is alive well before
    /// its node, so unclamped accumulation would count time outside the segment.
    /// </summary>
    private void updateCatchRecords()
    {
        var body = (DrawableSliderBody)ParentHitObject!;
        double now = Time.Current;

        double segmentStart = HitObject.Parent.GetSegmentStartTime(HitObject);
        double segmentEnd = HitObject.StartTime;

        if (now < segmentStart || now > segmentEnd)
            return;

        bool catching = analogInput.SliderCatchers[HitObject.Parent.Side]
                                   .IsCatchingAt((int)body.AngleDegAt(now));

        // Start a new run when the state flips, otherwise extend the current run.
        if (currentCatchRecord is null || currentCatchRecord.IsCatching != catching)
        {
            currentCatchRecord = new CatchRecord(catching, 0);
            catchRecords.Add(currentCatchRecord);
        }

        currentCatchRecord.Duration += Time.Elapsed;
    }

    private class CatchRecord(bool isCatching, double duration)
    {
        public bool IsCatching { get; } = isCatching;
        public double Duration { get; set; } = duration;
    }
}
