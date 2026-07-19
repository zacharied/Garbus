// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Scoring/DefaultHitWindows.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: hit windows are fixed — the difficulty interpolation is gone. Interim shared
// note windows (the cardinal table) — replaced by per-type windows and deleted in the next task.

using System;

namespace Garbus.Game.Gameplay.Scoring
{
    /// <summary>
    /// The fixed <see cref="HitWindows"/> used by Garbus notes.
    /// </summary>
    public class DefaultHitWindows : HitWindows
    {
        private const double critical_perfect_window = 32;
        private const double perfect_window = 64;
        private const double near_window = 110;
        private const double early_miss_window = 200;

        public override bool IsHitResultAllowed(HitResult result)
            => result is HitResult.CriticalPerfect or HitResult.Perfect or HitResult.Near or HitResult.Miss;

        public override HitWindowRange WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.CriticalPerfect:
                    return HitWindowRange.Symmetric(critical_perfect_window);

                case HitResult.Perfect:
                    return HitWindowRange.Symmetric(perfect_window);

                case HitResult.Near:
                    return HitWindowRange.Symmetric(near_window);

                case HitResult.Miss:
                    return new HitWindowRange(early_miss_window, 0);

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }
    }
}
