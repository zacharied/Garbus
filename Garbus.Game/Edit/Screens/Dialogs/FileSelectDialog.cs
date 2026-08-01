// Modal file picker: the one dialog every "choose a file" flow goes through.

using System;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;

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

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

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

            var footer = new DialogFooter(confirmText, Confirm, Cancel);
            footer.AddSetting(ShowHiddenCheckboxName, "Show hidden items", fileSelector.ShowHiddenFiles);

            Panel.AddRange(new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Bottom = DialogFooter.HEIGHT },
                    Child = fileSelector,
                },
                footer,
            });
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
    }
}
