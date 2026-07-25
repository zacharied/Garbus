using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;

namespace Garbus.Game.Edit.Preview;

internal partial class InlineChartPreviewController : CompositeComponent
{
    private const int max_pending_object_deltas = 4096;

    private readonly EditorChart editorChart;
    private readonly EditorClock editorClock;
    private readonly GarbusChartChangeHandler changeHandler;
    private readonly GarbusScrollingInfo scrollingInfo;
    private readonly IChartPreviewSink view;
    private readonly Func<long> timestampProvider;

    private readonly Dictionary<GarbusHitObject, long> objectIds = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<GarbusHitObject> sentObjects = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<GarbusHitObject, long> pendingRemoves = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<GarbusHitObject, long> pendingUpserts = new(ReferenceEqualityComparer.Instance);

    private bool pendingStructuralState;
    private bool pendingFullState;
    private bool pendingScrollSpeed;
    private bool pendingImmediateTransport;
    private bool pendingSmoothSeekTransport;
    private bool hasTransportState;
    private bool lastTransportRunning;
    private double lastTransportTime;
    private double lastTransportRate;
    private long lastTransportTimestamp;
    private long nextObjectId;
    private long revision;
    private StructureFingerprint? lastStructureFingerprint;
    private ControlPointInfo? subscribedControlPointInfo;
    private DesignPointInfo? subscribedDesignPointInfo;

    public InlineChartPreviewController(
        EditorChart editorChart,
        EditorClock editorClock,
        GarbusChartChangeHandler changeHandler,
        GarbusScrollingInfo scrollingInfo,
        IChartPreviewSink view,
        Func<long>? timestampProvider = null)
    {
        this.editorChart = editorChart;
        this.editorClock = editorClock;
        this.changeHandler = changeHandler;
        this.scrollingInfo = scrollingInfo;
        this.view = view;
        this.timestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
    }

    public bool Enabled { get; private set; }

    public event Action<string>? PreviewFailed;

    public void Open()
    {
        if (Enabled)
            return;

        Enabled = true;
        view.ResyncRequested += requestFullState;
        subscribe();

        try
        {
            sendFullState();
        }
        catch (Exception exception)
        {
            Close();
            PreviewFailed?.Invoke(exception.Message);
        }
    }

    public void Close()
    {
        if (!Enabled)
            return;

        Enabled = false;
        view.ResyncRequested -= requestFullState;
        unsubscribe();
        resetSessionState();
    }

    protected override void Update()
    {
        base.Update();

        if (!Enabled)
            return;

        try
        {
            flushPendingChanges();
        }
        catch (Exception exception)
        {
            Close();
            PreviewFailed?.Invoke(exception.Message);
        }
    }

    protected override void Dispose(bool isDisposing)
    {
        Close();
        base.Dispose(isDisposing);
    }

    private void subscribe()
    {
        editorChart.HitObjectAdded += onObjectAdded;
        editorChart.HitObjectRemoved += onObjectRemoved;
        editorChart.HitObjectUpdated += onObjectUpdated;
        editorChart.ChartChanged += onChartChanged;
        changeHandler.OnStateChange += onStructuralStateChanged;
        subscribeStructuralSources();
        scrollingInfo.TimeRange.ValueChanged += onTimeRangeChanged;
        editorClock.SeekingOrStopped.ValueChanged += onSeekingOrStoppedChanged;
        editorClock.DiscreteSeeked += onDiscreteSeeked;
        editorClock.SmoothSeekStarted += onSmoothSeekStarted;
    }

    private void unsubscribe()
    {
        editorChart.HitObjectAdded -= onObjectAdded;
        editorChart.HitObjectRemoved -= onObjectRemoved;
        editorChart.HitObjectUpdated -= onObjectUpdated;
        editorChart.ChartChanged -= onChartChanged;
        changeHandler.OnStateChange -= onStructuralStateChanged;
        unsubscribeStructuralSources();
        scrollingInfo.TimeRange.ValueChanged -= onTimeRangeChanged;
        editorClock.SeekingOrStopped.ValueChanged -= onSeekingOrStoppedChanged;
        editorClock.DiscreteSeeked -= onDiscreteSeeked;
        editorClock.SmoothSeekStarted -= onSmoothSeekStarted;
    }

    private void onObjectAdded(GarbusHitObject hitObject)
    {
        if (pendingFullState)
            return;

        pendingRemoves.Remove(hitObject);
        pendingUpserts[hitObject] = getOrAllocateObjectId(hitObject);
        enforcePendingObjectBound();
    }

    private void onObjectUpdated(GarbusHitObject hitObject)
    {
        if (!pendingFullState && !pendingRemoves.ContainsKey(hitObject))
            pendingUpserts[hitObject] = getOrAllocateObjectId(hitObject);

        enforcePendingObjectBound();
    }

    private void onObjectRemoved(GarbusHitObject hitObject)
    {
        if (pendingFullState)
            return;

        pendingUpserts.Remove(hitObject);

        if (sentObjects.Contains(hitObject))
            pendingRemoves[hitObject] = objectIds[hitObject];

        enforcePendingObjectBound();
    }

    private void enforcePendingObjectBound()
    {
        if (pendingRemoves.Count + pendingUpserts.Count <= max_pending_object_deltas)
            return;

        // Bound edit bursts so a hidden frame cannot retain an unbounded set of object references.
        // A full state is authoritative and cheaper than replaying an oversized delta batch.
        pendingRemoves.Clear();
        pendingUpserts.Clear();
        pendingFullState = true;
    }

    private void onStructuralStateChanged() => pendingStructuralState = true;

    private void onChartChanged(GarbusChart _, GarbusChart __)
    {
        unsubscribeStructuralSources();
        subscribeStructuralSources();
        pendingFullState = true;
    }

    private void subscribeStructuralSources()
    {
        subscribedControlPointInfo = editorChart.ControlPointInfo;
        subscribedDesignPointInfo = editorChart.DesignPointInfo;
        subscribedControlPointInfo.ControlPointsChanged += onStructuralStateChanged;
        subscribedDesignPointInfo.DesignPointsChanged += onStructuralStateChanged;
    }

    private void unsubscribeStructuralSources()
    {
        if (subscribedControlPointInfo != null)
            subscribedControlPointInfo.ControlPointsChanged -= onStructuralStateChanged;
        if (subscribedDesignPointInfo != null)
            subscribedDesignPointInfo.DesignPointsChanged -= onStructuralStateChanged;

        subscribedControlPointInfo = null;
        subscribedDesignPointInfo = null;
    }

    private void onTimeRangeChanged(ValueChangedEvent<double> _) => pendingScrollSpeed = true;

    private void onSeekingOrStoppedChanged(ValueChangedEvent<bool> change)
    {
        if (change.NewValue)
            pendingImmediateTransport = true;
    }

    private void onDiscreteSeeked()
    {
        pendingSmoothSeekTransport = false;
        pendingImmediateTransport = true;
    }

    private void onSmoothSeekStarted() => pendingSmoothSeekTransport = true;

    private void requestFullState() => pendingFullState = true;

    private void flushPendingChanges()
    {
        if (pendingFullState)
        {
            sendFullState();
            return;
        }

        KeyValuePair<GarbusHitObject, long>[] removes = pendingRemoves.ToArray();
        KeyValuePair<GarbusHitObject, long>[] upserts = pendingUpserts.ToArray();
        ImmutableArray<PreviewObjectId> detachedRemoves = removes.Select(pair => new PreviewObjectId(pair.Value)).ToImmutableArray();
        ImmutableArray<PreviewObjectState> detachedUpserts = upserts.Select(pair => new PreviewObjectState(
            new PreviewObjectId(pair.Value),
            GarbusChartCloner.CloneHitObject(pair.Key))).ToImmutableArray();
        PreviewChartStructure? detachedStructure = null;
        StructureFingerprint? candidateStructureFingerprint = null;
        double? timeRange = null;

        if (pendingStructuralState)
        {
            GarbusChart detached = GarbusChartCloner.CloneChart(editorChart.Chart, editorChart.ControlPointInfo);
            candidateStructureFingerprint = StructureFingerprint.Create(detached);
            if (lastStructureFingerprint == null || !lastStructureFingerprint.Matches(candidateStructureFingerprint))
                detachedStructure = structure(detached);
            else
                pendingStructuralState = false;
        }

        if (pendingScrollSpeed)
            timeRange = scrollingInfo.TimeRange.Value;

        PreviewTransportState transport = captureTransport();

        pendingSmoothSeekTransport |= editorClock.IsSeeking;

        bool hasChartChanges = !detachedRemoves.IsEmpty
                               || !detachedUpserts.IsEmpty
                               || detachedStructure != null
                               || timeRange.HasValue;
        bool stateChanged = !hasTransportState || transport.IsRunning != lastTransportRunning || transport.Rate != lastTransportRate;
        bool stoppedSeek = !transport.IsRunning && !pendingSmoothSeekTransport && transport.Time != lastTransportTime;
        bool cadenceElapsed = (transport.IsRunning || pendingSmoothSeekTransport)
                              && (transport.Timestamp - lastTransportTimestamp) * 60 >= Stopwatch.Frequency;

        if (!hasChartChanges && !stateChanged && !stoppedSeek && !pendingImmediateTransport && !cadenceElapsed)
            return;

        long candidateRevision = revision + 1;
        var batch = new ChartPreviewBatch(
            candidateRevision,
            detachedRemoves,
            detachedUpserts,
            detachedStructure,
            timeRange,
            transport);

        if (!view.Apply(batch))
        {
            sendFullState();
            return;
        }

        revision = candidateRevision;
        pendingRemoves.Clear();
        pendingUpserts.Clear();
        foreach ((GarbusHitObject hitObject, long _) in removes)
        {
            sentObjects.Remove(hitObject);
            objectIds.Remove(hitObject);
        }
        foreach ((GarbusHitObject hitObject, long id) in upserts)
        {
            objectIds[hitObject] = id;
            sentObjects.Add(hitObject);
        }
        pendingStructuralState = false;
        if (detachedStructure != null)
            lastStructureFingerprint = candidateStructureFingerprint;
        pendingScrollSpeed = false;
        pendingImmediateTransport = false;
        pendingSmoothSeekTransport = false;
        rememberTransport(transport);
    }

    private void sendFullState()
    {
        GarbusHitObject[] hitObjects = editorChart.HitObjects.ToArray();
        var candidateIds = new Dictionary<GarbusHitObject, long>(ReferenceEqualityComparer.Instance);
        foreach (GarbusHitObject hitObject in hitObjects)
            candidateIds.Add(hitObject, objectIds.TryGetValue(hitObject, out long id) ? id : ++nextObjectId);

        long stateRevision = revision + 1;
        PreviewTransportState transport = captureTransport();

        GarbusChart detached = GarbusChartCloner.CloneChart(editorChart.Chart, editorChart.ControlPointInfo);
        ImmutableArray<PreviewObjectState> objectStates = detached.HitObjects
                                                                  .Select((hitObject, index) => new PreviewObjectState(
                                                                      new PreviewObjectId(candidateIds[hitObjects[index]]),
                                                                      hitObject))
                                                                  .ToImmutableArray();

        var snapshot = new ChartPreviewSnapshot(
            stateRevision,
            structure(detached),
            objectStates,
            scrollingInfo.TimeRange.Value,
            transport);

        if (!view.Replace(snapshot))
            throw new InvalidOperationException("The Mini Preview rejected its authoritative state.");

        revision = stateRevision;
        objectIds.Clear();
        sentObjects.Clear();
        foreach ((GarbusHitObject hitObject, long id) in candidateIds)
        {
            objectIds.Add(hitObject, id);
            sentObjects.Add(hitObject);
        }
        pendingRemoves.Clear();
        pendingUpserts.Clear();
        pendingStructuralState = false;
        pendingFullState = false;
        pendingScrollSpeed = false;
        pendingImmediateTransport = false;
        pendingSmoothSeekTransport = false;
        lastStructureFingerprint = StructureFingerprint.Create(detached);
        rememberTransport(transport);
    }

    private PreviewTransportState captureTransport()
    {
        double time = editorClock.CurrentTime;
        long timestamp = timestampProvider();
        return new PreviewTransportState(time, editorClock.IsRunning, ((IClock)editorClock).Rate, timestamp);
    }

    private static PreviewChartStructure structure(GarbusChart chart) => new(
        chart.ChartId,
        chart.Metadata,
        chart.PreviewTime,
        chart.ControlPointInfo!,
        chart.DesignPointInfo);

    private void rememberTransport(PreviewTransportState transport)
    {
        hasTransportState = true;
        lastTransportTime = transport.Time;
        lastTransportRunning = transport.IsRunning;
        lastTransportRate = transport.Rate;
        lastTransportTimestamp = transport.Timestamp;
    }

    private long getOrAllocateObjectId(GarbusHitObject hitObject)
    {
        if (objectIds.TryGetValue(hitObject, out long id))
            return id;
        if (pendingUpserts.TryGetValue(hitObject, out id))
            return id;

        return ++nextObjectId;
    }

    private void resetSessionState()
    {
        pendingRemoves.Clear();
        pendingUpserts.Clear();
        objectIds.Clear();
        sentObjects.Clear();
        pendingStructuralState = false;
        pendingFullState = false;
        pendingScrollSpeed = false;
        pendingImmediateTransport = false;
        pendingSmoothSeekTransport = false;
        hasTransportState = false;
        lastStructureFingerprint = null;
    }

    internal long RevisionForTests => revision;

    internal int TrackedObjectCountForTests => objectIds.Count;

    internal long ObjectIdForTests(GarbusHitObject hitObject) => objectIds[hitObject];

    internal bool HasPendingStateForTests =>
        pendingRemoves.Count > 0
        || pendingUpserts.Count > 0
        || pendingStructuralState
        || pendingFullState
        || pendingScrollSpeed
        || pendingImmediateTransport
        || pendingSmoothSeekTransport;

    private sealed class StructureFingerprint
    {
        private readonly Guid chartId;
        private readonly MetadataFingerprint metadata;
        private readonly double? previewTime;
        private readonly ImmutableArray<TimingFingerprint> timing;
        private readonly ImmutableArray<double> emptyTimingGroups;
        private readonly ImmutableArray<DesignFingerprint> design;

        private StructureFingerprint(
            Guid chartId,
            MetadataFingerprint metadata,
            double? previewTime,
            ImmutableArray<TimingFingerprint> timing,
            ImmutableArray<double> emptyTimingGroups,
            ImmutableArray<DesignFingerprint> design)
        {
            this.chartId = chartId;
            this.metadata = metadata;
            this.previewTime = previewTime;
            this.timing = timing;
            this.emptyTimingGroups = emptyTimingGroups;
            this.design = design;
        }

        public static StructureFingerprint Create(GarbusChart chart) => new(
            chart.ChartId,
            MetadataFingerprint.Create(chart.Metadata),
            chart.PreviewTime,
            chart.ControlPointInfo!.TimingPoints.Select(point => new TimingFingerprint(
                point.Time,
                point.BeatLength,
                point.TimeSignature.Numerator,
                point.OmitFirstBarLine)).ToImmutableArray(),
            chart.ControlPointInfo.Groups.Where(group => group.ControlPoints.Count == 0)
                 .Select(group => group.Time).ToImmutableArray(),
            chart.DesignPointInfo.DesignPoints.Select(point => point switch
            {
                TutorialMessage message when point.GetType() == typeof(TutorialMessage) =>
                    new DesignFingerprint(message.StartTime, message.EndTime, message.Text),
                _ => throw new ArgumentOutOfRangeException(nameof(chart), point.GetType().Name, "design point type cannot be synchronized"),
            }).ToImmutableArray());

        public bool Matches(StructureFingerprint other) =>
            chartId == other.chartId
            && metadata == other.metadata
            && previewTime == other.previewTime
            && timing.SequenceEqual(other.timing)
            && emptyTimingGroups.SequenceEqual(other.emptyTimingGroups)
            && design.SequenceEqual(other.design);
    }

    private readonly record struct MetadataFingerprint(
        string Title,
        string Artist,
        string Charter,
        string ChartName,
        string RomanisedTitle,
        string RomanisedArtist,
        string Source,
        string Tags,
        string AudioFile,
        string BackgroundFile,
        int Level,
        Difficulty Difficulty)
    {
        public static MetadataFingerprint Create(ChartMetadata metadata) => new(
            metadata.Title,
            metadata.Artist,
            metadata.Charter,
            metadata.ChartName,
            metadata.RomanisedTitle,
            metadata.RomanisedArtist,
            metadata.Source,
            metadata.Tags,
            metadata.AudioFile,
            metadata.BackgroundFile,
            metadata.Level,
            metadata.Difficulty);
    }

    private readonly record struct TimingFingerprint(double Time, double BeatLength, int Signature, bool OmitFirstBarLine);

    private readonly record struct DesignFingerprint(double StartTime, double EndTime, string Text);
}
