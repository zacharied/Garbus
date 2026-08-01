// Modal file picker: the one dialog every "choose a file" flow goes through.

using System;
using System.IO;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Platform;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// Modal overlay wrapping a <see cref="GarbusFileSelector"/> filtered to the given extensions.
    /// Confirming calls <c>onFileSelected</c> with the chosen absolute path and hides the dialog;
    /// with nothing selected, confirm does nothing.
    /// </summary>
    public partial class FileSelectDialog : ModalOverlay
    {
        public const string FileSelectorName = "file select dialog selector";
        public const string ShowHiddenCheckboxName = "file select dialog show hidden";

        private readonly Action<string> onFileSelected;
        private readonly string[] validFileExtensions;
        private readonly LocalisableString confirmText;

        private GarbusFileSelector fileSelector = null!;
        private Container nestedDialogHost = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        public FileSelectDialog(string[] validFileExtensions, LocalisableString confirmText, Action<string> onFileSelected)
        {
            this.validFileExtensions = validFileExtensions;
            this.confirmText = confirmText;
            this.onFileSelected = onFileSelected;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            fileSelector = new GarbusFileSelector(LastFileDirectory.Get(config), validFileExtensions)
            {
                Name = FileSelectorName,
                RelativeSizeAxes = Axes.Both,
            };

            var header = new DialogHeader();
            header.AddAction("file select new folder", FontAwesome.Solid.FolderPlus, "Create a new folder here", showNewFolderDialog);
            header.AddAction("file select reveal", FontAwesome.Solid.ExternalLinkAlt, "Open this folder in the file manager", revealCurrentDirectory);

            var footer = new DialogFooter(confirmText, Confirm, Cancel);
            footer.AddSetting(ShowHiddenCheckboxName, "Show hidden items", fileSelector.ShowHiddenFiles);

            Panel.AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = DialogHeader.HEIGHT, Bottom = DialogFooter.HEIGHT },
                    Child = fileSelector,
                },
                header,
                footer,
            });

            // The new-folder prompt layers over the whole dialog rather than inside the panel, so it
            // dims this one too. Nesting it here is also what hands it the keyboard: blocking strips
            // everything queued before an overlay, and non-positional events dispatch deepest-first.
            AddInternal(nestedDialogHost = new Container { RelativeSizeAxes = Axes.Both });
        }

        protected override void Confirm()
        {
            var file = fileSelector.CurrentFile.Value;
            if (file == null)
                return;

            LastFileDirectory.Set(config, file.DirectoryName);

            Hide();
            onFileSelected(file.FullName);
        }

        private void showNewFolderDialog()
        {
            // Null at the drive-root listing, which is not a directory anything can be created in.
            if (fileSelector.CurrentPath.Value is not DirectoryInfo current)
                return;

            var dialog = new NewFolderDialog(current, created => fileSelector.CurrentPath.Value = created);

            dialog.State.BindValueChanged(state =>
            {
                if (state.NewValue == Visibility.Hidden)
                    nestedDialogHost.Clear();
            });

            nestedDialogHost.Child = dialog;
            dialog.Show();
        }

        private void revealCurrentDirectory()
        {
            if (fileSelector.CurrentPath.Value is not DirectoryInfo current)
                return;

            host.OpenFileExternally(current.FullName);
        }
    }
}
