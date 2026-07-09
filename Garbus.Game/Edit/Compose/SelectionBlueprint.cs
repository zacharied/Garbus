// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/SelectionBlueprint.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; ScreenSpaceAdditionalNodes/ScreenSpaceSnapPoints
// stripped (composer snapping infrastructure added in Task 12); JetBrains annotations removed; nullable
// enabled. Task 11 re-added ContextMenuItems using GarbusMenuItem (no osu.Game dependency).

using System;
using System.Collections.Generic;
using osu.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A blueprint placed above a displayed item, adding editing functionality.
    /// </summary>
    public abstract partial class SelectionBlueprint<T> : CompositeDrawable, IStateful<SelectionState>
    {
        /// <summary>
        /// The item this blueprint represents.
        /// </summary>
        public readonly T Item;

        /// <summary>
        /// Invoked when this <see cref="SelectionBlueprint{T}"/> has been selected.
        /// </summary>
        public event Action<SelectionBlueprint<T>>? Selected;

        /// <summary>
        /// Invoked when this <see cref="SelectionBlueprint{T}"/> has been deselected.
        /// </summary>
        public event Action<SelectionBlueprint<T>>? Deselected;

        public override bool HandlePositionalInput => IsSelectable;
        public override bool RemoveWhenNotAlive => false;

        protected SelectionBlueprint(T item)
        {
            Item = item;

            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateState();
        }

        private SelectionState state;

        public event Action<SelectionState>? StateChanged;

        public SelectionState State
        {
            get => state;
            set
            {
                if (state == value)
                    return;

                state = value;

                if (IsLoaded)
                    updateState();

                StateChanged?.Invoke(state);
            }
        }

        private void updateState()
        {
            switch (state)
            {
                case SelectionState.Selected:
                    OnSelected();
                    Selected?.Invoke(this);
                    break;

                case SelectionState.NotSelected:
                    OnDeselected();
                    Deselected?.Invoke(this);
                    break;
            }
        }

        protected virtual void OnDeselected()
        {
            // Selection blueprints are AlwaysPresent while the related item is visible.
            // Set the body piece's alpha directly to avoid arbitrarily rendering frame buffers of children.
            foreach (var d in InternalChildren)
                d.Hide();
        }

        protected virtual void OnSelected()
        {
            foreach (var d in InternalChildren)
                d.Show();
        }

        // When not selected, input is only required for the blueprint itself to receive IsHovering.
        protected override bool ShouldBeConsideredForInput(Drawable child) => State == SelectionState.Selected;

        /// <summary>
        /// Selects this <see cref="SelectionBlueprint{T}"/>, causing it to become visible.
        /// </summary>
        public void Select() => State = SelectionState.Selected;

        /// <summary>
        /// Deselects this <see cref="SelectionBlueprint{T}"/>, causing it to become invisible.
        /// </summary>
        public void Deselect() => State = SelectionState.NotSelected;

        /// <summary>
        /// Toggles the selection state of this <see cref="SelectionBlueprint{T}"/>.
        /// </summary>
        public void ToggleSelection() => State = IsSelected ? SelectionState.NotSelected : SelectionState.Selected;

        /// <summary>
        /// Whether this blueprint is currently selected.
        /// </summary>
        public bool IsSelected => State == SelectionState.Selected;

        /// <summary>
        /// Whether the <see cref="SelectionBlueprint{T}"/> can currently be selected via a click or a drag box.
        /// </summary>
        public virtual bool IsSelectable => ShouldBeAlive && IsPresent;

        /// <summary>
        /// The screen-space main point that causes this blueprint to be selected via a drag.
        /// </summary>
        public virtual Vector2 ScreenSpaceSelectionPoint => ScreenSpaceDrawQuad.Centre;

        /// <summary>
        /// The screen-space quad that outlines this blueprint for selections.
        /// </summary>
        public virtual Quad SelectionQuad => ScreenSpaceDrawQuad;

        /// <summary>
        /// Handle to perform a partial deletion when the user requests a quick delete (Shift+Right Click).
        /// </summary>
        /// <returns>True if the deletion was handled by this blueprint. Returning false will delete the full object.</returns>
        public virtual bool HandleQuickDeletion() => false;

        /// <summary>
        /// Context menu items specific to this blueprint. Combined with <see cref="SelectionHandler{T}"/>'s
        /// items when only one blueprint is selected.
        /// </summary>
        public virtual IEnumerable<MenuItem> ContextMenuItems => Array.Empty<MenuItem>();
    }
}
