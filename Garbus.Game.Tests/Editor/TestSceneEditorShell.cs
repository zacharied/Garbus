// Tests for the GarbusEditor shell: tab switching and dirty-state tracking.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Dialogs;
using Garbus.Game.Edit.Screens.Verify;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
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
        private ScreenStack stack = null!;

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
                Child = stack = new ScreenStack(editor = new GarbusEditor(chartFile)) { RelativeSizeAxes = Axes.Both },
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

            openViewMenu();

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

        [Test]
        public void TestPreviewModesViaViewMenu()
        {
            InlineChartPreviewPanel panel = null!;
            CardinalNote note = null!;

            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            openViewMenu();

            AddAssert("mini preview checkbox exists", () => menuItem("Mini Preview"), () => Is.TypeOf<ToggleMenuItem>());
            AddAssert("mini preview checked by default", () => toggleItem("Mini Preview").State.Value);
            AddAssert("no preview submenu", () => editor.ChildrenOfType<Menu.DrawableMenuItem>()
                .All(i => i.Item.Text.Value.ToString() is not "Preview" and not "Hide" and not "Mini"));
            AddAssert("mini panel visible in compose", () =>
                panel.Alpha, () => Is.EqualTo(1));

            AddStep("add object before disabling mini preview", () =>
                editor.EditorChart.Add(note = new CardinalNote { StartTime = 1000, AngleDeg = 90 }));
            AddUntilStep("mini receives object before disabling", () => panel.ViewForTests.ObjectCountForTests == 1);

            clickMenuItem("Mini Preview");
            AddUntilStep("checkbox disables mini", () =>
                !toggleItem("Mini Preview").State.Value
                && panel.Alpha == 0);
            AddStep("remove object while mini disabled", () => editor.EditorChart.Remove(note));
            AddAssert("closed mini ignores edit while disabled", () => panel.ViewForTests.ObjectCountForTests, () => Is.EqualTo(1));

            clickMenuItem("Mini Preview");
            AddUntilStep("checkbox reopens mini with current state", () =>
                toggleItem("Mini Preview").State.Value
                && panel.Alpha == 1
                && panel.ViewForTests.ObjectCountForTests == 0);
        }

        [Test]
        public void TestMiniClosesAndResyncsAcrossInactiveModes()
        {
            InlineChartPreviewPanel panel = null!;
            CardinalNote note = null!;

            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);

            AddStep("add object before disabling mini preview", () =>
                editor.EditorChart.Add(note = new CardinalNote { StartTime = 1000, AngleDeg = 90 }));
            AddUntilStep("mini receives object before disabling", () => panel.ViewForTests.ObjectCountForTests == 1);

            setMiniPreviewEnabledStep(false);
            AddUntilStep("disabling closes mini", () => panel.Alpha == 0);
            AddStep("remove object while hidden", () => editor.EditorChart.Remove(note));
            AddAssert("closed mini ignores edit while disabled", () => panel.ViewForTests.ObjectCountForTests, () => Is.EqualTo(1));

            setMiniPreviewEnabledStep(true);
            AddUntilStep("enabling resyncs mini", () => panel.Alpha == 1 && panel.ViewForTests.ObjectCountForTests == 0);
        }

        [Test]
        public void TestMiniPreviewSupportsSharedSongTiming()
        {
            InlineChartPreviewPanel panel = null!;

            AddStep("load shared-timing song", () =>
            {
                GarbusSong song = GarbusSong.CreateDefault();
                song.Charts[0].HitObjects.Add(new CardinalNote { StartTime = 1000, AngleDeg = 90 });

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = stack = new ScreenStack(editor = new GarbusEditor(new SongFile(song)))
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            });

            AddUntilStep("shared-timing mini loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            AddUntilStep("shared-timing mini remains active", () =>
                panel.Alpha == 1 && panel.ViewForTests.ObjectCountForTests == 1);
        }

        [Test]
        public void TestMiniPreviewResyncsAndTracksSelectedChartStructure()
        {
            InlineChartPreviewPanel panel = null!;
            GarbusChart replacement = null!;

            AddStep("load two-chart song", () =>
            {
                var firstTiming = new ControlPointInfo();
                firstTiming.Add(0, new TimingControlPoint { BeatLength = 500 });
                var first = new GarbusChart
                {
                    ControlPointInfo = firstTiming,
                    HitObjects = [new CardinalNote { StartTime = 1000, AngleDeg = 90 }],
                };

                var replacementTiming = new ControlPointInfo();
                replacementTiming.Add(500, new TimingControlPoint { BeatLength = 300 });
                replacement = new GarbusChart
                {
                    ControlPointInfo = replacementTiming,
                    HitObjects = [new CardinalNote { StartTime = 3000, AngleDeg = 180 }],
                };
                replacement.DesignPointInfo.Add(new TutorialMessage
                {
                    StartTime = 0,
                    EndTime = 10000,
                    Text = "replacement",
                });

                var song = new GarbusSong
                {
                    ControlPointInfo = null,
                    Charts = [first, replacement],
                };

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = stack = new ScreenStack(editor = new GarbusEditor(new SongFile(song)))
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            });

            AddUntilStep("two-chart mini loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true
                && panel.ViewForTests.ObjectCountForTests == 1);
            AddStep("select replacement chart", () => editor.EditorSong.SelectChart(replacement.ChartId));
            AddUntilStep("replacement mini state applied", () =>
                panel.ViewForTests.PlayfieldForTests.AllHitObjects.SingleOrDefault()?.HitObject.StartTime == 3000
                && panel.ViewForTests.DesignOverlayForTests.MessageTextForTests == "replacement");

            AddStep("change replacement design", () =>
            {
                replacement.DesignPointInfo.Clear();
                replacement.DesignPointInfo.Add(new TutorialMessage
                {
                    StartTime = 0,
                    EndTime = 10000,
                    Text = "updated replacement",
                });
            });
            AddUntilStep("replacement mini structure remains subscribed", () =>
                panel.ViewForTests.DesignOverlayForTests.MessageTextForTests == "updated replacement");
        }

        [Test]
        public void TestOnSuspendingClosesAndRestoresMiniPreview()
        {
            InlineChartPreviewPanel panel = null!;
            long revisionBeforeSuspension = 0;
            var reopenedFullStates = new List<ChartPreviewFullState>();

            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            AddStep("capture mini lifecycle state", () =>
            {
                revisionBeforeSuspension = panel.ViewForTests.AcceptedRevisionForTests;
                panel.ViewForTests.FullStateReceivedForTests += reopenedFullStates.Add;
            });
            AddStep("push unrelated screen", () => stack.Push(new PreviewSuspendingScreen()));
            AddUntilStep("unrelated screen pushed", () => stack.CurrentScreen is PreviewSuspendingScreen);
            AddAssert("suspension disables mini preview", () => !editor.MiniPreviewEnabled.Value);
            AddAssert("suspension closes mini", () => panel.Alpha, () => Is.Zero);
            AddStep("edit chart while mini suspended", () =>
                editor.EditorChart.Add(new CardinalNote { StartTime = 2000, AngleDeg = 180 }));
            AddAssert("closed mini ignores suspended edit", () => panel.ViewForTests.ObjectCountForTests, () => Is.Zero);

            AddStep("exit unrelated screen", () => stack.CurrentScreen.Exit());
            AddUntilStep("editor resumed", () => ReferenceEquals(stack.CurrentScreen, editor));
            AddUntilStep("mini restored with current state", () =>
                editor.MiniPreviewEnabled.Value
                && panel.Alpha == 1
                && panel.ViewForTests.ObjectCountForTests == 1
                && reopenedFullStates.Count == 1);
            AddAssert("restored state advances revision", () => reopenedFullStates[0].Revision,
                () => Is.GreaterThan(revisionBeforeSuspension));
        }

        [Test]
        public void TestMiniPreviewFailureDisablesCheckboxAndShowsDialog()
        {
            InlineChartPreviewPanel panel = null!;

            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            AddStep("fail mini preview", () =>
                typeof(InlineChartPreviewPanel)
                    .GetMethod("onPreviewFailed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(panel, new object[] { "Fake mini failure." }));
            AddUntilStep("one failure dialog", () =>
                editor.ChildrenOfType<ConfirmDialog>().Count(d => d.State.Value == Visibility.Visible) == 1);
            AddAssert("failure disables mini preview", () => !editor.MiniPreviewEnabled.Value);
            AddAssert("dialog uses Mini terminology", () =>
                editor.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString().Contains("Mini preview failed")));
        }

        [Test]
        public void TestInlinePreviewContentDisposesWithEditorHierarchy()
        {
            ChartPreviewContent content = null!;
            ChartFile chartFile = null!;
            bool disposedBeforeEditorCleanup = false;
            int disposeCount = 0;

            AddUntilStep("inline preview content loaded", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()?.ViewForTests.IsLoaded == true);
            AddStep("capture inline content and chart file", () =>
            {
                content = editor.ChildrenOfType<InlineChartPreviewPanel>().Single().ViewForTests;
                chartFile = editor.ChartFile;
                Action onDispose = () =>
                {
                    disposeCount++;
                    disposedBeforeEditorCleanup = !chartFile.IsDisposed;
                };
                typeof(Drawable).GetEvent("OnDispose",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                    .GetAddMethod(true)!
                    .Invoke(content, new object[] { onDispose });
            });
            AddStep("dispose editor hierarchy", () => input.Child = new Container());
            AddUntilStep("editor and inline content disposed", () => chartFile.IsDisposed && isDisposed(content));
            AddAssert("inline content disposed once within editor cleanup", () =>
            {
                return disposeCount == 1 && disposedBeforeEditorCleanup;
            });
        }

        private void openViewMenu()
        {
            AddStep("click View", () =>
            {
                var viewItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                     .First(i => i.Item.Text.Value.ToString() == "View");
                input.MoveMouseTo(viewItem);
                input.Click(MouseButton.Left);
            });

            AddUntilStep("view dropdown open", () =>
                editor.ChildrenOfType<BasicMenu>().Count(m => m.State == MenuState.Open) >= 2);
        }

        private void clickMenuItem(string text)
        {
            AddStep($"click {text}", () =>
            {
                var drawableItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                         .First(i => i.Item.Text.Value.ToString() == text);
                input.MoveMouseTo(drawableItem);
                input.Click(MouseButton.Left);
            });
        }

        private MenuItem menuItem(string text) =>
            editor.ChildrenOfType<Menu.DrawableMenuItem>()
                  .First(i => i.Item.Text.Value.ToString() == text).Item;

        private Edit.ToggleMenuItem toggleItem(string text) => (Edit.ToggleMenuItem)menuItem(text);

        private void setMiniPreviewEnabledStep(bool enabled) =>
            AddStep($"set mini preview enabled to {enabled}", () => editor.MiniPreviewEnabled.Value = enabled);

        private static bool isDisposed(Drawable drawable) =>
            (bool)typeof(Drawable).GetProperty("IsDisposed",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                .GetValue(drawable)!;

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

            // Regression guard: the 35px zoom column + 120px divisor column must actually be reserved
            // beside the timeline, not just present-somewhere-in-the-tree — a regression that zeroed a
            // reserved column or reverted TimelineStrip to full-width would otherwise still pass.
            AddUntilStep("timeline is narrower than the editor", () =>
            {
                var timelineStrip = editor.ChildrenOfType<ComposeTab>().Single()
                                          .ChildrenOfType<Edit.Screens.Timeline.TimelineStrip>().SingleOrDefault();
                return timelineStrip != null
                       && timelineStrip.DrawWidth > 0
                       && timelineStrip.DrawWidth < editor.DrawWidth - 100;
            });
        }

        /// <summary>
        /// Ctrl+scroll over the main compose area (below the waveform strip) must zoom the timeline,
        /// mirroring the strip's own ctrl+scroll behaviour. Without ComposeTab forwarding the event to
        /// its <see cref="Edit.Screens.Timeline.TimelineStrip"/>, the scroll bubbles up to
        /// GarbusEditor.OnScroll, which ignores ctrl — so nothing zooms.
        /// </summary>
        [Test]
        public void TestCtrlScrollOverComposeAreaZoomsTimeline()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddUntilStep("timeline zoom set up", () => timelineStrip().CurrentZoom.Value > 1);

            float capturedZoom = 0;
            AddStep("capture zoom", () => capturedZoom = timelineStrip().CurrentZoom.Value);

            AddStep("ctrl + wheel up over compose area", () =>
            {
                var compose = editor.ChildrenOfType<ComposeTab>().Single();
                input.MoveMouseTo(compose.ToScreenSpace(new osuTK.Vector2(compose.DrawWidth * 0.5f, compose.DrawHeight * 0.5f)));
                input.PressKey(Key.LControl);
                input.ScrollVerticalBy(1);
                input.ReleaseKey(Key.LControl);
            });

            AddUntilStep("timeline zoomed in", () => timelineStrip().CurrentZoom.Value > capturedZoom + 0.01f);
        }

        private Edit.Screens.Timeline.TimelineStrip timelineStrip() =>
            editor.ChildrenOfType<ComposeTab>().Single()
                  .ChildrenOfType<Edit.Screens.Timeline.TimelineStrip>().Single();

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
        public void TestPlaybackPlaysHitsoundInFullEditor()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);

            AddStep("add note + seek before it", () =>
            {
                editor.EditorChart.Add(new CardinalNote { StartTime = 3000, AngleDeg = 270 });
                var clock = editor.ChildrenOfType<EditorClock>().First();
                clock.Stop();
                clock.Seek(2000);
            });
            AddUntilStep("drawable exists", () => composer().HitObjects.Any());

            AddStep("start playback", () => editor.ChildrenOfType<EditorClock>().First().Start());
            AddUntilStep("clock passes note", () => editor.ChildrenOfType<EditorClock>().First().CurrentTime > 3200);
            AddStep("stop playback", () => editor.ChildrenOfType<EditorClock>().First().Stop());

            AddAssert("a hitsound played", () =>
                editor.ChildrenOfType<Garbus.Game.Gameplay.Audio.HitSoundContainer>().Sum(c => c.PlayCount) >= 1);

            // The real check: a sample must actually be LOADED (resolved from the store), otherwise
            // Play() increments PlayCount but emits no audio.
            AddAssert("a sample was actually loaded", () =>
                editor.ChildrenOfType<Garbus.Game.Gameplay.Audio.HitSoundContainer>().Sum(c => c.LoadedCount) >= 1);
        }

        private partial class PreviewSuspendingScreen : Screen
        {
        }

        /// <summary>
        /// Regression guard: SpriteText.IsPresent is false while Text is empty, and Drawable.Update()
        /// is skipped entirely while !IsPresent — a dynamically-computed label starting from empty
        /// text would otherwise never run the Update() that assigns it (permanent deadlock) without
        /// AlwaysPresent.
        /// </summary>
        [Test]
        public void TestChartTitleDisplayShowsFormattedTitle()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);

            AddAssert("empty ChartName falls back to difficulty name (chart pre-saved by SetUp)", () =>
                editor.ChildrenOfType<ChartTitleDisplay>().Single().Text.ToString() == " [Novice Lv??]");

            AddStep("set title/chartname/level", () =>
            {
                editor.EditorSong.Song.Metadata.Title = "Song";
                editor.EditorChart.Metadata.ChartName = "Hard";
                editor.EditorChart.Metadata.Level = 5;
            });
            AddUntilStep("shows full formatted text", () =>
                editor.ChildrenOfType<ChartTitleDisplay>().Single().Text.ToString() == "Song [Hard Lv5]");
        }

        [Test]
        public void TestChartTitleDisplayShowsNewChartWhenUnsaved()
        {
            GarbusEditor unsavedEditor = null!;

            AddStep("push editor with unsaved chart", () =>
            {
                var chart = new GarbusChart();
                chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                chart.Metadata.Title = "Song"; // even with metadata set, unsaved takes priority.

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = new ScreenStack(unsavedEditor = new GarbusEditor(new ChartFile(chart))) { RelativeSizeAxes = Axes.Both },
                };
            });

            AddUntilStep("shows New Song, greyed out", () =>
            {
                var display = unsavedEditor.ChildrenOfType<ChartTitleDisplay>().SingleOrDefault();
                return display != null && display.Text.ToString() == "New Song"
                       && display.Colour == (osu.Framework.Graphics.Colour4)new osuTK.Graphics.Color4(140, 140, 140, 255);
            });
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
