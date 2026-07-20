// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the osu-framework repository for full licence text.
// Adapted for Garbus: retains basic file-row visuals and adds selected-row highlighting.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens
{
    public partial class GarbusFileSelector : BasicFileSelector
    {
        internal static readonly Color4 SELECTION_COLOUR = new Color4(70, 90, 140, 255);

        public GarbusFileSelector(string? initialPath = null, string[]? validFileExtensions = null)
            : base(initialPath, validFileExtensions)
        {
        }

        protected override Drawable CreateHiddenToggleButton()
        {
            var button = new BasicButton
            {
                Size = new Vector2(200, 25),
                Action = ShowHiddenItems.Toggle,
            };

            ShowHiddenItems.BindValueChanged(showing =>
                button.Text = showing.NewValue ? "Hide hidden files" : "Show hidden files", true);

            return button;
        }

        protected override DirectoryListingFile CreateFileItem(FileInfo file) => new FileItem(file);

        private partial class FileItem : DirectoryListingFile
        {
            private Box selectionBackground = null!;
            private Bindable<FileInfo> currentFile = null!;
            private bool selectionInitialised;

            public FileItem(FileInfo file)
                : base(file)
            {
            }

            [BackgroundDependencyLoader]
            private void load(Bindable<FileInfo> currentFile)
            {
                this.currentFile = currentFile;

                AutoSizeAxes = Axes.Y;
                RelativeSizeAxes = Axes.X;
                Flow.AutoSizeAxes = Axes.Y;
                Flow.RelativeSizeAxes = Axes.X;

                AddInternal(selectionBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    BypassAutoSizeAxes = Axes.Both,
                    Depth = 1,
                });

                currentFile.BindValueChanged(onCurrentFileChanged, true);
            }

            protected override IconUsage? Icon
            {
                get
                {
                    switch (File.Extension)
                    {
                        case ".ogg":
                        case ".mp3":
                        case ".wav":
                            return FontAwesome.Regular.FileAudio;

                        case ".jpg":
                        case ".jpeg":
                        case ".png":
                            return FontAwesome.Regular.FileImage;

                        case ".mp4":
                        case ".avi":
                        case ".mov":
                        case ".flv":
                            return FontAwesome.Regular.FileVideo;

                        default:
                            return FontAwesome.Regular.File;
                    }
                }
            }

            protected override SpriteText CreateSpriteText() => new SpriteText
            {
                Font = FrameworkFont.Regular.With(size: FONT_SIZE),
            };

            private void onCurrentFileChanged(ValueChangedEvent<FileInfo> file)
            {
                bool selected = string.Equals(file.NewValue?.FullName, File.FullName, StringComparison.Ordinal);
                var targetColour = selected ? SELECTION_COLOUR : Color4.Transparent;

                if (selectionInitialised)
                    selectionBackground.FadeColour(targetColour, 120, Easing.OutQuint);
                else
                    selectionBackground.Colour = targetColour;

                selectionInitialised = true;
            }

            protected override void Dispose(bool isDisposing)
            {
                if (currentFile != null)
                    currentFile.ValueChanged -= onCurrentFileChanged;

                base.Dispose(isDisposing);
            }
        }
    }
}
