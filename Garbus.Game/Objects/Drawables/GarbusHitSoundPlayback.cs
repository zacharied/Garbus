// Shared judgement-based hitsound playback for Garbus drawables. Both DrawableGarbusHitObject<T> and
// DrawableSliderChild (which derives from the vendored DrawableHitObject directly) route PlaySamples
// through here so the resolution logic lives in one place.

using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Judgements;

namespace Garbus.Game.Objects.Drawables
{
    internal static class GarbusHitSoundPlayback
    {
        public static void Play(HitSoundContainer? samples, GarbusHitObject hitObject, JudgementResult? result)
        {
            // Playback is gated to hits; misses (and unjudged states) stay silent.
            if (samples == null || result?.IsHit != true)
                return;

            samples.Play(hitObject.Hitsounds.Resolve(result.Type));
        }
    }
}
