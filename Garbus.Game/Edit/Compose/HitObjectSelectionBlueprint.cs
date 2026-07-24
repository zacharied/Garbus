// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/HitObjectSelectionBlueprint.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; HitObject → GarbusHitObject;
// DrawableHitObject is Garbus.Game.Gameplay.Objects.Drawables.DrawableHitObject (set by the blueprint
// container in Task 12); ShowHitMarkers bindable and OsuConfigManager dependency stripped (no
// equivalent setting in Garbus); AlwaysShowWhenSelected kept; ShouldBeAlive delegates to
// DrawableObject alive state as in osu; the typed subclass uses T HitObject (not "new T Item" as the
// brief incorrectly states) — osu's real pattern, confirmed against osu source, and matches what
// GarbusSelectionBlueprint<T> actually consumes.

using osu.Framework.Graphics.Primitives;
using Garbus.Game.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    public abstract partial class HitObjectSelectionBlueprint : SelectionBlueprint<GarbusHitObject>
    {
        /// <summary>
        /// The <see cref="DrawableHitObject"/> which this blueprint applies to.
        /// Set by the blueprint container once the drawable is available from the playfield.
        /// </summary>
        public DrawableHitObject? DrawableObject { get; internal set; }

        /// <summary>
        /// Whether the blueprint should be shown even when the <see cref="DrawableObject"/> is not alive.
        /// </summary>
        protected virtual bool AlwaysShowWhenSelected => false;

        protected override bool ShouldBeAlive
            => (DrawableObject?.IsAlive == true && DrawableObject.IsPresent)
               || (AlwaysShowWhenSelected && State == SelectionState.Selected);

        protected HitObjectSelectionBlueprint(GarbusHitObject hitObject)
            : base(hitObject)
        {
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => DrawableObject?.ReceivePositionalInputAt(screenSpacePos) ?? false;

        public override Vector2 ScreenSpaceSelectionPoint
            => DrawableObject?.ScreenSpaceDrawQuad.Centre ?? ScreenSpaceDrawQuad.Centre;

        public override Quad SelectionQuad
            => DrawableObject?.ScreenSpaceDrawQuad ?? ScreenSpaceDrawQuad;
    }

    /// <summary>
    /// A typed <see cref="HitObjectSelectionBlueprint"/> that exposes the concrete hit object type.
    /// </summary>
    public abstract partial class HitObjectSelectionBlueprint<T> : HitObjectSelectionBlueprint
        where T : GarbusHitObject
    {
        /// <summary>
        /// The strongly-typed hit object this blueprint represents.
        /// </summary>
        public T HitObject => (T)Item;

        protected HitObjectSelectionBlueprint(T hitObject)
            : base(hitObject)
        {
        }
    }
}
