// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Objects/Types/IHasDuration.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace only.

namespace Garbus.Game.Gameplay.Objects.Types
{
    /// <summary>
    /// A HitObject that ends at a different time than its start time.
    /// </summary>
    public interface IHasDuration
    {
        /// <summary>
        /// The time at which the HitObject ends.
        /// </summary>
        double EndTime { get; }

        /// <summary>
        /// The duration of the HitObject.
        /// </summary>
        double Duration { get; set; }
    }
}
