// Left panel of the Design tab: header row + one row per design point (StartTime, EndTime, text
// preview). Mirrors TimingPointList. Selecting a row seeks the editor clock to the point's StartTime.
// Add creates a TutorialMessage at the snapped playhead; Delete removes the selected point. Both go
// through the change handler so they are one undo step each. Up/Down navigate the list.

using System;
using System.Linq;
using Garbus.Game.Charts.Design;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Screens.Design
{
    public partial class DesignPointList : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        private DesignPointInfo designPointInfo = null!;

        public readonly Bindable<DesignPoint?> SelectedPoint = new Bindable<DesignPoint?>();

        private const float header_height = 24;

        private FillFlowContainer<DesignPointRow> rowContainer = null!;
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
                            Text = "Start",
                            X = 8,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                        new SpriteText
                        {
                            Text = "End",
                            X = DesignPointRow.START_COLUMN_WIDTH,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                        new SpriteText
                        {
                            Text = "Message",
                            X = DesignPointRow.START_COLUMN_WIDTH + DesignPointRow.END_COLUMN_WIDTH,
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
                    Child = rowContainer = new FillFlowContainer<DesignPointRow>
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
                    },
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            deleteButton.Enabled.Value = SelectedPoint.Value != null;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            bindDesignPointInfo();
            editorChart.ChartChanged += onChartChanged;
            refreshRows();
        }

        private void onChartChanged(Charts.GarbusChart _, Charts.GarbusChart __)
        {
            SelectedPoint.Value = null;
            bindDesignPointInfo();
            scheduleRefresh();
        }

        private void bindDesignPointInfo()
        {
            if (designPointInfo != null)
                designPointInfo.DesignPointsChanged -= scheduleRefresh;
            designPointInfo = editorChart.DesignPointInfo;
            designPointInfo.DesignPointsChanged += scheduleRefresh;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
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
            var points = designPointInfo.DesignPoints;
            if (points.Count == 0)
                return false;

            int currentIndex = -1;
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == SelectedPoint.Value)
                {
                    currentIndex = i;
                    break;
                }
            }

            int targetIndex = currentIndex == -1
                ? (direction > 0 ? 0 : points.Count - 1)
                : Math.Clamp(currentIndex + direction, 0, points.Count - 1);

            var target = points[targetIndex];
            if (target != SelectedPoint.Value)
            {
                SelectedPoint.Value = target;
                editorClock.Seek(target.StartTime);
            }

            return true;
        }

        private void scheduleRefresh() => Scheduler.AddOnce(refreshRows);

        private void refreshRows()
        {
            rowContainer.Clear();

            foreach (var point in designPointInfo.DesignPoints)
            {
                var row = new DesignPointRow(point)
                {
                    IsSelected = { BindTarget = SelectedPoint },
                    Action = p =>
                    {
                        if (SelectedPoint.Value == p)
                        {
                            SelectedPoint.Value = null;
                            return;
                        }

                        SelectedPoint.Value = p;
                        editorClock.Seek(p.StartTime);
                    },
                };
                rowContainer.Add(row);
            }

            // Reselect the point at the same start time if the previous selection was replaced
            // (undo/redo rebuilds the container with new instances).
            if (SelectedPoint.Value != null)
            {
                var stillExists = designPointInfo.DesignPoints
                    .FirstOrDefault(p => Math.Abs(p.StartTime - SelectedPoint.Value.StartTime) < 1);
                SelectedPoint.Value = stillExists;
            }
        }

        private void addAtPlayhead()
        {
            double start = editorChart.ControlPointInfo.GetClosestSnappedTime(editorClock.CurrentTime);

            changeHandler.BeginChange();
            var point = new TutorialMessage { StartTime = start, EndTime = start + 2000, Text = "New message" };
            designPointInfo.Add(point);
            editorChart.SaveState();
            changeHandler.EndChange();

            SelectedPoint.Value = point;
        }

        private void deleteSelected()
        {
            if (SelectedPoint.Value == null)
                return;

            changeHandler.BeginChange();
            designPointInfo.Remove(SelectedPoint.Value);
            editorChart.SaveState();
            changeHandler.EndChange();

            SelectedPoint.Value = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (designPointInfo != null)
                designPointInfo.DesignPointsChanged -= scheduleRefresh;
            if (editorChart != null)
                editorChart.ChartChanged -= onChartChanged;
        }
    }

    public partial class DesignPointRow : ClickableContainer
    {
        public const float START_COLUMN_WIDTH = 90;
        public const float END_COLUMN_WIDTH = 90;

        private static readonly Colour4 row_background = new Colour4(42, 42, 48, 255);
        private static readonly Colour4 selected_background = new Colour4(70, 90, 140, 255);

        private readonly DesignPoint point;

        public readonly Bindable<DesignPoint?> IsSelected = new Bindable<DesignPoint?>();

        public new Action<DesignPoint>? Action;

        // Bound copies stored as fields so drawable disposal auto-unbinds them (lambda-leak gotcha).
        private readonly IBindable<double> startTime;
        private readonly IBindable<double> endTime;
        private readonly IBindable<string>? text;

        private Box background = null!;
        private SpriteText startText = null!;
        private SpriteText endText = null!;
        private SpriteText messageText = null!;

        public DesignPointRow(DesignPoint point)
        {
            this.point = point;

            startTime = point.StartTimeBindable.GetBoundCopy();
            endTime = point.EndTimeBindable.GetBoundCopy();
            if (point is TutorialMessage tm)
                text = tm.TextBindable.GetBoundCopy();

            RelativeSizeAxes = Axes.X;
            Height = 32;

            base.Action = () => Action?.Invoke(point);
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
                startText = new SpriteText
                {
                    X = 8,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                endText = new SpriteText
                {
                    X = START_COLUMN_WIDTH,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                messageText = new SpriteText
                {
                    X = START_COLUMN_WIDTH + END_COLUMN_WIDTH,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Font = FontUsage.Default.With(size: 14),
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            IsSelected.BindValueChanged(e =>
            {
                bool selected = e.NewValue == point;
                background.Colour = selected ? selected_background : row_background;
                Alpha = selected ? 1f : 0.85f;
            }, true);

            startTime.BindValueChanged(_ => startText.Text = $"{point.StartTime:0}ms", true);
            endTime.BindValueChanged(_ => endText.Text = $"{point.EndTime:0}ms", true);
            if (text != null)
                text.BindValueChanged(_ => messageText.Text = preview(text.Value), true);
            else
                messageText.Text = string.Empty;
        }

        private static string preview(string value)
        {
            value = value.Replace("\n", " ");
            return value.Length <= 24 ? value : value.Substring(0, 24) + "…";
        }
    }
}
