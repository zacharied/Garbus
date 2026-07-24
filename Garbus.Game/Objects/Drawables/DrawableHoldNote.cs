// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableHoldNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: judgement/input/catch-record logic factored into this generic base so the cardinal
// and shoulder hold drawables share it; subclasses supply only visuals.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Allocation;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects.Judgement;
using Garbus.Game.UI;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// Shared base for held notes: a nested judgemental head plus a time-accumulated ("catch record") tail,
/// judged deferred until the head resolves. Subclasses draw the head and body via <see cref="UpdateVisuals"/>.
/// </summary>
public abstract partial class DrawableHoldNote<THitObject, THead> : DrawableNote<THitObject>, ISelfPosition, IHasActivationProgress
    where THitObject : Note, IHasDuration
    where THead : Note
{
    /// <summary>
    /// The parent result grades the held proportion. Its application time at the tail is not a player
    /// timing offset and must not be labelled early or late.
    /// </summary>
    public override bool DisplayTimingOffset => false;

    [Resolved]
    protected GarbusScrollingHitObjectContainer ScrollingContainer { get; private set; } = null!;

    private readonly Container<DrawableHoldNoteHead<THead>> headContainer = new() { RelativeSizeAxes = Axes.Both };
    protected DrawableHoldNoteHead<THead> Head => headContainer.Child;

    private int holdPresses;
    private double lastActivationUpdate = double.NaN;
    private double activatedAfterOpeningGrace;
    private bool activatedDuringEndGrace;
    private bool? activatedAtEnd;
    private bool headPopPlayed;

    /// <summary>Whether the hold's button is currently held.</summary>
    protected bool Holding => holdPresses > 0;

    /// <summary>Whether one or more bindings for this hold are currently pressed.</summary>
    public bool IsHolding => Holding;

    /// <summary>
    /// The proportion of the hold duration currently credited as activated. This is the same
    /// activation duration passed to <see cref="DurationJudgement.Resolve"/> when the tail is judged.
    /// </summary>
    public double ActivationProgress
    {
        get
        {
            if (HitObject.Duration <= 0)
                return 1;

            double creditedOpeningGrace = Math.Min(HitObject.Duration, HitObject.HitWindows.LateEligibilityEdge);
            return Math.Clamp((creditedOpeningGrace + activatedAfterOpeningGrace) / HitObject.Duration, 0, 1);
        }
    }

    /// <summary>Whether the current time is within the hold body [StartTime, EndTime].</summary>
    protected bool HoldActive => Time.Current >= HitObject.StartTime && Time.Current <= HitObject.EndTime;

    protected DrawableHoldNote(THitObject hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.Both;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddInternal(headContainer);
    }

    protected override void OnFree()
    {
        base.OnFree();

        holdPresses = 0;
        headPopPlayed = false;
        lastActivationUpdate = double.NaN;
        activatedAfterOpeningGrace = 0;
        activatedDuringEndGrace = false;
        activatedAtEnd = null;
    }

    protected override void Update()
    {
        updateActivation();
        base.Update();

        if (Head.IsHit && !headPopPlayed)
        {
            headPopPlayed = true;
            OnHeadHit();
        }

        UpdateVisuals();
    }

    /// <summary>Positions/builds the head and body for the frame. Subclasses draw everything here.</summary>
    protected abstract void UpdateVisuals();

    /// <summary>Called once when the head is hit, for the head-pop animation.</summary>
    protected virtual void OnHeadHit()
    {
    }

    private void updateActivation()
    {
        double now = Time.Current;
        double previous = double.IsNaN(lastActivationUpdate) ? now - Time.Elapsed : lastActivationUpdate;
        lastActivationUpdate = now;

        if (now >= HitObject.EndTime && activatedAtEnd is null)
            activatedAtEnd = Holding;

        if (!Holding || now < HitObject.StartTime || previous > HitObject.EndTime)
            return;

        double grace = Head.HitObject.HitWindows.LateEligibilityEdge;
        double intervalStart = Math.Max(previous, HitObject.StartTime + grace);
        double intervalEnd = Math.Min(now, HitObject.EndTime);

        if (intervalEnd > intervalStart)
            activatedAfterOpeningGrace += intervalEnd - intervalStart;

        double endingGraceStart = Math.Max(HitObject.StartTime, HitObject.EndTime - grace);
        if (now >= endingGraceStart && previous <= HitObject.EndTime)
            activatedDuringEndGrace = true;
    }

    public override bool OnPressed(KeyBindingPressEvent<GarbusAction> e)
    {
        if (e.Action.ToButtonInput() != HitObject.ButtonInput)
            return false;

        holdPresses++;

        if (!Head.Judged && CheckHittable?.Invoke(this, Time.Current) != false)
            return Head.UpdateResult();

        return false;
    }

    public override void OnReleased(KeyBindingReleaseEvent<GarbusAction> e)
    {
        if (e.Action.ToButtonInput() != HitObject.ButtonInput)
            return;

        holdPresses = Math.Max(0, holdPresses - 1);
    }

    public override bool PressJudged => Head.Judged;

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (!Head.Judged)
            return;

        if (timeOffset < 0)
            return;

        double grace = Head.HitObject.HitWindows.LateEligibilityEdge;
        double creditedOpeningGrace = Math.Min(HitObject.Duration, grace);

        ApplyResult(DurationJudgement.Resolve(
            HitObject.Duration,
            creditedOpeningGrace + activatedAfterOpeningGrace,
            grace,
            Head.IsHit,
            activatedAtEnd == true,
            activatedDuringEndGrace,
            bestThreshold: 1,
            perfectThreshold: 0.95,
            badThreshold: 0.60));
    }

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
    {
        return hitObject is THead head
            ? new DrawableHoldNoteHead<THead>(head)
            : throw new InvalidOperationException($"cannot create nested hit object for type {hitObject.GetType().Name}");
    }

    protected override void AddNestedHitObject(DrawableHitObject hitObject)
    {
        if (hitObject is not DrawableHoldNoteHead<THead> head)
            throw new InvalidOperationException($"cannot add child of type {hitObject.GetType()}");

        headContainer.Child = head;
    }

    protected override void ClearNestedHitObjects()
    {
        var syntheticChildren = headContainer.Children
                                             .Where(child => child.Entry is SyntheticHitObjectEntry)
                                             .ToArray();

        headContainer.Clear(false);

        foreach (DrawableHitObject child in syntheticChildren)
            child.Dispose();
    }

}
