// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableHoldNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: judgement/input/catch-record logic factored into this generic base so the cardinal
// and shoulder hold drawables share it; subclasses supply only visuals.

using System;
using System.Collections.Generic;
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
using Garbus.Game.UI;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// Shared base for held notes: a nested judgemental head plus a time-accumulated ("catch record") tail,
/// judged deferred until the head resolves. Subclasses draw the head and body via <see cref="UpdateVisuals"/>.
/// </summary>
public abstract partial class DrawableHoldNote<THitObject, THead> : DrawableNote<THitObject>, ISelfPosition
    where THitObject : Note, IHasDuration
    where THead : Note
{
    [Resolved]
    protected GarbusScrollingHitObjectContainer ScrollingContainer { get; private set; } = null!;

    private readonly Container<DrawableHoldNoteHead<THead>> headContainer = new() { RelativeSizeAxes = Axes.Both };
    protected DrawableHoldNoteHead<THead> Head => headContainer.Child;

    private int holdPresses;
    private readonly List<CatchRecord> catchRecords = new();
    private CatchRecord? currentCatchRecord;
    private bool headPopPlayed;

    /// <summary>Whether the hold's button is currently held.</summary>
    protected bool Holding => holdPresses > 0;

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
        catchRecords.Clear();
        currentCatchRecord = null;
    }

    protected override void Update()
    {
        base.Update();

        if (Head.IsHit && !headPopPlayed)
        {
            headPopPlayed = true;
            OnHeadHit();
        }

        UpdateVisuals();
        updateCatchRecords();
    }

    /// <summary>Positions/builds the head and body for the frame. Subclasses draw everything here.</summary>
    protected abstract void UpdateVisuals();

    /// <summary>Called once when the head is hit, for the head-pop animation.</summary>
    protected virtual void OnHeadHit()
    {
    }

    private void updateCatchRecords()
    {
        double now = Time.Current;

        if (now < HitObject.StartTime || now > HitObject.EndTime)
            return;

        bool caught = holdPresses > 0;

        if (currentCatchRecord is null || currentCatchRecord.IsCatching != caught)
        {
            currentCatchRecord = new CatchRecord(caught, 0);
            catchRecords.Add(currentCatchRecord);
        }

        currentCatchRecord.Duration += Time.Elapsed;
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

    public override void MissForcefully()
    {
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        if (!Head.Judged)
            return;

        bool headCarries = HitObject.Duration < Head.HitObject.HitWindows.LateEligibilityEdge;

        if (headCarries && !Head.IsHit)
        {
            ApplyMinResult();
            return;
        }

        if (timeOffset < 0)
            return;

        double total = 0, caught = 0;

        foreach (var record in catchRecords)
        {
            total += record.Duration;
            if (record.IsCatching)
                caught += record.Duration;
        }

        double fraction = total > 0 ? caught / total : 1.0;
        var result = resultFor(fraction);

        if (headCarries)
            result = (HitResult)Math.Min((int)result, (int)Head.Result.Type);

        ApplyResult(result);
    }

    private static HitResult resultFor(double fraction)
    {
        if (fraction >= 1.0) return HitResult.CriticalPerfect;
        if (fraction >= 0.95) return HitResult.Perfect;
        if (fraction >= 0.60) return HitResult.Bad;

        return HitResult.Miss;
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
        headContainer.Clear(false);
    }

    protected class CatchRecord(bool isCatching, double duration)
    {
        public bool IsCatching { get; } = isCatching;
        public double Duration { get; set; } = duration;
    }
}
