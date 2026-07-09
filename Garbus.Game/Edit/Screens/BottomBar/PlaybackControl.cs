// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Components/PlaybackControl.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: no OsuColour/OverlayColourProvider/IconButton/OsuTabControl — uses
// BasicButton and BasicTabControl; drives EditorClock.AudioAdjustments.Tempo via a
// BindableDouble added as an adjustment (same pattern as osu's PlaybackControl).

using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.BottomBar
{
    /// <summary>
    /// Play/pause button and playback speed selector (0.25 / 0.5 / 0.75 / 1.0).
    /// Speed changes are applied to <see cref="EditorClock.AudioAdjustments"/> as a Tempo adjustment.
    /// </summary>
    public partial class PlaybackControl : CompositeDrawable
    {
        private static readonly double[] speeds = { 0.25, 0.5, 0.75, 1.0 };

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private BasicButton playPauseButton = null!;

        /// <summary>The bindable tempo multiplier applied to <see cref="EditorClock.AudioAdjustments"/>.</summary>
        private readonly BindableDouble tempoAdjust = new BindableDouble(1.0)
        {
            MinValue = 0.01,
            MaxValue = 2.0,
        };

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            var speedControl = new BasicTabControl<double>
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreRight,
                RelativeSizeAxes = Axes.Y,
                Width = 140,
                Current = tempoAdjust,
            };

            foreach (double speed in speeds)
                speedControl.AddItem(speed);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(20, 24, 32, 255),
                },
                playPauseButton = new BasicButton
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Width = 70,
                    Height = 36,
                    Text = "Play",
                    Action = togglePause,
                },
                speedControl,
            };

            // Register the tempo adjustment with the editor clock.
            editorClock.AudioAdjustments.AddAdjustment(AdjustableProperty.Tempo, tempoAdjust);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Keep the current value in sync after the tab control initialises (it may have
            // reset to the first item).
            tempoAdjust.BindValueChanged(e =>
            {
                // Clamp to valid speed list; if the tab control selects an invalid value, reset.
                bool valid = false;
                foreach (double s in speeds)
                    if (Math.Abs(e.NewValue - s) < 0.001) { valid = true; break; }
                if (!valid) tempoAdjust.Value = 1.0;
            });
        }

        protected override void Update()
        {
            base.Update();
            playPauseButton.Text = editorClock.IsRunning ? "Pause" : "Play";
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // Remove the tempo adjustment when disposed to avoid dangling adjustments.
            // editorClock may be null if we were never fully loaded.
            if (editorClock != null)
                editorClock.AudioAdjustments.RemoveAdjustment(AdjustableProperty.Tempo, tempoAdjust);
        }

        private void togglePause()
        {
            if (editorClock.IsRunning)
                editorClock.Stop();
            else
                editorClock.Start();
        }
    }
}
