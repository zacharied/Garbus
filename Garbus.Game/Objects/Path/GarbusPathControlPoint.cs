// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Core/Path/BacPathControlPoint.cs).
// BacPathControlPoint → GarbusPathControlPoint.

using osu.Framework.Graphics;

namespace Garbus.Game.Objects;

public class GarbusPathControlPoint
{
    public double TimeOffset;
    public int RotationOffset;

    /// <summary>
    /// Interpolation applied to the segment leading INTO this control point (from the previous node),
    /// letting a single path mix methods at different points.
    /// </summary>
    /// <remarks>
    /// When enabled, the angle over that segment uses a Catmull-Rom spline for a continuous sweep
    /// velocity. Its tangents are derived from neighbouring nodes, so it changes the rendered geometry
    /// (adds anticipation/overshoot) and only stays C1-continuous where adjacent segments are also
    /// smoothed — off by default so a segment keeps its exact linear geometry.
    /// </remarks>
    public bool Smooth;

    /// <summary>
    /// Eases the angle's progress across the segment leading INTO this control point, reshaping the
    /// sweep feel without moving the endpoints in time or angle and without look-ahead — so it changes
    /// neither timing nor geometry (no overshoot/anticipation). Defaults to <see cref="Easing.None"/>
    /// (constant-velocity linear). Note: overshooting easings (Back/Elastic/Bounce) reintroduce overshoot.
    /// </summary>
    public Easing SweepEasing = Easing.None;
}
