// A label + current filename display + Choose button row for picking a resource file.
// When the button is clicked a BasicFileSelector overlay is shown; on selection the
// OnFilePicked callback fires.

using System;
using System.IO;
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

        private void openSelector()
        {
            if (overlayContainer == null)
                return;

            var selector = new BasicFileSelector(validFileExtensions: validExtensions)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(700, 500),
            };

            var selectorPanel = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(700, 560),
                Children = new Drawable[]
                {
                    new osu.Framework.Graphics.Shapes.Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new osuTK.Graphics.Color4(30, 30, 40, 255),
                    },
                    selector,
                    new BasicButton
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Text = "Select",
                        Size = new Vector2(100, 40),
                        Margin = new MarginPadding { Left = 8, Bottom = 8 },
                        Action = () =>
                        {
                            var file = selector.CurrentFile.Value;
                            if (file != null)
                            {
                                overlayContainer.Clear();
                                commitPick(file.FullName);
                            }
                        },
                    },
                    new BasicButton
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Text = "Cancel",
                        Size = new Vector2(100, 40),
                        Margin = new MarginPadding { Right = 8, Bottom = 8 },
                        Action = () => overlayContainer.Clear(),
                    },
                },
            };

            overlayContainer.Child = selectorPanel;
        }

        private void commitPick(string fullPath)
        {
            fileNameText.Text = Path.GetFileName(fullPath);
            OnFilePicked?.Invoke(fullPath);
        }
    }
}
