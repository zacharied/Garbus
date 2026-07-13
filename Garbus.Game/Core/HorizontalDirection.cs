// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Core/HorizontalDirection.cs).

using System;
using osu.Framework.Graphics;

namespace Garbus.Game.Core;

public enum HorizontalDirection
{
    Left = -1,
    Right = 1
}

public static class HorizontalDirectionExtensions
{
    public static int ToAngleDeg(this HorizontalDirection horizontalDirection) => horizontalDirection switch
    {
        HorizontalDirection.Right => 0,
        HorizontalDirection.Left => 180,
        _ => throw new InvalidOperationException()
    };

    public static Colour4 ToColour(this HorizontalDirection horizontalDirection) => horizontalDirection switch
    {
        HorizontalDirection.Left => Constants.LeftColour,
        HorizontalDirection.Right => Constants.RightColour,
        _ => throw new InvalidOperationException()
    };
}
