// The thin replacement for osu.Game's SkinnableSound/PausableSkinnableSound (which are skin-entangled
// and deliberately not vendored — see PLAN-port.md). Plays a hit object's samples from the game's
// single sample store via osu-framework's DrawableSample, so audio adjustments flow through the
// drawable hierarchy exactly as they do for osu's skinnable sounds.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.Gameplay.Audio
{
    public partial class HitSoundContainer : CompositeDrawable
    {
        /// <summary>
        /// The single fixed playback volume (0–100) applied to every hitsound. Hitsounds are not
        /// chart-author configured, so there is no per-sample volume.
        /// </summary>
        public const int HITSOUND_VOLUME = 100;

        [Resolved]
        private ISampleStore sampleStore { get; set; } = null!;

        private readonly List<(GarbusHitSample sample, DrawableSample drawable)> drawableSamples = new List<(GarbusHitSample, DrawableSample)>();
        private readonly List<SampleChannel> playingChannels = new List<SampleChannel>();

        /// <summary>
        /// The longest length among the loaded samples, in milliseconds.
        /// </summary>
        public double Length => drawableSamples.Count == 0 ? 0 : drawableSamples.Max(s => s.drawable.Length);

        /// <summary>
        /// Loads the given samples, replacing any previously loaded set. Unresolvable lookups are skipped.
        /// </summary>
        public IEnumerable<GarbusHitSample> Samples
        {
            set
            {
                ClearSamples();

                foreach (var sample in value)
                {
                    Sample? loaded = sampleStore.Get(sample.Name);

                    if (loaded == null)
                        continue;

                    var drawableSample = new DrawableSample(loaded)
                    {
                        Volume = { Value = HITSOUND_VOLUME / 100.0 },
                    };

                    drawableSamples.Add((sample, drawableSample));
                    AddInternal(drawableSample);
                }
            }
        }

        public void ClearSamples()
        {
            Stop();

            foreach (var (_, drawable) in drawableSamples)
                RemoveInternal(drawable, true);

            drawableSamples.Clear();
        }

        /// <summary>How many times a sample has been played. Test observability seam.</summary>
        public int PlayCount { get; private set; }

        /// <summary>How many samples are currently loaded (resolved from the store). Test observability seam.</summary>
        public int LoadedCount => drawableSamples.Count;

        /// <summary>The sample last matched and played by <see cref="Play(GarbusHitSample?)"/>. Test seam.</summary>
        public GarbusHitSample? LastPlayed { get; private set; }

        public void Play()
        {
            PlayCount++;

            playingChannels.RemoveAll(c => !c.Playing);

            foreach (var (_, drawable) in drawableSamples)
                playingChannels.Add(drawable.Play());
        }

        /// <summary>
        /// Plays the single preloaded member whose <see cref="GarbusHitSample"/> equals <paramref name="sample"/>.
        /// A null or unmatched sample is a no-op.
        /// </summary>
        public void Play(GarbusHitSample? sample)
        {
            if (sample == null)
                return;

            foreach (var (loaded, drawable) in drawableSamples)
            {
                if (!loaded.Equals(sample))
                    continue;

                PlayCount++;
                LastPlayed = sample;

                playingChannels.RemoveAll(c => !c.Playing);
                playingChannels.Add(drawable.Play());
                return;
            }
        }

        public void Stop()
        {
            foreach (var channel in playingChannels)
                channel.Stop();

            playingChannels.Clear();
        }
    }
}
