// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/RepeatingButtonBehaviour.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace changed to Garbus.Game.Edit.Screens.Timing; osu.Game audio sample
// removed (notch-tick not available in Garbus resources — RepeatBegan/RepeatEnded callbacks remain
// so callers can react, but no audio plays on repeat).

using System;
using osu.Framework.Graphics;
using osu.Framework.Input.Events;
using osu.Framework.Threading;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Represents a component that provides the behaviour of triggering button clicks repeatedly
    /// while holding with mouse.
    /// </summary>
    public partial class RepeatingButtonBehaviour : Component
    {
        private const double initial_delay = 300;
        private const double minimum_delay = 80;

        private readonly Drawable button;

        public Action? RepeatBegan;
        public Action? RepeatEnded;

        public RepeatingButtonBehaviour(Drawable button)
        {
            this.button = button;
            RelativeSizeAxes = Axes.Both;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            RepeatBegan?.Invoke();
            beginRepeat();
            return true;
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            adjustDelegate?.Cancel();
            RepeatEnded?.Invoke();
            base.OnMouseUp(e);
        }

        private ScheduledDelegate? adjustDelegate;
        private double adjustDelay = initial_delay;

        private void beginRepeat()
        {
            adjustDelegate?.Cancel();

            adjustDelay = initial_delay;
            adjustNext();

            void adjustNext()
            {
                if (IsHovered)
                {
                    button.TriggerClick();
                    adjustDelay = Math.Max(minimum_delay, adjustDelay * 0.9f);
                }
                else
                {
                    adjustDelay = initial_delay;
                }

                adjustDelegate = Scheduler.AddDelayed(adjustNext, adjustDelay);
            }
        }
    }
}
