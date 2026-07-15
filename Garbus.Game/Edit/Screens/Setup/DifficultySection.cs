using Garbus.Game.Charts;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup
{
    public partial class DifficultySection : FillFlowContainer
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        public FormRow LevelRow { get; private set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 4);
            Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };

            var meta = editorChart.Metadata;

            LevelRow = new FormRow("Level", meta.Level.ToString(), value =>
            {
                if (int.TryParse(value, out int level))
                {
                    changeHandler.BeginChange();
                    meta.Level = level;
                    editorChart.SaveState();
                    changeHandler.EndChange();
                }
            }, numericOnly: true);

            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = "Difficulty",
                    Font = FontUsage.Default.With(size: 20),
                    Margin = new MarginPadding { Bottom = 8 },
                },
                LevelRow,
            };
        }
    }
}
