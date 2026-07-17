using System;
using Garbus.Game.Charts;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
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

        /// <summary>Exposes the difficulty dropdown so tests can read/set the selected value.</summary>
        public BasicDropdown<Difficulty> DifficultyDropdown { get; private set; } = null!;

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

            DifficultyDropdown = new BasicDropdown<Difficulty>
            {
                Width = 300,
                Items = Enum.GetValues<Difficulty>(),
                Current = { Value = meta.Difficulty },
            };

            DifficultyDropdown.Current.ValueChanged += e =>
            {
                if (e.NewValue == meta.Difficulty)
                    return;

                changeHandler.BeginChange();
                meta.Difficulty = e.NewValue;
                editorChart.SaveState();
                changeHandler.EndChange();
            };

            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = "Difficulty",
                    Font = FontUsage.Default.With(size: 20),
                    Margin = new MarginPadding { Bottom = 8 },
                },
                LevelRow,
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Padding = new MarginPadding { Vertical = 4 },
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Text = "Difficulty",
                            Font = FontUsage.Default.With(size: 16),
                            Width = 160,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        DifficultyDropdown,
                    },
                },
            };
        }
    }
}
