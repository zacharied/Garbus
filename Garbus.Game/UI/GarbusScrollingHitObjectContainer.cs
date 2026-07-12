// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/UI/BigAssCircleScrollingHitObjectContainer.cs).
// BigAssCircleScrollingHitObjectContainer → GarbusScrollingHitObjectContainer. Adapted: the scrolling
// info comes from GarbusScrollingInfo (replacing osu.Game's ScrollingTestContainer.TestScrollingInfo).

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Layout;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Utils;
using osuTK;

namespace Garbus.Game.UI;

[Cached]
public partial class GarbusScrollingHitObjectContainer : HitObjectContainer
{
    private readonly IBindable<double> timeRange = new BindableDouble();
    private readonly IBindable<IScrollAlgorithm> algorithm = new Bindable<IScrollAlgorithm>();

    /// <summary>
    /// A set of top-level <see cref="DrawableHitObject"/>s which have an up-to-date layout.
    /// </summary>
    private readonly HashSet<DrawableHitObject> layoutComputed = new HashSet<DrawableHitObject>();

    private GarbusScrollingInfo scrollingInfo { get; set; } = new GarbusScrollingInfo();

    // Responds to changes in the layout. When the layout changes, all hit object states must be recomputed.
    private readonly LayoutValue layoutCache = new LayoutValue(Invalidation.RequiredParentSizeToFit | Invalidation.DrawInfo);

    public GarbusScrollingHitObjectContainer()
    {
        RelativeSizeAxes = Axes.Both;

        AddLayout(layoutCache);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        timeRange.BindTo(scrollingInfo.TimeRange);
        algorithm.BindTo(scrollingInfo.Algorithm);

        timeRange.ValueChanged += _ => layoutCache.Invalidate();
        algorithm.ValueChanged += _ => layoutCache.Invalidate();
    }

    public float ProgressAtTime(double time, double currentTime, double? originTime = null)
    {
        float scrollPosition = algorithm.Value.PositionAt(time, currentTime, timeRange.Value, scrollLength, originTime);
        return MathF.Min(scrollLength, scrollLength - scrollPosition);
    }

    public float ProgressAtTime(double time) => ProgressAtTime(time, Time.Current);

    /// <summary>
    /// The distance from the playfield centre to the outer ring, in local pixels. An object reaches
    /// the ring exactly at its own time.
    /// </summary>
    public float ScrollLength => scrollLength;

    /// <summary>
    /// The unclamped distance from the centre at which an object with the given <paramref name="time"/>
    /// should be drawn. Unlike <see cref="ProgressAtTime(double,double,double?)"/>, this is allowed to
    /// exceed <see cref="ScrollLength"/> once the time has passed the ring — so callers can clip the
    /// portion of a shape that has already been consumed by the outer edge, rather than pinning it there.
    /// </summary>
    public float DistanceFromCentreAtTime(double time, double currentTime, double? originTime = null)
    {
        float scrollPosition = algorithm.Value.PositionAt(time, currentTime, timeRange.Value, scrollLength, originTime);
        return scrollLength - scrollPosition;
    }

    public float DistanceFromCentreAtTime(double time) => DistanceFromCentreAtTime(time, Time.Current);

    public Vector2 PositionAtTime(DrawableHitObject obj, double time, double currentTime, double? originTime = null)
    {
        float radians = obj.HitObject is IHasAngle angleObj ? MathUtils.DegToRad(angleObj.AngleDeg) : 0; // TODO Barline hitobject
        float distanceFromCentre = ProgressAtTime(time, currentTime, originTime);

        var localPosition = new Vector2(MathF.Cos(radians) * distanceFromCentre, -MathF.Sin(radians) * distanceFromCentre);
        return DrawRectangle.Centre + localPosition;
    }

    public float LengthAtTime(double startTime, double endTime)
    {
        return algorithm.Value.GetLength(startTime, endTime, timeRange.Value, scrollLength);
    }

    // A length is non-negative by definition. During a transient relayout frame the container's
    // DrawRectangle can briefly report a negative dimension (e.g. padding exceeding a not-yet-sized
    // parent), which would make this negative and break callers that clamp radii by it
    // (Math.Clamp(dist, 0, ScrollLength) throws when max < min). Floor it at zero.
    private float scrollLength => MathF.Max(0f, (DrawRectangle.Width < DrawRectangle.Height ? DrawRectangle.Width : DrawRectangle.Height) / 2);

    public override void Add(HitObjectLifetimeEntry entry)
    {
        // Scroll info is not available until loaded.
        // The lifetime of all entries will be updated in the first Update.
        if (IsLoaded)
            setComputedLifetime(entry);

        base.Add(entry);
    }

    protected override void AddDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
    {
        base.AddDrawable(entry, drawable);

        invalidateHitObject(drawable);
        drawable.DefaultsApplied += invalidateHitObject;
    }

    protected override void RemoveDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
    {
        base.RemoveDrawable(entry, drawable);

        drawable.DefaultsApplied -= invalidateHitObject;
        layoutComputed.Remove(drawable);
    }

    private void invalidateHitObject(DrawableHitObject hitObject)
    {
        layoutComputed.Remove(hitObject);
    }

    protected override void Update()
    {
        base.Update();

        if (layoutCache.IsValid) return;

        layoutComputed.Clear();

        foreach (var entry in Entries)
            setComputedLifetime(entry);

        algorithm.Value.Reset();

        layoutCache.Validate();
    }

    protected override void UpdateAfterChildrenLife()
    {
        base.UpdateAfterChildrenLife();

        // We need to calculate hit object positions (including nested hit objects) as soon as possible after lifetimes
        // to prevent hit objects displayed in a wrong position for one frame.
        // Only AliveEntries need to be considered for layout (reduces overhead in the case of scroll speed changes).
        // We are not using AliveObjects directly to avoid selection/sorting overhead since we don't care about the order at which positions will be updated.
        foreach (var entry in AliveEntries)
        {
            // Point-position anything that carries an angle, except drawables that manage their own
            // geometry each frame (ISelfPosition — e.g. paths, see DrawableSliderBody.updatePath).
            if (entry.Value is not { } obj || obj is ISelfPosition || obj.HitObject is not IHasAngle)
                continue;

            updatePosition(obj, Time.Current);

            if (layoutComputed.Contains(obj))
                continue;

            updateLayoutRecursive(obj);

            layoutComputed.Add(obj);
        }
    }

    /// <summary>
    /// Get a conservative maximum bounding box of a <see cref="DrawableHitObject"/> corresponding to <paramref name="entry"/>.
    /// It is used to calculate when the hit object appears.
    /// </summary>
    protected virtual RectangleF GetConservativeBoundingBox(HitObjectLifetimeEntry entry) => new RectangleF().Inflate(100);

    private double computeDisplayStartTime(HitObjectLifetimeEntry entry)
    {
        return algorithm.Value.GetDisplayStartTime(entry.HitObject.StartTime, 0, timeRange.Value, scrollLength);
    }

    private void setComputedLifetime(HitObjectLifetimeEntry entry)
    {
        double computedStartTime = computeDisplayStartTime(entry);

        // always load the hitobject before its first judgement offset
        entry.LifetimeStart = Math.Min(entry.HitObject.StartTime - entry.HitObject.MaximumJudgementOffset, computedStartTime);

        // This is likely not entirely correct, but sets a sane expectation of the ending lifetime.
        // A more correct lifetime will be overwritten after a DrawableHitObject is assigned via DrawableHitObject.updateState.
        //
        // It is required that we set a lifetime end here to ensure that in scenarios like loading a Player instance to a seeked
        // location in a chart doesn't churn every hit object into a DrawableHitObject. Even in a pooled scenario, the overhead
        // of this can be quite crippling.
        //
        // However, additionally do not attempt to alter lifetime of judged entries.
        // This is to prevent freak accidents like objects suddenly becoming alive because of this estimate assigning a later lifetime
        // than the object itself decided it should have when it underwent judgement.
        if (!entry.Judged)
            entry.LifetimeEnd = entry.HitObject.GetEndTime() + timeRange.Value;
    }

    private void updateLayoutRecursive(DrawableHitObject hitObject, double? parentHitObjectStartTime = null)
    {
        parentHitObjectStartTime ??= hitObject.HitObject.StartTime;

        if (hitObject.HitObject is IHasDuration e)
        {
            // TODO
        }

        foreach (var obj in hitObject.NestedHitObjects)
        {
            updateLayoutRecursive(obj, parentHitObjectStartTime);

            // Nested hitobjects don't need to scroll, but they do need accurate positions and start lifetime
            updatePosition(obj, hitObject.HitObject.StartTime, parentHitObjectStartTime);

            if (obj.Entry != null)
                setComputedLifetime(obj.Entry);
        }
    }

    private void updatePosition(DrawableHitObject hitObject, double currentTime, double? parentHitObjectStartTime = null)
    {
        var position = PositionAtTime(hitObject, hitObject.HitObject.StartTime, currentTime, parentHitObjectStartTime);
        hitObject.Position = position;
    }
}
