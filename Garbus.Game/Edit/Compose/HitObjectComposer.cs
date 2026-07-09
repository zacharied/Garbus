// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/HitObjectComposer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: Task 12 minimal stub — exposes only the surface consumed by
// EditorBlueprintContainer and ComposeBlueprintContainer. Task 13 will flesh this out with
// toolbox, DrawableRuleset wiring, snapping, and radio-button tool selection.
// HitObjects, Playfield, CursorInPlacementArea, CurrentTool are the seam points.
// No Ruleset, IBeatSnapProvider, DrawableRuleset, or osu.Game.Rulesets types here.

using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// Minimal abstract base for the editor composer.
    /// Provides the surface consumed by <see cref="EditorBlueprintContainer"/> and
    /// <see cref="ComposeBlueprintContainer"/>. Fleshed out in Task 13.
    /// </summary>
    public abstract partial class HitObjectComposer : CompositeDrawable
    {
        /// <summary>
        /// All currently-displayed <see cref="DrawableHitObject"/>s in the composer's playfield.
        /// </summary>
        public abstract IEnumerable<DrawableHitObject> HitObjects { get; }

        /// <summary>
        /// The composer's primary playfield.
        /// </summary>
        public abstract Playfield Playfield { get; }

        /// <summary>
        /// Whether the cursor is currently within the composer's placement area.
        /// </summary>
        public abstract bool CursorInPlacementArea { get; }

        /// <summary>
        /// The currently-active composition tool.
        /// Task 13 will wire this to the radio-button toolbox.
        /// </summary>
        public abstract CompositionTool? CurrentTool { get; }
    }
}
