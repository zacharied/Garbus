// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/UI/IHitObjectContainer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespaces only.

using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects.Drawables;

namespace Garbus.Game.Gameplay.UI
{
    public interface IHitObjectContainer
    {
        /// <summary>
        /// All currently in-use <see cref="DrawableHitObject"/>s.
        /// </summary>
        IEnumerable<DrawableHitObject> Objects { get; }

        /// <summary>
        /// All currently in-use <see cref="DrawableHitObject"/>s that are alive.
        /// </summary>
        /// <remarks>
        /// If this <see cref="IHitObjectContainer"/> uses pooled objects, this is equivalent to <see cref="Objects"/>.
        /// </remarks>
        IEnumerable<DrawableHitObject> AliveObjects { get; }
    }
}
