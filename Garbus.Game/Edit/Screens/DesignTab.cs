// Design tab: timeline strip on top; point list (left 40%) + editable point details (right 60%)
// below — the layout mirrors TimingTab. Instead of timing points it edits design points (time-ranged
// visual effects). The two grid cells are placeholders here; the list is filled in the DesignPointList
// task and the details pane in the DesignPointSettings task.

using Garbus.Game.Charts.Design;
using Garbus.Game.Edit.Screens.Design;
using Garbus.Game.Edit.Screens.Timeline;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens
{
    public partial class DesignTab : EditorTabScreen
    {
        // Shared selection between the list (Task 5) and settings pane (Task 6).
        private readonly Bindable<DesignPoint?> selectedPoint = new Bindable<DesignPoint?>();

        private TimelineStrip timelineStrip = null!;
        private DesignPointList designPointList = null!;
        private DesignPointSettings designPointSettings = null!;

        public DesignTab()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            const float zoom_button_width = 26;

            InternalChildren = new Drawable[]
            {
                timelineStrip = new TimelineStrip(),
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = zoom_button_width,
                    Height = TimelineStrip.HEIGHT / 2,
                    Text = "–",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value - 1f,
                },
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = zoom_button_width,
                    Height = TimelineStrip.HEIGHT / 2,
                    Position = new osuTK.Vector2(-zoom_button_width, 0),
                    Text = "+",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value + 1f,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = TimelineStrip.HEIGHT },
                    Child = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        ColumnDimensions = new[]
                        {
                            new Dimension(GridSizeMode.Relative, 0.40f),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                // Left: list of design points.
                                designPointList = new DesignPointList { RelativeSizeAxes = Axes.Both },
                                // Right: editable details for the selected design point.
                                new BasicScrollContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    ScrollbarOverlapsContent = false,
                                    Child = designPointSettings = new DesignPointSettings
                                    {
                                        RelativeSizeAxes = Axes.X,
                                    },
                                },
                            },
                        },
                    },
                },
            };

            selectedPoint.BindTo(designPointList.SelectedPoint);
            designPointSettings.SelectedPoint.BindTo(selectedPoint);
        }
    }
}
