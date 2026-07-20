using Garbus.Game.Edit.Screens.Setup;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.Edit.Screens;

public partial class SetupTab : EditorTabScreen
{
    public SetupTab()
    {
        RelativeSizeAxes = Axes.Both;
        var overlay = new Container { RelativeSizeAxes = Axes.Both, Depth = -10 };
        var resources = new SongResourcesSection { OverlayContainer = overlay };
        var timing = new TimingOwnershipSection { OverlayContainer = overlay };
        var charts = new ChartListSection { OverlayContainer = overlay };

        InternalChildren = new Drawable[]
        {
            new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                ColumnDimensions = new[] { new Dimension(), new Dimension() },
                Content = new[]
                {
                    new Drawable[]
                    {
                        column(new SongMetadataSection(), resources, timing, charts),
                        column(new ChartMetadataSection()),
                    },
                },
            },
            overlay,
        };
    }

    private static Drawable column(params Drawable[] children) => new BasicScrollContainer
    {
        RelativeSizeAxes = Axes.Both,
        Child = new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Padding = new MarginPadding { Vertical = 16, Horizontal = 8 },
            Spacing = new osuTK.Vector2(0, 10),
            Children = children,
        },
    };
}
