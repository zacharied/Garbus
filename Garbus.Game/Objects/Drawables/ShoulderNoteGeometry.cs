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
}
