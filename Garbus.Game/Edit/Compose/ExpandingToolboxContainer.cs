// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/ExpandingToolboxContainer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: osu's ExpandingToolboxContainer derives from osu.Game's ExpandingContainer, which is
// entangled with OsuScrollContainer, the editor config (EditorContractSidebars), and hover-expand
// animations — none of which exist in Garbus. Rewritten fresh as a plain fixed-width vertical
// FillFlowContainer (the brief types LeftToolbox/RightToolbox as FillFlowContainer). The hover
// expand/contract polish is deferred; the toolbox is always at its full width.

using Garbus.Game.UI;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A fixed-width vertical column that hosts <see cref="EditorToolboxGroup"/>s down one side of the
    /// composer. Front-first so a dropdown menu spilling out of one group pops over the groups below.
    /// </summary>
    public partial class ExpandingToolboxContainer : FrontFirstFillFlowContainer
    {
        public ExpandingToolboxContainer(float width)
        {
            Width = width;
            RelativeSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 5);
            Padding = new MarginPadding { Vertical = 5, Horizontal = 5 };
        }
    }
}
