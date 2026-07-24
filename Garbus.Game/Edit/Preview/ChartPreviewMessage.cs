namespace Garbus.Game.Edit.Preview;

internal abstract record ChartPreviewMessage;

internal sealed record ChartPreviewFullState(
    long Revision,
    string ChartJson,
    long[] ObjectIds,
    double TimeRange,
    ChartPreviewTransport Transport) : ChartPreviewMessage;

internal sealed record ChartPreviewObjectUpsert(long Revision, long ObjectId, string ObjectJson) : ChartPreviewMessage;

internal sealed record ChartPreviewObjectRemove(long Revision, long ObjectId) : ChartPreviewMessage;

internal sealed record ChartPreviewStructuralState(long Revision, string ChartJson) : ChartPreviewMessage;

internal sealed record ChartPreviewTransport(long Revision, double Time, bool IsRunning, double Rate, long Timestamp) : ChartPreviewMessage;

internal sealed record ChartPreviewScrollSpeed(long Revision, double TimeRange) : ChartPreviewMessage;
