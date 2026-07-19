// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Scoring/HitResult.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: osu's members are replaced by the Garbus judgement ladder
// (docs/rules-specs/Judgement.md) — one shared ordinal ladder whose subsets form the note, hold and
// early-permissive families. Ticks, bonuses, ComboBreak and the family-specific osu grades are gone;
// the Ignore pair remains for unscored expiry judgements (the slider body).

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Utils;

namespace Garbus.Game.Gameplay.Scoring
{
    [HasOrderedElements]
    public enum HitResult
    {
        /// <summary>
        /// Indicates that the object has not been judged yet.
        /// </summary>
        [Description(@"")]
        [Order(5)]
        None,

        /// <summary>
        /// The object was missed — by its windows elapsing with no qualifying input, or by an
        /// early-miss press. Shared by every judgement family.
        /// </summary>
        [Description(@"Miss")]
        [Order(4)]
        Miss,

        /// <summary>
        /// The hold-family intermediate judgement (hold tails, slider children).
        /// </summary>
        [Description(@"Bad")]
        [Order(3)]
        Bad,

        /// <summary>
        /// The note- and early-permissive-family intermediate judgement.
        /// </summary>
        [Description(@"Near")]
        [Order(2)]
        Near,

        /// <summary>
        /// Shared by every judgement family; the best result for catch-timed and early-permissive
        /// objects, whose families have no Critical Perfect.
        /// </summary>
        [Description(@"Perfect")]
        [Order(1)]
        Perfect,

        /// <summary>
        /// The best judgement of the note and hold families.
        /// </summary>
        [Description(@"Critical Perfect")]
        [Order(0)]
        CriticalPerfect,

        /// <summary>
        /// Indicates a miss that should be ignored for scoring purposes.
        /// </summary>
        [Order(6)]
        IgnoreMiss,

        /// <summary>
        /// Indicates a hit that should be ignored for scoring purposes.
        /// </summary>
        [Order(7)]
        IgnoreHit,
    }

    public static class HitResultExtensions
    {
        private static readonly IList<HitResult> order = EnumExtensions.GetValuesInOrder<HitResult>().ToList();

        /// <summary>
        /// Whether a <see cref="HitResult"/> increases the combo.
        /// </summary>
        public static bool IncreasesCombo(this HitResult result)
            => AffectsCombo(result) && IsHit(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> breaks the combo and resets it back to zero.
        /// </summary>
        public static bool BreaksCombo(this HitResult result)
            => AffectsCombo(result) && !IsHit(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> increases or breaks the combo: every basic non-Miss
        /// result increases it, Miss breaks it, the Ignore pair does neither.
        /// </summary>
        public static bool AffectsCombo(this HitResult result)
            => result >= HitResult.Miss && result <= HitResult.CriticalPerfect;

        /// <summary>
        /// Whether a <see cref="HitResult"/> affects the accuracy portion of the score.
        /// </summary>
        public static bool AffectsAccuracy(this HitResult result) => IsScorable(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> is a basic (scorable, non-ignore) result.
        /// </summary>
        public static bool IsBasic(this HitResult result) => IsScorable(result);

        /// <summary>
        /// Whether a <see cref="HitResult"/> represents a miss of any type.
        /// </summary>
        /// <remarks>
        /// Of note, both <see cref="IsMiss"/> and <see cref="IsHit"/> return <see langword="false"/> for <see cref="HitResult.None"/>.
        /// </remarks>
        public static bool IsMiss(this HitResult result)
            => result is HitResult.Miss or HitResult.IgnoreMiss;

        /// <summary>
        /// Whether a <see cref="HitResult"/> represents a successful hit.
        /// </summary>
        /// <remarks>
        /// Of note, both <see cref="IsMiss"/> and <see cref="IsHit"/> return <see langword="false"/> for <see cref="HitResult.None"/>.
        /// </remarks>
        public static bool IsHit(this HitResult result)
        {
            switch (result)
            {
                case HitResult.None:
                case HitResult.Miss:
                case HitResult.IgnoreMiss:
                    return false;

                default:
                    return true;
            }
        }

        /// <summary>
        /// Whether a <see cref="HitResult"/> is scorable.
        /// </summary>
        public static bool IsScorable(this HitResult result)
            => result >= HitResult.Miss && result < HitResult.IgnoreMiss;

        /// <summary>
        /// An array of all <see cref="HitResult"/>s.
        /// </summary>
        public static readonly HitResult[] ALL_TYPES = Enum.GetValues<HitResult>().ToArray();

        /// <summary>
        /// Whether a <see cref="HitResult"/> is valid within a given <see cref="HitResult"/> range.
        /// </summary>
        /// <param name="result">The <see cref="HitResult"/> to check.</param>
        /// <param name="minResult">The minimum <see cref="HitResult"/>.</param>
        /// <param name="maxResult">The maximum <see cref="HitResult"/>.</param>
        /// <returns>Whether <see cref="HitResult"/> falls between <paramref name="minResult"/> and <paramref name="maxResult"/>.</returns>
        public static bool IsValidHitResult(this HitResult result, HitResult minResult, HitResult maxResult)
        {
            if (result == HitResult.None)
                return false;

            if (result == minResult || result == maxResult)
                return true;

            Debug.Assert(minResult <= maxResult);
            return result > minResult && result < maxResult;
        }

        /// <summary>
        /// Ordered index of a <see cref="HitResult"/>. Used for consistent order when displaying hit results to the user.
        /// </summary>
        public static int GetIndexForOrderedDisplay(this HitResult result) => order.IndexOf(result);

        public static void ValidateHitResultPair(HitResult maxResult, HitResult minResult)
        {
            if (maxResult == HitResult.None || !IsHit(maxResult))
                throw new ArgumentOutOfRangeException(nameof(maxResult), $"{maxResult} is not a valid maximum judgement result.");

            if (minResult == HitResult.None || IsHit(minResult))
                throw new ArgumentOutOfRangeException(nameof(minResult), $"{minResult} is not a valid minimum judgement result.");

            if (maxResult == HitResult.IgnoreHit && minResult != HitResult.IgnoreMiss)
                throw new ArgumentOutOfRangeException(nameof(minResult), $"{minResult} is not a valid minimum result for a {maxResult} judgement.");

            if (maxResult.IsBasic() && minResult != HitResult.Miss)
                throw new ArgumentOutOfRangeException(nameof(minResult), $"{HitResult.Miss} is the only valid minimum result for a {maxResult} judgement.");
        }
    }
}
