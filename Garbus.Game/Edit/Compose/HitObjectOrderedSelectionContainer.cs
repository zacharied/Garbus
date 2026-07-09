// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/HitObjectOrderedSelectionContainer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; HitObject → GarbusHitObject;
// EditorBeatmap → EditorChart; BeatmapReprocessed → HitObjectUpdated (Garbus has no reprocess
// concept — HitObjectUpdated fires after any update that may change StartTime, which is the same
// trigger needed for re-sorting); IHasComboInformation fallback removed (no combo in Garbus);
// GetEndTime() extension comes from Garbus.Game.Gameplay.Objects.

using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Objects;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A container for <see cref="SelectionBlueprint{GarbusHitObject}"/> ordered by their <see cref="GarbusHitObject"/> start times.
    /// </summary>
    public sealed partial class HitObjectOrderedSelectionContainer : BlueprintContainer<GarbusHitObject>.SelectionBlueprintContainer
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            editorChart.HitObjectUpdated += onHitObjectUpdated;
        }

        private void onHitObjectUpdated(GarbusHitObject _) => SortInternal();

        public override void Add(SelectionBlueprint<GarbusHitObject> drawable)
        {
            Sort();
            base.Add(drawable);
        }

        public override bool Remove(SelectionBlueprint<GarbusHitObject> drawable, bool disposeImmediately)
        {
            Sort();
            return base.Remove(drawable, disposeImmediately);
        }

        internal void Sort() => SortInternal();

        protected override int Compare(Drawable x, Drawable y)
        {
            var xObj = ((SelectionBlueprint<GarbusHitObject>)x).Item;
            var yObj = ((SelectionBlueprint<GarbusHitObject>)y).Item;

            // Put earlier blueprints towards the end of the list, so they handle input first
            int result = yObj.StartTime.CompareTo(xObj.StartTime);
            if (result != 0) return result;

            // Fall back to end time if the start time is equal.
            result = yObj.GetEndTime().CompareTo(xObj.GetEndTime());
            if (result != 0) return result;

            return CompareReverseChildID(x, y);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorChart.IsNotNull())
                editorChart.HitObjectUpdated -= onHitObjectUpdated;
        }
    }
}
