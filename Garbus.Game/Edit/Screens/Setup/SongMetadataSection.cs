using Garbus.Game.Edit;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup;

public partial class SongMetadataSection : FillFlowContainer
{
    [Resolved] private EditorSong editorSong { get; set; } = null!;
    [Resolved] private IEditorChangeHandler changeHandler { get; set; } = null!;
    private FormRow title = null!;
    private FormRow romanizedTitle = null!;
    private FormRow artist = null!;
    private FormRow romanizedArtist = null!;
    private FormRow source = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        RelativeSizeAxes = Axes.X;
        AutoSizeAxes = Axes.Y;
        Direction = FillDirection.Vertical;
        Spacing = new Vector2(0, 4);
        Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };
        var metadata = editorSong.Song.Metadata;
        Children = new Drawable[]
        {
            heading("Song"),
            title = row("Title", metadata.Title, v => metadata.Title = v),
            romanizedTitle = row("Romanized Title", metadata.TitleRomanized, v => metadata.TitleRomanized = v),
            artist = row("Artist", metadata.Artist, v => metadata.Artist = v),
            romanizedArtist = row("Romanized Artist", metadata.ArtistRomanized, v => metadata.ArtistRomanized = v),
            source = row("Source", metadata.Source, v => metadata.Source = v),
        };
        editorSong.StateRestored += () => Schedule(refresh);
    }

    private FormRow row(string label, string initial, System.Action<string> setter) => new FormRow(label, initial, value =>
    {
        changeHandler.BeginChange();
        setter(value);
        editorSong.SaveState();
        changeHandler.EndChange();
    });

    private void refresh()
    {
        var metadata = editorSong.Song.Metadata;
        title.SetValue(metadata.Title);
        romanizedTitle.SetValue(metadata.TitleRomanized);
        artist.SetValue(metadata.Artist);
        romanizedArtist.SetValue(metadata.ArtistRomanized);
        source.SetValue(metadata.Source);
    }

    private static SpriteText heading(string text) => new SpriteText
    {
        Text = text,
        Font = FontUsage.Default.With(size: 22),
        Margin = new MarginPadding { Bottom = 8 },
    };
}
