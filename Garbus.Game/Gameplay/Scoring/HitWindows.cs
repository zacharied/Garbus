// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Scoring/HitWindows.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: SetDifficulty removed (windows are fixed), and windows are asymmetric
// (early, late) ranges per docs/rules-specs/Judgement.md — ResultFor is sign-aware, a zero side
// means "no window on that side" (the note-family Miss window is early-only), and hittability keys
// off LateEligibilityEdge (the late extent of the latest non-Miss window), not the Miss window.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Garbus.Game.Gameplay.Scoring
{
    /// <summary>
    /// An asymmetric timing window: how far before (<see cref="Early"/>) and after (<see cref="Late"/>)
    /// an object's time an input still falls within the window. Both bounds are inclusive. A zero side
    /// means the window does not extend to that side.
    /// </summary>
    public readonly struct HitWindowRange
    {
        public double Early { get; }
        public double Late { get; }

        public HitWindowRange(double early, double late)
        {
            Early = early;
            Late = late;
        }

        public static HitWindowRange Symmetric(double extent) => new HitWindowRange(extent, extent);

        /// <summary>
        /// Whether a signed time offset (negative = early) falls inside this window.
        /// </summary>
        public bool Contains(double timeOffset) => timeOffset < 0 ? -timeOffset <= Early : timeOffset <= Late;
    }

    /// <summary>
    /// A structure containing timing data for hit window based gameplay.
    /// </summary>
    public abstract class HitWindows
    {
        /// <summary>
        /// An empty <see cref="HitWindows"/> whose windows are all zero-width. Used by objects that
        /// have no timed button input.
        /// </summary>
        public static HitWindows Empty { get; } = new EmptyHitWindows();

        protected HitWindows()
        {
            ensureValidHitWindows();
        }

        [Conditional("DEBUG")]
        private void ensureValidHitWindows()
        {
            bool anyMiss = false;
            bool anyNonMiss = false;

            // Windows must nest: walking worst -> best, each present side must shrink or stay equal.
            // A zero side is "absent" (e.g. the early-only Miss window has Late == 0) and exempt.
            double lastEarly = double.PositiveInfinity;
            double lastLate = double.PositiveInfinity;

            foreach (var (result, window) in GetAllAvailableWindows())
            {
                anyMiss |= result == HitResult.Miss;
                anyNonMiss |= result != HitResult.Miss;

                if (window.Early > 0)
                {
                    Debug.Assert(window.Early <= lastEarly, $"{GetType().Name}: early extents must not grow toward better judgements.");
                    lastEarly = window.Early;
                }

                if (window.Late > 0)
                {
                    Debug.Assert(window.Late <= lastLate, $"{GetType().Name}: late extents must not grow toward better judgements.");
                    lastLate = window.Late;
                }
            }

            Debug.Assert(anyMiss, $"{nameof(GetAllAvailableWindows)} should always contain {nameof(HitResult.Miss)}");
            Debug.Assert(anyNonMiss, $"{nameof(GetAllAvailableWindows)} should always contain at least one result type other than {nameof(HitResult.Miss)}.");
        }

        /// <summary>
        /// Retrieves the <see cref="HitResult"/> with the largest hit window that produces a successful hit.
        /// </summary>
        /// <returns>The lowest allowed successful <see cref="HitResult"/>.</returns>
        protected HitResult LowestSuccessfulHitResult()
        {
            for (var result = HitResult.Bad; result <= HitResult.CriticalPerfect; ++result)
            {
                if (IsHitResultAllowed(result))
                    return result;
            }

            return HitResult.None;
        }

        /// <summary>
        /// Retrieves a mapping of <see cref="HitResult"/>s to their timing windows for all allowed
        /// <see cref="HitResult"/>s, worst (Miss) first.
        /// </summary>
        public IEnumerable<(HitResult result, HitWindowRange window)> GetAllAvailableWindows()
        {
            for (var result = HitResult.Miss; result <= HitResult.CriticalPerfect; ++result)
            {
                if (IsHitResultAllowed(result))
                    yield return (result, WindowFor(result));
            }
        }

        /// <summary>
        /// Check whether it is possible to achieve the provided <see cref="HitResult"/>.
        /// </summary>
        public virtual bool IsHitResultAllowed(HitResult result) => true;

        /// <summary>
        /// Retrieves the <see cref="HitResult"/> for a signed time offset (negative = early).
        /// </summary>
        /// <returns>The innermost (best) containing window's result, or <see cref="HitResult.None"/>
        /// if no window contains the offset — the input does not interact with the object.</returns>
        public HitResult ResultFor(double timeOffset)
        {
            for (var result = HitResult.CriticalPerfect; result >= HitResult.Miss; --result)
            {
                if (IsHitResultAllowed(result) && WindowFor(result).Contains(timeOffset))
                    return result;
            }

            return HitResult.None;
        }

        /// <summary>
        /// Retrieves the (early, late) hit window for a <see cref="HitResult"/>.
        /// </summary>
        public abstract HitWindowRange WindowFor(HitResult result);

        /// <summary>
        /// The late extent of the latest non-Miss window: how long after an object's time it stays
        /// hittable. Once this elapses the object is Missed automatically — there is no late Miss
        /// window (see the judgement spec).
        /// </summary>
        public double LateEligibilityEdge
        {
            get
            {
                var lowest = LowestSuccessfulHitResult();
                return lowest == HitResult.None ? 0 : WindowFor(lowest).Late;
            }
        }

        /// <summary>
        /// The largest early extent among the allowed windows: how long before an object's time an
        /// input can first interact with it (for note-family windows, the early-miss extent).
        /// </summary>
        public double EarliestInteractionEdge
        {
            get
            {
                double edge = 0;

                foreach (var (_, window) in GetAllAvailableWindows())
                    edge = Math.Max(edge, window.Early);

                return edge;
            }
        }

        /// <summary>
        /// Given a time offset, whether the <see cref="Objects.HitObject"/> can ever be hit in the
        /// future with a non-<see cref="HitResult.Miss"/> result.
        /// </summary>
        public bool CanBeHit(double timeOffset) => timeOffset <= LateEligibilityEdge;

        private class EmptyHitWindows : HitWindows
        {
            public override bool IsHitResultAllowed(HitResult result) => true;

            public override HitWindowRange WindowFor(HitResult result) => default;
        }
    }
}
