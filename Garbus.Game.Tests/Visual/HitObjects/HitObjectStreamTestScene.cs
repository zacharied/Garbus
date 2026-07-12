// Visual test harness for driving a single-object-type stream through the real PlayScreen.
// Subclasses populate a chart with an endless-feeling stream of one hit object type; the scene
// wraps it in a PlayScreen with a TrackVirtual so audio isn't needed.

using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Screens;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual.HitObjects
{
    public abstract partial class HitObjectStreamTestScene : GarbusTestScene
    {
        private PlayScreen playScreen = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("push play screen", () =>
            {
                var chart = new GarbusChart
                {
                    Metadata = new ChartMetadata
                    {
                        Title = GetType().Name,
                        Artist = "hit object stream",
                        Charter = "garbus",
                        ChartName = "stream",
                        AudioFile = string.Empty,
                    },
                };

                PopulateChart(chart);

                chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

                // PlayScreen's injected-chart path (editor test mode) expects the caller to have
                // already applied defaults — that's what populates HitWindows.
                chart.ApplyDefaults();

                Child = new ScreenStack(playScreen = new PlayScreen(chart, new TrackVirtual(120_000)))
                {
                    RelativeSizeAxes = Axes.Both,
                };
            });

            AddUntilStep("screen loaded", () => playScreen.IsLoaded);
        }

        protected abstract void PopulateChart(GarbusChart chart);
    }
}
