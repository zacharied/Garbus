// The per-hit-object-type hitsound families. Each type owns a distinct field so its sound set can
// diverge independently; today every one is the single soft-hitnormal member.

using Garbus.Game.Gameplay.Audio;

namespace Garbus.Game.Objects
{
    public static class HitsoundFamilies
    {
        private static HitsoundFamily softNormal()
            => HitsoundFamily.Single(new HitSampleInfo(HitSampleInfo.HIT_NORMAL, HitSampleInfo.BANK_SOFT));

        public static readonly HitsoundFamily CardinalNote = softNormal();
        public static readonly HitsoundFamily ShoulderNote = softNormal();
        public static readonly HitsoundFamily CardinalHoldNote = softNormal();
        public static readonly HitsoundFamily ShoulderHoldNote = softNormal();
        public static readonly HitsoundFamily HoldNoteHead = softNormal();
        public static readonly HitsoundFamily SliderHead = softNormal();
        public static readonly HitsoundFamily SliderChild = softNormal();
        public static readonly HitsoundFamily SliderBody = softNormal();
        public static readonly HitsoundFamily SlamCentered = softNormal();
        public static readonly HitsoundFamily SlamEdge = softNormal();
    }
}
