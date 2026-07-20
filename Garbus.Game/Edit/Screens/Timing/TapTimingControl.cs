// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/TapTimingControl.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: rebuilt on Basic* widgets; no osu.Game.Graphics/OverlayColourProvider;
// waveform comparison uses the Garbus beat-grid display instead of WaveformGraph;
// offset/BPM adjust rows under the metronome (repeat-on-hold).

using System.Linq;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
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

        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorSong editorSong { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        /// <summary>Bind to the list's SelectedGroup.</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        /// <summary>
        /// Bound to TimingPointSettings.AdjustObjectsOnTimingChange by TimingTab so both panels
        /// honour the same "Move objects with timing changes" toggle.
        /// </summary>
        public readonly BindableBool AdjustObjectsOnTimingChange = new BindableBool(true);

        private MetronomeDisplay metronome = null!;
        private WaveformComparisonDisplay waveform = null!;
        private TapButton tapButton = null!;
        private BasicCheckbox clickCheckbox = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

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
                        Height = 200,
                        Children = new Drawable[]
                        {
                            metronome = new MetronomeDisplay
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            },
                            clickCheckbox = new BasicCheckbox
                            {
                                Anchor = Anchor.BottomLeft,
                                Origin = Anchor.BottomLeft,
                                Position = new Vector2(8, -4),
                                LabelText = "Metronome",
                                Current = { BindTarget = metronome.EnableClicking },
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

                    // Fine adjustment of the selected point, osu's TapTimingControl extras:
                    // offset ±1/±10 ms and BPM ±0.1/±1, repeat-on-hold.
                    new SpriteText { Text = "Offset (ms)" },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new RepeatNudgeButton("-10") { Name = "tap-offset-minus10", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(-10) },
                                new RepeatNudgeButton("-1") { Name = "tap-offset-minus1", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(-1) },
                                new RepeatNudgeButton("+1") { Name = "tap-offset-plus1", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(+1) },
                                new RepeatNudgeButton("+10") { Name = "tap-offset-plus10", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(+10) },
                            }
                        },
                    },
                    new SpriteText { Text = "BPM" },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new RepeatNudgeButton("-1") { Name = "tap-bpm-minus1", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(-1) },
                                new RepeatNudgeButton("-.1") { Name = "tap-bpm-minus01", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(-0.1) },
                                new RepeatNudgeButton("+.1") { Name = "tap-bpm-plus01", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(+0.1) },
                                new RepeatNudgeButton("+1") { Name = "tap-bpm-plus1", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(+1) },
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

        private void adjustOffset(double amount)
        {
            if (SelectedGroup.Value == null) return;

            double newTime = SelectedGroup.Value.Time + amount;

            SelectedGroup.Value = TimingPointChanges.MoveGroup(
                editorSong, editorChart, changeHandler, SelectedGroup.Value, newTime, AdjustObjectsOnTimingChange.Value);

            if (!editorClock.IsRunning)
                editorClock.Seek(newTime);
        }

        private void adjustBpm(double amount)
        {
            var tp = SelectedGroup.Value?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();
            if (tp == null) return;

            TimingPointChanges.ChangeBpm(editorSong, editorChart, changeHandler, tp, tp.BPM + amount, AdjustObjectsOnTimingChange.Value);
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
