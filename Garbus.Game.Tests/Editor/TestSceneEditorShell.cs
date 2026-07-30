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
using Garbus.Game.Settings;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
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
        private SettingsProbe settingsProbe = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            // Pre-save to a temp path so Save() has a target for TestDirtyTracking.
            var chartFile = new ChartFile(chart);
            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".garbus");
            chartFile.Save(tempPath);

            settingsProbe = new SettingsProbe();
            Child = new SettingsProbeContainer(settingsProbe)
            {
                RelativeSizeAxes = Axes.Both,
                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = new ScreenStack(editor = new GarbusEditor(chartFile)) { RelativeSizeAxes = Axes.Both },
                },
            };
        });

        /// <summary>Records OpenSettings calls — the harness hosts no real settings overlay.</summary>
        private class SettingsProbe : ISettingsOverlayControl
        {
            public bool Opened;
            public void OpenSettings() => Opened = true;
        }

        private partial class SettingsProbeContainer : Container
        {
            private readonly SettingsProbe probe;

            public SettingsProbeContainer(SettingsProbe probe)
            {
                this.probe = probe;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                var deps = new DependencyContainer(base.CreateChildDependencies(parent));
                deps.CacheAs<ISettingsOverlayControl>(probe);
                return deps;
            }
        }

        /// <summary>
        /// The editor opts out of the floating gear (<c>ShowSettingsGear</c> is false) and instead
        /// exposes settings through File → Game settings; clicking that item must invoke the
        /// resolved <see cref="ISettingsOverlayControl"/>.
        /// </summary>
        [Test]
        public void TestSettingsExposedViaFileMenu()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddAssert("editor hides the floating gear", () =>
                !((Garbus.Game.Screens.IAllowSettings)editor).ShowSettingsGear);

            AddStep("click File", () =>
            {
                var fileItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                     .First(i => i.Item.Text.Value.ToString() == "File");
                input.MoveMouseTo(fileItem);
                input.Click(MouseButton.Left);
            });
            AddUntilStep("file dropdown open", () =>
                editor.ChildrenOfType<BasicMenu>().Count(m => m.State == MenuState.Open) >= 2);

            AddStep("click Game settings", () =>
            {
                var item = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                 .First(i => i.Item.Text.Value.ToString() == "Game settings");
                input.MoveMouseTo(item);
                input.Click(MouseButton.Left);
            });
            AddUntilStep("settings overlay open requested", () => settingsProbe.Opened);
        }

        [Test]
        public void TestTabSwitching()
        {
            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddStep("switch to setup", () => editor.Tab.Value = EditorTab.Setup);
            AddUntilStep("setup visible", () => editor.ChildrenOfType<SetupTab>().Single().State.Value == Visibility.Visible);
            AddUntilStep("compose hidden", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Hidden);
        }

        /// <summary>
        /// Regression guard: the top bar must receive positional input in front
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
        /// Regression guard: clicking a menu-bar item must open its dropdown
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
        /// Toggle menu options must render a checkbox reflecting their state. Clicking a
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
        /// draw over the timeline strip above the playfield.
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
            // The tab area is the editor minus the fixed top/bottom bars — assert the relation
            // (fills most of the screen) rather than pinning the bars' tuned heights.
            AddAssert("compose tab fills most of the editor height",
                () => editor.ChildrenOfType<ComposeTab>().Single().DrawHeight,
                () => Is.GreaterThan(editor.DrawHeight * 0.5f));
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
            double capturedTimeRange = 0;
            AddStep("capture zoom + composer time range", () =>
            {
                capturedZoom = timelineStrip().CurrentZoom.Value;
                capturedTimeRange = composer().TimelineTimeRange.Value;
            });

            AddStep("ctrl + wheel up over compose area", () =>
            {
                var compose = editor.ChildrenOfType<ComposeTab>().Single();
                input.MoveMouseTo(compose.ToScreenSpace(new osuTK.Vector2(compose.DrawWidth * 0.5f, compose.DrawHeight * 0.5f)));
                input.PressKey(Key.LControl);
                input.ScrollVerticalBy(1);
                input.ReleaseKey(Key.LControl);
            });

            AddUntilStep("timeline zoomed in", () => timelineStrip().CurrentZoom.Value > capturedZoom + 0.01f);

            // Zooming in shows less time: ComposeTab must sync the composer's visible time range
            // down from the new zoom (the strip→composer wiring, tested through the real ComposeTab).
            AddUntilStep("composer's visible time range shrank", () =>
                composer().TimelineTimeRange.Value < capturedTimeRange - 0.01);
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
                if (display == null || display.Text.ToString() != "New Song")
                    return false;

                // "Greyed out" is the contract — a neutral grey strictly dimmer than white — not any
                // exact shade.
                Colour4 c = display.Colour.TopLeft;
                return c.R == c.G && c.G == c.B && c.R > 0 && c.R < 1;
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
