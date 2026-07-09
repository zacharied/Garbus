// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/PlacementBlueprint.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; IKeyBindingHandler<GlobalAction>/Back
// handling removed (Garbus has no GlobalAction equivalent — escape-to-cancel can be added in Task 15
// via a plain KeyDownEvent override); PlacementState nested enum kept here as in osu.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A blueprint which governs the placement of something.
    /// </summary>
    public abstract partial class PlacementBlueprint : VisibilityContainer
    {
        /// <summary>
        /// Whether the hit object is currently mid-placement, but has not necessarily finished being placed.
        /// </summary>
        public PlacementState PlacementActive { get; private set; }

        /// <summary>
        /// Whether this blueprint is currently in a state that can be committed.
        /// </summary>
        /// <remarks>
        /// Override this with any preconditions that should be double-checked on committing.
        /// If <c>false</c> is returned and a commit is attempted, the blueprint will be destroyed instead.
        /// </remarks>
        protected virtual bool IsValidForPlacement => true;

        // The blueprint should still be considered for input even if it is hidden,
        // especially when such input is the reason for making the blueprint become visible.
        public override bool PropagatePositionalInputSubTree => true;
        public override bool PropagateNonPositionalInputSubTree => true;

        protected PlacementBlueprint()
        {
            RelativeSizeAxes = Axes.Both;

            // The blueprint should still be considered for input even if it is hidden,
            // especially when such input is the reason for making the blueprint become visible.
            AlwaysPresent = true;
        }

        /// <summary>
        /// Signals that the placement has started.
        /// </summary>
        /// <param name="commitStart">Whether this call is committing a value and continuing with further adjustments.</param>
        protected virtual void BeginPlacement(bool commitStart = false)
        {
            if (commitStart)
                PlacementActive = PlacementState.Active;
        }

        /// <summary>
        /// Signals that the placement has finished.
        /// This will destroy this <see cref="PlacementBlueprint"/>, and commit the changes.
        /// </summary>
        /// <param name="commit">Whether the changes should be committed. Note that a commit may fail if <see cref="IsValidForPlacement"/> is <c>false</c>.</param>
        public virtual void EndPlacement(bool commit)
        {
            switch (PlacementActive)
            {
                case PlacementState.Finished:
                    return;

                case PlacementState.Waiting:
                    // Ensure placement was started before ending to make state handling simpler.
                    BeginPlacement();
                    break;
            }

            PlacementActive = PlacementState.Finished;
        }

        /// <summary>
        /// Updates the time and position of this <see cref="PlacementBlueprint"/>.
        /// </summary>
        public abstract SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime);

        protected override bool Handle(UIEvent e)
        {
            base.Handle(e);

            switch (e)
            {
                case ScrollEvent:
                    return false;

                case DoubleClickEvent:
                    return false;

                case MouseButtonEvent mouse:
                    // Placement blueprints should generally block mouse from reaching underlying
                    // components (ie. performing clicks on interface buttons).
                    return mouse.Button == MouseButton.Left || PlacementActive == PlacementState.Active;

                default:
                    return false;
            }
        }

        protected override void PopIn() => this.FadeIn();
        protected override void PopOut() => this.FadeOut();

        public enum PlacementState
        {
            Waiting,
            Active,
            Finished
        }
    }
}
