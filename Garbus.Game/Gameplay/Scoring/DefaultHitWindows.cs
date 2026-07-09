// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Scoring/DefaultHitWindows.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: IBeatmapDifficultyInfo.DifficultyRange inlined as a local helper (no beatmap
// difficulty infrastructure); window values are osu's verbatim.

using System;

namespace Garbus.Game.Gameplay.Scoring
{
    /// <summary>
    /// The default <see cref="HitWindows"/> used by Garbus notes.
    /// </summary>
    public class DefaultHitWindows : HitWindows
    {
        private static readonly (double min, double mid, double max) perfect_window_range = (22.4D, 19.4D, 13.9D);
        private static readonly (double min, double mid, double max) great_window_range = (64, 49, 34);
        private static readonly (double min, double mid, double max) good_window_range = (97, 82, 67);
        private static readonly (double min, double mid, double max) ok_window_range = (127, 112, 97);
        private static readonly (double min, double mid, double max) meh_window_range = (151, 136, 121);
        private static readonly (double min, double mid, double max) miss_window_range = (188, 173, 158);

        private double perfect;
        private double great;
        private double good;
        private double ok;
        private double meh;
        private double miss;

        public override void SetDifficulty(double difficulty)
        {
            perfect = difficultyRange(difficulty, perfect_window_range);
            great = difficultyRange(difficulty, great_window_range);
            good = difficultyRange(difficulty, good_window_range);
            ok = difficultyRange(difficulty, ok_window_range);
            meh = difficultyRange(difficulty, meh_window_range);
            miss = difficultyRange(difficulty, miss_window_range);
        }

        public override double WindowFor(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                    return perfect;

                case HitResult.Great:
                    return great;

                case HitResult.Good:
                    return good;

                case HitResult.Ok:
                    return ok;

                case HitResult.Meh:
                    return meh;

                case HitResult.Miss:
                    return miss;

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        // Maps a difficulty value [0, 10] onto a (min, mid, max) range, mid at difficulty 5.
        // Matches osu's IBeatmapDifficultyInfo.DifficultyRange.
        private static double difficultyRange(double difficulty, (double min, double mid, double max) range)
        {
            if (difficulty > 5)
                return range.mid + (range.max - range.mid) * (difficulty - 5) / 5;

            if (difficulty < 5)
                return range.mid + (range.mid - range.min) * (difficulty - 5) / 5;

            return range.mid;
        }
    }
}
