// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/DiscreteAdjustmentControl.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: reduced to a single BasicButton with hold-to-repeat (no increment-level grid,
// no sample feedback); the hold is wrapped in one change-handler transaction so a long press is a
// single undo step.

using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// A nudge button that repeats its Action while held.
    /// The entire hold is recorded as one undo step.
    /// </summary>
    public partial class RepeatNudgeButton : BasicButton
    {
        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        public RepeatNudgeButton(string text)
        {
            Text = text;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // The behaviour swallows mouse-down and fires TriggerClick immediately, then repeats while
            // held — clicks reach Action exactly once per fire (the real click event stops at the
            // behaviour, which handled the mouse-down).
            AddInternal(new RepeatingButtonBehaviour(this)
            {
                RepeatBegan = () => changeHandler.BeginChange(),
                RepeatEnded = () => changeHandler.EndChange(),
            });
        }
    }
}
