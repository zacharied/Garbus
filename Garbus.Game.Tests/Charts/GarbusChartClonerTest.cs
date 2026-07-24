using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace Garbus.Game.Tests.Charts
{
    [TestFixture]
    public class GarbusChartClonerTest
    {
        [Test]
        public void CloneHitObjectCopiesEverySerializerSupportedType()
        {
            List<GarbusHitObject> source = createHitObjects();
            List<GarbusHitObject> clones = source.Select(GarbusChartCloner.CloneHitObject).ToList();

            assertHitObjectsEqual(source, clones);
            assertHitObjectsDetached(source, clones);

            mutateHitObjects(source);
            assertHitObjectsEqual(createHitObjects(), clones);

            var sourceNested = source.Select(h => h.NestedHitObjects.ToArray()).ToArray();
            foreach (var clone in clones)
                clone.ApplyDefaults();

            for (int i = 0; i < source.Count; i++)
                Assert.That(source[i].NestedHitObjects, Is.EqualTo(sourceNested[i]));
        }

        [Test]
        public void CloneMetadataCopiesEveryProperty()
        {
            ChartMetadata source = createMetadata();

            ChartMetadata clone = GarbusChartCloner.CloneMetadata(source);

            Assert.That(clone, Is.Not.SameAs(source));
            assertMetadataEqual(source, clone);

            source.Title = "changed";
            source.Artist = "changed";
            source.Charter = "changed";
            source.ChartName = "changed";
            source.RomanisedTitle = "changed";
            source.RomanisedArtist = "changed";
            source.Source = "changed";
            source.Tags = "changed";
            source.AudioFile = "changed";
            source.BackgroundFile = "changed";
            source.Level = 1;
            source.Difficulty = Difficulty.Novice;

            assertMetadataEqual(createMetadata(), clone);
        }

        [Test]
        public void CloneDesignPointInfoCopiesEverySerializerSupportedType()
        {
            DesignPointInfo source = createDesignPointInfo();

            DesignPointInfo clone = GarbusChartCloner.CloneDesignPointInfo(source);

            Assert.Multiple(() =>
            {
                Assert.That(clone, Is.Not.SameAs(source));
                Assert.That(clone.DesignPoints, Is.Not.SameAs(source.DesignPoints));
                Assert.That(clone.DesignPoints, Has.Count.EqualTo(1));
                Assert.That(clone.DesignPoints[0], Is.TypeOf<TutorialMessage>());
                Assert.That(clone.DesignPoints[0], Is.Not.SameAs(source.DesignPoints[0]));
            });

            assertTutorialMessageEqual((TutorialMessage)source.DesignPoints[0], (TutorialMessage)clone.DesignPoints[0]);

            var sourceMessage = (TutorialMessage)source.DesignPoints[0];
            sourceMessage.StartTime = 9999;
            sourceMessage.EndTime = 10000;
            sourceMessage.Text = "changed";
            assertTutorialMessageEqual((TutorialMessage)createDesignPointInfo().DesignPoints[0], (TutorialMessage)clone.DesignPoints[0]);
        }

        [Test]
        public void CloneChartOwnsAllMutableStateAndUsesEffectiveTiming()
        {
            GarbusChart source = createChart();
            ControlPointInfo effectiveTiming = createEffectiveTiming();

            GarbusChart clone = GarbusChartCloner.CloneChart(source, effectiveTiming);

            Assert.Multiple(() =>
            {
                Assert.That(clone, Is.Not.SameAs(source));
                Assert.That(clone.ChartId, Is.EqualTo(source.ChartId));
                Assert.That(clone.PreviewTime, Is.EqualTo(source.PreviewTime));
                Assert.That(clone.Metadata, Is.Not.SameAs(source.Metadata));
                Assert.That(clone.HitObjects, Is.Not.SameAs(source.HitObjects));
                Assert.That(clone.DesignPointInfo, Is.Not.SameAs(source.DesignPointInfo));
                Assert.That(clone.ControlPointInfo, Is.Not.SameAs(effectiveTiming));
                Assert.That(clone.ControlPointInfo, Is.Not.SameAs(source.ControlPointInfo));
            });

            assertMetadataEqual(source.Metadata, clone.Metadata);
            assertHitObjectsEqual(source.HitObjects, clone.HitObjects);
            assertHitObjectsDetached(source.HitObjects, clone.HitObjects);
            assertTimingEqual(effectiveTiming, clone.ControlPointInfo!);

            Assert.That(clone.ControlPointInfo!.TimingPoints, Is.Not.SameAs(effectiveTiming.TimingPoints));
            for (int i = 0; i < effectiveTiming.TimingPoints.Count; i++)
                Assert.That(clone.ControlPointInfo.TimingPoints[i], Is.Not.SameAs(effectiveTiming.TimingPoints[i]));

            var sourceMessage = (TutorialMessage)source.DesignPointInfo.DesignPoints.Single();
            var cloneMessage = (TutorialMessage)clone.DesignPointInfo.DesignPoints.Single();
            Assert.That(clone.DesignPointInfo.DesignPoints, Is.Not.SameAs(source.DesignPointInfo.DesignPoints));
            Assert.That(cloneMessage, Is.Not.SameAs(sourceMessage));
            assertTutorialMessageEqual(sourceMessage, cloneMessage);

            source.Metadata.Title = "changed";
            source.PreviewTime = 1;
            source.HitObjects.Clear();
            sourceMessage.Text = "changed";
            effectiveTiming.TimingPoints[0].BeatLength = 999;

            Assert.Multiple(() =>
            {
                Assert.That(clone.Metadata.Title, Is.EqualTo("Native title"));
                Assert.That(clone.PreviewTime, Is.EqualTo(12345.5));
                Assert.That(clone.HitObjects, Has.Count.EqualTo(7));
                Assert.That(cloneMessage.Text, Is.EqualTo("Read this\\nnow"));
                Assert.That(clone.ControlPointInfo.TimingPoints[0].BeatLength, Is.EqualTo(375));
            });
        }

        [Test]
        public void UnsupportedHitObjectTypeFailsExplicitly()
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => GarbusChartCloner.CloneHitObject(new UnsupportedHitObject()));
            Assert.That(exception!.Message, Does.Contain(nameof(UnsupportedHitObject)));
        }

        [Test]
        public void UnsupportedDesignPointTypeFailsExplicitly()
        {
            var source = new DesignPointInfo();
            source.Add(new UnsupportedDesignPoint());

            var exception = Assert.Throws<ArgumentOutOfRangeException>(() => GarbusChartCloner.CloneDesignPointInfo(source));
            Assert.That(exception!.Message, Does.Contain(nameof(UnsupportedDesignPoint)));
        }

        private static GarbusChart createChart()
        {
            var sourceTiming = new ControlPointInfo();
            sourceTiming.Add(50, new TimingControlPoint { BeatLength = 1000, TimeSignature = new TimeSignature(4) });

            return new GarbusChart
            {
                ChartId = Guid.Parse("c8610ad0-1807-492d-b3d4-ff0db86c9784"),
                Metadata = createMetadata(),
                PreviewTime = 12345.5,
                ControlPointInfo = sourceTiming,
                DesignPointInfo = createDesignPointInfo(),
                HitObjects = createHitObjects(),
            };
        }

        private static ChartMetadata createMetadata() => new ChartMetadata
        {
            Title = "Native title",
            Artist = "Native artist",
            Charter = "Chart author",
            ChartName = "Detached",
            RomanisedTitle = "Roman title",
            RomanisedArtist = "Roman artist",
            Source = "Source work",
            Tags = "one two three",
            AudioFile = "track.opus",
            BackgroundFile = "background.webp",
            Level = 17,
            Difficulty = Difficulty.Expert,
        };

        private static ControlPointInfo createEffectiveTiming()
        {
            var timing = new ControlPointInfo();
            timing.Add(100, new TimingControlPoint
            {
                BeatLength = 375,
                TimeSignature = new TimeSignature(7),
                OmitFirstBarLine = true,
            });
            timing.Add(2400, new TimingControlPoint
            {
                BeatLength = 500,
                TimeSignature = new TimeSignature(3),
                OmitFirstBarLine = false,
            });
            return timing;
        }

        private static DesignPointInfo createDesignPointInfo()
        {
            var info = new DesignPointInfo();
            info.Add(new TutorialMessage
            {
                StartTime = 1200.25,
                EndTime = 3456.75,
                Text = "Read this\\nnow",
            });
            return info;
        }

        private static List<GarbusHitObject> createHitObjects()
        {
            var hitObjects = new List<GarbusHitObject>
            {
                new CardinalNote { StartTime = 101.25, AngleDeg = 23 },
                new CardinalHoldNote { StartTime = 202.5, AngleDeg = 112, Duration = 333.75 },
                new ShoulderNote { StartTime = 303.75, Side = HorizontalDirection.Right },
                new ShoulderHoldNote { StartTime = 405, Side = HorizontalDirection.Left, Duration = 444.5 },
                new SliderBody
                {
                    StartTime = 506.25,
                    AngleDeg = 71,
                    Side = HorizontalDirection.Right,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint
                            {
                                TimeOffset = 250.5,
                                RotationOffset = -40,
                                Smooth = true,
                                SweepEasing = Easing.InOutQuint,
                            },
                            new GarbusPathControlPoint
                            {
                                TimeOffset = 725.75,
                                RotationOffset = 135,
                                Smooth = false,
                                SweepEasing = Easing.OutBounce,
                            },
                        },
                    },
                },
                new GarbusSlamCentered { StartTime = 607.5, AngleDeg = 154, Side = HorizontalDirection.Right },
                new GarbusSlamEdge
                {
                    StartTime = 708.75,
                    AngleDeg = 268,
                    Side = HorizontalDirection.Left,
                    Direction = RotationalDirection.Anticlockwise,
                },
            };

            foreach (var hitObject in hitObjects)
            {
                hitObject.ApplyDefaults();
                hitObject.Samples = new List<GarbusHitSample>
                {
                    new($"custom-{hitObject.StartTime}"),
                    new($"accent-{hitObject.GetType().Name}"),
                };
            }

            return hitObjects;
        }

        private static void mutateHitObjects(IReadOnlyList<GarbusHitObject> hitObjects)
        {
            foreach (var hitObject in hitObjects)
            {
                hitObject.StartTime += 10000;
                hitObject.Samples.Clear();

                switch (hitObject)
                {
                    case CardinalNote cardinal:
                        cardinal.AngleDeg = 0;
                        break;

                    case CardinalHoldNote hold:
                        hold.AngleDeg = 0;
                        hold.Duration = 1;
                        break;

                    case ShoulderNote shoulder:
                        shoulder.Side = HorizontalDirection.Left;
                        break;

                    case ShoulderHoldNote shoulderHold:
                        shoulderHold.Side = HorizontalDirection.Right;
                        shoulderHold.Duration = 1;
                        break;

                    case SliderBody slider:
                        slider.AngleDeg = 0;
                        slider.Side = HorizontalDirection.Left;
                        slider.Path.ControlPoints[0].TimeOffset = 1;
                        slider.Path.ControlPoints[0].RotationOffset = 1;
                        slider.Path.ControlPoints[0].Smooth = false;
                        slider.Path.ControlPoints[0].SweepEasing = Easing.None;
                        slider.Path.ControlPoints.RemoveAt(1);
                        break;

                    case GarbusSlamCentered slam:
                        slam.AngleDeg = 0;
                        slam.Side = HorizontalDirection.Left;
                        break;

                    case GarbusSlamEdge slam:
                        slam.AngleDeg = 0;
                        slam.Side = HorizontalDirection.Right;
                        slam.Direction = RotationalDirection.Clockwise;
                        break;
                }
            }
        }

        private static void assertMetadataEqual(ChartMetadata expected, ChartMetadata actual)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.Title, Is.EqualTo(expected.Title));
                Assert.That(actual.Artist, Is.EqualTo(expected.Artist));
                Assert.That(actual.Charter, Is.EqualTo(expected.Charter));
                Assert.That(actual.ChartName, Is.EqualTo(expected.ChartName));
                Assert.That(actual.RomanisedTitle, Is.EqualTo(expected.RomanisedTitle));
                Assert.That(actual.RomanisedArtist, Is.EqualTo(expected.RomanisedArtist));
                Assert.That(actual.Source, Is.EqualTo(expected.Source));
                Assert.That(actual.Tags, Is.EqualTo(expected.Tags));
                Assert.That(actual.AudioFile, Is.EqualTo(expected.AudioFile));
                Assert.That(actual.BackgroundFile, Is.EqualTo(expected.BackgroundFile));
                Assert.That(actual.Level, Is.EqualTo(expected.Level));
                Assert.That(actual.Difficulty, Is.EqualTo(expected.Difficulty));
            });
        }

        private static void assertTimingEqual(ControlPointInfo expected, ControlPointInfo actual)
        {
            Assert.That(actual.TimingPoints, Has.Count.EqualTo(expected.TimingPoints.Count));

            for (int i = 0; i < expected.TimingPoints.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(actual.TimingPoints[i].Time, Is.EqualTo(expected.TimingPoints[i].Time));
                    Assert.That(actual.TimingPoints[i].BeatLength, Is.EqualTo(expected.TimingPoints[i].BeatLength));
                    Assert.That(actual.TimingPoints[i].TimeSignature, Is.EqualTo(expected.TimingPoints[i].TimeSignature));
                    Assert.That(actual.TimingPoints[i].OmitFirstBarLine, Is.EqualTo(expected.TimingPoints[i].OmitFirstBarLine));
                });
            }
        }

        private static void assertTutorialMessageEqual(TutorialMessage expected, TutorialMessage actual)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.StartTime, Is.EqualTo(expected.StartTime));
                Assert.That(actual.EndTime, Is.EqualTo(expected.EndTime));
                Assert.That(actual.Text, Is.EqualTo(expected.Text));
            });
        }

        private static void assertHitObjectsDetached(IReadOnlyList<GarbusHitObject> expected, IReadOnlyList<GarbusHitObject> actual)
        {
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(actual[i], Is.Not.SameAs(expected[i]));
                    Assert.That(actual[i].SamplesBindable, Is.Not.SameAs(expected[i].SamplesBindable));
                });

                if (expected[i] is SliderBody expectedSlider)
                {
                    var actualSlider = (SliderBody)actual[i];
                    Assert.Multiple(() =>
                    {
                        Assert.That(actualSlider.Path, Is.Not.SameAs(expectedSlider.Path));
                        Assert.That(actualSlider.Path.ControlPoints, Is.Not.SameAs(expectedSlider.Path.ControlPoints));
                    });

                    for (int j = 0; j < expectedSlider.Path.ControlPoints.Count; j++)
                        Assert.That(actualSlider.Path.ControlPoints[j], Is.Not.SameAs(expectedSlider.Path.ControlPoints[j]));
                }
            }
        }

        private static void assertHitObjectsEqual(IReadOnlyList<GarbusHitObject> expected, IReadOnlyList<GarbusHitObject> actual)
        {
            Assert.That(actual, Has.Count.EqualTo(expected.Count));

            for (int i = 0; i < expected.Count; i++)
            {
                GarbusHitObject expectedObject = expected[i];
                GarbusHitObject actualObject = actual[i];

                Assert.Multiple(() =>
                {
                    Assert.That(actualObject.GetType(), Is.EqualTo(expectedObject.GetType()));
                    Assert.That(actualObject.StartTime, Is.EqualTo(expectedObject.StartTime));
                    Assert.That(actualObject.Samples.Select(s => s.Name), Is.EqualTo(expectedObject.Samples.Select(s => s.Name)));
                });

                switch (expectedObject)
                {
                    case CardinalNote cardinal:
                        Assert.That(((CardinalNote)actualObject).AngleDeg, Is.EqualTo(cardinal.AngleDeg));
                        break;

                    case CardinalHoldNote hold:
                        Assert.Multiple(() =>
                        {
                            Assert.That(((CardinalHoldNote)actualObject).AngleDeg, Is.EqualTo(hold.AngleDeg));
                            Assert.That(((CardinalHoldNote)actualObject).Duration, Is.EqualTo(hold.Duration));
                        });
                        break;

                    case ShoulderNote shoulder:
                        Assert.That(((ShoulderNote)actualObject).Side, Is.EqualTo(shoulder.Side));
                        break;

                    case ShoulderHoldNote shoulderHold:
                        Assert.Multiple(() =>
                        {
                            Assert.That(((ShoulderHoldNote)actualObject).Side, Is.EqualTo(shoulderHold.Side));
                            Assert.That(((ShoulderHoldNote)actualObject).Duration, Is.EqualTo(shoulderHold.Duration));
                        });
                        break;

                    case SliderBody slider:
                        var actualSlider = (SliderBody)actualObject;
                        Assert.Multiple(() =>
                        {
                            Assert.That(actualSlider.AngleDeg, Is.EqualTo(slider.AngleDeg));
                            Assert.That(actualSlider.Side, Is.EqualTo(slider.Side));
                            Assert.That(actualSlider.Path.ControlPoints, Has.Count.EqualTo(slider.Path.ControlPoints.Count));
                        });

                        for (int j = 0; j < slider.Path.ControlPoints.Count; j++)
                        {
                            GarbusPathControlPoint expectedPoint = slider.Path.ControlPoints[j];
                            GarbusPathControlPoint actualPoint = actualSlider.Path.ControlPoints[j];
                            Assert.Multiple(() =>
                            {
                                Assert.That(actualPoint.TimeOffset, Is.EqualTo(expectedPoint.TimeOffset));
                                Assert.That(actualPoint.RotationOffset, Is.EqualTo(expectedPoint.RotationOffset));
                                Assert.That(actualPoint.Smooth, Is.EqualTo(expectedPoint.Smooth));
                                Assert.That(actualPoint.SweepEasing, Is.EqualTo(expectedPoint.SweepEasing));
                            });
                        }
                        break;

                    case GarbusSlamCentered slam:
                        Assert.Multiple(() =>
                        {
                            Assert.That(((GarbusSlamCentered)actualObject).AngleDeg, Is.EqualTo(slam.AngleDeg));
                            Assert.That(((GarbusSlamCentered)actualObject).Side, Is.EqualTo(slam.Side));
                        });
                        break;

                    case GarbusSlamEdge slam:
                        Assert.Multiple(() =>
                        {
                            Assert.That(((GarbusSlamEdge)actualObject).AngleDeg, Is.EqualTo(slam.AngleDeg));
                            Assert.That(((GarbusSlamEdge)actualObject).Side, Is.EqualTo(slam.Side));
                            Assert.That(((GarbusSlamEdge)actualObject).Direction, Is.EqualTo(slam.Direction));
                        });
                        break;

                    default:
                        Assert.Fail($"unhandled hit object type {expectedObject.GetType().Name}");
                        break;
                }
            }
        }

        private sealed class UnsupportedHitObject : GarbusHitObject
        {
            public override HitsoundFamily Hitsounds => HitsoundFamilies.CardinalNote;
        }

        private sealed class UnsupportedDesignPoint : DesignPoint
        {
        }
    }
}
