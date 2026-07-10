// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/TapButton.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: rebuilt UI on Basic*; osu.Game.Graphics / OverlayColourProvider removed;
// tap-BPM averaging algorithm kept (last 8 intervals after initial_taps_to_ignore leading taps);
// timestamps are injectable via RecordTap(double) for headless test coverage;
// object-shifting on BPM change does NOT apply.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Threading;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Tap-to-BPM button. Clicking or pressing records a timestamp; after enough taps the BPM
    /// is averaged and written to the selected timing point.
    /// Timestamps are injectable via <see cref="RecordTap"/> for headless test coverage.
    /// </summary>
    public partial class TapButton : BasicButton
    {
        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        /// <summary>The selected group to write BPM into.</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        /// <summary>Fires when the computed BPM is written to the timing point.</summary>
        public event Action<double>? BpmWritten;

        private const int initial_taps_to_ignore = 4;
        private const int max_taps_to_consider = 128;

        private readonly List<double> tapTimings = new List<double>();
        private SpriteText bpmFeedback = null!;
        private ScheduledDelegate? resetDelegate;

        public TapButton()
        {
            Text = "Tap";
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = 40;

            // Add BPM feedback overlay inside the button.
            Add(bpmFeedback = new SpriteText
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                Padding = new MarginPadding { Right = 8 },
                Alpha = 0,
            });

            Action = () => RecordTap(Clock.CurrentTime);
        }

        /// <summary>
        /// Records a tap at the given timestamp (milliseconds). Separated from mouse handling so
        /// tests can inject synthetic timestamps without a display clock.
        /// </summary>
        public void RecordTap(double timestamp)
        {
            resetDelegate?.Cancel();

            tapTimings.Add(timestamp);

            // Rolling window: keep only initial_taps_to_ignore + max_taps_to_consider entries.
            if (tapTimings.Count > initial_taps_to_ignore + max_taps_to_consider)
                tapTimings.RemoveAt(0);

            // Need at least 2 * initial_taps_to_ignore taps before averaging.
            if (tapTimings.Count < initial_taps_to_ignore * 2)
            {
                bpmFeedback.Text = new string('.', tapTimings.Count);
                bpmFeedback.Alpha = 1;
                return;
            }

            // Average the intervals after discarding the first initial_taps_to_ignore taps.
            double averageBeatLength =
                (tapTimings.Last() - tapTimings.Skip(initial_taps_to_ignore).First())
                / (tapTimings.Count - initial_taps_to_ignore - 1);

            double bpm = Math.Round(60000.0 / averageBeatLength);

            bpmFeedback.Text = $"{bpm} BPM";
            bpmFeedback.Alpha = 1;

            // Write to the selected timing point.
            var tp = SelectedGroup.Value?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();
            if (tp != null)
            {
                changeHandler.BeginChange();
                tp.BeatLength = 60000.0 / bpm;
                tp.BeatLengthBindable.TriggerChange(); // ensure ControlPointsChanged fires
                changeHandler.EndChange();

                BpmWritten?.Invoke(bpm);
            }

            // Reset after 2 seconds of inactivity.
            resetDelegate = Scheduler.AddDelayed(resetTaps, 2000);
        }

        private void resetTaps()
        {
            tapTimings.Clear();
            bpmFeedback.FadeOut(300, Easing.OutQuint);
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            // Bypass the BasicButton Action path — call RecordTap directly so timing is accurate.
            RecordTap(Clock.CurrentTime);
            return true;
        }
    }
}
