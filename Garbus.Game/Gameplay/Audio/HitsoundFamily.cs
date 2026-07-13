using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Gameplay.Audio
{
    /// <summary>
    /// A per-hit-object-type set of hitsounds, one per earnable judgement. The sound played on a hit is
    /// chosen by the earned <see cref="HitResult"/>; a judgement with no member of its own falls back to
    /// the nearest better member, then the nearest worse.
    /// </summary>
    public class HitsoundFamily
    {
        private readonly Dictionary<HitResult, GarbusHitSample> members = new Dictionary<HitResult, GarbusHitSample>();

        /// <summary>
        /// Assigns a member for object-initializer construction: <c>new HitsoundFamily { [HitResult.Perfect] = sample }</c>.
        /// </summary>
        public GarbusHitSample this[HitResult result]
        {
            set => members[result] = value;
        }

        /// <summary>
        /// Every distinct member, for preloading.
        /// </summary>
        public IEnumerable<GarbusHitSample> AllSamples => members.Values.Distinct();

        /// <summary>
        /// Builds a family with a single member, keyed at the type's best judgement by default.
        /// </summary>
        public static HitsoundFamily Single(GarbusHitSample sample, HitResult key = HitResult.Perfect)
            => new HitsoundFamily { [key] = sample };

        /// <summary>
        /// Resolves the member to play for an earned judgement: nearest at-least-as-good member first,
        /// then nearest worse. Null if the family is empty.
        /// </summary>
        public GarbusHitSample? Resolve(HitResult earned)
        {
            int earnedIndex = earned.GetIndexForOrderedDisplay();

            HitResult? better = members.Keys
                                       .Where(k => k.GetIndexForOrderedDisplay() <= earnedIndex)
                                       .OrderByDescending(k => k.GetIndexForOrderedDisplay())
                                       .Cast<HitResult?>()
                                       .FirstOrDefault();

            if (better is HitResult b)
                return members[b];

            HitResult? worse = members.Keys
                                      .Where(k => k.GetIndexForOrderedDisplay() > earnedIndex)
                                      .OrderBy(k => k.GetIndexForOrderedDisplay())
                                      .Cast<HitResult?>()
                                      .FirstOrDefault();

            return worse is HitResult w ? members[w] : null;
        }
    }
}
