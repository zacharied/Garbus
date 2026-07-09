// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Objects/SyntheticHitObjectEntry.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespaces only.

using Garbus.Game.Gameplay.Objects.Drawables;

namespace Garbus.Game.Gameplay.Objects
{
    /// <summary>
    /// Created for a <see cref="DrawableHitObject"/> when only <see cref="HitObject"/> is given
    /// to make sure a <see cref="DrawableHitObject"/> is always associated with a <see cref="HitObjectLifetimeEntry"/>.
    /// </summary>
    internal class SyntheticHitObjectEntry : HitObjectLifetimeEntry
    {
        public SyntheticHitObjectEntry(HitObject hitObject)
            : base(hitObject)
        {
        }
    }
}
