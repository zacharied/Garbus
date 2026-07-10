// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/ScrollingHitObjectComposer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus (structural rework): osu's ScrollingHitObjectComposer derives from the
// DrawableRuleset-bound HitObjectComposer<T> and reads scroll info off the DrawableScrollingRuleset.
// Garbus has NO DrawableRuleset, so this base creates the playfield itself (CreatePlayfield), OWNS and
// caches the IScrollingInfo (EditorScrollingInfo) that the playfield resolves, and does the drawable
// lifecycle the DrawableRuleset used to do: non-pooled create-on-add from EditorChart.HitObjectAdded /
// remove-on-HitObjectRemoved (PlayScreen's non-pooled pattern), initial population from
// EditorChart.HitObjects; updates refresh the live drawable in place via HitObject.DefaultsApplied. The osu speed-change
// toggle / ISupportConstantAlgorithmToggle / OsuConfig plumbing is dropped. TimelineTimeRange (written by
// Task 17's zoom sync) flows into IScrollingInfo.TimeRange. The beat-snap-grid update loop is preserved.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Testing;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Compose
{
    public abstract partial class ScrollingHitObjectComposer<T> : HitObjectComposer
        where T : GarbusHitObject
    {
        /// <summary>
        /// When set, drives the scroll speed (visible time range) — written by the timeline's zoom sync.
        /// Flows into the composer-owned <see cref="IScrollingInfo.TimeRange"/>.
        /// </summary>
        public readonly Bindable<double> TimelineTimeRange = new Bindable<double>(3000);

        private readonly EditorScrollingInfo scrollingInfo = new EditorScrollingInfo();

        private Playfield? playfield;

        // Created lazily so it exists by the time the base composer's BDL references Playfield while
        // building InternalChildren (BDL runs base-first, before this class's own BDL).
        public override Playfield Playfield => playfield ??= CreatePlayfield();

        /// <summary>
        /// Tracks the non-pooled drawable created for each live hit object so Remove/Update can reach the
        /// correct <see cref="DrawableHitObject"/> overload on <see cref="Playfield"/> (the
        /// <see cref="HitObject"/> overload goes through <c>entryManager</c>, which is never populated by
        /// the non-pooled Add path and would silently no-op).
        /// </summary>
        private readonly Dictionary<GarbusHitObject, DrawableHitObject> drawableMap = new Dictionary<GarbusHitObject, DrawableHitObject>();

        public override IEnumerable<DrawableHitObject> HitObjects => Playfield.AllHitObjects;

        public override bool CursorInPlacementArea => Playfield.ReceivePositionalInputAt(InputManager.CurrentState.Mouse.Position);

        private BeatSnapGrid? beatSnapGrid;

        /// <summary>Create the concrete playfield hosted by this composer.</summary>
        protected abstract Playfield CreatePlayfield();

        /// <summary>Create the (non-pooled) drawable representation for a hit object.</summary>
        protected abstract DrawableHitObject? CreateDrawableRepresentation(T hitObject);

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // The playfield's ScrollingHitObjectContainer / ScrollingPlayfield resolve IScrollingInfo, and
            // so does the beat snap grid's DrawableGridLine — cache it before any child loads.
            dependencies.CacheAs<IScrollingInfo>(scrollingInfo);

            // The selection blueprints (siblings of the playfield, not its children) resolve Playfield to
            // reach its ScrollingHitObjectContainer for time↔screen mapping. osu supplies this through the
            // DrawableRuleset's cached dependencies; Garbus has no DrawableRuleset, so cache it here.
            dependencies.CacheAs<Playfield>(Playfield);

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            TimelineTimeRange.BindValueChanged(range => scrollingInfo.TimeRangeBindable.Value = range.NewValue, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Non-pooled: create a drawable per hit object as they are added, remove on removal.
            // Updates need NO composer action: EditorChart.UpdateState → HitObject.ApplyDefaults fires
            // DefaultsApplied, which makes the drawable re-Apply() itself in place (rebuilding nested
            // drawables) and the scrolling container invalidate its layout; the editor visuals read the
            // hit object live every frame. Recreating the drawable here instead used to tear down
            // framebuffer-backed visuals on every drag event (the slider node-drag GC storm).
            EditorChart.HitObjectAdded += addHitObject;
            EditorChart.HitObjectRemoved += removeHitObject;

            foreach (var hitObject in EditorChart.HitObjects)
                addHitObject(hitObject);
        }

        private void addHitObject(GarbusHitObject hitObject)
        {
            if (hitObject is not T typed)
                return;

            var drawable = CreateDrawableRepresentation(typed);
            if (drawable == null)
                return;

            drawableMap[hitObject] = drawable;
            Playfield.Add(drawable);
        }

        private void removeHitObject(GarbusHitObject hitObject)
        {
            if (!drawableMap.TryGetValue(hitObject, out var drawable))
                return;

            drawableMap.Remove(hitObject);
            // Note: Playfield.Remove(DrawableHitObject) has a pre-existing vendored quirk of returning
            // false even on success — we deliberately ignore the return value here.
            Playfield.Remove(drawable);

            // The container detaches without disposing (RemoveInternal(…, false) — correct for osu's
            // pooled path, but these drawables are non-pooled and nothing else disposes them). An
            // undisposed drawable stays subscribed to HitObject.DefaultsApplied and re-runs Apply()
            // on every later update of the same object, so each drag/edit would pile up zombies doing
            // quadratic work (the Phase4 GC storm). Dispose is what unhooks that subscription.
            drawable.Dispose();
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            updateBeatSnapGrid();
        }

        private void updateBeatSnapGrid()
        {
            beatSnapGrid ??= this.ChildrenOfType<BeatSnapGrid>().FirstOrDefault();

            if (beatSnapGrid == null)
                return;

            if (CurrentTool is SelectTool)
            {
                if (EditorChart.SelectedHitObjects.Any())
                    beatSnapGrid.SelectionTimeRange = (EditorChart.SelectedHitObjects.Min(h => h.StartTime), EditorChart.SelectedHitObjects.Max(h => h.GetEndTime()));
                else
                    beatSnapGrid.SelectionTimeRange = null;
            }
            else
            {
                var result = FindSnappedPositionAndTime(InputManager.CurrentState.Mouse.Position);
                if (result.Time is double time)
                    beatSnapGrid.SelectionTimeRange = (time, time);
                else
                    beatSnapGrid.SelectionTimeRange = null;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (EditorChart != null)
            {
                EditorChart.HitObjectAdded -= addHitObject;
                EditorChart.HitObjectRemoved -= removeHitObject;
            }

            drawableMap.Clear();
        }
    }
}
