using System;
using osuTK;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// Pure polar geometry for a <see cref="Objects.ShoulderNote"/>'s two-square-plus-arc visual. The two
/// squares sit at the ±45° quadrant diagonals either side of the note's cardinal angle (0° East for a
/// right shoulder, 180° West for a left one) and ride outward as the note's travel radius grows.
/// </summary>
public static class ShoulderNoteGeometry
{
    /// <summary>Angular offset of each square from the note's cardinal line — the quadrant diagonal.</summary>
    public const float DiagonalOffsetDeg = 45f;

    public static float ToRadians(float degrees) => degrees * MathF.PI / 180f;

    /// <summary>Playfield polar-to-cartesian: θ = 0 points right, increasing counter-clockwise.</summary>
    public static Vector2 Polar(float radians, float radius)
        => new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);

    /// <summary>
    /// Position of a shoulder square at <paramref name="radius"/> from centre, offset
    /// <paramref name="offsetSign"/>·45° from <paramref name="baseAngleDeg"/>. Pass +1 / −1 for the two squares.
    /// </summary>
    public static Vector2 SquarePosition(float baseAngleDeg, float radius, float offsetSign)
        => Polar(ToRadians(baseAngleDeg + offsetSign * DiagonalOffsetDeg), radius);

    /// <summary>
    /// Inner-end angle (degrees) of a growing spawn half-arc. The outer end stays pinned to its square at
    /// <paramref name="baseAngleDeg"/> + <paramref name="offsetSign"/>·45°; the inner end sweeps from there
    /// toward <paramref name="baseAngleDeg"/> as <paramref name="easedProgress"/> runs 0 → 1. At 0 the span
    /// is zero (nothing drawn); at 1 both halves' inner ends meet on the base angle.
    /// </summary>
    public static float SpawnArcInnerAngleDeg(float baseAngleDeg, float offsetSign, float easedProgress)
        => baseAngleDeg + offsetSign * DiagonalOffsetDeg * (1f - easedProgress);

    /// <summary>
    /// Rotation (degrees) for a <see cref="osu.Framework.Graphics.UserInterface.CircularProgress"/> whose
    /// 0.25 progress wedge should be centred on <paramref name="baseAngleDeg"/>'s screen direction, spanning
    /// ±45°. CircularProgress fills clockwise from local up; screen-clockwise angle for playfield angle θ is
    /// 90−θ, and the unrotated wedge centre sits at +45°, so rotation = (90−θ)−45 = 45−θ.
    /// </summary>
    public static float SectorRotationDeg(float baseAngleDeg) => 45f - baseAngleDeg;
}
