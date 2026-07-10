// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/TapTimingControl.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: rebuilt on Basic* widgets; no osu.Game.Graphics/OverlayColourProvider;
// waveform comparison uses the Garbus beat-grid display instead of WaveformGraph.
// Contains MetronomeDisplay, WaveformComparisonDisplay, and TapButton.

using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Right-column timing control: metronome + waveform comparison (top), tap-BPM button (bottom).
    /// </summary>
    public partial class TapTimingControl : CompositeDrawable
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        /// <summary>Bind to the list's SelectedGroup.</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        private MetronomeDisplay metronome = null!;
        private WaveformComparisonDisplay waveform = null!;
        private TapButton tapButton = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Padding = new MarginPadding(8),
                Children = new Drawable[]
                {
                    // Metronome + waveform row.
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 160,
                        Children = new Drawable[]
                        {
                            metronome = new MetronomeDisplay
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            },
                            waveform = new WaveformComparisonDisplay
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.Y,
                                Width = 0.65f,
                            },
                        },
                    },

                    // Playback controls.
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new PlaybackButton("Stop", () => { editorClock.Stop(); if (SelectedGroup.Value != null) editorClock.Seek(SelectedGroup.Value.Time); }),
                                new PlaybackButton("Play", () => { if (SelectedGroup.Value != null) editorClock.Seek(SelectedGroup.Value.Time); editorClock.Start(); }),
                            }
                        },
                    },

                    // Tap button.
                    tapButton = new TapButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 40,
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedGroup.BindValueChanged(e =>
            {
                metronome.SelectedGroup.Value = e.NewValue;
                waveform.SelectedGroup.Value = e.NewValue;
                tapButton.SelectedGroup.Value = e.NewValue;
            }, true);
        }

        private partial class PlaybackButton : osu.Framework.Graphics.UserInterface.BasicButton
        {
            public PlaybackButton(string label, System.Action action)
            {
                RelativeSizeAxes = Axes.Both;
                Text = label;
                Action = action;
            }
        }
    }
}
