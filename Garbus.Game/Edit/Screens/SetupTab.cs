// Setup tab: scrollable list of three sections — Metadata, Difficulty, Resources.

using Garbus.Game.Edit.Screens.Setup;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Garbus.Game.Edit.Screens
{
    public partial class SetupTab : EditorTabScreen
    {
        private readonly Container overlayContainer;
        private readonly ResourcesSection resourcesSection;

        public SetupTab()
        {
            RelativeSizeAxes = Axes.Both;

            resourcesSection = new ResourcesSection();
            overlayContainer = new Container { RelativeSizeAxes = Axes.Both, Depth = -10 };
            resourcesSection.OverlayContainer = overlayContainer;

            // FillFlowContainer children in a vertical flow must use AutoSizeAxes.Y (not RelativeSizeAxes.Y).
            // The scroll container itself fills the tab area.
            InternalChildren = new Drawable[]
            {
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 16),
                        Padding = new MarginPadding { Vertical = 16, Horizontal = 8 },
                        Children = new Drawable[]
                        {
                            new MetadataSection(),
                            new DifficultySection(),
                            resourcesSection,
                        },
                    },
                },
                overlayContainer,
            };
        }
    }
}
