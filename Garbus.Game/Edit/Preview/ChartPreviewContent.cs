using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Gameplay;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Timing;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Preview;

internal partial class ChartPreviewContent : CompositeDrawable
{
    internal const float TARGET_DRAW_SIZE = 768;

    private readonly ChartPreviewClock previewClock = new();
    private readonly ManualClock manualClock = new();
    private readonly Func<GarbusHitObject, DrawableHitObject> drawableFactory;
    private readonly Func<DrawableHitObject, bool> isDrawableReady;
    private Dictionary<PreviewObjectId, GarbusHitObject> objects = new();
    private readonly Dictionary<PreviewObjectId, DrawableHitObject> drawables = new();
    private readonly Dictionary<PreviewObjectId, DrawableHitObject> pendingVisualRefreshes = new();
    private readonly GarbusScrollingInfo scrollingInfo = new();

    private readonly FramedClock framedClock;

    private Container clockContent = null!;
    private GarbusPlayfield playfield = null!;
    private DesignOverlay designOverlay = null!;

    public ChartPreviewContent()
        : this(PlayScreen.CreateDrawableRepresentation, drawable => drawable.IsLoaded)
    {
    }

    internal ChartPreviewContent(
        Func<GarbusHitObject, DrawableHitObject> drawableFactory,
        Func<DrawableHitObject, bool> isDrawableReady)
    {
        this.drawableFactory = drawableFactory;
        this.isDrawableReady = isDrawableReady;
        framedClock = new FramedClock(manualClock);
    }

    public event Action? ResyncRequested;

    internal event Action<ChartPreviewSnapshot>? SnapshotReceivedForTests;

    internal event Action<ChartPreviewBatch>? BatchAppliedForTests;

    internal long AcceptedRevision { get; private set; }

    internal GarbusChart CurrentChart { get; private set; } = new();

    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
    {
        var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
        dependencies.CacheAs<IGameplayPresentationPolicy>(new PreviewGameplayPresentationPolicy(scrollingInfo));
        dependencies.Cache(scrollingInfo);
        return dependencies;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        playfield = new GarbusPlayfield { Size = Vector2.One };
        playfield.DisplayJudgements.Value = false;

        InternalChildren =
        [
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(18, 18, 26, 255),
            },
            clockContent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = framedClock,
                Children =
                [
                    playfield,
                    designOverlay = new DesignOverlay(CurrentChart),
                ],
            },
        ];
    }

    public bool Replace(ChartPreviewSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        SnapshotReceivedForTests?.Invoke(snapshot);

        if (snapshot.Revision <= AcceptedRevision)
            return reject(snapshot.Revision < AcceptedRevision);

        if (!tryStageSnapshot(snapshot, out GarbusChart? nextChart, out Dictionary<PreviewObjectId, GarbusHitObject>? nextObjects))
            return reject();

        DrawableHitObject[] staleDrawables = drawables.Values.ToArray();

        pendingVisualRefreshes.Clear();
        foreach (DrawableHitObject drawable in staleDrawables)
            detach(drawable);
        drawables.Clear();

        CurrentChart = nextChart;
        objects = nextObjects;

        foreach ((PreviewObjectId id, GarbusHitObject hitObject) in objects)
        {
            DrawableHitObject drawable = createDrawable(hitObject);
            drawables.Add(id, drawable);
            playfield.Add(drawable);
        }

        foreach (DrawableHitObject drawable in staleDrawables)
            drawable.Dispose();

        refreshHitObjects();
        replaceDesignOverlay();
        scrollingInfo.TimeRange.Value = snapshot.TimeRange;
        previewClock.Apply(snapshot.Transport);
        AcceptedRevision = snapshot.Revision;
        return true;
    }

    public bool Apply(ChartPreviewBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Revision != AcceptedRevision + 1)
            return reject(batch.Revision != AcceptedRevision);

        if (!tryStageBatch(batch, out GarbusChart? nextChart, out Dictionary<PreviewObjectId, GarbusHitObject>? nextObjects))
            return reject();

        var retained = new List<(PreviewObjectId Id, DrawableHitObject Drawable, GarbusHitObject HitObject)>();
        var stale = new List<(PreviewObjectId Id, DrawableHitObject Drawable)>();
        var created = new List<(PreviewObjectId Id, GarbusHitObject HitObject)>();

        foreach (PreviewObjectId id in batch.Removes)
            stale.Add((id, drawables[id]));

        foreach (PreviewObjectState upsert in batch.Upserts)
        {
            if (!drawables.TryGetValue(upsert.Id, out DrawableHitObject? drawable))
                created.Add((upsert.Id, upsert.HitObject));
            else if (objects[upsert.Id].GetType() == upsert.HitObject.GetType())
                retained.Add((upsert.Id, drawable, upsert.HitObject));
            else
            {
                stale.Add((upsert.Id, drawable));
                created.Add((upsert.Id, upsert.HitObject));
            }
        }

        foreach ((PreviewObjectId id, DrawableHitObject drawable) in stale)
        {
            pendingVisualRefreshes.Remove(id);
            detach(drawable);
            drawables.Remove(id);
        }

        foreach ((PreviewObjectId _, DrawableHitObject drawable, GarbusHitObject _) in retained)
            detach(drawable);

        CurrentChart = nextChart;
        objects = nextObjects;

        foreach ((PreviewObjectId _, DrawableHitObject drawable, GarbusHitObject hitObject) in retained)
        {
            drawable.Apply(hitObject);
            playfield.Add(drawable);
            playfield.TrackJudgedResult(drawable);
        }

        foreach ((PreviewObjectId id, GarbusHitObject hitObject) in created)
        {
            DrawableHitObject drawable = createDrawable(hitObject);
            drawables.Add(id, drawable);
            playfield.Add(drawable);
        }

        foreach ((PreviewObjectId _, DrawableHitObject drawable) in stale)
            drawable.Dispose();

        if (!batch.Removes.IsEmpty || !batch.Upserts.IsEmpty || batch.Structure != null)
            refreshHitObjects();

        if (batch.Structure != null)
            replaceDesignOverlay();
        if (batch.TimeRange.HasValue)
            scrollingInfo.TimeRange.Value = batch.TimeRange.Value;
        if (batch.Transport.HasValue)
            previewClock.Apply(batch.Transport.Value);

        AcceptedRevision = batch.Revision;
        BatchAppliedForTests?.Invoke(batch);
        return true;
    }

    protected override void Update()
    {
        manualClock.CurrentTime = previewClock.CurrentTime;

        foreach ((PreviewObjectId id, DrawableHitObject drawable) in pendingVisualRefreshes.ToArray())
        {
            if (!drawables.TryGetValue(id, out DrawableHitObject? current) || !ReferenceEquals(current, drawable))
            {
                if (pendingVisualRefreshes.TryGetValue(id, out DrawableHitObject? pending)
                    && ReferenceEquals(pending, drawable))
                    pendingVisualRefreshes.Remove(id);

                continue;
            }

            if (isDrawableReady(drawable))
            {
                pendingVisualRefreshes.Remove(id);
                drawable.RefreshVisualState();
            }
        }

        foreach (DrawableHitObject drawable in drawables.Values
                                                        .SelectMany(withNested)
                                                        .Where(d => !d.Judged && d.IsLoaded
                                                                 && d.HitObject.GetEndTime() <= manualClock.CurrentTime)
                                                        .OrderBy(d => d.HitObject.GetEndTime()))
            drawable.ApplyExternalResult();

        base.Update();
    }

    private bool tryStageSnapshot(
        ChartPreviewSnapshot snapshot,
        out GarbusChart chart,
        out Dictionary<PreviewObjectId, GarbusHitObject> stagedObjects)
    {
        chart = null!;
        stagedObjects = null!;

        if (snapshot.Structure == null
            || snapshot.Objects.IsDefault
            || !validStructure(snapshot.Structure)
            || !validRange(snapshot.TimeRange)
            || !validTransport(snapshot.Transport))
            return false;

        stagedObjects = new Dictionary<PreviewObjectId, GarbusHitObject>();
        foreach (PreviewObjectState state in snapshot.Objects)
        {
            if (!validObjectState(state) || !stagedObjects.TryAdd(state.Id, state.HitObject))
                return false;
        }

        applyDefaults(stagedObjects.Values);
        chart = createChart(snapshot.Structure, stagedObjects);
        return true;
    }

    private bool tryStageBatch(
        ChartPreviewBatch batch,
        out GarbusChart chart,
        out Dictionary<PreviewObjectId, GarbusHitObject> stagedObjects)
    {
        chart = null!;
        stagedObjects = null!;

        if (batch.Removes.IsDefault
            || batch.Upserts.IsDefault
            || batch.Structure != null && (!validStructure(batch.Structure) || batch.Structure.ChartId != CurrentChart.ChartId)
            || batch.TimeRange.HasValue && !validRange(batch.TimeRange.Value)
            || batch.Transport.HasValue && !validTransport(batch.Transport.Value))
            return false;

        var removeIds = new HashSet<PreviewObjectId>();
        foreach (PreviewObjectId id in batch.Removes)
        {
            if (id.Value <= 0 || !removeIds.Add(id) || !objects.ContainsKey(id))
                return false;
        }

        var upsertIds = new HashSet<PreviewObjectId>();
        foreach (PreviewObjectState state in batch.Upserts)
        {
            if (!validObjectState(state)
                || !upsertIds.Add(state.Id)
                || removeIds.Contains(state.Id))
                return false;
        }

        stagedObjects = new Dictionary<PreviewObjectId, GarbusHitObject>(objects);
        foreach (PreviewObjectId id in batch.Removes)
            stagedObjects.Remove(id);
        foreach (PreviewObjectState state in batch.Upserts)
            stagedObjects[state.Id] = state.HitObject;

        applyDefaults(batch.Upserts.Select(state => state.HitObject));
        chart = createChart(batch.Structure ?? structureFrom(CurrentChart), stagedObjects);
        return true;
    }

    private static void applyDefaults(IEnumerable<GarbusHitObject> hitObjects)
    {
        foreach (GarbusHitObject hitObject in hitObjects)
            hitObject.ApplyDefaults();
    }

    private static GarbusChart createChart(
        PreviewChartStructure structure,
        IReadOnlyDictionary<PreviewObjectId, GarbusHitObject> chartObjects)
        => new()
        {
            ChartId = structure.ChartId,
            Metadata = structure.Metadata,
            PreviewTime = structure.PreviewTime,
            ControlPointInfo = structure.ControlPointInfo,
            DesignPointInfo = structure.DesignPointInfo,
            HitObjects = chartObjects.OrderBy(pair => pair.Value.StartTime)
                                     .ThenBy(pair => pair.Key.Value)
                                     .Select(pair => pair.Value)
                                     .ToList(),
        };

    private static PreviewChartStructure structureFrom(GarbusChart chart) => new(
        chart.ChartId,
        chart.Metadata,
        chart.PreviewTime,
        chart.ControlPointInfo!,
        chart.DesignPointInfo);

    private static bool validStructure(PreviewChartStructure structure) =>
        structure.ChartId != Guid.Empty
        && structure.Metadata != null
        && structure.ControlPointInfo != null
        && structure.DesignPointInfo != null;

    private static bool validObjectState(PreviewObjectState? state) =>
        state != null
        && state.Id.Value > 0
        && state.HitObject != null
        && isSupported(state.HitObject);

    private static bool isSupported(GarbusHitObject hitObject) => hitObject is
        CardinalNote or CardinalHoldNote or ShoulderNote or ShoulderHoldNote
        or GarbusSlamCentered or GarbusSlamEdge or SliderBody;

    private static bool validRange(double timeRange) => double.IsFinite(timeRange) && timeRange > 0;

    private static bool validTransport(PreviewTransportState transport) =>
        double.IsFinite(transport.Time)
        && double.IsFinite(transport.Rate)
        && transport.Rate > 0;

    private bool reject(bool requestResync = true)
    {
        if (requestResync)
            ResyncRequested?.Invoke();
        return false;
    }

    private static IEnumerable<DrawableHitObject> withNested(DrawableHitObject drawable)
    {
        yield return drawable;

        foreach (DrawableHitObject nested in drawable.NestedHitObjects.SelectMany(withNested))
            yield return nested;
    }

    private void refreshHitObjects()
    {
        playfield.SetHitObjects(CurrentChart.HitObjects);

        foreach ((PreviewObjectId id, DrawableHitObject drawable) in drawables)
        {
            if (isDrawableReady(drawable))
            {
                pendingVisualRefreshes.Remove(id);
                drawable.RefreshVisualState();
            }
            else
                pendingVisualRefreshes[id] = drawable;
        }
    }

    private DrawableHitObject createDrawable(GarbusHitObject hitObject)
    {
        DrawableHitObject drawable = drawableFactory(hitObject);
        disableInput(drawable);
        return drawable;
    }

    private void disableInput(DrawableHitObject drawable)
    {
        drawable.HandleUserInput = false;
        drawable.OnNestedDrawableCreated += disableInput;

        foreach (DrawableHitObject nested in drawable.NestedHitObjects)
            disableInput(nested);
    }

    private void detach(DrawableHitObject drawable)
    {
        playfield.UntrackJudgedResult(drawable);
        playfield.Remove(drawable);
    }

    private void replaceDesignOverlay()
    {
        clockContent.Remove(designOverlay, true);
        clockContent.Add(designOverlay = new DesignOverlay(CurrentChart));
    }

    protected override void Dispose(bool isDisposing)
    {
        pendingVisualRefreshes.Clear();
        base.Dispose(isDisposing);
    }

    internal int ObjectCountForTests => drawables.Count;

    internal DrawableHitObject DrawableForTests(PreviewObjectId objectId) => drawables[objectId];

    internal GarbusPlayfield PlayfieldForTests => playfield;

    internal DesignOverlay DesignOverlayForTests => designOverlay;

    internal double ClockTimeForTests => manualClock.CurrentTime;

    internal double CurrentTimeRangeForTests => scrollingInfo.TimeRange.Value;
}
