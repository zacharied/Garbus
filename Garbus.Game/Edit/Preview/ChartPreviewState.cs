using System;
using System.Collections.Immutable;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Preview;

internal readonly record struct PreviewObjectId(long Value);

internal readonly record struct PreviewTransportState(double Time, bool IsRunning, double Rate, long Timestamp);

internal sealed record PreviewObjectState(PreviewObjectId Id, GarbusHitObject HitObject);

internal sealed record PreviewChartStructure(
    Guid ChartId,
    ChartMetadata Metadata,
    double? PreviewTime,
    ControlPointInfo ControlPointInfo,
    DesignPointInfo DesignPointInfo);

internal sealed record ChartPreviewSnapshot(
    long Revision,
    PreviewChartStructure Structure,
    ImmutableArray<PreviewObjectState> Objects,
    double TimeRange,
    PreviewTransportState Transport);

internal sealed record ChartPreviewBatch(
    long Revision,
    ImmutableArray<PreviewObjectId> Removes,
    ImmutableArray<PreviewObjectState> Upserts,
    PreviewChartStructure? Structure,
    double? TimeRange,
    PreviewTransportState? Transport);
