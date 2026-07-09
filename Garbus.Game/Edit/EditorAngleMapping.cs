// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/EditorAngleMapping.cs).
// Zero osu.Game dependencies — verbatim port, namespace only.

using System;
using System.Collections.Generic;

namespace Garbus.Game.Edit;

/// <summary>
/// The single authority for converting between hit-object angles and the editor timeline's x-axis.
///
/// The editor unrolls the circle onto a horizontal axis: the left edge of the main grid sits on the
/// North/West quadrant boundary (135°) and angle increases (counter-clockwise in the game's polar
/// convention) to the right — so the four cardinal lanes read West, South, East, North left-to-right,
/// each centred a comfortable margin inside the grid rather than split across the wrap seam. The seam
/// itself lands on a diagonal quadrant boundary, so no cardinal lane straddles it. On each side sits a
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
    /// The absolute angle at the left edge of the main grid — the North/West quadrant boundary, chosen
    /// so the wrap seam falls on a diagonal and every cardinal lane stays whole.
    /// </summary>
    public const int ANGLE_ORIGIN = 135;

    /// <summary>The angular width of each ghost wrap-around band.</summary>
    public const int GHOST_DEGREES = 30;

    /// <summary>The angular span of the full editor width: the 360° grid plus both ghost bands.</summary>
    public const int TOTAL_DEGREES = 360 + 2 * GHOST_DEGREES;

    public static int NormalizeDeg(int angleDeg) => ((angleDeg % 360) + 360) % 360;

    public static float NormalizeDeg(float angleDeg)
    {
        angleDeg %= 360;
        return angleDeg < 0 ? angleDeg + 360 : angleDeg;
    }

    /// <summary>Degrees counter-clockwise from the left (West) edge of the main grid, in [0, 360).</summary>
    public static float ToGridDegrees(float angleDeg) => NormalizeDeg(angleDeg - ANGLE_ORIGIN);

    /// <summary>The x-fraction (of the full width, ghost bands included) at which an angle is drawn.</summary>
    public static float ToX(float angleDeg) => (GHOST_DEGREES + ToGridDegrees(angleDeg)) / TOTAL_DEGREES;

    /// <summary>
    /// The angle at an x-fraction of the full width, in [0, 360). Positions inside a ghost band wrap
    /// onto the far side of the grid, which is what makes clicks in the bands "just work".
    /// </summary>
    public static float ToAngle(float xFrac) => NormalizeDeg(xFrac * TOTAL_DEGREES - GHOST_DEGREES + ANGLE_ORIGIN);

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
    /// Snaps in the unwrapped band domain so a cursor inside a ghost band stays there visually. Returns
    /// both the snapped x-fraction (same domain as the input) and the wrapped absolute angle to store.
    /// </summary>
    public static (float xFrac, int angleDeg) SnapX(float xFrac, int increment)
    {
        // Unwrapped absolute angle over the full width: [ANGLE_ORIGIN − GHOST, ANGLE_ORIGIN + 360 + GHOST].
        float unwrapped = xFrac * TOTAL_DEGREES - GHOST_DEGREES + ANGLE_ORIGIN;
        float snappedUnwrapped = MathF.Round(unwrapped / increment) * increment;
        float snappedX = (snappedUnwrapped - ANGLE_ORIGIN + GHOST_DEGREES) / TOTAL_DEGREES;
        return (snappedX, NormalizeDeg((int)snappedUnwrapped));
    }
}
