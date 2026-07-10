// Timing tab: timeline strip on top; point list (left 40%) + settings & tap timing (right 60%) below.
// The timeline strip is a second, independent TimelineStrip instance (osu gives each editor screen
// its own timeline too) — all of its dependencies are DI-cached by the editor shell, and its zoom
// state is per-instance.

using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit.Screens.Timeline;
using Garbus.Game.Edit.Screens.Timing;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens
{
    public partial class TimingTab : EditorTabScreen
    {
        private readonly Bindable<ControlPointGroup?> selectedGroup = new Bindable<ControlPointGroup?>();

        private TimelineStrip timelineStrip = null!;
        private TimingPointList timingPointList = null!;
        private TimingPointSettings timingPointSettings = null!;
        private TapTimingControl tapTimingControl = null!;

        public TimingTab()
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
                // Zoom-out button (–) at the right edge of the timeline strip.
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = zoom_button_width,
                    Height = TimelineStrip.HEIGHT / 2,
                    Text = "–",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value - 1f,
                },
                // Zoom-in button (+) just to the left of the zoom-out button.
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
                                // Left: list of timing points.
                                timingPointList = new TimingPointList
                                {
                                    RelativeSizeAxes = Axes.Both,
                                },

                                // Right: settings + tap timing stacked.
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Children = new Drawable[]
                                    {
                                        // Auto-sizes vertically to its content; the tap control flows below it.
                                        timingPointSettings = new TimingPointSettings
                                        {
                                            RelativeSizeAxes = Axes.X,
                                        },
                                        tapTimingControl = new TapTimingControl
                                        {
                                            RelativeSizeAxes = Axes.X,
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };

            // Wire up shared SelectedGroup binding.
            selectedGroup.BindTo(timingPointList.SelectedGroup);
            timingPointSettings.SelectedGroup.BindTo(selectedGroup);
            tapTimingControl.SelectedGroup.BindTo(selectedGroup);

            // Both panels honour the same "Move objects with timing changes" toggle.
            tapTimingControl.AdjustObjectsOnTimingChange.BindTo(timingPointSettings.AdjustObjectsOnTimingChange);
        }
    }
}
