// Tests for the GarbusEditor shell: tab switching and dirty-state tracking.

using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Verify;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorShell : GarbusTestScene
    {
        private GarbusEditor editor = null!;
        private ManualInputManager input = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            // Pre-save to a temp path so Save() has a target for TestDirtyTracking.
            var chartFile = new ChartFile(chart);
            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".garbus");
            chartFile.Save(tempPath);

            Child = input = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                UseParentInput = false,
                Child = new ScreenStack(editor = new GarbusEditor(chartFile)) { RelativeSizeAxes = Axes.Both },
            };
        });

        [Test]
        public void TestTabSwitching()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddStep("switch to setup", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup visible", () => editor.ChildrenOfType<SetupTab>().Single().State.Value == Visibility.Visible);
            AddUntilStep("compose hidden", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Hidden);
        }

        /// <summary>
        /// Regression guard for Phase4-Issues.md: the top bar must receive positional input in front
        /// of the compose blueprint stack (whose ReceivePositionalInputAt covers the whole screen).
        /// Clicking a tab button with the real input pipeline must switch tabs.
        /// </summary>
        [Test]
        public void TestTabSwitchingViaClick()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);

            clickTabButton(EditorTab.Setup);
            AddAssert("tab is Setup", () => editor.Tab.Value == EditorTab.Setup);
            AddUntilStep("setup visible", () => editor.ChildrenOfType<SetupTab>().Single().State.Value == Visibility.Visible);

            clickTabButton(EditorTab.Verify);
            AddAssert("tab is Verify", () => editor.Tab.Value == EditorTab.Verify);

            clickTabButton(EditorTab.Compose);
            AddAssert("tab is Compose", () => editor.Tab.Value == EditorTab.Compose);
        }

        /// <summary>
        /// Regression guard for Phase4-Issues.md: clicking a menu-bar item must open its dropdown
        /// (in front of the tab content), and clicking a dropdown item must run its action.
        /// Drives File → Save end-to-end and asserts the dirty flag clears.
        /// </summary>
        [Test]
        public void TestFileMenuSaveViaClick()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);

            AddStep("add object", () => editor.EditorChart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 }));
            AddAssert("dirty", () => editor.HasUnsavedChanges);

            AddStep("click File", () =>
            {
                var fileItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                     .First(i => i.Item.Text.Value.ToString() == "File");
                input.MoveMouseTo(fileItem);
                input.Click(MouseButton.Left);
            });

            // The submenu is a second open menu (the top-level bar is always open).
            AddUntilStep("file dropdown open", () =>
                editor.ChildrenOfType<BasicMenu>().Count(m => m.State == MenuState.Open) >= 2);

            AddStep("click Save", () =>
            {
                var saveItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                     .First(i => i.Item.Text.Value.ToString() == "Save");
                input.MoveMouseTo(saveItem);
                input.Click(MouseButton.Left);
            });

            AddUntilStep("clean after menu save", () => !editor.HasUnsavedChanges);
        }

        /// <summary>
        /// ISSUES.md: toggle menu options must render a checkbox reflecting their state. Clicking a
        /// toggle flips its bound state and keeps the menu open for further toggling.
        /// </summary>
        [Test]
        public void TestViewMenuToggleViaClick()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);

            AddStep("click View", () =>
            {
                var viewItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                     .First(i => i.Item.Text.Value.ToString() == "View");
                input.MoveMouseTo(viewItem);
                input.Click(MouseButton.Left);
            });

            AddUntilStep("view dropdown open", () =>
                editor.ChildrenOfType<BasicMenu>().Count(m => m.State == MenuState.Open) >= 2);

            bool initialState = false;
            AddStep("capture state", () => initialState = toggleItem("Show Beat Ticks").State.Value);

            AddStep("click Show Beat Ticks", () =>
            {
                var drawableItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                         .First(i => i.Item.Text.Value.ToString() == "Show Beat Ticks");
                input.MoveMouseTo(drawableItem);
                input.Click(MouseButton.Left);
            });

            AddAssert("state flipped", () => toggleItem("Show Beat Ticks").State.Value == !initialState);
            AddAssert("menu stayed open", () =>
                editor.ChildrenOfType<BasicMenu>().Count(m => m.State == MenuState.Open) >= 2);

            AddStep("click Show Beat Ticks again", () =>
            {
                var drawableItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                         .First(i => i.Item.Text.Value.ToString() == "Show Beat Ticks");
                input.MoveMouseTo(drawableItem);
                input.Click(MouseButton.Left);
            });

            AddAssert("state restored", () => toggleItem("Show Beat Ticks").State.Value == initialState);
        }

        private Edit.ToggleMenuItem toggleItem(string text) =>
            (Edit.ToggleMenuItem)editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                       .First(i => i.Item.Text.Value.ToString() == text).Item;

        private void clickTabButton(EditorTab tab)
        {
            AddStep($"click {tab} tab button", () =>
            {
                var tabItem = editor.ChildrenOfType<BasicTabControl<EditorTab>.BasicTabItem>()
                                    .First(t => t.Value == tab);
                input.MoveMouseTo(tabItem);
                input.Click(MouseButton.Left);
            });
        }

        /// <summary>
        /// The composer subtree must be clipped: the scrolling container positions grid/bar lines
        /// outside the visible window with no masking of its own, so without a masking wrapper they
        /// draw over the timeline strip above the playfield (ISSUES.md).
        /// </summary>
        [Test]
        public void TestComposerIsMasked()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddAssert("composer parent masks", () =>
            {
                var composer = editor.ChildrenOfType<Edit.GarbusHitObjectComposer>().Single();
                return composer.Parent is Container { Masking: true };
            });
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

        /// <summary>
        /// Task 5: the Compose tab reserves a top-right column for <see cref="GarbusBeatDivisorControl"/>,
        /// hosted inside a <see cref="osu.Framework.Graphics.Cursor.PopoverContainer"/> so its
        /// custom-divisor popover (Task 4) has an ancestor to attach to.
        /// </summary>
        [Test]
        public void TestBeatDivisorControlPresentInComposeTab()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddAssert("beat-divisor control exists", () => editor.ChildrenOfType<GarbusBeatDivisorControl>().Any());
            AddAssert("popover container hosts it", () => editor.ChildrenOfType<osu.Framework.Graphics.Cursor.PopoverContainer>().Any());
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

        /// <summary>
        /// Regression guard: right-clicking a selected, hovered hit-object blueprint in the compose
        /// view must open its context menu (Delete, plus per-object items). This only works if the
        /// editor hosts a <see cref="osu.Framework.Graphics.Cursor.ContextMenuContainer"/> — osu's
        /// Editor wraps its whole screen in one; Garbus was missing it, so right-click did nothing.
        /// </summary>
        [Test]
        public void TestRightClickSelectedNoteOpensContextMenu()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);

            AddStep("add note + park clock ahead of it", () =>
            {
                editor.EditorChart.Add(new CardinalNote { StartTime = 4000, AngleDeg = 270 });
                var clock = editor.ChildrenOfType<EditorClock>().First();
                clock.Stop();
                clock.Seek(2000);
            });

            AddUntilStep("drawable exists", () => composer().HitObjects.Any());
            AddStep("switch to select tool", () => input.Key(Key.Number1));

            // The note sits at the judgement line (composer bottom) when the playhead is on it; nudge the
            // clock until it sits in a safe inner band so the mouse can land squarely on the blueprint.
            AddUntilStep("note in hoverable zone", () =>
            {
                var clock = editor.ChildrenOfType<EditorClock>().First();
                var q = composer().ScreenSpaceDrawQuad;
                float y = composer().HitObjects.Single().ScreenSpaceDrawQuad.Centre.Y;
                if (y > q.BottomLeft.Y - 80) { clock.Seek(clock.CurrentTime - 100); return false; }
                if (y < q.TopLeft.Y + 80) { clock.Seek(clock.CurrentTime + 100); return false; }
                return true;
            });

            AddUntilStep("hover blueprint", () =>
            {
                input.MoveMouseTo(composer().HitObjects.Single().ScreenSpaceDrawQuad.Centre);
                return composer().ChildrenOfType<HitObjectSelectionBlueprint>().Any(b => b.IsHovered);
            });
            AddStep("click to select", () => input.Click(MouseButton.Left));
            AddAssert("note selected", () => editor.EditorChart.SelectedHitObjects.Count == 1);

            AddStep("right click the selected note", () =>
            {
                input.MoveMouseTo(composer().HitObjects.Single().ScreenSpaceDrawQuad.Centre);
                input.Click(MouseButton.Right);
            });

            AddAssert("context menu shows Delete", () =>
                editor.ChildrenOfType<Menu.DrawableMenuItem>()
                      .Any(i => i.Item.Text.Value.ToString() == "Delete" && i.IsPresent));
        }

        private GarbusHitObjectComposer composer() => editor.ChildrenOfType<GarbusHitObjectComposer>().Single();

        [Test]
        public void TestDirtyTracking()
        {
            AddAssert("clean at start", () => !editor.HasUnsavedChanges);
            AddStep("add object", () => editor.EditorChart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 }));
            AddAssert("dirty", () => editor.HasUnsavedChanges);
            AddStep("save", () => editor.Save());
            AddAssert("clean again", () => !editor.HasUnsavedChanges);
        }

        /// <summary>
        /// Verifies that exiting the editor disposes the ChartFile (and its cached track store).
        /// The ScreenStack disposes screens when they are exited; this test confirms the Dispose
        /// override on GarbusEditor wires through to ChartFile.Dispose().
        /// </summary>
        [Test]
        public void TestEditorDisposesChartFileOnExit()
        {
            ChartFile capturedChartFile = null!;

            AddStep("capture ChartFile reference", () => capturedChartFile = editor.ChartFile);
            AddAssert("not disposed yet", () => !capturedChartFile.IsDisposed);

            // Exit the editor; the ScreenStack removes and disposes the screen.
            AddStep("exit editor", () => editor.Exit());

            // After the framework pumps a frame the screen is disposed.
            AddUntilStep("ChartFile is disposed", () => capturedChartFile.IsDisposed);
        }

        /// <summary>
        /// Verifies that ChartFile.Dispose() is idempotent — calling it twice does not throw.
        /// </summary>
        [Test]
        public void TestChartFileDisposeIsIdempotent()
        {
            AddStep("dispose ChartFile twice", () =>
            {
                var cf = editor.ChartFile;
                cf.Dispose();
                cf.Dispose(); // must not throw
            });
            AddAssert("IsDisposed is true", () => editor.ChartFile.IsDisposed);
        }
    }
}
