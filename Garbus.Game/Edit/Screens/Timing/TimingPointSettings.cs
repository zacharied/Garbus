// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/TimingSection.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: rebuilt UI on Basic* widgets; no osu.Game.Overlays; no object-shifting on
// timing change; offset and BPM text boxes + nudge buttons + time-signature dropdown.

using System;
using System.Globalization;
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
    /// Right-panel settings area for the selected timing control point: offset textbox + nudge,
    /// BPM textbox + nudge, time-signature dropdown.
    /// </summary>
    public partial class TimingPointSettings : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        /// <summary>Bind this to TimingPointList.SelectedGroup.</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        private BasicTextBox offsetTextBox = null!;
        private BasicTextBox bpmTextBox = null!;
        private BasicDropdown<int> signatureDropdown = null!;

        private bool updatingFromModel;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;
            Padding = new MarginPadding(12);

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    // --- Offset ---
                    new SpriteText { Text = "Offset (ms)" },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 34),
                            new Dimension(GridSizeMode.Absolute, 34),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                offsetTextBox = new BasicTextBox
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    PlaceholderText = "0",
                                },
                                new NudgeButton("-1")
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Action = () => nudgeOffset(-1),
                                },
                                new NudgeButton("+1")
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Action = () => nudgeOffset(+1),
                                },
                            }
                        },
                    },

                    // --- BPM ---
                    new SpriteText { Text = "BPM" },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 34),
                            new Dimension(GridSizeMode.Absolute, 34),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                bpmTextBox = new BasicTextBox
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    PlaceholderText = "120",
                                },
                                new NudgeButton("-1")
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Action = () => nudgeBpm(-1),
                                },
                                new NudgeButton("+1")
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Action = () => nudgeBpm(+1),
                                },
                            }
                        },
                    },

                    // --- Time Signature ---
                    new SpriteText { Text = "Time Signature (x/4)" },
                    signatureDropdown = new BasicDropdown<int>
                    {
                        RelativeSizeAxes = Axes.X,
                        Items = new[] { 1, 2, 3, 4, 5, 6, 7 },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedGroup.BindValueChanged(_ => updateFromModel(), true);

            offsetTextBox.OnCommit += (_, _) => commitOffset();
            bpmTextBox.OnCommit += (_, _) => commitBpm();
            signatureDropdown.Current.BindValueChanged(e =>
            {
                if (!updatingFromModel)
                    commitSignature(e.NewValue);
            });
        }

        private void updateFromModel()
        {
            var tp = currentTimingPoint;
            bool hasPoint = tp != null;

            offsetTextBox.ReadOnly = !hasPoint;
            bpmTextBox.ReadOnly = !hasPoint;
            signatureDropdown.Current.Disabled = !hasPoint;

            if (!hasPoint) return;

            updatingFromModel = true;

            offsetTextBox.Text = SelectedGroup!.Value!.Time.ToString("0", CultureInfo.InvariantCulture);
            bpmTextBox.Text = (60000.0 / tp!.BeatLength).ToString("0.##", CultureInfo.InvariantCulture);
            signatureDropdown.Current.Value = tp.TimeSignature.Numerator;

            updatingFromModel = false;
        }

        private void commitOffset()
        {
            if (updatingFromModel || SelectedGroup.Value == null) return;

            if (!double.TryParse(offsetTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newOffset))
            {
                updateFromModel();
                return;
            }

            var currentItems = SelectedGroup.Value.ControlPoints.ToArray();
            double oldTime = SelectedGroup.Value.Time;

            if (Math.Abs(newOffset - oldTime) < 0.01) return;

            changeHandler.BeginChange();

            editorChart.ControlPointInfo.RemoveGroup(SelectedGroup.Value);

            foreach (var cp in currentItems)
                editorChart.ControlPointInfo.Add(newOffset, cp);

            editorChart.SaveState();
            changeHandler.EndChange();

            // Re-select the moved group.
            var movedGroup = editorChart.ControlPointInfo.GroupAt(newOffset);
            SelectedGroup.Value = movedGroup;

            if (!editorClock.IsRunning)
                editorClock.Seek(newOffset);
        }

        private void commitBpm()
        {
            if (updatingFromModel) return;

            var tp = currentTimingPoint;
            if (tp == null) return;

            if (!double.TryParse(bpmTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm)
                || bpm <= 0)
            {
                updateFromModel();
                return;
            }

            double newBeatLength = 60000.0 / bpm;

            changeHandler.BeginChange();
            tp.BeatLength = newBeatLength;
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private void commitSignature(int numerator)
        {
            var tp = currentTimingPoint;
            if (tp == null) return;

            changeHandler.BeginChange();
            tp.TimeSignature = new TimeSignature(numerator);
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private void nudgeOffset(int direction)
        {
            if (SelectedGroup.Value == null) return;

            offsetTextBox.Text = (SelectedGroup.Value.Time + direction).ToString("0", CultureInfo.InvariantCulture);
            commitOffset();
        }

        private void nudgeBpm(int direction)
        {
            var tp = currentTimingPoint;
            if (tp == null) return;

            double currentBpm = 60000.0 / tp.BeatLength;
            bpmTextBox.Text = (currentBpm + direction).ToString("0.##", CultureInfo.InvariantCulture);
            commitBpm();
        }

        /// <summary>
        /// Test seam: sets the BPM textbox text and immediately commits it.
        /// Equivalent to the user typing in the BPM box and pressing Enter.
        /// </summary>
        public void SetBpmAndCommit(double bpm)
        {
            bpmTextBox.Text = bpm.ToString("0.##", CultureInfo.InvariantCulture);
            commitBpm();
        }

        private TimingControlPoint? currentTimingPoint =>
            SelectedGroup.Value?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();

        private partial class NudgeButton : BasicButton
        {
            public NudgeButton(string text)
            {
                Text = text;
            }
        }
    }
}
