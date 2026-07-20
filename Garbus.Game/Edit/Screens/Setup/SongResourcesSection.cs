using Garbus.Game.Charts;
using Garbus.Game.Edit;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup;

public partial class SongResourcesSection : FillFlowContainer
{
    [Resolved] private EditorSong editorSong { get; set; } = null!;
    [Resolved] private IEditorChangeHandler changeHandler { get; set; } = null!;
    [Resolved] private SongFile songFile { get; set; } = null!;
    [Resolved] private GarbusEditor editor { get; set; } = null!;

    public Container? OverlayContainer { get; set; }
    private FileChooserRow trackRow = null!;
    private FileChooserRow backgroundRow = null!;
    private SpriteText saveFirstHint = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Direction = FillDirection.Vertical;
        Spacing = new Vector2(0, 4);
        Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };

        trackRow = new FileChooserRow("Track", new[] { ".mp3", ".ogg", ".wav" }, editorSong.Song.Resources.Track)
        {
            OnFilePicked = path => import(path, true),
        };
        backgroundRow = new FileChooserRow("Background", new[] { ".jpg", ".jpeg", ".png" }, editorSong.Song.Resources.Background)
        {
            OnFilePicked = path => import(path, false),
        };
        if (OverlayContainer != null)
        {
            trackRow.SetOverlayContainer(OverlayContainer);
            backgroundRow.SetOverlayContainer(OverlayContainer);
        }
        Children = new Drawable[]
        {
            new SpriteText { Text = "Resources", Font = FontUsage.Default.With(size: 20) },
            trackRow,
            backgroundRow,
            saveFirstHint = new SpriteText
            {
                Text = "Save the song first to add resources.",
                Font = FontUsage.Default.With(size: 14),
                Colour = new osuTK.Graphics.Color4(200, 140, 80, 255),
            },
        };
        editorSong.StateRestored += () => Schedule(refresh);
    }

    private void refresh()
    {
        trackRow.SetValue(editorSong.Song.Resources.Track);
        backgroundRow.SetValue(editorSong.Song.Resources.Background);
    }

    protected override void Update()
    {
        base.Update();
        bool enabled = songFile.Directory != null;
        trackRow.ChooseButton.Enabled.Value = enabled;
        backgroundRow.ChooseButton.Enabled.Value = enabled;
        saveFirstHint.Alpha = enabled ? 0 : 1;
    }

    private void import(string path, bool track)
    {
        if (songFile.Directory == null) return;
        string name = songFile.ImportResource(path);
        changeHandler.BeginChange();
        if (track) editorSong.Song.Resources.Track = name;
        else editorSong.Song.Resources.Background = name;
        editorSong.SaveState();
        changeHandler.EndChange();
        if (track) editor.ReloadTrack();
    }
}
