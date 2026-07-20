using System;
using Garbus.Game.Charts;
using Garbus.Game.Edit;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup;

public partial class ChartMetadataSection : FillFlowContainer
{
    [Resolved] private EditorSong editorSong { get; set; } = null!;
    [Resolved] private IEditorChangeHandler changeHandler { get; set; } = null!;
    private FormRow nameRow = null!;
    private FormRow charterRow = null!;
    private FormRow levelRow = null!;
    public BasicDropdown<Difficulty> DifficultyDropdown { get; private set; } = null!;
    private bool refreshing;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Direction = FillDirection.Vertical;
        Spacing = new Vector2(0, 4);
        Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };
        Children = new Drawable[]
        {
            new SpriteText { Text = "Chart", Font = FontUsage.Default.With(size: 22), Margin = new MarginPadding { Bottom = 8 } },
            nameRow = row("Chart Name", v => editorSong.ActiveChart.Metadata.ChartName = v),
            charterRow = row("Charter", v => editorSong.ActiveChart.Metadata.Charter = v),
            levelRow = new FormRow("Level", "0", value =>
            {
                if (int.TryParse(value, out int parsed)) commit(() => editorSong.ActiveChart.Metadata.Level = parsed);
            }, numericOnly: true),
            new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                Children = new Drawable[]
                {
                    new SpriteText { Text = "Difficulty", Width = 160, Font = FontUsage.Default.With(size: 16) },
                    DifficultyDropdown = new BasicDropdown<Difficulty> { Width = 300, Items = Enum.GetValues<Difficulty>() },
                },
            },
        };
        DifficultyDropdown.Current.ValueChanged += e =>
        {
            if (!refreshing && e.NewValue != editorSong.ActiveChart.Metadata.Difficulty)
                commit(() => editorSong.ActiveChart.Metadata.Difficulty = e.NewValue);
        };
        editorSong.ActiveChartChanged += (_, _) => Schedule(refresh);
        refresh();
    }

    private FormRow row(string label, Action<string> setter) => new FormRow(label, string.Empty, v => commit(() => setter(v)));

    private void commit(Action setter)
    {
        changeHandler.BeginChange();
        setter();
        editorSong.SaveState();
        changeHandler.EndChange();
    }

    private void refresh()
    {
        refreshing = true;
        var metadata = editorSong.ActiveChart.Metadata;
        nameRow.SetValue(metadata.ChartName);
        charterRow.SetValue(metadata.Charter);
        levelRow.SetValue(metadata.Level.ToString());
        DifficultyDropdown.Current.Value = metadata.Difficulty;
        refreshing = false;
    }
}
