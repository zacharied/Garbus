// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/ControlPointList.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: stripped to timing-only (one control point type); rebuilt UI on Basic* widgets;
// no OverlayColourProvider; object-shifting on timing change does NOT apply.

using System;
using System.Linq;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Left panel of the Timing tab: lists all timing control points with offset/BPM/signature columns.
    /// Selecting a row seeks the editor clock to the point's time.
    /// </summary>
    public partial class TimingPointList : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        /// <summary>The currently selected timing control point group (shared with settings panel).</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        private FillFlowContainer<TimingPointRow> rowContainer = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Bottom = 40 },
                    Child = rowContainer = new FillFlowContainer<TimingPointRow>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Children = new Drawable[]
                    {
                        new BasicButton
                        {
                            Text = "Add",
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Action = addAtPlayhead,
                        },
                        new BasicButton
                        {
                            Text = "Delete",
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Action = deleteSelected,
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            editorChart.ControlPointInfo.ControlPointsChanged += scheduleRefresh;
            refreshRows();
        }

        private void scheduleRefresh() => Scheduler.AddOnce(refreshRows);

        private void refreshRows()
        {
            rowContainer.Clear();

            foreach (var group in editorChart.ControlPointInfo.Groups)
            {
                var tp = group.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();
                if (tp == null) continue;

                var row = new TimingPointRow(group, tp)
                {
                    IsSelected = { BindTarget = SelectedGroup },
                    Action = g =>
                    {
                        SelectedGroup.Value = g;
                        editorClock.Seek(g.Time);
                    },
                };
                rowContainer.Add(row);
            }

            // Reselect if the previously selected group still exists.
            if (SelectedGroup.Value != null)
            {
                var stillExists = editorChart.ControlPointInfo.Groups
                    .FirstOrDefault(g => Math.Abs(g.Time - SelectedGroup.Value.Time) < 1);
                SelectedGroup.Value = stillExists;
            }

            // Auto-select first if nothing is selected.
            if (SelectedGroup.Value == null && editorChart.ControlPointInfo.Groups.Count > 0)
            {
                var firstGroup = editorChart.ControlPointInfo.Groups[0];
                SelectedGroup.Value = firstGroup;
            }
        }

        private void addAtPlayhead()
        {
            double time = editorChart.ControlPointInfo.GetClosestSnappedTime(editorClock.CurrentTime);

            // Copy BeatLength from the active point at that time (or use default 500ms = 120 BPM).
            var prevPoint = editorChart.ControlPointInfo.TimingPointAt(time);
            double beatLength = editorChart.ControlPointInfo.TimingPoints.Count > 0
                ? prevPoint.BeatLength
                : 500;

            changeHandler.BeginChange();

            var newPoint = new TimingControlPoint
            {
                BeatLength = beatLength,
            };

            editorChart.ControlPointInfo.Add(time, newPoint);
            editorChart.SaveState();

            changeHandler.EndChange();

            // Select the newly added group.
            var addedGroup = editorChart.ControlPointInfo.GroupAt(time);
            SelectedGroup.Value = addedGroup;
        }

        private void deleteSelected()
        {
            if (SelectedGroup.Value == null)
                return;

            // Don't delete the only timing point.
            if (editorChart.ControlPointInfo.TimingPoints.Count <= 1)
                return;

            changeHandler.BeginChange();
            editorChart.ControlPointInfo.RemoveGroup(SelectedGroup.Value);
            editorChart.SaveState();
            changeHandler.EndChange();

            SelectedGroup.Value = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorChart != null)
                editorChart.ControlPointInfo.ControlPointsChanged -= scheduleRefresh;
        }
    }

    /// <summary>
    /// A single row in the timing point list.
    /// </summary>
    public partial class TimingPointRow : BasicButton
    {
        private readonly ControlPointGroup group;
        private readonly TimingControlPoint timingPoint;

        /// <summary>Bindable to the parent list's SelectedGroup — drives visual selection state.</summary>
        public readonly Bindable<ControlPointGroup?> IsSelected = new Bindable<ControlPointGroup?>();

        public new Action<ControlPointGroup>? Action;

        public TimingPointRow(ControlPointGroup group, TimingControlPoint timingPoint)
        {
            this.group = group;
            this.timingPoint = timingPoint;

            RelativeSizeAxes = Axes.X;
            Height = 32;
            Text = formatRow();

            base.Action = () => Action?.Invoke(group);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            IsSelected.BindValueChanged(e =>
            {
                bool selected = e.NewValue != null && Math.Abs(e.NewValue.Time - group.Time) < 1;
                Alpha = selected ? 1f : 0.7f;
            }, true);

            timingPoint.BeatLengthBindable.BindValueChanged(_ => Text = formatRow());
            timingPoint.TimeSignatureBindable.BindValueChanged(_ => Text = formatRow());
        }

        private string formatRow()
        {
            double bpm = Math.Round(60000 / timingPoint.BeatLength, 2);
            return $"{group.Time:0}ms  {bpm:0.##} BPM  {timingPoint.TimeSignature.Numerator}/4";
        }
    }
}
