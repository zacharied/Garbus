// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Beatmaps/ControlPoints/IControlPoint.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace only.

namespace Garbus.Game.Charts.Timing
{
    public interface IControlPoint
    {
        /// <summary>
        /// The time at which the control point takes effect.
        /// </summary>
        double Time { get; }
    }
}
