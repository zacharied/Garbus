using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts.Format;
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

    private readonly ChartPreviewModel model = new();
    private readonly ChartPreviewClock previewClock = new();
    private readonly ManualClock manualClock = new();
    private readonly Func<GarbusHitObject, DrawableHitObject> drawableFactory;
    private readonly Func<DrawableHitObject, bool> isDrawableReady;
    private readonly Dictionary<long, DrawableHitObject> drawables = new();
    private readonly Dictionary<long, DrawableHitObject> pendingVisualRefreshes = new();
    private readonly GarbusScrollingInfo scrollingInfo = new();

    private readonly FramedClock framedClock;

    private long revision;
    private int applyBatchDepth;
    private bool hitObjectRefreshPending;

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

    public event Action? ResyncRequested
    {
        add => model.ResyncRequested += value;
        remove => model.ResyncRequested -= value;
    }

    internal event Action<ChartPreviewFullState>? FullStateReceivedForTests;

    internal event Action<ChartPreviewMessage>? MessageAppliedForTests;

    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
    {
        var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
        dependencies.Cache(new ChartPreviewContext(scrollingInfo));
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
                    designOverlay = new DesignOverlay(model.Chart),
                ],
            },
        ];
    }

    public bool Apply(ChartPreviewMessage message)
    {
        // A full state replaces chart and clock together; mixed revisions would create a snapshot that never existed.
        if (message is ChartPreviewFullState mismatchedFullState
            && mismatchedFullState.Transport.Revision != mismatchedFullState.Revision)
            return model.RequestResync();

        if (message is ChartPreviewFullState receivedFullState)
            FullStateReceivedForTests?.Invoke(receivedFullState);

        long? incomingRevision = message switch
        {
            ChartPreviewFullState state => state.Revision,
            ChartPreviewObjectUpsert state => state.Revision,
            ChartPreviewObjectRemove state => state.Revision,
            ChartPreviewStructuralState state => state.Revision,
            ChartPreviewTransport state => state.Revision,
            ChartPreviewScrollSpeed state => state.Revision,
            _ => null,
        };

        // Strict ordering prevents a same-frame remove/upsert batch from being replayed in a different order.
        if (incomingRevision <= revision)
            return incomingRevision < revision && model.RequestResync();

        bool applied;

        switch (message)
        {
            case ChartPreviewFullState fullState:
                if (!model.ApplyFullState(fullState))
                    return false;

                rebuildObjects();
                replaceDesignOverlay();
                scrollingInfo.TimeRange.Value = fullState.TimeRange;
                previewClock.Apply(fullState.Transport);
                refreshHitObjects();
                applied = true;
                break;

            case ChartPreviewObjectUpsert upsert:
                applied = applyObjectUpsert(upsert);
                break;

            case ChartPreviewObjectRemove remove:
                if (!model.ApplyObjectRemove(remove))
                    return false;

                if (!drawables.Remove(remove.ObjectId, out DrawableHitObject? removed))
                    return model.RequestResync();

                pendingVisualRefreshes.Remove(remove.ObjectId);
                playfield.UntrackJudgedResult(removed);
                playfield.Remove(removed);
                removed.Dispose();
                refreshHitObjects();
                applied = true;
                break;

            case ChartPreviewStructuralState structuralState:
                if (!model.ApplyStructuralState(structuralState))
                    return false;

                replaceDesignOverlay();
                refreshHitObjects();
                applied = true;
                break;

            case ChartPreviewTransport transport:
                previewClock.Apply(transport);
                applied = true;
                break;

            case ChartPreviewScrollSpeed scrollSpeed:
                scrollingInfo.TimeRange.Value = scrollSpeed.TimeRange;
                applied = true;
                break;

            default:
                return true;
        }

        if (applied && incomingRevision.HasValue)
            revision = incomingRevision.Value;

        if (applied)
            MessageAppliedForTests?.Invoke(message);

        return applied;
    }

    internal void ApplyBatch(Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);

        applyBatchDepth++;

        try
        {
            apply();
        }
        finally
        {
            if (--applyBatchDepth == 0 && hitObjectRefreshPending)
            {
                hitObjectRefreshPending = false;
                refreshHitObjects();
            }
        }
    }

    protected override void Update()
    {
        manualClock.CurrentTime = previewClock.CurrentTime;

        foreach ((long id, DrawableHitObject drawable) in pendingVisualRefreshes.ToArray())
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
            drawable.ApplyPreviewResult();

        base.Update();
    }

    private static IEnumerable<DrawableHitObject> withNested(DrawableHitObject drawable)
    {
        yield return drawable;

        foreach (DrawableHitObject nested in drawable.NestedHitObjects.SelectMany(withNested))
            yield return nested;
    }

    private bool applyObjectUpsert(ChartPreviewObjectUpsert state)
    {
        GarbusHitObject incoming;
        try
        {
            incoming = GarbusChartSerializer.DecodeHitObject(state.ObjectJson);
        }
        catch
        {
            return model.ApplyObjectUpsert(state);
        }

        bool hadDrawable = drawables.TryGetValue(state.ObjectId, out DrawableHitObject? drawable);
        bool retainDrawable = hadDrawable
                              && model.Objects.TryGetValue(state.ObjectId, out GarbusHitObject? existing)
                              && existing.GetType() == incoming.GetType();

        if (retainDrawable)
        {
            playfield.UntrackJudgedResult(drawable!);
            playfield.Remove(drawable!);
        }

        if (!model.ApplyObjectUpsert(state))
        {
            if (retainDrawable)
            {
                playfield.Add(drawable!);
                playfield.TrackJudgedResult(drawable!);
            }
            return false;
        }

        if (retainDrawable)
        {
            playfield.Add(drawable!);
            playfield.TrackJudgedResult(drawable!);
        }
        else
        {
            if (hadDrawable)
            {
                pendingVisualRefreshes.Remove(state.ObjectId);
                playfield.UntrackJudgedResult(drawable!);
                playfield.Remove(drawable!);
                drawable!.Dispose();
            }

            DrawableHitObject replacement = createDrawable(model.Objects[state.ObjectId]);
            drawables[state.ObjectId] = replacement;
            playfield.Add(replacement);
        }

        refreshHitObjects();
        return true;
    }

    private void refreshHitObjects()
    {
        if (applyBatchDepth > 0)
        {
            hitObjectRefreshPending = true;
            return;
        }

        playfield.SetHitObjects(model.Chart.HitObjects);

        foreach ((long id, DrawableHitObject drawable) in drawables)
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

    private void rebuildObjects()
    {
        pendingVisualRefreshes.Clear();

        foreach (DrawableHitObject drawable in drawables.Values)
        {
            playfield.UntrackJudgedResult(drawable);
            playfield.Remove(drawable);
            drawable.Dispose();
        }

        drawables.Clear();

        foreach ((long id, GarbusHitObject hitObject) in model.Objects)
        {
            DrawableHitObject drawable = createDrawable(hitObject);
            drawables.Add(id, drawable);
            playfield.Add(drawable);
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

    private void replaceDesignOverlay()
    {
        clockContent.Remove(designOverlay, true);
        clockContent.Add(designOverlay = new DesignOverlay(model.Chart));
    }

    protected override void Dispose(bool isDisposing)
    {
        pendingVisualRefreshes.Clear();
        base.Dispose(isDisposing);
    }

    internal int ObjectCountForTests => drawables.Count;

    internal DrawableHitObject DrawableForTests(long objectId) => drawables[objectId];

    internal GarbusPlayfield PlayfieldForTests => playfield;

    internal DesignOverlay DesignOverlayForTests => designOverlay;

    internal double ClockTimeForTests => manualClock.CurrentTime;

    internal double CurrentTimeRangeForTests => scrollingInfo.TimeRange.Value;

    internal long AcceptedRevisionForTests => revision;
}
