// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/BeatDivisorType.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace changed to Garbus.Game.Edit (was osu.Game.Screens.Edit.Compose.Components).

namespace Garbus.Game.Edit
{
    public enum BeatDivisorType
    {
        /// <summary>
        /// Most common divisors, all with denominators being powers of two.
        /// </summary>
        Common,

        /// <summary>
        /// Divisors with denominators divisible by 3.
        /// </summary>
        Triplets,

        /// <summary>
        /// Fully arbitrary/custom beat divisors.
        /// </summary>
        Custom,
    }
}
