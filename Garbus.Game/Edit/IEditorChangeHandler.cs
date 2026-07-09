// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/IEditorChangeHandler.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: removed [Cached] DI attribute and osu.Game.Rulesets.Objects.HitObject reference
// (Garbus uses GarbusHitObject); interface otherwise identical.

using System;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Interface for a component that tracks and manages state changes in the editor.
    /// </summary>
    public interface IEditorChangeHandler
    {
        /// <summary>Fired whenever a state change occurs.</summary>
        event Action? OnStateChange;

        /// <summary>
        /// Begins a bulk state change event. <see cref="EndChange"/> should be invoked soon after.
        /// </summary>
        void BeginChange();

        /// <summary>Ends a bulk state change event.</summary>
        void EndChange();

        /// <summary>
        /// Immediately saves the current state.
        /// This is a no-op if there is a change in progress via <see cref="BeginChange"/>.
        /// </summary>
        void SaveState();

        /// <summary>
        /// Restores an older or newer state.
        /// </summary>
        /// <param name="direction">
        /// Negative for undo (older), positive for redo (newer).
        /// </param>
        void RestoreState(int direction);
    }
}
