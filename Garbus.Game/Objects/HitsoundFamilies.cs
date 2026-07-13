// The per-hit-object-type hitsound families. Each type owns a distinct field so its sound set can
// diverge independently; today every one is the single soft-hitnormal member.

using Garbus.Game.Gameplay.Audio;

namespace Garbus.Game.Objects
{
    public static class HitsoundFamilies
    {
        // The bundled hitsound asset (Samples/Gameplay/soft-hitnormal.wav).
        private const string soft_hitnormal = "Gameplay/soft-hitnormal";

        private static HitsoundFamily softNormal()
            => HitsoundFamily.Single(new GarbusHitSample(soft_hitnormal));

        public static readonly HitsoundFamily CardinalNote = HitsoundFamily.Single(new("Gameplay/cardinal"));
        public static readonly HitsoundFamily ShoulderNote = HitsoundFamily.Single(new("Gameplay/shoulder"));
        public static readonly HitsoundFamily CardinalHoldNote = HitsoundFamily.Single(new("Gameplay/cardinal"));
        public static readonly HitsoundFamily ShoulderHoldNote = HitsoundFamily.Single(new("Gameplay/shoulder"));
        public static readonly HitsoundFamily SliderHead = HitsoundFamily.Single(new("Gameplay/slider"));
        public static readonly HitsoundFamily SliderChild = HitsoundFamily.Single(new("Gameplay/slider"));
        public static readonly HitsoundFamily SliderBody = HitsoundFamily.Single(new("Gameplay/slider"));
        public static readonly HitsoundFamily SlamCentered = HitsoundFamily.Single(new("Gameplay/slam"));
        public static readonly HitsoundFamily SlamEdge = HitsoundFamily.Single(new("Gameplay/slam"));
    }
}
