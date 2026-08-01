// Small modal asking for a folder name, then creating it.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// Prompts for a folder name and creates it inside <c>parent</c>, calling <c>onCreated</c> with the
    /// new directory. A name that is empty, contains characters invalid in a file name, already exists,
    /// or cannot be created leaves the dialog open with the reason shown.
    /// </summary>
    public partial class NewFolderDialog : ModalOverlay
    {
        private static readonly Vector2 panel_size = new Vector2(440, 190);

        private readonly DirectoryInfo parent;
        private readonly Action<DirectoryInfo> onCreated;

        private BasicTextBox nameBox = null!;
        private SpriteText errorText = null!;

        public NewFolderDialog(DirectoryInfo parent, Action<DirectoryInfo> onCreated)
        {
            this.parent = parent;
            this.onCreated = onCreated;

            PanelSize = panel_size;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            nameBox = new BasicTextBox
            {
                Name = "new folder name",
                RelativeSizeAxes = Axes.X,
                Height = 30,
                PlaceholderText = "Folder name",
            };

            // Enter inside a text box is consumed by the box to commit, so it never reaches the
            // overlay's own key handling — route the commit to the same action.
            nameBox.OnCommit += (_, _) => Confirm();

            Panel.AddRange(new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = "New Folder",
                    Font = FontUsage.Default.With(size: 20),
                    Colour = Color4.White,
                    Y = 8,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 6),
                    Padding = new MarginPadding { Horizontal = 12, Top = 44 },
                    Children = new Drawable[]
                    {
                        nameBox,
                        errorText = new SpriteText
                        {
                            Name = "new folder error",
                            Colour = Color4.OrangeRed,
                            Alpha = 0,
                        },
                    },
                },
                new DialogFooter("Create", Confirm, Cancel),
            });
        }

        // Typing should go straight into the name box. Redirecting on focus rather than on show is what
        // survives FocusedOverlayContainer's focus contention, which drops focus a frame after PopIn and
        // then hands it back to this overlay.
        protected override void OnFocus(FocusEvent e)
        {
            base.OnFocus(e);

            GetContainingFocusManager()?.ChangeFocus(nameBox);
        }

        protected override void Confirm()
        {
            string name = nameBox.Text.Trim();

            if (name.Length == 0)
            {
                fail("Enter a folder name.");
                return;
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                fail("That name contains characters a folder name cannot use.");
                return;
            }

            string fullPath = Path.Combine(parent.FullName, name);

            if (Directory.Exists(fullPath) || File.Exists(fullPath))
            {
                fail("Something with that name is already here.");
                return;
            }

            DirectoryInfo created;

            try
            {
                created = Directory.CreateDirectory(fullPath);
            }
            catch (Exception e)
            {
                fail(e.Message);
                return;
            }

            Hide();
            onCreated(created);
        }

        private void fail(string reason)
        {
            errorText.Text = reason;
            errorText.Alpha = 1;
            nameBox.FlashColour(Color4.OrangeRed, 400, Easing.OutQuint);
        }
    }
}
