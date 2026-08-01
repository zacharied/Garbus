// Interactive tuning scene for the file-select dialog chrome: panel proportions, action-button
// block at the bottom-right, and the settings column at the bottom-left. Every visual parameter is
// a slider in the test browser's step sidebar and applies live to the open dialog. The dialog opens
// on the directory last used by a real file dialog, so it lists real files. [Explicit] so it never
// runs in a headless "run all"; pick it in the test browser.

using System.IO;
using System.Linq;
using Garbus.Game.Edit.Screens.Dialogs;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osuTK;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneFileSelectDialogTuning : GarbusTestScene
    {
        private FileSelectDialog? dialog;
        private SpriteText pickReadout = null!;

        // Defaults mirror ModalOverlay's and DialogFooter's current constants; tweak them there once
        // a combination is chosen here.
        private float panelWidth = 700;
        private float panelHeight = 560;
        private float buttonWidth = 100;
        private float buttonHeight = 36;
        private float buttonSpacing = 8;

        public TestSceneFileSelectDialogTuning()
        {
            AddSliderStep("panel width", 400f, 1200f, panelWidth, v => { panelWidth = v; apply(); });
            AddSliderStep("panel height", 300f, 900f, panelHeight, v => { panelHeight = v; apply(); });
            AddSliderStep("button width", 60f, 220f, buttonWidth, v => { buttonWidth = v; apply(); });
            AddSliderStep("button height", 20f, 60f, buttonHeight, v => { buttonHeight = v; apply(); });
            AddSliderStep("button spacing", 0f, 40f, buttonSpacing, v => { buttonSpacing = v; apply(); });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Add(pickReadout = new SpriteText
            {
                Margin = new MarginPadding(8),
                Text = "nothing picked yet",
            });

            openDialog();

            AddStep("reopen dialog", openDialog);
            AddToggleStep("show hidden items", v =>
            {
                var checkbox = showHiddenCheckbox();
                if (checkbox != null)
                    checkbox.Current.Value = v;
            });
        }

        private void openDialog()
        {
            if (dialog != null)
                Remove(dialog, true);

            Add(dialog = new FileSelectDialog(new[] { ".garbus", ".mp3", ".ogg", ".wav" }, "Open",
                path => pickReadout.Text = $"picked: {Path.GetFileName(path)}"));

            dialog.Show();

            // The dialog builds its drawables asynchronously; apply once they exist.
            Schedule(apply);
        }

        private BasicCheckbox? showHiddenCheckbox() =>
            dialog?.ChildrenOfType<BasicCheckbox>().FirstOrDefault(c => c.Name == FileSelectDialog.ShowHiddenCheckboxName);

        private void apply()
        {
            if (dialog?.IsLoaded != true)
                return;

            dialog.PanelSize = new Vector2(panelWidth, panelHeight);

            var footer = dialog.ChildrenOfType<DialogFooter>().FirstOrDefault();
            if (footer == null)
                return;

            footer.ButtonSize = new Vector2(buttonWidth, buttonHeight);
            footer.ButtonSpacing = buttonSpacing;
        }
    }
}
