// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Scoring/DefaultHitWindows.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: hit windows are fixed — the difficulty interpolation is gone and the values are
// osu's at the middle of its difficulty range.

using System;

namespace Garbus.Game.Gameplay.Scoring
{
    /// <summary>
    /// The fixed <see cref="HitWindows"/> used by Garbus notes.
    /// </summary>
    public class DefaultHitWindows : HitWindows
    {
        private const double perfect_window = 19.4;
        private const double great_window = 49;
        private const double good_window = 82;
        private const double ok_window = 112;
        private const double meh_window = 136;
        private const double miss_window = 173;

        public override HitWindowRange WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return HitWindowRange.Symmetric(perfect_window);

                case HitResult.Great:
                    return HitWindowRange.Symmetric(great_window);

                case HitResult.Good:
                    return HitWindowRange.Symmetric(good_window);

                case HitResult.Ok:
                    return HitWindowRange.Symmetric(ok_window);

                case HitResult.Meh:
                    return HitWindowRange.Symmetric(meh_window);

                case HitResult.Miss:
                    return HitWindowRange.Symmetric(miss_window);

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}
