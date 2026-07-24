using System;
using System.Collections.Generic;
using System.Linq;

namespace Garbus.Game.Edit;

/// <summary>
/// The single authority for converting between hit-object angles and the editor timeline's x-axis.
///
/// The editor unrolls the circle onto a horizontal axis. The grid centre is South (270°); cardinals
/// read North(edge) · West · South(centre) · East · North(edge) — the reflected <see cref="Direction"/>
/// <c>== −1</c> view centres North instead, reflecting about the East–West axis. On each side sits a
/// <see cref="GHOST_DEGREES"/>-wide "ghost" band previewing the wrap-around, so the full drawable width
/// spans <see cref="TOTAL_DEGREES"/>.
///
/// All x values here are fractions of the FULL editor playfield width (ghost bands included).
/// "Grid degrees" are degrees counter-clockwise from the left edge of the main grid, i.e.
/// <c>angle − ANGLE_ORIGIN</c> normalised to <c>[0, 360)</c>.
/// </summary>
public static class EditorAngleMapping
{
    /// <summary>
    /// The absolute angle at the grid centre column's reference — the origin from which grid-degrees are
    /// measured (grid-degree 0 is the left edge). At 90° the grid centre (grid-degree 180) lands on South
    /// (270°), so the cardinals read North(edge) · West · South(centre) · East · North(edge).
    /// </summary>
    public const int ANGLE_ORIGIN = 90;

    /// <summary>The angular width of each ghost wrap-around band.</summary>
    public const int GHOST_DEGREES = 30;

    /// <summary>The angular span of the full editor width: the 360° grid plus both ghost bands.</summary>
    public const int TOTAL_DEGREES = 360 + 2 * GHOST_DEGREES;

    /// <summary>
    /// View direction: <c>+1</c> unrolls the circle counter-clockwise with increasing x (South at the
    /// grid centre); <c>−1</c> reflects the mapping about the East–West axis (North at the centre,
    /// clockwise, West still left / East still right). Only +1 and −1 are valid. This is a global view
    /// preference — never serialized.
    /// </summary>
    public static int Direction { get; set; } = 1;

    /// <summary>Applies the current <see cref="Direction"/> reflection (θ → −θ when reversed).</summary>
    private static float applyDirection(float angleDeg) => Direction == 1 ? angleDeg : NormalizeDeg(-angleDeg);

    private static int applyDirection(int angleDeg) => Direction == 1 ? angleDeg : NormalizeDeg(-angleDeg);

    public static int NormalizeDeg(int angleDeg) => ((angleDeg % 360) + 360) % 360;

    public static float NormalizeDeg(float angleDeg)
    {
        angleDeg %= 360;
        return angleDeg < 0 ? angleDeg + 360 : angleDeg;
    }

    /// <summary>
    /// Degrees from the left edge of the main grid, in [0, 360) — grid-degree 0 is North (<see
    /// cref="ANGLE_ORIGIN"/> == 90°). Honors the current view <see cref="Direction"/>: in the reversed
    /// view the sense reads clockwise instead of counter-clockwise.
    /// </summary>
    public static float ToGridDegrees(float angleDeg) => NormalizeDeg(applyDirection(angleDeg) - ANGLE_ORIGIN);

    /// <summary>
    /// The absolute angle (in [0,360)) at a whole-degree grid position, honoring the given
    /// direction sign (+1/−1) explicitly rather than the static <see cref="Direction"/>. The integer
    /// inverse of <see cref="ToGridDegrees"/> at whole degrees — callers who must derive direction from
    /// something other than the (possibly stale, order-dependent) static should use this instead of
    /// re-deriving the reflection by hand.
    /// </summary>
    public static int GridDegreesToAngle(int gridDeg, int direction) =>
        direction == 1 ? NormalizeDeg(gridDeg + ANGLE_ORIGIN) : NormalizeDeg(-(gridDeg + ANGLE_ORIGIN));

    /// <summary>The x-fraction (of the full width, ghost bands included) at which an angle is drawn.</summary>
    public static float ToX(float angleDeg) => (GHOST_DEGREES + ToGridDegrees(angleDeg)) / TOTAL_DEGREES;

    /// <summary>
    /// The angle at an x-fraction of the full width, in [0, 360). Positions inside a ghost band wrap
    /// onto the far side of the grid, which is what makes clicks in the bands "just work".
    /// </summary>
    public static float ToAngle(float xFrac) => applyDirection(NormalizeDeg(xFrac * TOTAL_DEGREES - GHOST_DEGREES + ANGLE_ORIGIN));

    /// <summary>
    /// Where the ghost twin of an angle is drawn, as an x-fraction of the full width — non-null only
    /// when the angle lies within <see cref="GHOST_DEGREES"/> of a grid edge (so its clone is visible
    /// in the opposite band).
    /// </summary>
    public static float? GhostTwinX(float angleDeg)
    {
        float grid = ToGridDegrees(angleDeg);

        if (grid < GHOST_DEGREES)
            return (GHOST_DEGREES + grid + 360) / (float)TOTAL_DEGREES;
        if (grid > 360 - GHOST_DEGREES)
            return (GHOST_DEGREES + grid - 360) / (float)TOTAL_DEGREES;

        return null;
    }

    /// <summary>Rounds an absolute angle to the nearest multiple of <paramref name="increment"/>, normalised to [0, 360).</summary>
    public static int SnapAngle(float angleDeg, int increment) => NormalizeDeg((int)MathF.Round(angleDeg / increment) * increment);

    /// <summary>
    /// The minimal signed rotation (in (−180, 180]) taking <paramref name="fromDeg"/> to
    /// <paramref name="toDeg"/> — used to keep slider node offsets from spinning the long way round.
    /// </summary>
    public static int MinimalDiff(int fromDeg, int toDeg)
    {
        int d = NormalizeDeg(toDeg - fromDeg);
        return d > 180 ? d - 360 : d;
    }

    /// <summary>
    /// The 360°-multiples <c>k</c> for which a copy of an unwrapped grid-degree range, shifted left by
    /// <c>k·360</c>, intersects the visible window (the main grid plus both ghost bands). <c>k</c> = 0 is
    /// the unshifted copy; a range crossing the wrap seam yields two values, and each additional full
    /// turn of sweep adds one more. This is the ghost-twin idea generalised from a point to an extent,
    /// which is what lets slider paths wrap around the seam (and wind multiple turns).
    /// </summary>
    public static IEnumerable<int> VisibleWrapCopies(float minGridDeg, float maxGridDeg)
    {
        int first = (int)MathF.Ceiling((minGridDeg - 360 - GHOST_DEGREES) / 360);
        int last = (int)MathF.Floor((maxGridDeg + GHOST_DEGREES) / 360);

        for (int k = first; k <= last; k++)
            yield return k;
    }

    /// <summary>
    /// The signed grid-degree displacement, from a slider body's grid position, of a control-point node
    /// whose angle is <paramref name="rotationOffset"/> degrees counter-clockwise from the body. Equal to
    /// <c>ToGridDegrees(body + offset) − ToGridDegrees(body)</c> locally: <c>+offset</c> in the normal
    /// view, <c>−offset</c> in the reversed view (grid degrees increase the opposite way). Multiply by
    /// pixels-per-degree to get the node's on-screen x-offset from the head.
    /// </summary>
    public static int GridOffset(int rotationOffset) => Direction * rotationOffset;

    /// <summary>
    /// <see cref="VisibleWrapCopies"/> for a slider whose body sits at <paramref name="bodyGridDeg"/> and
    /// whose node rotation offsets (CCW degrees from the body) span
    /// [<paramref name="minOffset"/>, <paramref name="maxOffset"/>]. Applies the current
    /// <see cref="Direction"/> (via <see cref="GridOffset"/>) so a reversed view maps the offsets onto the
    /// correct grid side, and orders the endpoints (the reflection swaps min/max).
    /// </summary>
    public static IEnumerable<int> VisibleWrapCopiesForOffsets(float bodyGridDeg, int minOffset, int maxOffset)
    {
        float g1 = bodyGridDeg + GridOffset(minOffset);
        float g2 = bodyGridDeg + GridOffset(maxOffset);
        return VisibleWrapCopies(MathF.Min(g1, g2), MathF.Max(g1, g2));
    }

    /// <summary>
    /// Snaps in the unwrapped band domain so a cursor inside a ghost band stays there visually. Returns
    /// both the snapped x-fraction (same domain as the input) and the wrapped absolute angle to store.
    /// </summary>
    public static (float xFrac, int angleDeg) SnapX(float xFrac, int increment)
    {
        // Unwrapped absolute angle over the full width: [ANGLE_ORIGIN − GHOST, ANGLE_ORIGIN + 360 + GHOST].
        float unwrapped = xFrac * TOTAL_DEGREES - GHOST_DEGREES + ANGLE_ORIGIN;
        float snappedUnwrapped = MathF.Round(unwrapped / increment) * increment;
        float snappedX = (snappedUnwrapped - ANGLE_ORIGIN + GHOST_DEGREES) / TOTAL_DEGREES;
        return (snappedX, applyDirection(NormalizeDeg((int)snappedUnwrapped)));
    }

    /// <summary>
    /// The reflection "sum" (<c>2·φ</c>, normalised to [0,360)) whose pivot <c>φ</c> is the centre of the
    /// tightest angular arc covering <paramref name="angles"/> — the seam-robust bounding-box centre.
    /// Reflecting an angle <c>θ</c> by this sum is <c>NormalizeDeg(sum − θ)</c>. Returns 0 for empty input
    /// (the identity reflection about East). The covering arc is found as the complement of the largest
    /// circular gap between the sorted angles, so a selection straddling the wrap seam mirrors in place.
    /// </summary>
    public static int ReflectionSum(IEnumerable<int> angles)
    {
        var sorted = angles.Select(NormalizeDeg).Distinct().OrderBy(a => a).ToList();

        if (sorted.Count == 0)
            return 0;
        if (sorted.Count == 1)
            return NormalizeDeg(2 * sorted[0]);

        // Largest circular gap → the covering arc is everything else, starting just after that gap.
        int gapStart = 0;
        int largestGap = 360 - sorted[^1] + sorted[0]; // wrap gap: last → first
        for (int i = 1; i < sorted.Count; i++)
        {
            int gap = sorted[i] - sorted[i - 1];
            if (gap > largestGap)
            {
                largestGap = gap;
                gapStart = i;
            }
        }

        // Unwrap the run starting after the gap into monotone values; min is the start, max the far end.
        int start = sorted[gapStart];
        int max = start;
        for (int i = 0; i < sorted.Count; i++)
        {
            int a = sorted[(gapStart + i) % sorted.Count];
            max = Math.Max(max, start + NormalizeDeg(a - start));
        }

        return NormalizeDeg(start + max);
    }
}
