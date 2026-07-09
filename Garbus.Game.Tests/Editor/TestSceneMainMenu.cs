// Tests for MainMenuScreen button flows: New Chart, Open Chart, Play; editor exit-dirty dialog.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Dialogs;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneMainMenu : GarbusTestScene
    {
        private ScreenStack stack = null!;

        [SetUp]
        public new void SetUp() => Schedule(() => Child = stack = new ScreenStack(new MainMenuScreen()) { RelativeSizeAxes = Axes.Both });

        [Test]
        public void TestNewChartOpensEditor()
        {
            AddStep("click new chart", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "New Chart").TriggerClick());
            AddUntilStep("editor pushed", () => stack.CurrentScreen is GarbusEditor);
            AddAssert("chart has default timing point", () =>
                ((GarbusEditor)stack.CurrentScreen).ChartFile.Chart.ControlPointInfo.TimingPoints.Any());
        }

        [Test]
        public void TestExitDirtyPrompts()
        {
            AddStep("click new chart", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "New Chart").TriggerClick());
            AddUntilStep("editor pushed and loaded", () => stack.CurrentScreen is GarbusEditor e && e.IsLoaded);
            AddStep("dirty the chart", dirtyEditor);
            AddUntilStep("editor is dirty", () => stack.CurrentScreen is GarbusEditor editor && editor.HasUnsavedChanges);
            AddStep("try exit", () => stack.CurrentScreen.Exit());
            AddUntilStep("dialog shown", () => this.ChildrenOfType<ConfirmDialog>().Any(d => d.State.Value == Visibility.Visible));
        }

        [Test]
        public void TestExitDirtyDiscard()
        {
            AddStep("click new chart", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "New Chart").TriggerClick());
            AddUntilStep("editor pushed and loaded", () => stack.CurrentScreen is GarbusEditor e && e.IsLoaded);
            AddStep("dirty the chart", dirtyEditor);
            AddUntilStep("editor is dirty", () => stack.CurrentScreen is GarbusEditor editor && editor.HasUnsavedChanges);
            AddStep("try exit", () => stack.CurrentScreen.Exit());
            AddUntilStep("dialog shown", () => this.ChildrenOfType<ConfirmDialog>().Any(d => d.State.Value == Visibility.Visible));
            AddStep("click discard", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "Discard").TriggerClick());
            AddUntilStep("editor exited — main menu current", () => stack.CurrentScreen is MainMenuScreen);
        }

        [Test]
        public void TestExitDirtyCancel()
        {
            AddStep("click new chart", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "New Chart").TriggerClick());
            AddUntilStep("editor pushed and loaded", () => stack.CurrentScreen is GarbusEditor e && e.IsLoaded);
            AddStep("dirty the chart", dirtyEditor);
            AddUntilStep("editor is dirty", () => stack.CurrentScreen is GarbusEditor editor && editor.HasUnsavedChanges);
            AddStep("try exit", () => stack.CurrentScreen.Exit());
            AddUntilStep("dialog shown", () => this.ChildrenOfType<ConfirmDialog>().Any(d => d.State.Value == Visibility.Visible));
            AddStep("click cancel", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "Cancel").TriggerClick());
            AddUntilStep("dialog hidden", () => !this.ChildrenOfType<ConfirmDialog>().Any(d => d.State.Value == Visibility.Visible));
            AddAssert("editor still current screen", () => stack.CurrentScreen is GarbusEditor);
        }

        [Test]
        public void TestPlayButtonPushesPlayScreen()
        {
            AddStep("click play", () => this.ChildrenOfType<BasicButton>().Single(b => b.Text == "Play").TriggerClick());
            AddUntilStep("play screen pushed", () => stack.CurrentScreen is PlayScreen);
        }

        // Helper: resolve the current editor and add a CardinalNote to mark it dirty.
        private void dirtyEditor()
        {
            var editor = (GarbusEditor)stack.CurrentScreen;
            editor.EditorChart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 });
        }
    }
}
