using System.Linq;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens.Dialogs;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup;

public partial class ChartListSection : FillFlowContainer
{
    [Resolved] private EditorSong editorSong { get; set; } = null!;
    public Container? OverlayContainer { get; set; }
    private SpriteText header = null!;
    private FillFlowContainer rows = null!;
    public BasicButton AddButton { get; private set; } = null!;
    public BasicButton RemoveButton { get; private set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Direction = FillDirection.Vertical;
        Spacing = new Vector2(0, 6);
        Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };
        Children = new Drawable[]
        {
            header = new SpriteText { Font = FontUsage.Default.With(size: 20) },
            new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 210,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, 98),
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        new BasicScrollContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = rows = new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 3),
                            },
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.Y,
                            AutoSizeAxes = Axes.X,
                            Direction = FillDirection.Vertical,
                            Spacing = new Vector2(0, 8),
                            Padding = new MarginPadding { Left = 8 },
                            Children = new Drawable[]
                            {
                                AddButton = new BasicButton { Text = "Add", Size = new Vector2(90, 30), Action = () => editorSong.AddChart() },
                                RemoveButton = new BasicButton { Text = "Remove", Size = new Vector2(90, 30), Action = remove },
                            },
                        },
                    },
                },
            },
        };
        editorSong.ChartsChanged += () => Schedule(refresh);
        editorSong.ActiveChartChanged += (_, _) => Schedule(refresh);
        refresh();
    }

    private void refresh()
    {
        header.Text = $"Charts ({editorSong.Song.Charts.Count})";
        rows.Clear();
        foreach (var chart in editorSong.Song.Charts)
        {
            string name = string.IsNullOrEmpty(chart.Metadata.ChartName) ? chart.Metadata.Difficulty.ToString() : chart.Metadata.ChartName;
            if (chart.Metadata.Level > 0) name += $" · Lv.{chart.Metadata.Level}";
            bool selected = chart.ChartId == editorSong.ActiveChartId;
            rows.Add(new BasicButton
            {
                RelativeSizeAxes = Axes.X,
                Height = 32,
                Text = selected ? $"▶ {name}" : name,
                Action = () => editorSong.SelectChart(chart.ChartId),
            });
        }
        RemoveButton.Enabled.Value = editorSong.Song.Charts.Count > 1;
    }

    private void remove()
    {
        if (editorSong.Song.Charts.Count <= 1) return;
        bool hasContent = editorSong.ActiveChart.HitObjects.Any() || editorSong.ActiveChart.DesignPointInfo.DesignPoints.Any();
        if (!hasContent)
        {
            editorSong.RemoveActiveChart();
            return;
        }

        var dialog = new ConfirmDialog("Remove this chart and all of its objects?",
            ("Remove", () => editorSong.RemoveActiveChart()),
            ("Cancel", () => { }));
        if (OverlayContainer != null)
        {
            OverlayContainer.Child = dialog;
            dialog.Show();
        }
    }
}
