// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/UI/IPooledHitObjectProvider.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespaces only.

using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;

namespace Garbus.Game.Gameplay.UI
{
    internal interface IPooledHitObjectProvider
    {
        /// <summary>
        /// Attempts to retrieve the poolable <see cref="DrawableHitObject"/> representation of a <see cref="HitObject"/>.
        /// </summary>
        /// <param name="hitObject">The <see cref="HitObject"/> to retrieve the <see cref="DrawableHitObject"/> representation of.</param>
        /// <param name="parent">The parenting <see cref="DrawableHitObject"/>, if any.</param>
        /// <returns>The <see cref="DrawableHitObject"/> representing <see cref="HitObject"/>, or <c>null</c> if no poolable representation exists.</returns>
        DrawableHitObject? GetPooledDrawableRepresentation(HitObject hitObject, DrawableHitObject? parent);
    }
}
