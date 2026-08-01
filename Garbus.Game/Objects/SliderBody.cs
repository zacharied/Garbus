using System.Linq;
using System.Threading;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Gameplay.Scoring;
using osu.Framework.Graphics;

namespace Garbus.Game.Objects;

public class SliderBody : GarbusHitObject, IHasDuration, IHasMutableAngle, IHasSide
{
    /// <summary>
    /// The initial direction of the path, in degrees. Each child control point's
    /// <see cref="GarbusPathControlPoint.RotationOffset"/> is applied relative to this.
    /// </summary>
    public required int AngleDeg { get; set; }

    public required HorizontalDirection Side { get; set; }

    public required GarbusPath Path { get; init; }

    /// <summary>
    /// The duration of the path, derived from the furthest-in-time control point.
    /// The setter is a no-op as the value is always computed from <see cref="Path"/>.
    /// </summary>
    public double Duration
    {
        get => Path == null || Path.ControlPoints.Count == 0 ? 0 : Path.ControlPoints.Max(c => c.TimeOffset);
        set { }
    }

    public double EndTime => StartTime + Duration;

    /// <summary>
    /// The absolute time of the judged node immediately preceding <paramref name="child"/> along the path —
    /// the start of the segment that ends at the child. For the first control point this is the head
    /// node at <see cref="HitObject.StartTime"/>.
    /// </summary>
    public double GetSegmentStartTime(SliderChild child)
    {
        // Node 0 is the head at StartTime; control point i is node i+1 at StartTime + TimeOffset.
        // IndexOf is by reference (each control point instance is unique), matching DrawableSliderBody.
        int index = Path.ControlPoints.IndexOf(child.ControlPoint) - 1;

        // Shape-only points are not nodes — the segment reaches back to the previous judged node.
        while (index >= 0 && Path.ControlPoints[index].ShapeOnly)
            index--;

        return index < 0 ? StartTime : StartTime + Path.ControlPoints[index].TimeOffset;
    }

    /// <summary>
    /// The absolute angle of the swept path at the given <paramref name="time"/>, in degrees, matching the
    /// geometry the body is rendered with (same per-link easing / smoothing via <see cref="SliderSweep"/>).
    /// Times before the head or after the last node clamp to the respective end node's angle; a head-only
    /// slider (no control points) has a constant angle. The result may fall outside [0, 360) — callers that
    /// compare against a wrapped angle should reduce it.
    ///
    /// This is the model-side counterpart to <c>DrawableSliderBody.AngleDegAt</c>. The drawable caches its
    /// node arrays for the per-frame gameplay hot path; this allocates per call and is for editor / tooling
    /// use (e.g. slider decomposition). Both route through <see cref="SliderSweep"/> so they cannot drift.
    /// </summary>
    public float AngleDegAt(double time)
    {
        var controlPoints = Path.ControlPoints;
        int count = 1 + controlPoints.Count;

        // A single node (head-only) has no link to interpolate — the angle is constant.
        if (count < 2)
            return AngleDeg;

        // Node value = angle offset from AngleDeg (head = 0); node time = absolute StartTime + TimeOffset.
        var values = new float[count];
        var times = new double[count];
        var linkEasing = new Easing[count - 1];
        var linkSmooth = new bool[count - 1];

        values[0] = 0f;
        times[0] = StartTime;

        for (int i = 0; i < controlPoints.Count; i++)
        {
            var cp = controlPoints[i];

            values[i + 1] = cp.RotationOffset;
            times[i + 1] = StartTime + cp.TimeOffset;

            // A control point governs the segment leading into it: link[i] ends at node[i+1] = CP[i].
            linkEasing[i] = cp.SweepEasing;
            linkSmooth[i] = cp.Smooth;
        }

        if (time <= times[0])
            return AngleDeg + values[0];
        if (time >= times[^1])
            return AngleDeg + values[^1];

        var slopes = SliderSweep.ComputeSlopes(values, times);

        // Find the link this time falls in: link i spans [times[i], times[i + 1]].
        int link = 0;
        while (link < count - 2 && time > times[link + 1])
            link++;

        double span = times[link + 1] - times[link];
        float t = span > 0 ? (float)((time - times[link]) / span) : 0f;

        return AngleDeg + SliderSweep.ValueAt(values, slopes, times, linkEasing[link], linkSmooth[link], link, t);
    }

    protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
    {
        var head = new SliderHead(this)
        {
            StartTime = StartTime,
        };
        AddNested(head);

        GarbusHitObject previousNode = head;

        foreach (var controlPoint in Path.ControlPoints)
        {
            // Shape-only points contribute geometry but are not nodes; previousNode advances only on
            // spawned children, so the head-reference chain skips them.
            if (controlPoint.ShapeOnly)
                continue;

            var childHitObject = new SliderChild(this, controlPoint, previousNode)
            {
                StartTime = StartTime + controlPoint.TimeOffset,
            };
            AddNested(childHitObject);
            previousNode = childHitObject;
        }
    }

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderBody;

    // The body itself is unscored — the head and children carry the slider's judgement. It only earns an
    // IgnoreHit so it can leave the Idle state and expire once the path plays out; without a result the
    // body would live (and re-render its glow framebuffer every frame) for the rest of the chart.
    public override Gameplay.Judgements.Judgement CreateJudgement() => new SliderBodyJudgement();

    private class SliderBodyJudgement : Gameplay.Judgements.Judgement
    {
        public override HitResult MaxResult => HitResult.IgnoreHit;
    }
}
