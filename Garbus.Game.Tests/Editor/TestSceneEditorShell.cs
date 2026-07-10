// Tests for the GarbusEditor shell: tab switching and dirty-state tracking.

using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Verify;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorShell : GarbusTestScene
    {
        private GarbusEditor editor = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            // Pre-save to a temp path so Save() has a target for TestDirtyTracking.
            var chartFile = new ChartFile(chart);
            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".garbus");
            chartFile.Save(tempPath);

            Child = new ScreenStack(editor = new GarbusEditor(chartFile)) { RelativeSizeAxes = Axes.Both };
        });

        [Test]
        public void TestTabSwitching()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddStep("switch to setup", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup visible", () => editor.ChildrenOfType<SetupTab>().Single().State.Value == Visibility.Visible);
            AddUntilStep("compose hidden", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestTabContentHasHeight()
        {
            // Regression guard: tab content must not collapse to 0px.
            // A vertical FillFlowContainer with a RelativeSizeAxes.Both child collapses it to zero;
            // the layout now uses a padded plain Container to avoid this.
            AddUntilStep("compose tab visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddAssert("compose tab has positive height", () => editor.ChildrenOfType<ComposeTab>().Single().DrawHeight > 0);
            // Top bar = 40, bottom bar = 60 → tab area = screen height − 100.
            AddAssert("compose tab height ≈ screen − 100",
                () => editor.ChildrenOfType<ComposeTab>().Single().DrawHeight,
                () => Is.GreaterThan(editor.DrawHeight - 101));
        }

        [Test]
        public void TestVerifyTabHasHeight()
        {
            // Regression guard: VerifyTab content must not collapse to 0px.
            AddStep("switch to verify", () => editor.Tab.Value = EditorTab.Verify);
            AddUntilStep("verify visible", () => editor.ChildrenOfType<VerifyTab>().Single().State.Value == Visibility.Visible);
            AddAssert("verify tab has positive height", () => editor.ChildrenOfType<VerifyTab>().Single().DrawHeight > 0);
            // IssueTable inside should also be drawn.
            AddAssert("issue table visible", () => editor.ChildrenOfType<IssueTable>().Single().DrawHeight > 0);
        }

        [Test]
        public void TestDirtyTracking()
        {
            AddAssert("clean at start", () => !editor.HasUnsavedChanges);
            AddStep("add object", () => editor.EditorChart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 }));
            AddAssert("dirty", () => editor.HasUnsavedChanges);
            AddStep("save", () => editor.Save());
            AddAssert("clean again", () => !editor.HasUnsavedChanges);
        }
    }
}
