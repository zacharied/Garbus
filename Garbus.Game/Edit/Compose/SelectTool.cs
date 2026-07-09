// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/Tools/SelectTool.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; OsuIcon dependency removed (icon deferred
// to Task 13 when OsuIcon equivalents are available); CreatePlacementBlueprint returns null.

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A tool for selecting and moving existing hit objects (no placement).
    /// </summary>
    public class SelectTool : CompositionTool
    {
        public SelectTool()
            : base("Select")
        {
        }

        public override PlacementBlueprint? CreatePlacementBlueprint() => null;
    }
}
