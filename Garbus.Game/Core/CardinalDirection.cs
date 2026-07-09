// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Core/CardinalDirection.cs).

using System;

namespace Garbus.Game.Core;

public enum CardinalDirection
{
    East = 0,
    North = 1,
    West = 2,
    South = 3
}

public static class CardinalDirectionExtensions
{
    public static float ToRadians(this CardinalDirection direction)
    {
        return (int)direction * MathF.PI / 2;
    }

    public static int ToDegrees(this CardinalDirection direction)
    {
        return (int)direction * 90;
    }

    /// <summary>
    /// The cardinal direction nearest to <paramref name="angleDeg"/>. The angle is normalised into
    /// <c>[0, 360)</c> (so negatives are handled) and rounded to the closest quadrant — e.g. 46° → North,
    /// 315° → East. Exact half-way angles (45°, 135°, …) round up to the next counter-clockwise cardinal.
    /// </summary>
    public static CardinalDirection FromAngle(int angleDeg)
    {
        int normalised = ((angleDeg % 360) + 360) % 360;
        return (CardinalDirection)(((normalised + 45) / 90) % 4);
    }
}
