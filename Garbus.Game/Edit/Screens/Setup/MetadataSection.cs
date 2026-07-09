// Eight FormRows for editing the eight user-facing metadata fields.
// Each commit (focus loss / Enter) is wrapped in one BeginChange/EndChange transaction — one undo step.

using Garbus.Game.Charts;
using Garbus.Game.Edit;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup
{
    public partial class MetadataSection : FillFlowContainer
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 4);
            Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };

            var meta = editorChart.Metadata;

            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = "Metadata",
                    Font = FontUsage.Default.With(size: 20),
                    Margin = new MarginPadding { Bottom = 8 },
                },
                createRow("Title",             meta.Title,           v => meta.Title           = v),
                createRow("Romanised Title",   meta.RomanisedTitle,  v => meta.RomanisedTitle  = v),
                createRow("Artist",            meta.Artist,          v => meta.Artist           = v),
                createRow("Romanised Artist",  meta.RomanisedArtist, v => meta.RomanisedArtist = v),
                createRow("Charter",           meta.Charter,         v => meta.Charter          = v),
                createRow("Chart Name",        meta.ChartName,       v => meta.ChartName        = v),
                createRow("Source",            meta.Source,          v => meta.Source           = v),
                createRow("Tags",              meta.Tags,            v => meta.Tags             = v),
            };
        }

        private FormRow createRow(string label, string initial, System.Action<string> setter)
        {
            return new FormRow(label, initial, value =>
            {
                changeHandler.BeginChange();
                setter(value);
                editorChart.SaveState();
                changeHandler.EndChange();
            });
        }
    }
}
