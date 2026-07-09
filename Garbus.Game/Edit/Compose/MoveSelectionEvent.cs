// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/MoveSelectionEvent.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose.

using osuTK;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// An event which occurs when a <see cref="SelectionBlueprint{T}"/> is moved.
    /// </summary>
    public class MoveSelectionEvent<T>
    {
        /// <summary>
        /// The <see cref="SelectionBlueprint{T}"/> that triggered this <see cref="MoveSelectionEvent{T}"/>.
        /// </summary>
        public readonly SelectionBlueprint<T> Blueprint;

        /// <summary>
        /// The screen-space delta of this move event.
        /// </summary>
        public readonly Vector2 ScreenSpaceDelta;

        public MoveSelectionEvent(SelectionBlueprint<T> blueprint, Vector2 screenSpaceDelta)
        {
            Blueprint = blueprint;
            ScreenSpaceDelta = screenSpaceDelta;
        }
    }
}
