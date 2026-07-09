// Two FileChooserRows for the audio track and background image resources.
// Both rows are disabled if ChartFile.Directory is null (unsaved chart).

using Garbus.Game.Charts;
using Garbus.Game.Edit;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup
{
    /// <summary>
    /// FillFlow section for the two resource-picker rows (audio track + background image).
    /// </summary>
    public partial class ResourcesSection : FillFlowContainer
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        [Resolved]
        private ChartFile chartFile { get; set; } = null!;

        [Resolved]
        private GarbusEditor editor { get; set; } = null!;

        private FileChooserRow audioRow = null!;
        private FileChooserRow bgRow = null!;

        /// <summary>
        /// Overlay container hosted by <see cref="SetupTab"/> (which has RelativeSizeAxes.Both).
        /// Must be set before the component is loaded.
        /// </summary>
        public Container? OverlayContainer { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, 4);
            Padding = new MarginPadding { Vertical = 8, Horizontal = 16 };

            bool hasSavedDir = chartFile.Directory != null;
            var meta = editorChart.Metadata;

            audioRow = new FileChooserRow(
                "Audio Track",
                new[] { ".mp3", ".ogg", ".wav" },
                meta.AudioFile)
            {
                OnFilePicked = onAudioPicked,
            };
            audioRow.ChooseButton.Enabled.Value = hasSavedDir;

            bgRow = new FileChooserRow(
                "Background Image",
                new[] { ".jpg", ".jpeg", ".png" },
                meta.BackgroundFile)
            {
                OnFilePicked = onBackgroundPicked,
            };
            bgRow.ChooseButton.Enabled.Value = hasSavedDir;

            if (OverlayContainer != null)
            {
                audioRow.SetOverlayContainer(OverlayContainer);
                bgRow.SetOverlayContainer(OverlayContainer);
            }

            Children = new Drawable[]
            {
                new SpriteText
                {
                    Text = "Resources",
                    Font = FontUsage.Default.With(size: 20),
                    Margin = new MarginPadding { Bottom = 8 },
                },
                audioRow,
                bgRow,
            };

            if (!hasSavedDir)
            {
                Add(new SpriteText
                {
                    Text = "Save the chart first to add resources.",
                    Font = FontUsage.Default.With(size: 14),
                    Colour = new osuTK.Graphics.Color4(200, 140, 80, 255),
                    Margin = new MarginPadding { Top = 4 },
                });
            }
        }

        private void onAudioPicked(string fullPath)
        {
            if (chartFile.Directory == null) return;

            string fileName = chartFile.ImportResource(fullPath);

            changeHandler.BeginChange();
            editorChart.Metadata.AudioFile = fileName;
            editorChart.SaveState();
            changeHandler.EndChange();

            // Ask the editor to reload the track so the clock sees the new file.
            editor.ReloadTrack();
        }

        private void onBackgroundPicked(string fullPath)
        {
            if (chartFile.Directory == null) return;

            string fileName = chartFile.ImportResource(fullPath);

            changeHandler.BeginChange();
            editorChart.Metadata.BackgroundFile = fileName;
            editorChart.SaveState();
            changeHandler.EndChange();
        }
    }
}
