using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;

namespace Garbus.Game.Edit.Preview;

internal sealed class PreviewResultTimeline
{
    private readonly Playfield playfield;
    private readonly Func<DrawableHitObject, bool> isRootReady;
    private ScheduledResult[] entries = [];
    private int cursor;

    internal PreviewResultTimeline(Playfield playfield, Func<DrawableHitObject, bool> isRootReady)
    {
        this.playfield = playfield;
        this.isRootReady = isRootReady;
    }

    internal long VisitedEntryCount { get; private set; }

    internal void Rebuild(IEnumerable<KeyValuePair<PreviewObjectId, DrawableHitObject>> roots)
    {
        var nextEntries = new List<ScheduledResult>();

        foreach ((PreviewObjectId rootId, DrawableHitObject root) in roots)
        {
            int treeOrder = 0;
            addPostOrder(root, root, rootId, ref treeOrder, nextEntries);
        }

        entries = nextEntries.OrderBy(entry => entry.Time)
                            .ThenBy(entry => entry.RootId.Value)
                            .ThenBy(entry => entry.TreeOrder)
                            .ToArray();
        cursor = 0;
    }

    internal void Seek(double time)
    {
        while (cursor > 0 && entries[cursor - 1].Time > time)
        {
            ScheduledResult entry = entries[--cursor];

            if (entry.Drawable.Judged && !playfield.RevertResult(entry.Drawable.Result))
                throw new InvalidOperationException("The preview result stack no longer matches timeline chronology.");

            VisitedEntryCount++;
        }

        while (cursor < entries.Length && entries[cursor].Time <= time)
        {
            ScheduledResult entry = entries[cursor];

            // A due generation blocks every later result until its complete drawable tree is ready.
            if (!isRootReady(entry.Root) || !entry.Drawable.IsLoaded)
                break;

            if (!entry.Drawable.Judged)
                entry.Drawable.ApplyResultAt(entry.Drawable.Result.Judgement.MaxResult, entry.Time);

            cursor++;
            VisitedEntryCount++;
        }
    }

    internal void RevertAll()
    {
        while (cursor > 0)
        {
            ScheduledResult entry = entries[--cursor];

            if (entry.Drawable.Judged && !playfield.RevertResult(entry.Drawable.Result))
                throw new InvalidOperationException("The preview result stack no longer matches timeline chronology.");

            VisitedEntryCount++;
        }
    }

    internal void Clear()
    {
        entries = [];
        cursor = 0;
    }

    private static void addPostOrder(
        DrawableHitObject drawable,
        DrawableHitObject root,
        PreviewObjectId rootId,
        ref int treeOrder,
        ICollection<ScheduledResult> target)
    {
        foreach (DrawableHitObject nested in drawable.NestedHitObjects)
            addPostOrder(nested, root, rootId, ref treeOrder, target);

        target.Add(new ScheduledResult(
            drawable.HitObject.GetEndTime(),
            rootId,
            treeOrder++,
            root,
            drawable));
    }

    private readonly record struct ScheduledResult(
        double Time,
        PreviewObjectId RootId,
        int TreeOrder,
        DrawableHitObject Root,
        DrawableHitObject Drawable);
}
