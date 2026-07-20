// File picker dialog for opening an existing .garbus chart.

using System;
using System.IO;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// Modal overlay that shows a GarbusFileSelector filtered to .garbus files.
    /// Calls the <c>onFileSelected</c> callback with the chosen absolute path and hides itself.
    /// </summary>
    public partial class OpenChartDialog : VisibilityContainer
    {
        private readonly Action<string> onFileSelected;
        private GarbusFileSelector fileSelector = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        public OpenChartDialog(Action<string> onFileSelected)
        {
            this.onFileSelected = onFileSelected;

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

            fileSelector = new GarbusFileSelector(LastFileDirectory.Get(config), validFileExtensions: new[] { ".garbus" })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(700, 500),
            };

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
                    fileSelector,
                    new BasicButton
                    {
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        Text = "Open",
                        Size = new Vector2(100, 40),
                        Margin = new MarginPadding { Left = 8, Bottom = 8 },
                        Action = onOpen,
                    },
                    new BasicButton
                    {
                        Anchor = Anchor.BottomRight,
                        Origin = Anchor.BottomRight,
                        Text = "Cancel",
                        Size = new Vector2(100, 40),
                        Margin = new MarginPadding { Right = 8, Bottom = 8 },
                        Action = () => Hide(),
                    },
                },
            };

            AddInternal(panel);
        }

        private void onOpen()
        {
            var file = fileSelector.CurrentFile.Value;
            if (file == null)
                return;

            LastFileDirectory.Set(config, file.DirectoryName);

            Hide();
            onFileSelected(file.FullName);
        }

        protected override void PopIn() => this.FadeIn(150);
        protected override void PopOut() => this.FadeOut(150);
    }
}
