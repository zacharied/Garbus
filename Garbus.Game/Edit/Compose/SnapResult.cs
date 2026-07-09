// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/SnapResult.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; Playfield is Garbus.Game.Gameplay.UI.Playfield.

using Garbus.Game.Gameplay.UI;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// The result of a position/time snapping process.
    /// </summary>
    public class SnapResult
    {
        /// <summary>
        /// The screen space position, potentially altered for snapping.
        /// </summary>
        public Vector2 ScreenSpacePosition;

        /// <summary>
        /// The resultant time for snapping, if a value could be attained.
        /// </summary>
        public double? Time;

        /// <summary>
        /// The <see cref="Playfield"/> on which the snap occurred, if any.
        /// </summary>
        public readonly Playfield? Playfield;

        public SnapResult(Vector2 screenSpacePosition, double? time, Playfield? playfield = null)
        {
            ScreenSpacePosition = screenSpacePosition;
            Time = time;
            Playfield = playfield;
        }
    }
}
