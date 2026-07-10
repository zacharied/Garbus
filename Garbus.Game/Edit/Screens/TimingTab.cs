// Timing tab: point list (left 40%) + settings & tap timing (right 60%).

using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit.Screens.Timing;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.Edit.Screens
{
    public partial class TimingTab : EditorTabScreen
    {
        private readonly Bindable<ControlPointGroup?> selectedGroup = new Bindable<ControlPointGroup?>();

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

            InternalChild = new GridContainer
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
