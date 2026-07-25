using System;
using System.Collections.Immutable;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor;

public partial class TestSceneChartPreviewContent
{
    [Test]
    public void TestTypedMultiObjectBatchConsumesOneRevision()
    {
        AddStep("replace typed state", () => Assert.That(preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 2000, AngleDeg = 90 }), [7, 8], 500, 700)), Is.True));

        AddStep("apply two-object batch", () => Assert.That(preview.Apply(batch(
            2,
            upserts:
            [
                state(7, new CardinalNote { StartTime = 1100, AngleDeg = 45 }),
                state(8, new CardinalNote { StartTime = 2100, AngleDeg = 135 }),
            ])), Is.True));

        AddAssert("batch consumes one revision", () => preview.AcceptedRevision, () => Is.EqualTo(2));
        AddAssert("both objects committed", () => preview.CurrentChart.HitObjects.Select(hitObject => hitObject.StartTime),
            () => Is.EqualTo(new[] { 1100, 2100 }));
    }

    [Test]
    public void TestTypedRevisionGapRejectsWholeBatch()
    {
        DrawableHitObject original = null!;

        AddStep("replace typed state", () => preview.Replace(snapshot(3, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture drawable", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("reject revision gap", () => Assert.That(preview.Apply(batch(
            5,
            upserts: [state(7, new CardinalNote { StartTime = 9000, AngleDeg = 180 })],
            timeRange: 2400,
            transport: new PreviewTransportState(9000, false, 1, 0))), Is.False));

        AddAssert("gap changes nothing", () => preview.AcceptedRevision == 3
                                                   && preview.CurrentChart.HitObjects.Single().StartTime == 1000
                                                   && ReferenceEquals(preview.DrawableForTests(new PreviewObjectId(7)), original)
                                                   && preview.CurrentTimeRangeForTests == 700
                                                   && preview.ClockTimeForTests == 500);
    }

    [Test]
    public void TestTypedInvalidCollectionsAndValuesReject()
    {
        AddStep("replace typed state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));

        AddStep("reject duplicate snapshot ids", () => Assert.That(preview.Replace(snapshot(2, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 90 },
            new CardinalNote { StartTime = 3000, AngleDeg = 180 }), [8, 8], 9000, 2400)), Is.False));
        AddStep("reject default collections", () => Assert.That(preview.Apply(new ChartPreviewBatch(
            2,
            default,
            ImmutableArray<PreviewObjectState>.Empty,
            null,
            null,
            null)), Is.False));
        AddStep("reject nonpositive id", () => Assert.That(preview.Apply(batch(
            2,
            upserts: [state(0, new CardinalNote { AngleDeg = 0 })])), Is.False));
        AddStep("reject duplicate upsert ids", () => Assert.That(preview.Apply(batch(2, upserts:
        [
            state(8, new CardinalNote { AngleDeg = 0 }),
            state(8, new CardinalNote { AngleDeg = 0 }),
        ])), Is.False));
        AddStep("reject missing removal", () => Assert.That(preview.Apply(batch(2, removes: [new PreviewObjectId(99)])), Is.False));
        AddStep("reject remove upsert overlap", () => Assert.That(preview.Apply(batch(
            2,
            removes: [new PreviewObjectId(7)],
            upserts: [state(7, new CardinalNote { AngleDeg = 0 })])), Is.False));
        AddStep("reject invalid range", () => Assert.That(preview.Apply(batch(2, timeRange: double.NaN)), Is.False));
        AddStep("reject invalid rate", () => Assert.That(preview.Apply(batch(
            2,
            transport: new PreviewTransportState(500, false, double.PositiveInfinity, 0))), Is.False));
        AddStep("reject mismatched chart identity", () =>
        {
            GarbusChart current = preview.CurrentChart;
            Assert.That(preview.Apply(batch(2, structure: new PreviewChartStructure(
                Guid.NewGuid(),
                current.Metadata,
                current.PreviewTime,
                current.ControlPointInfo!,
                current.DesignPointInfo))), Is.False);
        });
        AddStep("reject unsupported object", () => Assert.That(preview.Apply(batch(
            2,
            upserts: [state(8, new UnsupportedHitObject())])), Is.False));
        AddStep("reject derived supported object", () => Assert.That(preview.Apply(batch(
            2,
            upserts: [state(8, new DerivedCardinalNote { AngleDeg = 0 })])), Is.False));

        AddAssert("all invalid batches leave initial state", () => preview.AcceptedRevision == 1
                                                               && preview.ObjectCountForTests == 1
                                                               && preview.CurrentChart.HitObjects.Single().StartTime == 1000
                                                               && preview.CurrentTimeRangeForTests == 700
                                                               && preview.ClockTimeForTests == 500);
    }

    [Test]
    public void TestTypedInvalidLaterUpsertMutatesNothing()
    {
        DrawableHitObject original = null!;

        AddStep("replace typed state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture drawable", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("reject invalid later upsert", () => Assert.That(preview.Apply(batch(
            2,
            upserts:
            [
                state(7, new CardinalNote { StartTime = 2000, AngleDeg = 90 }),
                state(8, new UnsupportedHitObject { StartTime = 3000 }),
            ],
            timeRange: 2400,
            transport: new PreviewTransportState(9000, false, 1, 0))), Is.False));

        AddAssert("later failure is atomic", () => preview.AcceptedRevision == 1
                                                      && preview.ObjectCountForTests == 1
                                                      && preview.CurrentChart.HitObjects.Single().StartTime == 1000
                                                      && ReferenceEquals(preview.DrawableForTests(new PreviewObjectId(7)), original)
                                                      && original.HitObject.StartTime == 1000
                                                      && preview.CurrentTimeRangeForTests == 700
                                                      && preview.ClockTimeForTests == 500);
    }

    [Test]
    public void TestTypedNewerSnapshotAuthoritativelyReplacesState()
    {
        DrawableHitObject original = null!;
        var replacement = new ShoulderNote { StartTime = 3000, Side = Core.HorizontalDirection.Right };

        AddStep("replace initial state", () => preview.Replace(snapshot(2, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture initial drawable", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("replace authoritative state", () => Assert.That(preview.Replace(snapshot(
            8, chartWith(replacement), [12], 4000, 2400)), Is.True));

        AddAssert("snapshot owns all new state", () => preview.AcceptedRevision == 8
                                                        && preview.ObjectCountForTests == 1
                                                        && preview.CurrentChart.HitObjects.Single() is ShoulderNote { StartTime: 3000 }
                                                        && preview.DrawableForTests(new PreviewObjectId(12)).HitObject is ShoulderNote
                                                        && preview.CurrentTimeRangeForTests == 2400
                                                        && preview.ClockTimeForTests == 4000);
        AddAssert("old snapshot drawable disposed", () => isDisposed(original));
    }

    [Test]
    public void TestTypedSameTypeUpsertRetainsRootAndAppliesIncomingObject()
    {
        DrawableHitObject original = null!;
        var incoming = new CardinalNote { StartTime = 2000, AngleDeg = 180 };

        AddStep("replace initial state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture root", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("apply same type", () => Assert.That(preview.Apply(batch(2, upserts: [state(7, incoming)])), Is.True));

        AddAssert("root retained", () => preview.DrawableForTests(new PreviewObjectId(7)), () => Is.SameAs(original));
        AddAssert("incoming object is content state", () => preview.CurrentChart.HitObjects.Single(), () => Is.SameAs(incoming));
        AddAssert("incoming object applied to root", () => original.HitObject, () => Is.SameAs(incoming));
    }

    [Test]
    public void TestTypedTypeChangingUpsertReplacesAndDisposesRoot()
    {
        DrawableHitObject original = null!;

        AddStep("replace initial state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture root", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("apply type replacement", () => Assert.That(preview.Apply(batch(2, upserts:
        [
            state(7, new ShoulderNote { StartTime = 2000, Side = Core.HorizontalDirection.Left }),
        ])), Is.True));

        AddAssert("root replaced", () => preview.DrawableForTests(new PreviewObjectId(7)), () => Is.Not.SameAs(original));
        AddAssert("old root disposed", () => isDisposed(original));
    }

    [Test]
    public void TestTypedEqualValuedObjectsRemainDistinctById()
    {
        var first = new CardinalNote { StartTime = 1000, AngleDeg = 90 };
        var second = new CardinalNote { StartTime = 1000, AngleDeg = 90 };

        AddStep("replace equal valued objects", () => Assert.That(preview.Replace(snapshot(
            1, chartWith(first, second), [7, 8], 500, 700)), Is.True));

        AddAssert("both ids retained", () => preview.ObjectCountForTests, () => Is.EqualTo(2));
        AddAssert("roots are distinct", () => preview.DrawableForTests(new PreviewObjectId(7)),
            () => Is.Not.SameAs(preview.DrawableForTests(new PreviewObjectId(8))));
    }

    private static ChartPreviewSnapshot snapshot(
        long revision,
        GarbusChart source,
        long[] ids,
        double time,
        double timeRange)
    {
        GarbusChart detached = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(source));
        var structure = new PreviewChartStructure(
            source.ChartId,
            detached.Metadata,
            detached.PreviewTime,
            detached.ControlPointInfo!,
            detached.DesignPointInfo);
        ImmutableArray<PreviewObjectState> objects = detached.HitObjects
                                                             .Select((hitObject, index) => state(ids[index], hitObject))
                                                             .ToImmutableArray();
        return new ChartPreviewSnapshot(
            revision,
            structure,
            objects,
            timeRange,
            new PreviewTransportState(time, false, 1, 0));
    }

    private static ChartPreviewBatch batch(
        long revision,
        ImmutableArray<PreviewObjectId> removes = default,
        ImmutableArray<PreviewObjectState> upserts = default,
        PreviewChartStructure? structure = null,
        double? timeRange = null,
        PreviewTransportState? transport = null)
        => new(
            revision,
            removes.IsDefault ? ImmutableArray<PreviewObjectId>.Empty : removes,
            upserts.IsDefault ? ImmutableArray<PreviewObjectState>.Empty : upserts,
            structure,
            timeRange,
            transport);

    private static PreviewObjectState state(long id, GarbusHitObject hitObject) => new(new PreviewObjectId(id), hitObject);

    private sealed class UnsupportedHitObject : GarbusHitObject
    {
        public override HitsoundFamily Hitsounds { get; } = new();
    }

    private sealed class DerivedCardinalNote : CardinalNote
    {
    }
}
