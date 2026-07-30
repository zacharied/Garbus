// The combo-break one-shot: a single feedback sample played when a Miss ends a long combo. Preloaded
// through HitSoundContainer (the same DrawableSample-backed path hitsounds use) so the sound is
// resolved from the store at load time and playback on the miss itself costs no store lookup.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Gameplay.Audio
{
    public partial class ComboBreakSound : CompositeDrawable
    {
        /// <summary>
        /// The bundled combo-break asset (Samples/Gameplay/combo-break.wav).
        /// </summary>
        public static readonly GarbusHitSample SAMPLE = new GarbusHitSample("Gameplay/combo-break");

        /// <summary>
        /// The combo a Miss must break for the sound to play — a broken combo of exactly this value
        /// stays silent, so the sound marks the loss of a run that had gone past it.
        /// </summary>
        public const int MINIMUM_BROKEN_COMBO = 50;

        private readonly HitSoundContainer sound = new HitSoundContainer();

        [BackgroundDependencyLoader]
        private void load()
        {
            // AddInternal during load resolves the container's sample store immediately, so assigning
            // Samples here preloads the asset off the update thread.
            AddInternal(sound);
            sound.Samples = new List<GarbusHitSample> { SAMPLE };
        }

        /// <summary>
        /// Plays the sound if <paramref name="result"/> is a Miss that broke a combo above
        /// <see cref="MINIMUM_BROKEN_COMBO"/>. Any other judgement, or a shorter combo, is a no-op.
        /// </summary>
        /// <param name="result">The judgement just earned.</param>
        /// <param name="comboBeforeJudgement">The combo the player held going into that judgement.</param>
        public void PlayIfComboBroken(HitResult result, int comboBeforeJudgement)
        {
            if (result != HitResult.Miss || comboBeforeJudgement <= MINIMUM_BROKEN_COMBO)
                return;

            sound.Play(SAMPLE);
        }

        /// <summary>How many times the sound has played. Test observability seam.</summary>
        public int PlayCount => sound.PlayCount;

        /// <summary>Whether the sample resolved from the store. Test observability seam.</summary>
        public bool Preloaded => sound.LoadedCount == 1;
    }
}
