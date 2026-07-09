// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Beatmaps/BacTestBeatmapGenerator.cs).
// BacTestBeatmapGenerator → GarbusTestChartGenerator; returns a GarbusChart instead of Beatmap<T>.
// Since Phase 3 this is the source of truth for the bundled test chart file — regenerate it via the
// explicit test TestChartFormat.RegenerateBundledTestChart after changing anything here.

using osu.Framework.Bindables;
using osu.Framework.Graphics;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Objects;

namespace Garbus.Game.Charts;

public class GarbusTestChartGenerator
{
    public static GarbusChart GenerateChart()
    {
        var chart = new GarbusChart
        {
            Metadata = new ChartMetadata
            {
                Title = "test track",
                Artist = "unknown",
                Charter = "garbus",
                ChartName = "test chart",
                AudioFile = "test-track.ogg",
            },
            OverallDifficulty = 5,
            HitObjects =
            [
                new SliderBody()
                {
                    AngleDeg = 0,
                    StartTime = 2000,
                    Side = HorizontalDirection.Right,
                    Path = new GarbusPath()
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>([
                            new GarbusPathControlPoint()
                            {
                                RotationOffset = 0, TimeOffset = 1000
                            },
                            new GarbusPathControlPoint()
                            {
                                RotationOffset = 90, TimeOffset = 2000, SweepEasing = Easing.None
                            },
                            new GarbusPathControlPoint()
                            {
                                RotationOffset = 90, TimeOffset = 4000
                            }
                        ])
                    }
                },
                new SliderBody()
                {
                    AngleDeg = 0,
                    StartTime = 5000,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath()
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>([
                            new GarbusPathControlPoint()
                            {
                                RotationOffset = 0, TimeOffset = 1000
                            },
                            new GarbusPathControlPoint()
                            {
                                RotationOffset = 90, TimeOffset = 2000, SweepEasing = Easing.None
                            },
                            new GarbusPathControlPoint()
                            {
                                RotationOffset = 90, TimeOffset = 4000
                            }
                        ])
                    }
                },
                new CardinalNote()
                {
                    StartTime = 2000,
                    AngleDeg = 90,
                },
                new CardinalNote()
                {
                    StartTime = 2150,
                    AngleDeg = 90,
                },
                new CardinalNote()
                {
                    StartTime = 4000,
                    AngleDeg = 90,
                },
                new ShoulderNote()
                {
                    StartTime = 3000,
                    Side = HorizontalDirection.Right,
                },
                new ShoulderNote()
                {
                    StartTime = 3500,
                    Side = HorizontalDirection.Left,
                },
                new HoldNote()
                {
                    StartTime = 6000,
                    Duration = 1000,
                    AngleDeg = 180,
                },
                new HoldNote()
                {
                    StartTime = 8000,
                    Duration = 2000,
                    AngleDeg = 270,
                },
                // A hold shorter than the head press window: the deferred tail should just inherit the head.
                new HoldNote()
                {
                    StartTime = 11000,
                    Duration = 80,
                    AngleDeg = 0,
                }
            ]
        };

        // Placeholder timing until the track is properly timed: 120 BPM from zero. Nothing consumes
        // this during gameplay yet; it exists for the chart format roundtrip and the Phase 4 beat snap.
        chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

        return chart;
    }
}
