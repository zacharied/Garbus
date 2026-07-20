// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/TimingSection.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: rebuilt UI on Basic* widgets; no osu.Game.Overlays; object-shifting on timing
// changes via TimingSectionAdjustments behind a session toggle; offset and BPM text boxes +
// time-signature numerator textbox + omit-first-barline + use-current-time (the repeat nudge
// steppers live under the tap-timing metronome instead).

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
    /// Right-panel settings area for the selected timing control point: offset textbox +
    /// use-current-time, BPM textbox, time-signature numerator textbox, omit-first-barline toggle,
    /// and the move-objects-with-timing-changes toggle. The repeat nudge steppers live under the
    /// tap-timing metronome (<see cref="TapTimingControl"/>).
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

        /// <summary>
        /// Whether timing edits shift/rescale the objects in the affected timing section
        /// (osu's "adjust existing objects on timing changes"). Session-scoped, default on.
        /// </summary>
        public readonly BindableBool AdjustObjectsOnTimingChange = new BindableBool(true);

        private BasicCheckbox moveObjectsCheckbox = null!;
        private BasicTextBox offsetTextBox = null!;
        private BasicTextBox bpmTextBox = null!;
        private BasicTextBox signatureNumeratorBox = null!;
        private BasicCheckbox omitBarLineCheckbox = null!;
        private BasicButton useCurrentTimeButton = null!;

        private bool updatingFromModel;

        // Bound copy of the SELECTED point's beat length, re-pointed on selection change. Kept as a
        // field so disposal auto-unbinds it. Keeps the BPM textbox live when the point is changed
        // from outside this panel (tap button, tap-timing adjust buttons).
        private IBindable<double>? selectedBeatLength;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding(12);

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    moveObjectsCheckbox = new BasicCheckbox
                    {
                        LabelText = "Move objects with timing changes",
                        Current = { BindTarget = AdjustObjectsOnTimingChange },
                    },

                    // --- Offset ---
                    new SpriteText { Text = "Offset (ms)" },
                    offsetTextBox = new BasicTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        PlaceholderText = "0",
                        CommitOnFocusLost = true,
                    },

                    useCurrentTimeButton = new BasicButton
                    {
                        Text = "Use current time",
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        Action = useCurrentTime,
                    },

                    // --- BPM ---
                    new SpriteText { Text = "BPM" },
                    bpmTextBox = new BasicTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        PlaceholderText = "120",
                        CommitOnFocusLost = true,
                    },

                    // --- Time Signature ---
                    new SpriteText { Text = "Time Signature" },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5, 0),
                        Children = new Drawable[]
                        {
                            signatureNumeratorBox = new BasicTextBox
                            {
                                Width = 60,
                                RelativeSizeAxes = Axes.Y,
                                PlaceholderText = "4",
                                CommitOnFocusLost = true,
                            },
                            new SpriteText
                            {
                                Text = "/ 4",
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            },
                        },
                    },

                    // --- Omit first barline ---
                    omitBarLineCheckbox = new BasicCheckbox
                    {
                        LabelText = "Omit first barline",
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedGroup.BindValueChanged(_ =>
            {
                rebindBeatLength();
                updateFromModel();
            }, true);

            offsetTextBox.OnCommit += (_, _) => commitOffset();
            bpmTextBox.OnCommit += (_, _) => commitBpm();
            signatureNumeratorBox.OnCommit += (_, _) => commitSignature();
            omitBarLineCheckbox.Current.BindValueChanged(e =>
            {
                if (!updatingFromModel)
                    commitOmitBarLine(e.NewValue);
            });
        }

        private void rebindBeatLength()
        {
            selectedBeatLength?.UnbindAll();
            selectedBeatLength = null;

            var tp = currentTimingPoint;
            if (tp == null) return;

            var boundCopy = tp.BeatLengthBindable.GetBoundCopy();
            selectedBeatLength = boundCopy;

            boundCopy.BindValueChanged(e =>
            {
                // Never clobber text the user is editing.
                if (updatingFromModel || bpmTextBox.HasFocus) return;

                updatingFromModel = true;
                bpmTextBox.Text = (60000.0 / e.NewValue).ToString("0.##", CultureInfo.InvariantCulture);
                updatingFromModel = false;
            });
        }

        private void updateFromModel()
        {
            var tp = currentTimingPoint;
            bool hasPoint = tp != null;

            offsetTextBox.ReadOnly = !hasPoint;
            bpmTextBox.ReadOnly = !hasPoint;
            signatureNumeratorBox.ReadOnly = !hasPoint;
            omitBarLineCheckbox.Current.Disabled = false;
            useCurrentTimeButton.Enabled.Value = hasPoint;

            if (!hasPoint)
            {
                omitBarLineCheckbox.Current.Disabled = true;
                return;
            }

            updatingFromModel = true;

            offsetTextBox.Text = SelectedGroup!.Value!.Time.ToString("0", CultureInfo.InvariantCulture);
            bpmTextBox.Text = (60000.0 / tp!.BeatLength).ToString("0.##", CultureInfo.InvariantCulture);
            signatureNumeratorBox.Text = tp.TimeSignature.Numerator.ToString(CultureInfo.InvariantCulture);
            omitBarLineCheckbox.Current.Value = tp.OmitFirstBarLine;

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

            double oldTime = SelectedGroup.Value.Time;
            if (Math.Abs(newOffset - oldTime) < 0.01) return;

            SelectedGroup.Value = TimingPointChanges.MoveGroup(
                editorChart, changeHandler, SelectedGroup.Value, newOffset, AdjustObjectsOnTimingChange.Value);

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

            TimingPointChanges.ChangeBpm(editorChart, changeHandler, tp, bpm, AdjustObjectsOnTimingChange.Value);
        }

        private void commitSignature()
        {
            if (updatingFromModel) return;

            var tp = currentTimingPoint;
            if (tp == null) return;

            if (!int.TryParse(signatureNumeratorBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numerator) || numerator <= 0)
            {
                // Restore the textbox to the current valid value.
                updateFromModel();
                return;
            }

            if (numerator == tp.TimeSignature.Numerator) return;

            changeHandler.BeginChange();
            tp.TimeSignature = new TimeSignature(numerator);
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private void useCurrentTime()
        {
            if (SelectedGroup.Value == null) return;

            offsetTextBox.Text = editorClock.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
            commitOffset();
        }

        /// <summary>
        /// Test seam: sets the offset textbox text and immediately commits it.
        /// Equivalent to the user typing in the offset box and pressing Enter.
        /// </summary>
        public void SetOffsetAndCommit(double offset)
        {
            offsetTextBox.Text = offset.ToString("0.##", CultureInfo.InvariantCulture);
            commitOffset();
        }

        /// <summary>Test seam: the BPM textbox's current text.</summary>
        public string BpmTextForTests => bpmTextBox.Text;

        /// <summary>
        /// Test seam: sets the BPM textbox text and immediately commits it.
        /// Equivalent to the user typing in the BPM box and pressing Enter.
        /// </summary>
        public void SetBpmAndCommit(double bpm)
        {
            bpmTextBox.Text = bpm.ToString("0.##", CultureInfo.InvariantCulture);
            commitBpm();
        }

        /// <summary>
        /// Test seam: sets the time-signature numerator textbox text and immediately commits it.
        /// Equivalent to the user typing in the numerator box and pressing Enter.
        /// </summary>
        public void SetSignatureAndCommit(string text)
        {
            signatureNumeratorBox.Text = text;
            commitSignature();
        }

        private void commitOmitBarLine(bool omit)
        {
            var tp = currentTimingPoint;
            if (tp == null || tp.OmitFirstBarLine == omit) return;

            changeHandler.BeginChange();
            tp.OmitFirstBarLine = omit;
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private TimingControlPoint? currentTimingPoint =>
            SelectedGroup.Value?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();

    }
}
