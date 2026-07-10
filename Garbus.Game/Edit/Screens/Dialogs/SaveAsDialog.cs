// Directory picker + filename input dialog for saving a new chart.

using System;
using System.IO;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// Modal overlay: directory selector + filename text box + Save/Cancel buttons.
    /// Calls the <c>onSave</c> callback with the resolved absolute path (guaranteed to end with .garbus).
    /// </summary>
    public partial class SaveAsDialog : VisibilityContainer
    {
        private readonly Action<string> onSave;
        private readonly string defaultFilename;
        private BasicDirectorySelector directorySelector = null!;
        private BasicTextBox filenameBox = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        public SaveAsDialog(Action<string> onSave, string defaultFilename = "new-chart")
        {
            this.onSave = onSave;
            this.defaultFilename = defaultFilename;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // dim background
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
                Alpha = 0.6f,
            });

            filenameBox = new BasicTextBox
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                Text = defaultFilename,
            };

            directorySelector = new BasicDirectorySelector
            {
                RelativeSizeAxes = Axes.Both,
            };

            // The panel uses a fixed height. We use a Container with explicit padding to carve out
            // room for the title, filename box, and button row at the bottom.
            // directorySelector is placed in a Container that leaves bottom room.
            const float titleHeight = 32;
            const float filenameHeight = 38;
            const float buttonRowHeight = 48;
            const float bottomReserve = filenameHeight + buttonRowHeight;

            var panel = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(700, 560),
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(30, 30, 40, 255),
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = "Save Chart As…",
                        Font = FontUsage.Default.With(size: 20),
                        Colour = Color4.White,
                        Y = 6,
                    },
                    // Directory selector fills the middle section.
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Top = titleHeight, Bottom = bottomReserve },
                        Child = directorySelector,
                    },
                    // Filename label + box near the bottom.
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = filenameHeight,
                        Y = -buttonRowHeight,
                        Padding = new MarginPadding { Horizontal = 8, Vertical = 4 },
                        Child = filenameBox,
                    },
                    // Button row at the very bottom.
                    new Container
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = buttonRowHeight,
                        Children = new Drawable[]
                        {
                            new BasicButton
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = "Save",
                                Size = new Vector2(100, 36),
                                X = 8,
                                Action = onSavePressed,
                            },
                            new BasicButton
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                Text = "Cancel",
                                Size = new Vector2(100, 36),
                                X = -8,
                                Action = () => Hide(),
                            },
                        },
                    },
                },
            };

            AddInternal(panel);
        }

        private void onSavePressed()
        {
            var dir = directorySelector.CurrentPath.Value;
            if (dir == null)
                return;

            string filename = filenameBox.Text.Trim();
            if (string.IsNullOrEmpty(filename))
                filename = "new-chart";

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

        protected override void PopIn() => this.FadeIn(150);
        protected override void PopOut() => this.FadeOut(150);
    }
}
