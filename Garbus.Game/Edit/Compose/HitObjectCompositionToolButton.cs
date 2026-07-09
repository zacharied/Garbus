// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/HitObjectCompositionToolButton.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; CompositionTool is local; "add a timing point"
// disabled-state tooltip kept (Garbus may disable tools until timing exists, as osu does).

using System;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A <see cref="RadioButton"/> that carries the <see cref="CompositionTool"/> it selects.
    /// </summary>
    public class HitObjectCompositionToolButton : RadioButton
    {
        public CompositionTool Tool { get; }

        public HitObjectCompositionToolButton(CompositionTool tool, Action? action)
            : base(tool.Name, action, tool.CreateIcon)
        {
            Tool = tool;

            Selected.BindDisabledChanged(isDisabled =>
            {
                TooltipText = isDisabled ? "Add at least one timing point first!" : Tool.TooltipText;
            }, true);
        }
    }
}
