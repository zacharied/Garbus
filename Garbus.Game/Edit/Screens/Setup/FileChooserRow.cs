// A label + current filename display + Choose button row for picking a resource file.
// When the button is clicked a GarbusFileSelector overlay is shown; on selection the
// OnFilePicked callback fires.

using System;
using System.IO;
using Garbus.Game.Configuration;
using Garbus.Game.Edit.Screens.Dialogs;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup
{
    /// <summary>
    /// A horizontal row: label | current filename | Choose button.
    /// When a file is chosen via the overlay or via <see cref="SimulatePick"/> (tests only),
    /// <see cref="OnFilePicked"/> is invoked with the full path of the chosen file.
    /// </summary>
    public partial class FileChooserRow : FillFlowContainer
    {
        /// <summary>Called when a file has been picked. Receives the full absolute path.</summary>
        public Action<string>? OnFilePicked;

        /// <summary>Exposed so tests (and ResourcesSection) can enable/disable the button.</summary>
        public BasicButton ChooseButton { get; }

        private readonly SpriteText fileNameText;
        private readonly string[] validExtensions;

        /// <summary>Overlay container injected by <see cref="ResourcesSection"/> so the selector
        /// can be presented as a child of the tab rather than bloating this row.</summary>
        private Container? overlayContainer;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        public FileChooserRow(string label, string[] validExtensions, string currentValue)
        {
            this.validExtensions = validExtensions;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(8, 0);
            Padding = new MarginPadding { Vertical = 4 };

            string displayName = string.IsNullOrEmpty(currentValue) ? "(none)" : currentValue;

            ChooseButton = new BasicButton
            {
                Text = "Choose",
                Width = 80,
                Height = 30,
                Action = openSelector,
            };

            InternalChildren = new Drawable[]
            {
                new SpriteText
                {
                    Text = label,
                    Font = FontUsage.Default.With(size: 16),
                    Width = 160,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                fileNameText = new SpriteText
                {
                    Text = displayName,
                    Font = FontUsage.Default.With(size: 16),
                    Width = 200,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Colour = new osuTK.Graphics.Color4(180, 180, 180, 255),
                },
                ChooseButton,
            };
        }

        /// <summary>
        /// Sets the container that will host the file-selector overlay.
        /// Must be called by <see cref="ResourcesSection"/> before the first pick.
        /// </summary>
        public void SetOverlayContainer(Container container)
        {
            overlayContainer = container;
        }

        /// <summary>
        /// Test seam: simulate a file pick without needing a real UI interaction.
        /// Fires <see cref="OnFilePicked"/> with the given path and updates the filename display.
        /// </summary>
        public void SimulatePick(string fullPath)
        {
            Schedule(() => commitPick(fullPath));
        }

        public void SetValue(string value) => fileNameText.Text = string.IsNullOrEmpty(value) ? "(none)" : value;

        private void openSelector()
        {
            if (overlayContainer == null)
                return;

            var container = overlayContainer;

            var dialog = new FileSelectDialog(validExtensions, "Select", commitPick);

            // The dialog hides itself on both confirm and cancel; clearing the host on hide keeps a
            // dismissed dialog from sitting there swallowing input.
            dialog.State.BindValueChanged(state =>
            {
                if (state.NewValue == Visibility.Hidden)
                    container.Clear();
            });

            container.Child = dialog;
            dialog.Show();
        }

        private void commitPick(string fullPath)
        {
            // FileSelectDialog already persisted the directory; this path also serves SimulatePick.
            LastFileDirectory.Set(config, Path.GetDirectoryName(fullPath));

            fileNameText.Text = Path.GetFileName(fullPath);
            OnFilePicked?.Invoke(fullPath);
        }
    }
}
