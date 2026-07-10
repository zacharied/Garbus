// The thin replacement for osu.Game's SkinnableSound/PausableSkinnableSound (which are skin-entangled
// and deliberately not vendored — see PLAN-port.md). Plays a hit object's samples from the game's
// single sample store via osu-framework's DrawableSample, so audio adjustments flow through the
// drawable hierarchy exactly as they do for osu's skinnable sounds.

using System;
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
        /// The minimum allowable volume for sample playback, matching osu's floor.
        /// </summary>
        public const int MINIMUM_SAMPLE_VOLUME = 5;

        [Resolved]
        private ISampleStore sampleStore { get; set; } = null!;

        private readonly List<DrawableSample> drawableSamples = new List<DrawableSample>();
        private readonly List<SampleChannel> playingChannels = new List<SampleChannel>();

        /// <summary>
        /// The longest length among the loaded samples, in milliseconds.
        /// </summary>
        public double Length => drawableSamples.Count == 0 ? 0 : drawableSamples.Max(s => s.Length);

        /// <summary>
        /// Loads the given samples, replacing any previously loaded set. Unresolvable lookups are skipped.
        /// </summary>
        public IEnumerable<HitSampleInfo> Samples
        {
            set
            {
                ClearSamples();

                foreach (var info in value)
                {
                    Sample? sample = info.LookupNames.Select(name => sampleStore.Get(name)).FirstOrDefault(s => s != null);

                    if (sample == null)
                        continue;

                    var drawableSample = new DrawableSample(sample)
                    {
                        Volume = { Value = Math.Max(info.Volume, MINIMUM_SAMPLE_VOLUME) / 100.0 },
                    };

                    drawableSamples.Add(drawableSample);
                    AddInternal(drawableSample);
                }
            }
        }

        public void ClearSamples()
        {
            Stop();

            foreach (var sample in drawableSamples)
                RemoveInternal(sample, true);

            drawableSamples.Clear();
        }

        /// <summary>How many times <see cref="Play"/> has been invoked. Test observability seam.</summary>
        public int PlayCount { get; private set; }

        public void Play()
        {
            PlayCount++;

            playingChannels.RemoveAll(c => !c.Playing);

            foreach (var sample in drawableSamples)
                playingChannels.Add(sample.Play());
        }

        public void Stop()
        {
            foreach (var channel in playingChannels)
                channel.Stop();

            playingChannels.Clear();
        }
    }
}
