// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/SelectionHandler.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; OsuContextMenuContainer dependency
// dropped (Garbus uses BasicContextMenuContainer — no DI resolve needed, DeselectAll just clears
// SelectedItems); IKeyBindingHandler<GlobalAction>/flip/reverse key bindings dropped (GlobalAction is
// osu.Game-only; flip/reverse unused here); SelectionRotationHandler/SelectionScaleHandler and
// CreateChildDependencies override dropped entirely (no rotation/scale); OsuMenuItem
// → GarbusMenuItem; CommonStrings.ButtonsDelete → plain "Delete" string; Hotkey property dropped from
// delete menu item; GetContextMenuItemsForSelection uses IEnumerable<SelectionBlueprint<T>> matching
// osu's real virtual signature (IReadOnlyList would be a narrowing that breaks overrides).

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A component which outlines items and handles movement of selections.
    /// </summary>
    public abstract partial class SelectionHandler<T> : CompositeDrawable, IKeyBindingHandler<PlatformAction>, IHasContextMenu
    {
        /// <summary>
        /// How much padding around the selection area is added.
        /// </summary>
        public const float INFLATE_SIZE = 5;

        /// <summary>
        /// The currently selected blueprints.
        /// Should be used when operations are dealing directly with the visible blueprints.
        /// For more general selection operations, use <see cref="SelectedItems"/> instead.
        /// </summary>
        public IReadOnlyList<SelectionBlueprint<T>> SelectedBlueprints => selectedBlueprints;

        /// <summary>
        /// The currently selected items.
        /// </summary>
        public readonly BindableList<T> SelectedItems = new BindableList<T>();

        [Resolved(CanBeNull = true)]
        protected IEditorChangeHandler? ChangeHandler { get; private set; }

        private readonly List<SelectionBlueprint<T>> selectedBlueprints;

        public SelectionBox SelectionBox { get; private set; } = null!;

        protected SelectionHandler()
        {
            selectedBlueprints = new List<SelectionBlueprint<T>>();

            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            AddInternal(SelectionBox = CreateSelectionBox());

            SelectedItems.BindCollectionChanged((_, _) => Scheduler.AddOnce(updateVisibility), true);
        }

        public SelectionBox CreateSelectionBox()
            => new SelectionBox
            {
                OperationStarted = OnOperationBegan,
                OperationEnded = OnOperationEnded,

                OnFlip = HandleFlip,
                OnReverse = HandleReverse,
            };

        /// <summary>
        /// Fired when a drag operation begins from the selection box.
        /// </summary>
        protected virtual void OnOperationBegan()
        {
            ChangeHandler?.BeginChange();
        }

        /// <summary>
        /// Fired when a drag operation ends from the selection box.
        /// </summary>
        protected virtual void OnOperationEnded()
        {
            ChangeHandler?.EndChange();
        }

        #region User Input Handling

        /// <remarks>
        /// Positional input must be received outside the container's bounds,
        /// in order to handle blueprints which are partially offscreen.
        /// </remarks>
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        /// <summary>
        /// Handles the selected items being moved.
        /// </summary>
        /// <param name="moveEvent">The move event.</param>
        /// <returns>Whether any items could be moved.</returns>
        public virtual bool HandleMovement(MoveSelectionEvent<T> moveEvent) => false;

        /// <summary>
        /// Handles the selected items being flipped.
        /// </summary>
        /// <param name="direction">The direction to flip.</param>
        /// <param name="flipOverOrigin">Whether the flip operation should be global to the playfield's origin or local to the selected pattern.</param>
        /// <returns>Whether any items could be flipped.</returns>
        public virtual bool HandleFlip(Direction direction, bool flipOverOrigin) => false;

        /// <summary>
        /// Handles the selected items being reversed pattern-wise.
        /// </summary>
        /// <returns>Whether any items could be reversed.</returns>
        public virtual bool HandleReverse() => false;

        public bool OnPressed(KeyBindingPressEvent<PlatformAction> e)
        {
            switch (e.Action)
            {
                case PlatformAction.Delete:
                    DeleteSelected();
                    return true;
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<PlatformAction> e)
        {
        }

        #endregion

        #region Selection Handling

        /// <summary>
        /// Deselect all selected items.
        /// </summary>
        protected void DeselectAll()
        {
            SelectedItems.Clear();
        }

        /// <summary>
        /// Handle a blueprint becoming selected.
        /// </summary>
        /// <param name="blueprint">The blueprint.</param>
        internal virtual void HandleSelected(SelectionBlueprint<T> blueprint)
        {
            // there are potentially multiple SelectionHandlers active, but we only want to add items to the selected list once.
            if (!SelectedItems.Contains(blueprint.Item))
                SelectedItems.Add(blueprint.Item);

            selectedBlueprints.Add(blueprint);
        }

        /// <summary>
        /// Handle a blueprint becoming deselected.
        /// </summary>
        /// <param name="blueprint">The blueprint.</param>
        internal virtual void HandleDeselected(SelectionBlueprint<T> blueprint)
        {
            SelectedItems.Remove(blueprint.Item);
            selectedBlueprints.Remove(blueprint);
        }

        protected virtual bool ShouldQuickDelete(MouseButtonEvent e) => e.Button == MouseButton.Middle || (e.ShiftPressed && e.Button == MouseButton.Right);

        /// <summary>
        /// Handle a blueprint requesting selection.
        /// </summary>
        /// <param name="blueprint">The blueprint.</param>
        /// <param name="e">The mouse event responsible for selection.</param>
        /// <returns>Whether an action was performed.</returns>
        internal virtual bool MouseDownSelectionRequested(SelectionBlueprint<T> blueprint, MouseButtonEvent e)
        {
            if (ShouldQuickDelete(e))
            {
                handleQuickDeletion(blueprint);
                return true;
            }

            // while holding control, we only want to add to selection, not replace an existing selection.
            if (e.ControlPressed && e.Button == MouseButton.Left && !blueprint.IsSelected)
            {
                blueprint.ToggleSelection();
                return true;
            }

            return ensureSelected(blueprint);
        }

        /// <summary>
        /// Handle a blueprint requesting deselection.
        /// </summary>
        /// <param name="blueprint">The blueprint.</param>
        /// <param name="e">The mouse event responsible for deselection.</param>
        /// <returns>Whether a deselection was performed.</returns>
        internal bool MouseUpSelectionRequested(SelectionBlueprint<T> blueprint, MouseButtonEvent e)
        {
            if (blueprint.IsSelected)
            {
                blueprint.ToggleSelection();
                return true;
            }

            return false;
        }

        private void handleQuickDeletion(SelectionBlueprint<T> blueprint)
        {
            if (blueprint.HandleQuickDeletion())
                return;

            if (!blueprint.IsSelected)
                DeleteItems(new[] { blueprint.Item });
            else
                DeleteSelected();
        }

        /// <summary>
        /// Given a selection target and a function of truth, retrieve the correct ternary state for display.
        /// </summary>
        public static TernaryState GetStateFromSelection<TObject>(IEnumerable<TObject> selection, Func<TObject, bool> func)
        {
            if (selection.Any(func))
                return selection.All(func) ? TernaryState.True : TernaryState.Indeterminate;

            return TernaryState.False;
        }

        /// <summary>
        /// Called whenever the deletion of items has been requested.
        /// </summary>
        /// <param name="items">The items to be deleted.</param>
        protected abstract void DeleteItems(IEnumerable<T> items);

        /// <summary>
        /// Ensure the blueprint is in a selected state.
        /// </summary>
        /// <param name="blueprint">The blueprint to select.</param>
        /// <returns>Whether selection state was changed.</returns>
        private bool ensureSelected(SelectionBlueprint<T> blueprint)
        {
            if (blueprint.IsSelected)
                return false;

            DeselectAll();
            blueprint.Select();
            return true;
        }

        protected void DeleteSelected()
        {
            DeleteItems(SelectedItems.ToArray());
            DeselectAll();
        }

        #endregion

        #region Outline Display

        /// <summary>
        /// Updates whether this <see cref="SelectionHandler{T}"/> is visible.
        /// </summary>
        private void updateVisibility()
        {
            int count = SelectedItems.Count;

            SelectionBox.Text = count > 0 ? count.ToString() : string.Empty;
            SelectionBox.FadeTo(count > 0 ? 1 : 0);

            OnSelectionChanged();
        }

        /// <summary>
        /// Triggered whenever the set of selected items changes.
        /// Should update the selection box's state to match supported operations.
        /// </summary>
        protected virtual void OnSelectionChanged()
        {
        }

        protected override void Update()
        {
            base.Update();

            if (selectedBlueprints.Count == 0)
                return;

            // Move the rectangle to cover the items
            RectangleF selectionRect = ToLocalSpace(selectedBlueprints[0].SelectionQuad).AABBFloat;

            for (int i = 1; i < selectedBlueprints.Count; i++)
                selectionRect = RectangleF.Union(selectionRect, ToLocalSpace(selectedBlueprints[i].SelectionQuad).AABBFloat);

            selectionRect = selectionRect.Inflate(INFLATE_SIZE);

            SelectionBox.Position = selectionRect.Location;
            SelectionBox.Size = selectionRect.Size;
        }

        #endregion

        #region Context Menu

        public MenuItem[] ContextMenuItems
        {
            get
            {
                if (!SelectedBlueprints.Any(b => b.IsHovered))
                    return Array.Empty<MenuItem>();

                var items = new List<MenuItem>();

                items.AddRange(GetContextMenuItemsForSelection(SelectedBlueprints));

                if (SelectedBlueprints.Count == 1)
                    items.AddRange(SelectedBlueprints[0].ContextMenuItems);

                items.Add(new GarbusMenuItem("Delete", MenuItemType.Destructive, DeleteSelected));

                return items.ToArray();
            }
        }

        /// <summary>
        /// Provide context menu items relevant to current selection. Calling base is not required.
        /// </summary>
        /// <param name="selection">The current selection.</param>
        /// <returns>The relevant menu items.</returns>
        protected virtual IEnumerable<MenuItem> GetContextMenuItemsForSelection(IEnumerable<SelectionBlueprint<T>> selection)
            => Enumerable.Empty<MenuItem>();

        #endregion
    }
}
