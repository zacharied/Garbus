using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Format;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public class TestChartPreviewModel
{
    [Test]
    public void TestSameTypeUpdatesRetainReferencesAndCopyAllMutableState()
    {
        GarbusChart chart = createChartWithEveryObjectType();
        long[] ids = [1, 2, 3, 4, 5, 6, 7];
        var model = new ChartPreviewModel();

        Assert.That(model.ApplyFullState(fullState(1, chart, ids)), Is.True);
        var before = new Dictionary<long, GarbusHitObject>(model.Objects);

        GarbusHitObject[] changes =
        [
            new CardinalNote { StartTime = 1100, AngleDeg = 180 },
            new CardinalHoldNote { StartTime = 1200, AngleDeg = 270, Duration = 450 },
            new ShoulderNote { StartTime = 1300, Side = HorizontalDirection.Right },
            new ShoulderHoldNote { StartTime = 1400, Side = HorizontalDirection.Right, Duration = 550 },
            new GarbusSlamCentered { StartTime = 1500, AngleDeg = 135, Side = HorizontalDirection.Right },
            new GarbusSlamEdge
            {
                StartTime = 1600,
                AngleDeg = 225,
                Side = HorizontalDirection.Right,
                Direction = RotationalDirection.Anticlockwise,
            },
            new SliderBody
            {
                StartTime = 1700,
                AngleDeg = 315,
                Side = HorizontalDirection.Right,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint
                        {
                            TimeOffset = 250,
                            RotationOffset = -45,
                            Smooth = true,
                            SweepEasing = Easing.InOutQuad,
                        },
                        new GarbusPathControlPoint
                        {
                            TimeOffset = 800,
                            RotationOffset = 90,
                            Smooth = false,
                            SweepEasing = Easing.OutQuint,
                        },
                    },
                },
            },
        ];

        for (int i = 0; i < changes.Length; i++)
        {
            Assert.That(model.ApplyObjectUpsert(new ChartPreviewObjectUpsert(
                2 + i,
                ids[i],
                GarbusChartSerializer.EncodeHitObject(changes[i]))), Is.True);
            Assert.That(model.Objects[ids[i]], Is.SameAs(before[ids[i]]));
        }

        Assert.Multiple(() =>
        {
            var cardinal = (CardinalNote)model.Objects[1];
            Assert.That(cardinal.StartTime, Is.EqualTo(1100));
            Assert.That(cardinal.AngleDeg, Is.EqualTo(180));

            var cardinalHold = (CardinalHoldNote)model.Objects[2];
            Assert.That(cardinalHold.StartTime, Is.EqualTo(1200));
            Assert.That(cardinalHold.AngleDeg, Is.EqualTo(270));
            Assert.That(cardinalHold.Duration, Is.EqualTo(450));

            var shoulder = (ShoulderNote)model.Objects[3];
            Assert.That(shoulder.StartTime, Is.EqualTo(1300));
            Assert.That(shoulder.Side, Is.EqualTo(HorizontalDirection.Right));

            var shoulderHold = (ShoulderHoldNote)model.Objects[4];
            Assert.That(shoulderHold.StartTime, Is.EqualTo(1400));
            Assert.That(shoulderHold.Side, Is.EqualTo(HorizontalDirection.Right));
            Assert.That(shoulderHold.Duration, Is.EqualTo(550));

            var centred = (GarbusSlamCentered)model.Objects[5];
            Assert.That(centred.StartTime, Is.EqualTo(1500));
            Assert.That(centred.AngleDeg, Is.EqualTo(135));
            Assert.That(centred.Side, Is.EqualTo(HorizontalDirection.Right));

            var edge = (GarbusSlamEdge)model.Objects[6];
            Assert.That(edge.StartTime, Is.EqualTo(1600));
            Assert.That(edge.AngleDeg, Is.EqualTo(225));
            Assert.That(edge.Side, Is.EqualTo(HorizontalDirection.Right));
            Assert.That(edge.Direction, Is.EqualTo(RotationalDirection.Anticlockwise));

            var slider = (SliderBody)model.Objects[7];
            Assert.That(slider.StartTime, Is.EqualTo(1700));
            Assert.That(slider.AngleDeg, Is.EqualTo(315));
            Assert.That(slider.Side, Is.EqualTo(HorizontalDirection.Right));
            Assert.That(slider.Path.ControlPoints, Has.Count.EqualTo(2));
            Assert.That(slider.Path.ControlPoints[0].TimeOffset, Is.EqualTo(250));
            Assert.That(slider.Path.ControlPoints[0].RotationOffset, Is.EqualTo(-45));
            Assert.That(slider.Path.ControlPoints[0].Smooth, Is.True);
            Assert.That(slider.Path.ControlPoints[0].SweepEasing, Is.EqualTo(Easing.InOutQuad));
            Assert.That(slider.Path.ControlPoints[1].TimeOffset, Is.EqualTo(800));
            Assert.That(slider.Path.ControlPoints[1].RotationOffset, Is.EqualTo(90));
            Assert.That(slider.Path.ControlPoints[1].Smooth, Is.False);
            Assert.That(slider.Path.ControlPoints[1].SweepEasing, Is.EqualTo(Easing.OutQuint));
        });
    }

    [Test]
    public void TestAddReplaceAndRemove()
    {
        var model = new ChartPreviewModel();
        Assert.That(model.ApplyFullState(fullState(1, new GarbusChart(), [])), Is.True);

        var cardinal = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
        Assert.That(model.ApplyObjectUpsert(upsert(2, 42, cardinal)), Is.True);
        Assert.That(model.Objects[42], Is.TypeOf<CardinalNote>());
        Assert.That(model.Chart.HitObjects, Has.Count.EqualTo(1));

        var shoulder = new ShoulderNote { StartTime = 1200, Side = HorizontalDirection.Left };
        Assert.That(model.ApplyObjectUpsert(upsert(3, 42, shoulder)), Is.True);
        Assert.That(model.Objects[42], Is.TypeOf<ShoulderNote>());
        Assert.That(model.Objects[42], Is.Not.SameAs(cardinal));
        Assert.That(model.Chart.HitObjects, Has.Count.EqualTo(1));

        Assert.That(model.ApplyObjectRemove(new ChartPreviewObjectRemove(4, 42)), Is.True);
        Assert.That(model.Objects, Is.Empty);
        Assert.That(model.Chart.HitObjects, Is.Empty);
    }

    [Test]
    public void TestStaleRevisionRejectedAndRequestsResync()
    {
        var model = new ChartPreviewModel();
        int resyncRequests = 0;
        model.ResyncRequested += () => resyncRequests++;

        var cardinal = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
        Assert.That(model.ApplyFullState(fullState(5, chartWith(cardinal), [7])), Is.True);

        Assert.That(model.ApplyObjectUpsert(upsert(4, 7,
            new CardinalNote { StartTime = 2000, AngleDeg = 180 })), Is.False);
        Assert.That(model.Objects[7].StartTime, Is.EqualTo(1000));
        Assert.That(resyncRequests, Is.EqualTo(1));
    }

    [Test]
    public void TestEqualRevisionIgnoredWithoutRequestingResync()
    {
        var model = new ChartPreviewModel();
        int resyncRequests = 0;
        model.ResyncRequested += () => resyncRequests++;

        var cardinal = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
        Assert.That(model.ApplyFullState(fullState(5, chartWith(cardinal), [7])), Is.True);

        Assert.That(model.ApplyObjectUpsert(upsert(5, 7,
            new CardinalNote { StartTime = 2000, AngleDeg = 180 })), Is.False);
        Assert.That(model.Objects[7].StartTime, Is.EqualTo(1000));
        Assert.That(resyncRequests, Is.Zero);
    }

    [Test]
    public void TestFullStateIdMismatchRejectedWithoutReplacingState()
    {
        var model = new ChartPreviewModel();
        int resyncRequests = 0;
        model.ResyncRequested += () => resyncRequests++;

        var original = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
        Assert.That(model.ApplyFullState(fullState(1, chartWith(original), [7])), Is.True);
        GarbusHitObject before = model.Objects[7];

        Assert.That(model.ApplyFullState(fullState(2, createChartWithEveryObjectType(), [1, 2])), Is.False);
        Assert.That(model.Objects, Has.Count.EqualTo(1));
        Assert.That(model.Objects[7], Is.SameAs(before));
        Assert.That(resyncRequests, Is.EqualTo(1));
    }

    [Test]
    public void TestDuplicateIdsRejected()
    {
        var model = new ChartPreviewModel();
        int resyncRequests = 0;
        model.ResyncRequested += () => resyncRequests++;
        var chart = chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 2000, AngleDeg = 90 });

        Assert.That(model.ApplyFullState(fullState(1, chart, [7, 7])), Is.False);
        Assert.That(model.Objects, Is.Empty);
        Assert.That(resyncRequests, Is.EqualTo(1));
    }

    [Test]
    public void TestStructuralStateReplacesDesignAndTimingButRetainsObjects()
    {
        var model = new ChartPreviewModel();
        var cardinal = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
        Assert.That(model.ApplyFullState(fullState(1, chartWith(cardinal), [7])), Is.True);
        GarbusHitObject before = model.Objects[7];

        var structural = new GarbusChart { PreviewTime = 4321 };
        structural.ControlPointInfo.Add(250, new TimingControlPoint { BeatLength = 375 });
        structural.DesignPointInfo.Add(new TutorialMessage
        {
            StartTime = 500,
            EndTime = 1500,
            Text = "replacement",
        });

        Assert.That(model.ApplyStructuralState(new ChartPreviewStructuralState(
            2,
            GarbusChartSerializer.Encode(structural))), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(model.Objects[7], Is.SameAs(before));
            Assert.That(model.Chart.HitObjects.Single(), Is.SameAs(before));
            Assert.That(model.Chart.PreviewTime, Is.EqualTo(4321));
            Assert.That(model.Chart.ControlPointInfo.TimingPoints.Single().Time, Is.EqualTo(250));
            Assert.That(model.Chart.ControlPointInfo.TimingPoints.Single().BeatLength, Is.EqualTo(375));
            Assert.That(model.Chart.DesignPointInfo.DesignPoints.Single(), Is.TypeOf<TutorialMessage>());
            Assert.That(((TutorialMessage)model.Chart.DesignPointInfo.DesignPoints.Single()).Text, Is.EqualTo("replacement"));
        });
    }

    [Test]
    public void TestStructuralEncodingExcludesHitObjectsAndIncludesMetadata()
    {
        GarbusChart first = chartWith(new CardinalNote { StartTime = 1000, AngleDeg = 0 });
        GarbusChart objectOnlyChange = chartWith(new CardinalNote { StartTime = 2000, AngleDeg = 180 });

        Assert.That(
            GarbusChartSerializer.EncodeStructural(objectOnlyChange),
            Is.EqualTo(GarbusChartSerializer.EncodeStructural(first)));

        objectOnlyChange.Metadata.Title = "Changed title";
        string metadataChange = GarbusChartSerializer.EncodeStructural(objectOnlyChange);

        Assert.That(metadataChange, Is.Not.EqualTo(GarbusChartSerializer.EncodeStructural(first)));
        Assert.That(GarbusChartSerializer.Decode(metadataChange).HitObjects, Is.Empty);
    }

    private static ChartPreviewFullState fullState(long revision, GarbusChart chart, long[] ids) =>
        new(revision, GarbusChartSerializer.Encode(chart), ids, 700,
            new ChartPreviewTransport(revision, 0, false, 1, 0));

    private static ChartPreviewObjectUpsert upsert(long revision, long id, GarbusHitObject hitObject) =>
        new(revision, id, GarbusChartSerializer.EncodeHitObject(hitObject));

    private static GarbusChart chartWith(params GarbusHitObject[] hitObjects)
    {
        var chart = new GarbusChart();
        chart.HitObjects.AddRange(hitObjects);
        return chart;
    }

    private static GarbusChart createChartWithEveryObjectType() => chartWith(
        new CardinalNote { StartTime = 100, AngleDeg = 0 },
        new CardinalHoldNote { StartTime = 200, AngleDeg = 90, Duration = 100 },
        new ShoulderNote { StartTime = 300, Side = HorizontalDirection.Left },
        new ShoulderHoldNote { StartTime = 400, Side = HorizontalDirection.Left, Duration = 200 },
        new GarbusSlamCentered { StartTime = 500, AngleDeg = 45, Side = HorizontalDirection.Left },
        new GarbusSlamEdge
        {
            StartTime = 600,
            AngleDeg = 90,
            Side = HorizontalDirection.Left,
            Direction = RotationalDirection.Clockwise,
        },
        new SliderBody
        {
            StartTime = 700,
            AngleDeg = 135,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 },
                },
            },
        });
}
