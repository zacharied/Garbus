// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/EditorSelectionHandler.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; EditorBeatmap → EditorChart;
// HitObject → GarbusHitObject; ALL sample/bank/new-combo ternary state removed (HitSampleInfo,
// IHasRepeats, IHasComboInformation, Humanizer — none present in Garbus); HIT_BANK_AUTO constant
// and all related bindables dropped; UpdateTernaryStates() kept as protected virtual no-op so
// BacSelectionHandler (Task 16) can override it; GetContextMenuItemsForSelection override dropped
// (no New Combo / Sample / Bank items relevant to Garbus); RightClickAlwaysQuickDeletes kept;
// DeleteItems wraps EditorChart.RemoveRange in a ChangeHandler transaction.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace Garbus.Game.Edit.Compose
{
    public partial class EditorSelectionHandler : SelectionHandler<Objects.GarbusHitObject>
    {
        /// <summary>
        /// Whether right click should delete even when shift is not held.
        /// </summary>
        public bool RightClickAlwaysQuickDeletes { get; set; }

        [Resolved]
        protected EditorChart EditorChart { get; private set; } = null!;

        [Resolved]
        protected IEditorChangeHandler? ChangeHandler { get; private set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            SelectedItems.CollectionChanged += (_, _) => Scheduler.AddOnce(UpdateTernaryStates);
        }

        protected override bool ShouldQuickDelete(MouseButtonEvent e)
        {
            if (RightClickAlwaysQuickDeletes && e.Button == MouseButton.Right)
                return true;

            return base.ShouldQuickDelete(e);
        }

        protected override void DeleteItems(IEnumerable<Objects.GarbusHitObject> items)
        {
            ChangeHandler?.BeginChange();
            EditorChart.RemoveRange(items);
            ChangeHandler?.EndChange();
        }

        /// <summary>
        /// Called when context menu ternary states may need to be recalculated (selection changed or hit object updated).
        /// Override in BacSelectionHandler (Task 16) to update direction toggles.
        /// </summary>
        protected virtual void UpdateTernaryStates()
        {
        }
    }
}
