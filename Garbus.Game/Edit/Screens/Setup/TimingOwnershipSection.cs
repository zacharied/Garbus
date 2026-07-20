using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens.Dialogs;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
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
    private BasicDropdown<TimingOwnership> dropdown = null!;
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
            dropdown = new BasicDropdown<TimingOwnership>
            {
                Width = 300,
                Items = new[] { TimingOwnership.SharedSongTiming, TimingOwnership.PerChartTiming },
            },
        };
        dropdown.Current.Value = editorSong.Song.UsesSharedTiming ? TimingOwnership.SharedSongTiming : TimingOwnership.PerChartTiming;
        dropdown.Current.ValueChanged += e =>
        {
            if (refreshing) return;
            if (e.NewValue == TimingOwnership.PerChartTiming)
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
                ("Cancel", () => setDropdown(TimingOwnership.PerChartTiming)));
            if (OverlayContainer != null)
            {
                OverlayContainer.Child = dialog;
                dialog.Show();
            }
            else setDropdown(TimingOwnership.PerChartTiming);
        };
        editorSong.TimingSourceChanged += () => Schedule(() => setDropdown(
            editorSong.Song.UsesSharedTiming ? TimingOwnership.SharedSongTiming : TimingOwnership.PerChartTiming));
    }

    private void setDropdown(TimingOwnership value)
    {
        refreshing = true;
        dropdown.Current.Value = value;
        refreshing = false;
    }
}
