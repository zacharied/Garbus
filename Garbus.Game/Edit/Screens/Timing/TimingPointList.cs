// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/ControlPointList.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: stripped to timing-only (one control point type); rebuilt UI on Basic* widgets;
// no OverlayColourProvider; plain flow instead of VirtualisedListContainer (chart sizes don't need
// virtualisation) with osu's ControlPointTable layout (header row, fixed time column, attribute
// chips); object-shifting on timing change does NOT apply.

using System;
using System.Linq;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Left panel of the Timing tab: header row + one row per timing control point, each showing the
    /// point's time and attribute chips (BPM, time signature, no-barline).
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

        private const float header_height = 24;

        private FillFlowContainer<TimingPointRow> rowContainer = null!;
        private BasicButton addButton = null!;
        private BasicButton deleteButton = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Text = "Time",
                            X = 8,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                        new SpriteText
                        {
                            Text = "Attributes",
                            X = TimingPointRow.TIME_COLUMN_WIDTH,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                    },
                },
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = header_height, Bottom = 40 },
                    Child = rowContainer = new FillFlowContainer<TimingPointRow>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 1),
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
                        addButton = new BasicButton
                        {
                            Text = "Add",
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Action = addAtPlayhead,
                        },
                        deleteButton = new BasicButton
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

        protected override void Update()
        {
            base.Update();

            // Keep the buttons' enabled state honest so an impossible action reads as a greyed-out
            // button instead of a silent no-op (fresh chart: playhead parked at 0 on the initial
            // point made both buttons look dead — ISSUES.md).
            //
            // osu semantics: Add is "add or focus the group at the playhead" — it only greys out when
            // that group is already the selected one (selecting a row seeks onto the point, so a
            // plain "no group here" check would grey Add after every selection — ISSUES.md).
            double snapped = editorChart.ControlPointInfo.GetClosestSnappedTime(editorClock.CurrentTime);
            var groupAtPlayhead = editorChart.ControlPointInfo.GroupAt(snapped);
            addButton.Enabled.Value = groupAtPlayhead == null || SelectedGroup.Value != groupAtPlayhead;
            deleteButton.Enabled.Value = SelectedGroup.Value != null && editorChart.ControlPointInfo.TimingPoints.Count > 1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            editorChart.ControlPointInfo.ControlPointsChanged += scheduleRefresh;
            refreshRows();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // Arrow selection deliberately shadows the editor's divisor binding while this tab is
            // visible — the table owns Up/Down here, matching osu's timing-screen feel.
            if (e.Repeat || e.ControlPressed || e.AltPressed || e.ShiftPressed || e.SuperPressed)
                return base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Up:
                    return moveSelection(-1);

                case Key.Down:
                    return moveSelection(1);
            }

            return base.OnKeyDown(e);
        }

        private bool moveSelection(int direction)
        {
            var groups = editorChart.ControlPointInfo.Groups;
            if (groups.Count == 0)
                return false;

            int currentIndex = -1;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] == SelectedGroup.Value)
                {
                    currentIndex = i;
                    break;
                }
            }

            int targetIndex = currentIndex == -1
                ? (direction > 0 ? 0 : groups.Count - 1)
                : Math.Clamp(currentIndex + direction, 0, groups.Count - 1);

            var target = groups[targetIndex];

            if (target != SelectedGroup.Value)
            {
                SelectedGroup.Value = target;
                editorClock.Seek(target.Time);
            }

            return true;
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
                        // Re-clicking the selected row deselects it (ISSUES.md: there was no way to
                        // clear the selection at all).
                        if (SelectedGroup.Value == g)
                        {
                            SelectedGroup.Value = null;
                            return;
                        }

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

            // A group already at the playhead: focus it instead of silently replacing its point
            // in place (osu's "+" semantics).
            var existing = editorChart.ControlPointInfo.GroupAt(time);
            if (existing != null)
            {
                SelectedGroup.Value = existing;
                return;
            }

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
    /// A single row in the timing point list: time column + attribute chips (BPM, time signature,
    /// and a "no barline" chip shown only while <see cref="TimingControlPoint.OmitFirstBarLine"/> is set).
    /// </summary>
    public partial class TimingPointRow : ClickableContainer
    {
        public const float TIME_COLUMN_WIDTH = 110;

        private static readonly Colour4 row_background = new Colour4(42, 42, 48, 255);
        private static readonly Colour4 selected_background = new Colour4(70, 90, 140, 255);

        private readonly ControlPointGroup group;

        /// <summary>Bindable to the parent list's SelectedGroup — drives visual selection state.</summary>
        public readonly Bindable<ControlPointGroup?> IsSelected = new Bindable<ControlPointGroup?>();

        public new Action<ControlPointGroup>? Action;

        // Bound copies stored as fields so drawable disposal auto-unbinds them. (Subscribing lambdas
        // directly to the point's bindables would keep every discarded row alive after each list
        // refresh — the lambda-leak gotcha.)
        private readonly IBindable<double> beatLength;
        private readonly IBindable<TimeSignature> timeSignature;
        private readonly IBindable<bool> omitFirstBarLine;

        private Box background = null!;
        private AttributeChip bpmChip = null!;
        private AttributeChip signatureChip = null!;
        private AttributeChip omitBarLineChip = null!;

        public TimingPointRow(ControlPointGroup group, TimingControlPoint timingPoint)
        {
            this.group = group;

            beatLength = timingPoint.BeatLengthBindable.GetBoundCopy();
            timeSignature = timingPoint.TimeSignatureBindable.GetBoundCopy();
            omitFirstBarLine = timingPoint.OmitFirstBarLineBindable.GetBoundCopy();

            RelativeSizeAxes = Axes.X;
            Height = 32;

            base.Action = () => Action?.Invoke(group);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = row_background,
                },
                new SpriteText
                {
                    Text = $"{group.Time:0}ms",
                    X = 8,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    X = TIME_COLUMN_WIDTH,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        bpmChip = new AttributeChip(),
                        signatureChip = new AttributeChip(),
                        omitBarLineChip = new AttributeChip { Text = "no barline", Alpha = 0 },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            IsSelected.BindValueChanged(e =>
            {
                bool selected = e.NewValue != null && Math.Abs(e.NewValue.Time - group.Time) < 1;
                background.Colour = selected ? selected_background : row_background;
                Alpha = selected ? 1f : 0.85f;
            }, true);

            beatLength.BindValueChanged(_ => bpmChip.Text = $"{60000 / beatLength.Value:0.##} BPM", true);
            timeSignature.BindValueChanged(_ => signatureChip.Text = $"{timeSignature.Value.Numerator}/4", true);
            omitFirstBarLine.BindValueChanged(e => omitBarLineChip.Alpha = e.NewValue ? 1 : 0, true);
        }

        /// <summary>
        /// A small rounded pill showing one attribute of the timing point (osu's RowAttribute,
        /// simplified: no representing-colour circle, plain SpriteText).
        /// </summary>
        public partial class AttributeChip : CompositeDrawable
        {
            private readonly SpriteText text;

            public LocalisableString Text
            {
                get => text.Text;
                set => text.Text = value;
            }

            public AttributeChip()
            {
                AutoSizeAxes = Axes.X;
                Height = 20;
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                Masking = true;
                CornerRadius = 3;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Colour4(25, 25, 30, 255),
                    },
                    text = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Margin = new MarginPadding { Horizontal = 6 },
                        Font = FontUsage.Default.With(size: 14),
                    },
                };
            }
        }
    }
}
