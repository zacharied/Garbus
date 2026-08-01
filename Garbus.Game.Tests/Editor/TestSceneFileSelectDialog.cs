using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Dialogs;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneFileSelectDialog : GarbusTestScene
    {
        private string? tempDirectory;

        private ManualInputManager input = null!;
        private KeyCountingContainer host = null!;
        private Container dialogHost = null!;
        private BasicTextBox textBox = null!;
        private FileSelectDialog dialog = null!;

        private readonly List<string> picked = new List<string>();

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            picked.Clear();

            string directory = tempDirectory = Directory.CreateTempSubdirectory("garbus-dialog-").FullName;
            File.WriteAllText(Path.Combine(directory, "visible.garbus"), string.Empty);

            // Dot-prefixed AND attribute-flagged so the file counts as hidden on either platform.
            string hidden = Path.Combine(directory, ".concealed.garbus");
            File.WriteAllText(hidden, string.Empty);
            File.SetAttributes(hidden, FileAttributes.Hidden);

            LastFileDirectory.Set(config, directory);

            // host stands in for the editor shell: an ancestor of the dialog with its own hotkeys.
            Child = input = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = host = new KeyCountingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        // Stands in for a metadata field on the screen behind the dialog.
                        textBox = new BasicTextBox
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Width = 200,
                            Height = 30,
                            Text = "abc",
                        },
                        dialogHost = new Container { RelativeSizeAxes = Axes.Both },
                    },
                },
            };
        });

        [TearDown]
        public void TearDown()
        {
            if (tempDirectory != null && Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }

        [Test]
        public void TestHostReceivesKeysOnlyWhileDialogIsClosed()
        {
            AddStep("press space with no dialog", () => input.Key(Key.Space));
            AddAssert("host saw the key", () => host.KeyCount == 1);

            showDialog();

            AddStep("press editor hotkeys", () =>
            {
                input.Key(Key.Space);
                input.Key(Key.Left);
                input.Key(Key.Z);
            });
            AddAssert("host saw none of them", () => host.KeyCount == 1);

            AddStep("cancel", () => input.Key(Key.Escape));
            AddUntilStep("dialog hidden", () => dialog.State.Value == Visibility.Hidden && !dialog.IsPresent);

            AddStep("press space again", () => input.Key(Key.Space));
            AddAssert("host sees keys once more", () => host.KeyCount == 2);
        }

        [Test]
        public void TestFocusedTextBoxBehindDialogStopsReceivingInput()
        {
            AddStep("focus the text box", () =>
            {
                input.MoveMouseTo(textBox);
                input.Click(MouseButton.Left);
            });
            AddUntilStep("text box has focus", () => textBox.HasFocus);

            // Character entry travels a separate text-input path; backspace is an editing command the
            // text box handles from the key event itself, so it observes the keyboard route.
            AddStep("backspace behind no dialog", () => input.Key(Key.BackSpace));
            AddAssert("text box consumed the key", () => textBox.Text == "ab");

            showDialog();

            AddAssert("text box lost focus", () => !textBox.HasFocus);
            AddStep("backspace with dialog open", () =>
            {
                input.Key(Key.BackSpace);
                input.Key(Key.BackSpace);
            });
            AddAssert("text box unchanged", () => textBox.Text == "ab");
        }

        [Test]
        public void TestEscapeCancelsWithoutPicking()
        {
            showDialog();
            selectFile("visible.garbus");

            AddStep("press escape", () => input.Key(Key.Escape));
            AddUntilStep("dialog hidden", () => dialog.State.Value == Visibility.Hidden);
            AddAssert("nothing picked", () => picked.Count == 0);
        }

        [Test]
        public void TestEnterConfirmsOnlyWithASelection()
        {
            showDialog();

            AddStep("press enter with no selection", () => input.Key(Key.Enter));
            AddAssert("nothing picked", () => picked.Count == 0);
            AddAssert("dialog still visible", () => dialog.State.Value == Visibility.Visible);

            selectFile("visible.garbus");
            AddStep("press enter", () => input.Key(Key.Enter));
            AddUntilStep("selection picked", () => picked.Count == 1);
            AddAssert("picked the clicked file", () => Path.GetFileName(picked[0]) == "visible.garbus");
            AddUntilStep("dialog hidden", () => dialog.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestConfirmButtonPicksSelectedFile()
        {
            showDialog();
            selectFile("visible.garbus");

            AddStep("click confirm", () => confirmButton().TriggerClick());
            AddUntilStep("selection picked", () => picked.Count == 1);
            AddAssert("picked the clicked file", () => Path.GetFileName(picked[0]) == "visible.garbus");
        }

        [Test]
        public void TestCancelButtonDismissesWithoutPicking()
        {
            showDialog();
            selectFile("visible.garbus");

            AddStep("click cancel", () => cancelButton().TriggerClick());
            AddUntilStep("dialog hidden", () => dialog.State.Value == Visibility.Hidden);
            AddAssert("nothing picked", () => picked.Count == 0);
        }

        [Test]
        public void TestShowHiddenCheckboxRevealsHiddenFiles()
        {
            showDialog();

            AddUntilStep("only the visible file is listed", () => listedFileNames().SequenceEqual(new[] { "visible.garbus" }));

            AddStep("check show hidden items", () => showHiddenCheckbox().TriggerClick());
            AddUntilStep("hidden file appears", () => listedFileNames().Contains(".concealed.garbus"));

            AddStep("uncheck show hidden items", () => showHiddenCheckbox().TriggerClick());
            AddUntilStep("hidden file disappears", () => !listedFileNames().Contains(".concealed.garbus"));
        }

        [Test]
        public void TestFooterLayout()
        {
            showDialog();

            AddAssert("cancel sits left of confirm", () =>
                quad(cancelButton()).Right <= quad(confirmButton()).Left);

            AddAssert("both buttons in the panel's bottom-right quadrant", () =>
                inQuadrant(quad(cancelButton()), right: true, bottom: true)
                && inQuadrant(quad(confirmButton()), right: true, bottom: true));

            AddAssert("settings column in the panel's bottom-left quadrant", () =>
                inQuadrant(quad(settingsFlow()), right: false, bottom: true));

            AddAssert("buttons sit below the file listing", () =>
                quad(cancelButton()).Top >= quad(fileSelector()).Bottom
                && quad(confirmButton()).Top >= quad(fileSelector()).Bottom);

            AddAssert("settings column does not overlap the buttons", () =>
                quad(settingsFlow()).Right <= quad(cancelButton()).Left);
        }

        private void showDialog()
        {
            AddStep("show dialog", () =>
            {
                dialogHost.Child = dialog = new FileSelectDialog(new[] { ".garbus" }, "Open", path => picked.Add(path));
                dialog.Show();
            });

            AddUntilStep("listing loaded", () => listedFileNames().Any());
        }

        private void selectFile(string name)
        {
            AddStep($"click {name}", () =>
            {
                input.MoveMouseTo(fileRow(name));
                input.Click(MouseButton.Left);
            });

            AddUntilStep($"{name} is the current file", () => fileSelector().CurrentFile.Value?.Name == name);
        }

        private GarbusFileSelector fileSelector() => dialog.ChildrenOfType<GarbusFileSelector>().Single();

        private BasicButton cancelButton() =>
            dialog.ChildrenOfType<BasicButton>().Single(b => b.Name == DialogFooter.CancelButtonName);

        private BasicButton confirmButton() =>
            dialog.ChildrenOfType<BasicButton>().Single(b => b.Name == DialogFooter.ConfirmButtonName);

        private BasicCheckbox showHiddenCheckbox() =>
            dialog.ChildrenOfType<BasicCheckbox>().Single(c => c.Name == FileSelectDialog.ShowHiddenCheckboxName);

        private Drawable settingsFlow() =>
            dialog.ChildrenOfType<FillFlowContainer>().Single(f => f.Name == DialogFooter.SettingsFlowName);

        private Container panel() =>
            dialog.ChildrenOfType<Container>().Single(c => c.Name == ModalOverlay.PanelName);

        private IEnumerable<string> listedFileNames() =>
            dialog.ChildrenOfType<DirectorySelectorItem>()
                  .SelectMany(item => item.ChildrenOfType<SpriteText>())
                  .Select(text => text.Text.ToString())
                  .Where(name => name.EndsWith(".garbus", System.StringComparison.Ordinal));

        private DirectorySelectorItem fileRow(string name) =>
            dialog.ChildrenOfType<DirectorySelectorItem>()
                  .Single(item => item.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString() == name));

        private static RectangleF quad(Drawable drawable) => drawable.ScreenSpaceDrawQuad.AABBFloat;

        /// <summary>Whether the drawable's centre falls in the named quadrant of the dialog panel.</summary>
        private bool inQuadrant(RectangleF target, bool right, bool bottom)
        {
            var p = quad(panel());
            float centreX = p.Left + p.Width / 2;
            float centreY = p.Top + p.Height / 2;

            return (right ? target.Centre.X > centreX : target.Centre.X < centreX)
                   && (bottom ? target.Centre.Y > centreY : target.Centre.Y < centreY);
        }

        /// <summary>Stands in for the editor shell — an ancestor that binds its own keyboard hotkeys.</summary>
        private partial class KeyCountingContainer : Container
        {
            public int KeyCount { get; private set; }

            protected override bool OnKeyDown(KeyDownEvent e)
            {
                KeyCount++;
                return true;
            }
        }
    }
}
