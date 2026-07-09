// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/Tools/CompositionTool.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; PlacementBlueprint is local;
// Drawable icon kept; TooltipText kept.

using osu.Framework.Graphics;
using osu.Framework.Localisation;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A tool that can be selected in the editor toolbox to place and edit hit objects.
    /// </summary>
    public abstract class CompositionTool
    {
        public readonly string Name;

        public LocalisableString TooltipText { get; init; }

        protected CompositionTool(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Creates a <see cref="PlacementBlueprint"/> for this tool.
        /// Returns <c>null</c> for tools that do not place objects (e.g. <see cref="SelectTool"/>).
        /// </summary>
        public abstract PlacementBlueprint? CreatePlacementBlueprint();

        public virtual Drawable? CreateIcon() => null;

        public override string ToString() => Name;
    }
}
