// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableHoldNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.UI;
using Garbus.Game.Utils;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A held cardinal note: a cardinal head sprite trailing a straight radial line that represents the hold.
///
/// The head behaves like a <see cref="DrawableCardinalNote"/> (emerges from the centre and reaches the ring
/// at <see cref="HitObject.StartTime"/>), while the trailing line runs from the head inward toward the tail —
/// the node at <see cref="HoldNote.EndTime"/>, which is closer to the centre because it is later in time. As
/// time advances the whole thing sweeps outward: the head crosses the ring first, then the tail.
///
/// This is an <see cref="ISelfPosition"/> drawable (like <see cref="DrawableSliderBody"/>): it computes both
/// the head position and the trailing line in polar space each frame, so the scrolling container's alive
/// loop leaves it alone.
///
/// <para>
/// Judgement has two parts. The nested <see cref="DrawableHoldNoteHead"/> is judged like a cardinal note on
/// the press that starts the hold. The hold itself (the "tail") is judged with <see cref="DrawableSliderChild"/>'s
/// time-accumulation algorithm over [head-hit, EndTime], but with finer result granularity (top tier at ≥99%
/// caught) and capped by the head's grade. The tail judgement is <b>deferred until the head has been judged</b>
/// (a missed head fails the whole hold immediately; a very short hold with no body simply inherits the head),
/// which removes the need for the previous grace-period fudge. The note is re-grabbable: releasing then
/// pressing again just flips the recorded state, and the trail greys out whenever the hold is dropped.
/// </para>
/// </summary>
public partial class DrawableHoldNote : DrawableNote<HoldNote>, ISelfPosition
{
    /// <summary>Full width of the trailing line, in pixels. Half of this is the <see cref="Path.PathRadius"/>.</summary>
    private const float body_thickness = 20f;

    /// <summary>Side length of the square head sprite, matching <see cref="DrawableCardinalNote"/>.</summary>
    private const float head_size = 80f;

    private static readonly Colour4 held_colour = Colour4.White;
    private static readonly Colour4 dropped_colour = Colour4.Gray;

    [Resolved]
    private GarbusScrollingHitObjectContainer scrollingContainer { get; set; } = null!;

    private readonly Sprite headSprite;
    private readonly SmoothPath body;

    // Nested judgemental head. Drawn by this parent (headSprite), judged via Head.UpdateResult() on press.
    private readonly Container<DrawableHoldNoteHead> headContainer = new() { RelativeSizeAxes = Axes.Both };
    private DrawableHoldNoteHead Head => headContainer.Child;

    // Number of currently-pressed bindings for this note's button (a cardinal has both a d-pad and a face
    // action collapsing onto the same input, so this is a count rather than a bool). holding == holdPresses > 0.
    private int holdPresses;

    // Runs of constant catch state across the hold body, exactly as DrawableSliderChild accumulates them.
    private readonly List<CatchRecord> catchRecords = new();
    private CatchRecord? currentCatchRecord;
    private bool headPopPlayed;

    public DrawableHoldNote(HoldNote hitObject)
        : base(hitObject)
    {
        RelativeSizeAxes = Axes.Both;

        // The line is added behind the head so the head sprite sits on top of where they meet.
        body = new SmoothPath
        {
            Anchor = Anchor.Centre,
            PathRadius = body_thickness / 2,
            Colour = held_colour,
        };

        headSprite = new Sprite
        {
            Size = new Vector2(head_size),
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        headSprite.Texture = textures.Get("square");

        AddInternal(body);
        AddInternal(headSprite);

        // Nested head lives in the tree so it receives a clock and is updated/judged; it draws nothing.
        AddInternal(headContainer);
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();

        headSprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
        body.FadeInFromZero(100, Easing.In);
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

        // Play the head's catch pop once, when it is hit.
        if (Head.IsHit && !headPopPlayed)
        {
            headPopPlayed = true;
            headSprite.ScaleTo(1.2f, 80, Easing.OutQuint).Then().ScaleTo(1f, 120, Easing.OutQuint);
        }

        updateVisuals();
        updateCatchRecords();
    }

    /// <summary>
    /// Positions the head and rebuilds the trailing line for the current frame, entirely in the playfield's
    /// polar coordinate system (vertex (0,0) is the centre), and greys the trail while the hold is dropped.
    /// </summary>
    private void updateVisuals()
    {
        float ring = scrollingContainer.ScrollLength;
        float radians = MathUtils.DegToRad(HitObject.AngleDeg);

        // Head: same mapping as a cardinal note — clamped to the ring, so it emerges from the centre and
        // then sits at the ring once its time has passed.
        float headProgress = scrollingContainer.ProgressAtTime(HitObject.StartTime);
        headSprite.Position = polarToCartesian(radians, headProgress);

        // Trailing line: outer end tracks the head (StartTime), inner end tracks the tail (EndTime, later
        // in time and thus a smaller radius). The whole segment is radial (constant angle), so clipping it
        // to the visible band [0, ring] is equivalent to clamping the two endpoints — there is no swept
        // contact point to preserve, unlike DrawableSliderBody, so a plain clamp is correct here.
        float outer = Math.Clamp(scrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, ring);
        float inner = Math.Clamp(scrollingContainer.DistanceFromCentreAtTime(HitObject.EndTime), 0f, ring);

        if (outer - inner > 1f)
        {
            body.Vertices = new[]
            {
                polarToCartesian(radians, inner),
                polarToCartesian(radians, outer),
            };

            // Path auto-sizes to its vertex bounds and offsets content by vertexBounds.TopLeft; undo that
            // so a vertex at the polar origin (0,0) lands on the playfield centre (our anchor).
            body.Position = -body.PositionInBoundingBox(Vector2.Zero);
        }
        else
        {
            // Not yet emerged, or already fully consumed by the ring.
            body.Vertices = Array.Empty<Vector2>();
        }

        // Grey the trail whenever the hold is active (between head and tail) but not currently held. Before it
        // starts it is pending (white); once judged, the hit/miss transforms own the colour.
        if (!Judged)
        {
            bool active = Time.Current >= HitObject.StartTime && Time.Current <= HitObject.EndTime;
            body.Colour = active && holdPresses == 0 ? dropped_colour : held_colour;
        }
    }

    /// <summary>
    /// Accumulates the current frame's elapsed time into runs of constant catch state while within the hold
    /// body [StartTime, EndTime]. Independent of the head, so a missed head still lets the body be judged on
    /// how much of it was held. Mirrors <see cref="DrawableSliderChild"/>.
    /// </summary>
    private void updateCatchRecords()
    {
        double now = Time.Current;

        if (now < HitObject.StartTime || now > HitObject.EndTime)
            return;

        bool caught = holdPresses > 0;

        // Start a new run when the state flips, otherwise extend the current run.
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

        // Judge the head on the first note-lock-permitted press; later presses just re-grab the hold.
        if (!Head.Judged && CheckHittable?.Invoke(this, Time.Current) != false)
            return Head.UpdateResult();

        // Don't consume re-grab presses: the body is judged continuously, so the press stays free to reach a
        // co-timed note in the lane.
        return false;
    }

    public override void OnReleased(KeyBindingReleaseEvent<GarbusAction> e)
    {
        if (e.Action.ToButtonInput() != HitObject.ButtonInput)
            return;

        holdPresses = Math.Max(0, holdPresses - 1);
    }

    /// <summary>
    /// A hold judges itself continuously and always resolves at its tail, so it must never be nuked by the
    /// lane's note-lock the way a skipped instantaneous note is.
    /// </summary>
    public override void MissForcefully()
    {
    }

    protected override void CheckForResult(bool userTriggered, double timeOffset)
    {
        // Defer the tail until the head has been judged (hit on press, or auto-missed once its window passed).
        // For a short hold the tail can arrive before the head resolves, so we must know the head's outcome
        // before grading; for a normal hold the head is always judged well before the tail anyway.
        if (!Head.Judged)
            return;

        // The head only carries the whole hold for holds shorter than its late window — those have no
        // meaningful body to hold, so the head's result stands in for them. Longer holds are judged purely on
        // how much of the body was held, so a missed head never fails them (it just costs the head's own result).
        bool headCarries = HitObject.Duration < HitObject.Head.HitWindows.WindowFor(HitResult.Miss);

        // Short hold + missed head → nothing was ever held; fail it now without waiting for the tail.
        if (headCarries && !Head.IsHit)
        {
            ApplyMinResult();
            return;
        }

        // Grade the body at the tail. timeOffset is relative to EndTime.
        if (timeOffset < 0)
            return;

        double total = 0, caught = 0;

        foreach (var record in catchRecords)
        {
            total += record.Duration;
            if (record.IsCatching)
                caught += record.Duration;
        }

        // No body window (a hold shorter than the head press latency) is treated as fully caught — the head
        // hit alone carries it.
        double fraction = total > 0 ? caught / total : 1.0;
        var result = resultFor(fraction);

        // A short hold also can't grade higher than the head was struck, so fold the head's grade in.
        if (headCarries)
            result = (HitResult)Math.Min((int)result, (int)Head.Result.Type);

        ApplyResult(result);
    }

    /// <summary>
    /// Maps a caught fraction to a graded <see cref="HitResult"/>. The top grade requires at least 99% caught.
    /// </summary>
    private static HitResult resultFor(double fraction)
    {
        if (fraction >= 0.99) return HitResult.Perfect;
        if (fraction >= 0.90) return HitResult.Great;
        if (fraction >= 0.80) return HitResult.Good;
        if (fraction >= 0.65) return HitResult.Ok;
        if (fraction >= 0.50) return HitResult.Meh;

        return HitResult.Miss;
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                body.FadeOut(350, Easing.OutQuint);
                headSprite.Spin(700, RotationDirection.Clockwise)
                          .FadeOut(350, Easing.OutQuint)
                          .ScaleTo(new Vector2(2), 350, Easing.OutQuint)
                          .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                body.FadeColour(Color4.Red, duration);
                body.FadeOut(duration, Easing.InQuint);
                headSprite.FadeColour(Color4.Red, duration);
                headSprite.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }

    protected override DrawableHitObject CreateNestedHitObject(HitObject hitObject)
    {
        return hitObject switch
        {
            HoldNoteHead head => new DrawableHoldNoteHead(head),
            _ => throw new InvalidOperationException($"cannot create nested hit object for type {hitObject.GetType().Name}")
        };
    }

    protected override void AddNestedHitObject(DrawableHitObject hitObject)
    {
        if (hitObject is not DrawableHoldNoteHead head)
            throw new InvalidOperationException($"cannot add child of type {hitObject.GetType()}");

        headContainer.Child = head;
    }

    protected override void ClearNestedHitObjects()
    {
        headContainer.Clear(false);
    }

    private static Vector2 polarToCartesian(float radians, float radius)
        => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);

    private class CatchRecord(bool isCatching, double duration)
    {
        public bool IsCatching { get; } = isCatching;
        public double Duration { get; set; } = duration;
    }
}
