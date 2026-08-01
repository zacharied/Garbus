// Interactive tuning scene for the spawn halo: halo radius, spawn duration and scroll speed are
// sliders in the test browser's step sidebar, over a looping stream of mixed objects on all four
// cardinal angles plus both shoulders — so the hold reads on point notes and durationed objects at
// once. All three parameters are live bindables, so nothing rebuilds on change. [Explicit] so it
// never runs in a headless "run all"; pick it in the test browser.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects; // GetEndTime extension
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneSpawnHaloTuning : GarbusTestScene
    {
        private const double stream_start = 2000;
        private const double stream_end = 14_000;
        private const double note_gap = 400;
        private const double hold_length = 900;

        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };

        private double loopStart;
        private double loopEnd;
        private float playbackRate = 1;

        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        public TestSceneSpawnHaloTuning()
        {
            AddSliderStep("spawn halo fraction", 0f, 0.4f, (float)GarbusScrollingInfo.DEFAULT_SPAWN_HALO_FRACTION,
                v => { if (IsLoaded) scrollingInfo.SpawnHaloFraction.Value = v; });

            AddSliderStep("spawn duration (ms)", 0f, 500f, (float)GarbusScrollingInfo.DEFAULT_SPAWN_DURATION,
                v => { if (IsLoaded) scrollingInfo.SpawnDuration.Value = v; });

            AddSliderStep("scroll time range (ms)", 200f, 2000f, (float)GarbusScrollingInfo.DEFAULT_TIME_RANGE,
                v => { if (IsLoaded) scrollingInfo.TimeRange.Value = v; });

            AddSliderStep("playback rate", 0f, 2f, 1f, v => playbackRate = v);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var objects = buildStream().ToList();

            foreach (var hitObject in objects)
                hitObject.ApplyDefaults();

            // Loop from before the first object spawns to after the last one clears the ring.
            loopStart = objects.Min(o => o.StartTime) - 2000;
            loopEnd = objects.Max(o => o.GetEndTime()) + 1500;

            manualClock.CurrentTime = loopStart;

            GarbusPlayfield playfield;

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = new FramedClock(manualClock),
                Child = new GarbusInputManager
                {
                    Child = playfield = new GarbusPlayfield { Size = Vector2.One },
                },
            };

            foreach (var hitObject in objects)
                playfield.Add(PlayScreen.CreateDrawableRepresentation(hitObject));
        }

        // Cardinal notes cycling the four angles, a shoulder note every fourth beat, and a cardinal
        // hold every eighth — enough variety to see the halo hold on point and durationed objects.
        private static IEnumerable<GarbusHitObject> buildStream()
        {
            int[] angles = { 0, 90, 180, 270 };
            int i = 0;

            for (double t = stream_start; t < stream_end; t += note_gap, i++)
            {
                if (i % 8 == 7)
                {
                    yield return new CardinalHoldNote { StartTime = t, AngleDeg = angles[i % 4], Duration = hold_length };
                    continue;
                }

                if (i % 4 == 3)
                {
                    yield return new ShoulderNote
                    {
                        StartTime = t,
                        Side = i % 8 == 3 ? HorizontalDirection.Left : HorizontalDirection.Right,
                    };
                    continue;
                }

                yield return new CardinalNote { StartTime = t, AngleDeg = angles[i % 4] };
            }
        }

        protected override void Update()
        {
            base.Update();

            manualClock.CurrentTime += Time.Elapsed * playbackRate;

            if (manualClock.CurrentTime > loopEnd)
                manualClock.CurrentTime = loopStart;
        }
    }
}
