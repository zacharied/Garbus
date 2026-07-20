using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Screens.Dialogs;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup;

public enum TimingOwnership
{
    SharedSongTiming,
    PerChartTiming,
}

public partial class TimingOwnershipSection : FillFlowContainer
{
    [Resolved] private EditorSong editorSong { get; set; } = null!;
    public Container? OverlayContainer { get; set; }
    private RadioButton sharedButton = null!;
    private RadioButton perChartButton = null!;
    private bool refreshing;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Direction = FillDirection.Horizontal;
        Spacing = new Vector2(8, 0);
        Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };
        Children = new Drawable[]
        {
            new SpriteText { Text = "Timing", Width = 160, Font = FontUsage.Default.With(size: 16) },
            new EditorRadioButtonCollection
            {
                Width = 300,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(8, 0),
                HorizontalButtonWidth = 146,
                TransparentBackground = true,
                Items = new[]
                {
                    sharedButton = new RadioButton("Shared", () => select(TimingOwnership.SharedSongTiming)),
                    perChartButton = new RadioButton("Per-chart", () => select(TimingOwnership.PerChartTiming)),
                },
            },
        };
        setSelection(editorSong.Song.UsesSharedTiming ? TimingOwnership.SharedSongTiming : TimingOwnership.PerChartTiming);
        editorSong.TimingSourceChanged += () => Schedule(() => setSelection(
            editorSong.Song.UsesSharedTiming ? TimingOwnership.SharedSongTiming : TimingOwnership.PerChartTiming));
    }

    private void select(TimingOwnership value)
    {
        if (refreshing) return;
        if (value == TimingOwnership.PerChartTiming)
        {
            editorSong.UsePerChartTiming();
            return;
        }
        if (editorSong.ChartTimingsAreIdentical())
        {
            editorSong.UseSharedTiming();
            return;
        }

        var dialog = new ConfirmDialog("Chart timings differ. Use the active chart's timing for every chart?",
            ("Use Active", editorSong.UseSharedTiming),
            ("Cancel", () => setSelection(TimingOwnership.PerChartTiming)));
        if (OverlayContainer != null)
        {
            OverlayContainer.Child = dialog;
            dialog.Show();
        }
        else setSelection(TimingOwnership.PerChartTiming);
    }

    private void setSelection(TimingOwnership value)
    {
        refreshing = true;
        if (value == TimingOwnership.SharedSongTiming)
            sharedButton.Selected.Value = true;
        else
            perChartButton.Selected.Value = true;
        refreshing = false;
    }
}
