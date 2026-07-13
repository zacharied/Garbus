// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/SliderBody.cs).

using System.Linq;
using System.Threading;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Types;

namespace Garbus.Game.Objects;

public class SliderBody : GarbusHitObject, IHasDuration, IHasMutableAngle
{
    /// <summary>
    /// The initial direction of the path, in degrees. Each child control point's
    /// <see cref="GarbusPathControlPoint.RotationOffset"/> is applied relative to this.
    /// </summary>
    public required int AngleDeg { get; set; }

    public required HorizontalDirection Side;

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
    /// The absolute time of the node immediately preceding <paramref name="child"/> along the path —
    /// the start of the segment that ends at the child. For the first control point this is the head
    /// node at <see cref="HitObject.StartTime"/>.
    /// </summary>
    public double GetSegmentStartTime(SliderChild child)
    {
        // Node 0 is the head at StartTime; control point i is node i+1 at StartTime + TimeOffset.
        // IndexOf is by reference (each control point instance is unique), matching DrawableSliderBody.
        int index = Path.ControlPoints.IndexOf(child.ControlPoint);
        return index <= 0 ? StartTime : StartTime + Path.ControlPoints[index - 1].TimeOffset;
    }

    protected override void CreateNestedHitObjects(CancellationToken cancellationToken)
    {
        AddNested(new SliderHead(this)
        {
            StartTime = StartTime,
        });

        foreach (var controlPoint in Path.ControlPoints)
        {
            var childHitObject = new SliderChild(this, controlPoint)
            {
                StartTime = StartTime + controlPoint.TimeOffset,
            };
            AddNested(childHitObject);
        }
    }

    public override HitsoundFamily Hitsounds => HitsoundFamilies.SliderBody;
}
