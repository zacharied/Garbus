// Directory picker + filename input dialog for saving a new chart.

using System;
using System.IO;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// Modal overlay: directory selector + filename text box, with the show-hidden-items setting at
    /// the bottom-left and Cancel/Save at the bottom-right.
    /// Calls the <c>onSave</c> callback with the resolved absolute path (guaranteed to end with .garbus).
    /// </summary>
    public partial class SaveAsDialog : ModalOverlay
    {
        public const string FilenameBoxName = "save as dialog filename";
        public const string ShowHiddenCheckboxName = "save as dialog show hidden";

        private readonly Action<string> onSave;
        private readonly string defaultFilename;

        private GarbusDirectorySelector directorySelector = null!;
        private BasicTextBox filenameBox = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        public SaveAsDialog(Action<string> onSave, string defaultFilename = "new-song")
        {
            this.onSave = onSave;
            this.defaultFilename = defaultFilename;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            filenameBox = new BasicTextBox
            {
                Name = FilenameBoxName,
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Text = defaultFilename,
            };

            // Enter inside the text box never reaches the dialog's own key handling — TextBox
            // consumes it to commit — so route the commit to the same action.
            filenameBox.OnCommit += (_, _) => Confirm();

            directorySelector = new GarbusDirectorySelector
            {
                RelativeSizeAxes = Axes.Both,
            };

            var footer = new DialogFooter("Save", Confirm, Cancel);
            footer.AddSetting(ShowHiddenCheckboxName, "Show hidden items", directorySelector.ShowHiddenDirectories);

            const float title_height = 32;
            const float filename_height = 38;

            Panel.AddRange(new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = "Save Song As…",
                    Font = FontUsage.Default.With(size: 20),
                    Colour = Color4.White,
                    Y = 6,
                },
                // Directory selector fills the middle section.
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = title_height, Bottom = filename_height + DialogFooter.HEIGHT },
                    Child = directorySelector,
                },
                // Filename box sits directly above the footer.
                new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Height = filename_height,
                    Y = -DialogFooter.HEIGHT,
                    Padding = new MarginPadding { Horizontal = 8, Vertical = 4 },
                    Child = filenameBox,
                },
                footer,
            });
        }

        protected override void Confirm()
        {
            var dir = directorySelector.CurrentPath.Value;
            if (dir == null)
                return;

            string filename = filenameBox.Text.Trim();
            if (string.IsNullOrEmpty(filename))
                filename = "new-song";

            // Ensure .garbus extension.
            if (!filename.EndsWith(".garbus", StringComparison.OrdinalIgnoreCase))
                filename += ".garbus";

            string fullPath = Path.Combine(dir.FullName, filename);

            LastFileDirectory.Set(config, dir.FullName);

            Hide();
            onSave(fullPath);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Applied after load: the selector's own load resolves its default path, which would
            // overwrite anything set earlier.
            if (LastFileDirectory.Get(config) is string last)
                directorySelector.CurrentPath.Value = new DirectoryInfo(last);
        }
    }
}
