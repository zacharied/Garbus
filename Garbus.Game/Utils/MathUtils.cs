// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/MathUtils/MathUtils.cs).

using System;

namespace Garbus.Game.Utils;

public static class MathUtils
{
    public static float DegToRad(float degrees) => degrees * MathF.PI / 180f;
    public static float DegToRad(int degrees) => degrees * MathF.PI / 180f;
    public static float RadToDeg(float radians) => radians * 180f / MathF.PI;
    public static int RadToDegI(float radians) => (int)MathF.Round(radians * 180f / MathF.PI);
}
