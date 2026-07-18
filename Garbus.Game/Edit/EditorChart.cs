// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/EditorBeatmap.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: heavily trimmed — IBeatmap/IBeatSnapProvider/BeatmapProcessor/skins/storyboard
// removed; operates on GarbusChart and GarbusHitObject directly; ApplyDefaults takes no arguments
// (Garbus hit windows are fixed, no ControlPointInfo/Difficulty needed); no IBeatmapProcessor; no
// startTime bindable tracking (editor does not need the auto-reorder-on-StartTime-change path yet);
// namespace Garbus.Game.Edit.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Objects;
using osu.Framework.Bindables;
using osu.Framework.Utils;

namespace Garbus.Game.Edit
{
    public partial class EditorChart : TransactionalCommitComponent
    {
        /// <summary>The source chart this editor operates on.</summary>
        public readonly GarbusChart Chart;

        /// <summary>Invoked when a <see cref="GarbusHitObject"/> is added.</summary>
        public event Action<GarbusHitObject>? HitObjectAdded;

        /// <summary>Invoked when a <see cref="GarbusHitObject"/> is removed.</summary>
        public event Action<GarbusHitObject>? HitObjectRemoved;

        /// <summary>Invoked when a <see cref="GarbusHitObject"/> is updated (defaults re-applied).</summary>
        public event Action<GarbusHitObject>? HitObjectUpdated;

        /// <summary>All currently selected hit objects.</summary>
        public readonly BindableList<GarbusHitObject> SelectedHitObjects = new BindableList<GarbusHitObject>();

        /// <summary>Time-ordered view of all hit objects.</summary>
        public IReadOnlyList<GarbusHitObject> HitObjects => hitObjects;

        public Charts.Timing.ControlPointInfo ControlPointInfo => Chart.ControlPointInfo;
        public Charts.Design.DesignPointInfo DesignPointInfo => Chart.DesignPointInfo;
        public ChartMetadata Metadata => Chart.Metadata;

        // Direct alias to Chart.HitObjects — no shadow copy. All mutations go to the same list
        // that serialization reads, so Save() sees every edit without any extra bookkeeping.
        // (osu's EditorBeatmap uses the identical pattern: mutableHitObjects => PlayableBeatmap.HitObjects.)
        private readonly List<GarbusHitObject> hitObjects;

        // Pending batch collections, flushed in UpdateState.
        private readonly List<GarbusHitObject> batchPendingInserts = new List<GarbusHitObject>();
        private readonly List<GarbusHitObject> batchPendingDeletes = new List<GarbusHitObject>();
        private readonly HashSet<GarbusHitObject> batchPendingUpdates = new HashSet<GarbusHitObject>();

        public EditorChart(GarbusChart chart)
        {
            Chart = chart;

            // Alias the chart's own list. Ensure it starts time-ordered.
            hitObjects = chart.HitObjects;
            hitObjects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        }

        /// <summary>
        /// Adds a hit object, inserting it in time order, applying defaults, and firing the event.
        /// Mutations outside a transaction save immediately.
        /// </summary>
        public void Add(GarbusHitObject h)
        {
            int index = findInsertionIndex(h.StartTime);
            hitObjects.Insert(index, h);

            BeginChange();
            batchPendingInserts.Add(h);
            EndChange();
        }

        /// <summary>Adds multiple hit objects as a single transaction.</summary>
        public void AddRange(IEnumerable<GarbusHitObject> hs)
        {
            BeginChange();
            foreach (var h in hs)
                Add(h);
            EndChange();
        }

        /// <summary>
        /// Removes a hit object and deselects it.
        /// </summary>
        public void Remove(GarbusHitObject h)
        {
            if (!hitObjects.Remove(h))
                return;

            BeginChange();
            batchPendingDeletes.Add(h);
            EndChange();
        }

        /// <summary>Removes multiple hit objects as a single transaction.</summary>
        public void RemoveRange(IEnumerable<GarbusHitObject> hs)
        {
            BeginChange();
            foreach (var h in hs)
                Remove(h);
            EndChange();
        }

        /// <summary>
        /// Re-applies defaults to a hit object (regenerates nested objects), re-sorts the list, and
        /// fires <see cref="HitObjectUpdated"/>.
        /// </summary>
        public void Update(GarbusHitObject h)
        {
            batchPendingUpdates.Add(h);
            SaveState();
        }

        /// <summary>
        /// Performs an action on each selected hit object as a single transaction, updating each.
        /// </summary>
        public void PerformOnSelection(Action<GarbusHitObject> action)
        {
            if (SelectedHitObjects.Count == 0)
                return;

            BeginChange();
            foreach (var h in SelectedHitObjects)
            {
                action(h);
                Update(h);
            }
            EndChange();
        }

        /// <summary>
        /// Performs an action on every hit object whose start time falls in [start, end) as a single
        /// transaction, updating each. Boundary semantics match osu's timing sections: inclusive
        /// start, exclusive end, with floating-point tolerance.
        /// </summary>
        public void PerformOnRange(double start, double end, Action<GarbusHitObject> action)
        {
            var affected = hitObjects.Where(h => Precision.AlmostBigger(h.StartTime, start)
                                                 && Precision.DefinitelyBigger(end, h.StartTime)).ToArray();
            if (affected.Length == 0)
                return;

            BeginChange();

            foreach (var h in affected)
            {
                action(h);
                Update(h);
            }

            EndChange();
        }

        /// <summary>Removes all hit objects.</summary>
        public void Clear() => RemoveRange(hitObjects.ToArray());

        protected override void UpdateState()
        {
            if (batchPendingInserts.Count == 0 && batchPendingDeletes.Count == 0 && batchPendingUpdates.Count == 0)
                return;

            // Apply defaults to inserts and updates (regenerates nested objects).
            foreach (var h in batchPendingInserts)
                applyDefaults(h);
            foreach (var h in batchPendingUpdates)
                applyDefaults(h);

            // Re-sort after updates may have changed start times.
            if (batchPendingUpdates.Count > 0)
                hitObjects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

            // Snapshot and clear before invoking callbacks (callbacks may mutate).
            var inserts = batchPendingInserts.ToArray();
            batchPendingInserts.Clear();

            var deletes = batchPendingDeletes.ToArray();
            batchPendingDeletes.Clear();

            var updates = batchPendingUpdates.ToArray();
            batchPendingUpdates.Clear();

            // Deselect removed objects.
            foreach (var h in deletes)
                SelectedHitObjects.Remove(h);

            // Fire events.
            foreach (var h in deletes) HitObjectRemoved?.Invoke(h);
            foreach (var h in inserts) HitObjectAdded?.Invoke(h);
            foreach (var h in updates) HitObjectUpdated?.Invoke(h);
        }

        private void applyDefaults(GarbusHitObject h) => h.ApplyDefaults();

        /// <summary>
        /// Returns the insertion index to maintain time ordering. Inserts after all objects with the
        /// same or earlier start time (stable append at end of ties, matching osu's behaviour).
        /// </summary>
        private int findInsertionIndex(double startTime)
        {
            for (int i = 0; i < hitObjects.Count; i++)
            {
                if (hitObjects[i].StartTime > startTime)
                    return i;
            }
            return hitObjects.Count;
        }
    }
}
