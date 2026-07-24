using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Format;
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
    private readonly ChartPreviewContent view;

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
    private string? lastStructuralState;
    private ControlPointInfo? subscribedControlPointInfo;
    private DesignPointInfo? subscribedDesignPointInfo;

    public InlineChartPreviewController(
        EditorChart editorChart,
        EditorClock editorClock,
        GarbusChartChangeHandler changeHandler,
        GarbusScrollingInfo scrollingInfo,
        ChartPreviewContent view)
    {
        this.editorChart = editorChart;
        this.editorClock = editorClock;
        this.changeHandler = changeHandler;
        this.scrollingInfo = scrollingInfo;
        this.view = view;
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
            updateTransport();
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
        pendingUpserts[hitObject] = getObjectId(hitObject);
        enforcePendingObjectBound();
    }

    private void onObjectUpdated(GarbusHitObject hitObject)
    {
        if (!pendingFullState && !pendingRemoves.ContainsKey(hitObject))
            pendingUpserts[hitObject] = getObjectId(hitObject);

        enforcePendingObjectBound();
    }

    private void onObjectRemoved(GarbusHitObject hitObject)
    {
        if (pendingFullState)
            return;

        pendingUpserts.Remove(hitObject);

        if (sentObjects.Contains(hitObject))
            pendingRemoves[hitObject] = getObjectId(hitObject);
        else
            objectIds.Remove(hitObject);

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
        pendingRemoves.Clear();
        pendingUpserts.Clear();

        foreach ((GarbusHitObject hitObject, long id) in removes)
        {
            apply(new ChartPreviewBatch(
                nextRevision(),
                [new PreviewObjectId(id)],
                ImmutableArray<PreviewObjectState>.Empty,
                null,
                null,
                null));
            sentObjects.Remove(hitObject);
            objectIds.Remove(hitObject);
        }

        foreach ((GarbusHitObject hitObject, long id) in upserts)
        {
            GarbusHitObject detached = GarbusChartSerializer.DecodeHitObject(GarbusChartSerializer.EncodeHitObject(hitObject));
            apply(new ChartPreviewBatch(
                nextRevision(),
                ImmutableArray<PreviewObjectId>.Empty,
                [new PreviewObjectState(new PreviewObjectId(id), detached)],
                null,
                null,
                null));
            sentObjects.Add(hitObject);
        }

        if (pendingStructuralState)
        {
            pendingStructuralState = false;
            GarbusChart source = editorChart.CreateSerializableChart();
            string structuralState = GarbusChartSerializer.EncodeStructural(source);

            if (structuralState != lastStructuralState)
            {
                lastStructuralState = structuralState;
                GarbusChart detached = GarbusChartSerializer.Decode(structuralState);
                apply(new ChartPreviewBatch(
                    nextRevision(),
                    ImmutableArray<PreviewObjectId>.Empty,
                    ImmutableArray<PreviewObjectState>.Empty,
                    structure(source.ChartId, detached),
                    null,
                    null));
            }
        }

        if (pendingScrollSpeed)
        {
            pendingScrollSpeed = false;
            apply(new ChartPreviewBatch(
                nextRevision(),
                ImmutableArray<PreviewObjectId>.Empty,
                ImmutableArray<PreviewObjectState>.Empty,
                null,
                scrollingInfo.TimeRange.Value,
                null));
        }
    }

    private void updateTransport()
    {
        PreviewTransportState transport = captureTransport();

        pendingSmoothSeekTransport |= editorClock.IsSeeking;

        bool stateChanged = !hasTransportState || transport.IsRunning != lastTransportRunning || transport.Rate != lastTransportRate;
        bool stoppedSeek = !transport.IsRunning && !pendingSmoothSeekTransport && transport.Time != lastTransportTime;
        bool cadenceElapsed = (transport.IsRunning || pendingSmoothSeekTransport)
                              && (transport.Timestamp - lastTransportTimestamp) * 60 >= Stopwatch.Frequency;

        if (!stateChanged && !stoppedSeek && !pendingImmediateTransport && !cadenceElapsed)
            return;

        pendingImmediateTransport = false;
        pendingSmoothSeekTransport = false;

        apply(new ChartPreviewBatch(
            nextRevision(),
            ImmutableArray<PreviewObjectId>.Empty,
            ImmutableArray<PreviewObjectState>.Empty,
            null,
            null,
            transport));
        rememberTransport(transport);
    }

    private void sendFullState()
    {
        GarbusHitObject[] hitObjects = editorChart.HitObjects.ToArray();
        var currentObjects = new HashSet<GarbusHitObject>(hitObjects, ReferenceEqualityComparer.Instance);

        foreach (GarbusHitObject hitObject in objectIds.Keys.ToArray())
        {
            if (!currentObjects.Contains(hitObject))
                objectIds.Remove(hitObject);
        }

        pendingRemoves.Clear();
        pendingUpserts.Clear();
        pendingStructuralState = false;
        pendingFullState = false;
        pendingScrollSpeed = false;
        pendingImmediateTransport = false;
        pendingSmoothSeekTransport = false;
        sentObjects.Clear();

        long[] ids = hitObjects.Select(hitObject =>
        {
            sentObjects.Add(hitObject);
            return getObjectId(hitObject);
        }).ToArray();

        long stateRevision = nextRevision();
        PreviewTransportState transport = captureTransport();

        GarbusChart serializableChart = editorChart.CreateSerializableChart();
        GarbusChart detached = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(serializableChart));
        ImmutableArray<PreviewObjectState> objectStates = detached.HitObjects
                                                                  .Select((hitObject, index) => new PreviewObjectState(
                                                                      new PreviewObjectId(ids[index]),
                                                                      hitObject))
                                                                  .ToImmutableArray();

        replace(new ChartPreviewSnapshot(
            stateRevision,
            structure(serializableChart.ChartId, detached),
            objectStates,
            scrollingInfo.TimeRange.Value,
            transport));
        lastStructuralState = GarbusChartSerializer.EncodeStructural(serializableChart);
        rememberTransport(transport);
    }

    private PreviewTransportState captureTransport()
    {
        double time = editorClock.CurrentTime;
        long timestamp = Stopwatch.GetTimestamp();
        return new PreviewTransportState(time, editorClock.IsRunning, ((IClock)editorClock).Rate, timestamp);
    }

    private void apply(ChartPreviewBatch batch)
    {
        if (!view.Apply(batch))
            pendingFullState = true;
    }

    private void replace(ChartPreviewSnapshot snapshot)
    {
        if (!view.Replace(snapshot))
            pendingFullState = true;
    }

    private static PreviewChartStructure structure(Guid chartId, GarbusChart chart) => new(
        chartId,
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

    private long getObjectId(GarbusHitObject hitObject)
    {
        if (!objectIds.TryGetValue(hitObject, out long id))
            objectIds.Add(hitObject, id = ++nextObjectId);

        return id;
    }

    private long nextRevision() => ++revision;

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
        lastStructuralState = null;
    }
}
