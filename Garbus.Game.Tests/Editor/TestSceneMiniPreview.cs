// The editor Mini preview: a non-interactive autoHit playfield hosted over the compose workspace,
// mirroring the editor's live hit objects on a clock slaved to the EditorClock.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Configuration;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Screens;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Utils;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneMiniPreview : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        [Resolved]
        private GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        [Test]
        public void TestNonInteractivePlayfieldInstallsNoInput()
        {
            GarbusPlayfield preview = null!;
            AddStep("add non-interactive playfield", () =>
                Child = preview = new GarbusPlayfield(interactive: false) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => preview.IsLoaded);
            AddAssert("no analog input manager", () => !preview.ChildrenOfType<AnalogInputManager>().Any());
            AddAssert("no stick indicators", () => !preview.ChildrenOfType<StickIndicator>().Any());
        }

        [Test]
        public void TestInteractivePlayfieldStillInstallsInput()
        {
            GarbusPlayfield gameplay = null!;
            AddStep("add interactive playfield", () =>
                Child = gameplay = new GarbusPlayfield(interactive: true) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => gameplay.IsLoaded);
            AddAssert("has analog input manager", () => gameplay.ChildrenOfType<AnalogInputManager>().Any());
            AddAssert("has two stick indicators", () => gameplay.ChildrenOfType<StickIndicator>().Count() == 2);
        }

        [Test]
        public void TestPreviewMirrorsEditorHitObjects()
        {
            MiniPreviewTestHost host = null!;
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);

            AddAssert("preview has a drawable per editor object", () =>
                host.Preview.PlayfieldForTests.AllHitObjects.Count() == host.EditorChart.HitObjects.Count);

            int before = 0;
            AddStep("count before add", () => before = host.Preview.PlayfieldForTests.AllHitObjects.Count());
            AddStep("add a note to the editor", () => host.AddNote(9000));
            AddUntilStep("preview reflects the add", () =>
                host.Preview.PlayfieldForTests.AllHitObjects.Count() == before + 1);

            AddStep("remove the note from the editor", () => host.RemoveLastAddedNote());
            AddUntilStep("preview reflects the remove", () =>
                host.Preview.PlayfieldForTests.AllHitObjects.Count() == before);
        }

        [Test]
        public void TestLiveEditRefreshesDrawableInPlace()
        {
            MiniPreviewTestHost host = null!;
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);
            AddStep("add a note", () => host.AddNote(9000));

            DrawableHitObject? drawable() =>
                host.Preview.PlayfieldForTests.AllHitObjects.FirstOrDefault(d => d.HitObject.StartTime == 9000 || d.HitObject.StartTime == 9500);

            AddUntilStep("drawable present", () => drawable() != null);
            DrawableHitObject captured = null!;
            AddStep("capture drawable instance", () => captured = drawable()!);
            AddStep("move the note in the editor", () => host.MoveLastAddedNoteTo(9500));
            AddUntilStep("same drawable instance retained (in-place refresh)",
                () => host.Preview.PlayfieldForTests.AllHitObjects.Contains(captured) && captured.HitObject.StartTime == 9500);
        }

        [Test]
        public void TestPreviewAutoHitStillHitsAfterLiveEdit()
        {
            MiniPreviewTestHost host = null!;
            AddStep("pin the scroll time range", () => scrollingInfo.TimeRange.Value = 700);
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);
            AddStep("add a note", () => host.AddNote(2000));

            DrawableHitObject? drawable() =>
                host.Preview.PlayfieldForTests.AllHitObjects.FirstOrDefault(d => d.HitObject.StartTime == 2500);

            AddStep("move the note in the editor (live edit)", () => host.MoveLastAddedNoteTo(2500));
            AddUntilStep("drawable present at new time", () => drawable() != null);

            // Alive window (GarbusScrollingHitObjectContainer.setComputedLifetime) is
            // [StartTime - TimeRange, GetEndTime() + TimeRange] = [1800, 3200]; the 350ms hit fade
            // completes at 2850. Seek into the gap between fade-complete and lifetime end so a stuck
            // (never re-armed) drawable is distinguishable from one that correctly re-forced Hit after
            // the DefaultsApplied re-apply triggered by EditorChart.Update.
            AddStep("seek past the (moved) hit + animation", () => host.EditorClock.Seek(3050));
            AddUntilStep("autoHit animation still plays after the live edit (faded out)", () =>
                drawable() != null && drawable()!.ChildrenOfType<Sprite>().First().Alpha < 0.05f);
        }

        [Test]
        public void TestPreviewHoldExitUnderRealPlayback()
        {
            // All prior hold tests used Seek (jumps). This one PLAYS the editor clock forward in real time
            // through the hold — the actual preview scenario — and checks the head is fading right after
            // EndTime rather than sitting static.
            MiniPreviewTestHost host = null!;
            AddStep("pin the scroll time range", () => scrollingInfo.TimeRange.Value = 752);
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);
            AddStep("add a cardinal hold 2000..3000", () => host.AddHold(2000, 1000));

            DrawableCardinalHoldNote hold() =>
                host.Preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalHoldNote>().Single();
            PersistentSprite head() => hold().ChildrenOfType<PersistentSprite>().First();

            AddStep("seek before hold and play", () =>
            {
                host.EditorClock.Seek(1500);
                host.EditorClock.Start();
            });
            AddUntilStep("played to just past EndTime", () => host.EditorClock.CurrentTime >= 3080);
            AddStep("stop", () => host.EditorClock.Stop());
            // Regression: under real forward playback the head-pop (OnHeadHit, fired at StartTime) used to
            // prune the load-forced exit chain on the head sprite, leaving it static. A Seek/jump hides this
            // because OnHeadHit then fires after the exit's start. The body fades either way; assert the head.
            AddAssert("head plays its exit under real playback (not static)", () => head().Alpha < 0.85f);
        }

        [Test]
        public void TestChordNotesTintedYellowInPreview()
        {
            // Root cause of the reported bug: MiniPreview never fed the playfield's ChordHighlighter
            // (no SetHitObjects call), so IsInChord was always false and chord notes rendered white.
            MiniPreviewTestHost host = null!;
            AddStep("pin the scroll time range", () => scrollingInfo.TimeRange.Value = 700);
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);

            AddStep("place a two-note chord at 2000ms", () =>
            {
                host.AddCardinal(2000, 90);
                host.AddCardinal(2000, 270);
            });
            AddStep("seek so the chord is alive (pre-hit)", () => host.EditorClock.Seek(1900));

            DrawableCardinalNote cardinalAt(int angle) =>
                host.Preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().Single(d => d.HitObject.AngleDeg == angle);

            AddUntilStep("both chord notes alive", () =>
                host.Preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().Count(d => d.IsAlive) == 2);
            AddUntilStep("first chord note yellow", () => cardinalAt(90).Colour.Equals((ColourInfo)ChordColours.Highlight));
            AddUntilStep("second chord note yellow", () => cardinalAt(270).Colour.Equals((ColourInfo)ChordColours.Highlight));
        }

        [Test]
        public void TestLiveChordFormationRetintsExistingNote()
        {
            // A lone note starts white; adding a same-time neighbour forms a chord and must retint the
            // ALREADY-SPAWNED note yellow live (rebuild the highlighter on edit + per-frame recompute
            // under autoHit — PrepareForUse alone fires once and would leave the existing note white).
            MiniPreviewTestHost host = null!;
            AddStep("pin the scroll time range", () => scrollingInfo.TimeRange.Value = 700);
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);

            AddStep("place a lone note at 2000ms", () => host.AddCardinal(2000, 90));
            AddStep("seek so it is alive (pre-hit)", () => host.EditorClock.Seek(1900));

            DrawableCardinalNote cardinalAt(int angle) =>
                host.Preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().Single(d => d.HitObject.AngleDeg == angle);

            AddUntilStep("lone note alive and white", () =>
                cardinalAt(90).IsAlive && cardinalAt(90).Colour.Equals((ColourInfo)Colour4.White));

            AddStep("add a same-time neighbour (forms a chord)", () => host.AddCardinal(2000, 270));
            AddUntilStep("the pre-existing note retints yellow live", () =>
                cardinalAt(90).Colour.Equals((ColourInfo)ChordColours.Highlight));
        }

        [Test]
        public void TestSliderSideRecoloursLiveInPreview()
        {
            // A slider's side colour was baked into the DrawableSliderBody once in its constructor. The
            // preview shares the editor's live instance and only re-Apply()s it on edit (never rebuilds),
            // so flipping Side in the editor left the preview tube the old colour. It must retint live,
            // mirroring the editor's own SliderPolylineVisual.
            MiniPreviewTestHost host = null!;
            AddStep("pin the scroll time range", () => scrollingInfo.TimeRange.Value = 700);
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);

            AddStep("add a left-side slider at 2000ms", () => host.AddSlider(2000, HorizontalDirection.Left));
            AddStep("seek so the slider is alive (pre-hit)", () => host.EditorClock.Seek(1900));

            DrawableSliderBody body() =>
                host.Preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().Single();

            AddUntilStep("slider alive and left-coloured", () =>
                body().IsAlive && body().SideColourForTests.Equals(Constants.LeftColour));

            AddStep("flip the slider to the right side (live edit)", () => host.FlipLastSliderSide());
            AddUntilStep("slider retints to the right colour live", () =>
                body().SideColourForTests.Equals(Constants.RightColour));
        }

        [Test]
        public void TestContentScaledDownToFitPanel()
        {
            // The reported bug: hit-object sprites (fixed 80px) were not scaled down, swamping the small
            // ring. The preview must render the playfield at a gameplay-faithful reference size and scale
            // it uniformly to fit the panel, so a 190px panel shows ~gameplay proportions.
            MiniPreviewTestHost host = null!;
            AddStep("host the preview in a panel-sized (190) box", () =>
                Child = host = new MiniPreviewTestHost(new Vector2(InlineChartPreviewPanel.SIZE)));
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);

            AddUntilStep("content scaled well below 1:1", () =>
                Precision.AlmostEquals(host.Preview.ContentScaleForTests, InlineChartPreviewPanel.SIZE / MiniPreview.ReferenceSize, 0.02f)
                && host.Preview.ContentScaleForTests < 0.5f);
        }

        [Test]
        public void TestDisposingPreviewReleasesLifetimeRefreshSubscriptions()
        {
            // Pins the Task 9 review fix: GarbusScrollingHitObjectContainer.Add subscribes
            // HitObject.DefaultsApplied per entry, previously unsubscribed only on explicit
            // per-object Remove — never on container disposal. MiniPreview shares the editor's
            // long-lived HitObject instances (no clone), so a disposed-but-still-subscribed handler
            // would otherwise leak the whole preview subtree forever.
            //
            // A cardinal note is routed to its own Lane's container (Ring.laneFor), not Ring's own
            // ScrollingContainer, so every GarbusScrollingHitObjectContainer in the nested-playfield
            // tree (ring + lanes) must be swept, not just the ring's.
            MiniPreviewTestHost host = null!;
            List<GarbusScrollingHitObjectContainer> containers = null!;

            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);
            AddStep("add a note", () => host.AddNote(9000));
            AddStep("capture every scrolling container in the preview's playfield tree", () =>
                containers = allPlayfields(host.Preview.PlayfieldForTests)
                    .Select(p => p.HitObjectContainer)
                    .OfType<GarbusScrollingHitObjectContainer>()
                    .ToList());

            AddAssert("handlers registered while alive", () => containers.Sum(c => c.LifetimeRefreshHandlerCountForTests) > 0);

            AddStep("dispose the preview host", () => Remove(host, true));

            AddAssert("handlers released on disposal", () => containers.Sum(c => c.LifetimeRefreshHandlerCountForTests) == 0);

            static IEnumerable<Playfield> allPlayfields(Playfield root)
            {
                yield return root;

                foreach (var nested in root.NestedPlayfields)
                foreach (var descendant in allPlayfields(nested))
                    yield return descendant;
            }
        }

        [Test]
        public void TestPanelClampsWithinParent()
        {
            InlineChartPreviewPanelHarness harness = null!;
            AddStep("host panel in a small workspace", () =>
                Child = harness = new InlineChartPreviewPanelHarness(new Vector2(400)) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => harness.Panel.IsLoaded);
            AddStep("show panel", () => harness.Panel.SetVisible(true));
            AddStep("shove offset far past the edge", () => harness.Panel.SetOffsetForTests(new Vector2(10000)));
            AddUntilStep("panel stays inside the workspace", () =>
            {
                var pos = harness.Panel.ToSpaceOfOtherDrawable(Vector2.Zero, harness.Workspace);
                return pos.X >= -0.5f && pos.Y >= -0.5f
                    && pos.X + harness.Panel.DrawWidth <= harness.Workspace.DrawWidth + 0.5f
                    && pos.Y + harness.Panel.DrawHeight <= harness.Workspace.DrawHeight + 0.5f;
            });
        }

        [Test]
        public void TestPanelPersistsOffsetOnDragEnd()
        {
            InlineChartPreviewPanelHarness harness = null!;

            AddStep("reset config to defaults", () =>
            {
                config.SetValue(GarbusSetting.MiniPreviewX, 5f);
                config.SetValue(GarbusSetting.MiniPreviewY, 5f);
            });
            AddStep("host panel with manual input", () =>
                Child = harness = new InlineChartPreviewPanelHarness(new Vector2(600)) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => harness.Panel.IsLoaded);
            AddStep("show panel", () => harness.Panel.SetVisible(true));

            // Drag the panel up-and-left by a known amount: offset (distance from the right/bottom
            // edges) grows by exactly the negated mouse delta (InlineChartPreviewPanel.OnDrag).
            var dragDelta = new Vector2(-53, -37);
            Vector2 startOffset = default;

            AddStep("capture starting offset", () => startOffset = harness.Panel.OffsetForTests);
            AddStep("press on the panel", () =>
            {
                harness.Input.MoveMouseTo(harness.Panel.ScreenSpaceDrawQuad.Centre);
                harness.Input.PressButton(MouseButton.Left);
            });
            AddStep("drag", () => harness.Input.MoveMouseTo(harness.Panel.ScreenSpaceDrawQuad.Centre + dragDelta));
            AddStep("release (fires OnDragEnd)", () => harness.Input.ReleaseButton(MouseButton.Left));

            Vector2 expectedOffset() => startOffset - dragDelta;

            AddAssert("in-memory offset reflects the drag (not still default)", () =>
                !Precision.AlmostEquals(harness.Panel.OffsetForTests.X, startOffset.X, 0.5f)
                && Precision.AlmostEquals(harness.Panel.OffsetForTests.X, expectedOffset().X, 1f)
                && Precision.AlmostEquals(harness.Panel.OffsetForTests.Y, expectedOffset().Y, 1f));

            AddAssert("config was written with the real dragged offset", () =>
                Precision.AlmostEquals(config.Get<float>(GarbusSetting.MiniPreviewX), harness.Panel.OffsetForTests.X, 0.01f)
                && Precision.AlmostEquals(config.Get<float>(GarbusSetting.MiniPreviewY), harness.Panel.OffsetForTests.Y, 0.01f));

            InlineChartPreviewPanel freshPanel = null!;
            AddStep("construct a fresh panel reading the same config", () => Add(freshPanel = new InlineChartPreviewPanel()));
            AddUntilStep("fresh panel loaded", () => freshPanel.IsLoaded);
            AddAssert("fresh panel reads back the persisted offset", () =>
                Precision.AlmostEquals(freshPanel.OffsetForTests.X, expectedOffset().X, 1f)
                && Precision.AlmostEquals(freshPanel.OffsetForTests.Y, expectedOffset().Y, 1f));
        }

        // ------------------------------------------------------------------
        // GarbusEditor-level wiring: enable toggle, Compose-only visibility gating, and
        // suspend/restore around Test mode (Task 8). Hosts a real GarbusEditor on a ScreenStack,
        // modelled on TestSceneEditorShell's setup.
        // ------------------------------------------------------------------

        private static GarbusEditor buildEditor()
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            var chartFile = new ChartFile(chart);
            chartFile.Save(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".garbus"));

            return new GarbusEditor(chartFile);
        }

        [Test]
        public void TestPreviewVisibleOnlyInComposeWhenEnabled()
        {
            GarbusEditor editor = null!;
            AddStep("create editor", () =>
                Child = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = new ScreenStack(editor = buildEditor()) { RelativeSizeAxes = Axes.Both },
                });
            AddUntilStep("editor loaded", () => editor?.IsLoaded == true && editor.ChildrenOfType<ComposeTab>().Any());

            InlineChartPreviewPanel panel() => editor.ChildrenOfType<InlineChartPreviewPanel>().Single();

            AddAssert("on Compose + enabled → visible", () => editor.Tab.Value == EditorTab.Compose && panel().Alpha > 0);
            AddStep("switch to Timing tab", () => editor.Tab.Value = EditorTab.Timing);
            AddUntilStep("hidden off Compose", () => panel().Alpha == 0);
            AddStep("back to Compose", () => editor.Tab.Value = EditorTab.Compose);
            AddUntilStep("visible again", () => panel().Alpha > 0);
            AddStep("disable via toggle", () => editor.MiniPreviewEnabled.Value = false);
            AddUntilStep("hidden when disabled", () => panel().Alpha == 0);
        }

        [Test]
        public void TestViewMenuMiniPreviewToggleFlipsBinding()
        {
            GarbusEditor editor = null!;
            ManualInputManager input = null!;
            AddStep("create editor", () =>
                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = new ScreenStack(editor = buildEditor()) { RelativeSizeAxes = Axes.Both },
                });
            AddUntilStep("editor loaded", () => editor?.IsLoaded == true && editor.ChildrenOfType<ComposeTab>().Any());

            AddStep("click View", () =>
            {
                var viewItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                     .First(i => i.Item.Text.Value.ToString() == "View");
                input.MoveMouseTo(viewItem);
                input.Click(MouseButton.Left);
            });

            AddUntilStep("view dropdown open", () =>
                editor.ChildrenOfType<BasicMenu>().Count(m => m.State == MenuState.Open) >= 2);

            AddAssert("Mini Preview item present", () =>
                editor.ChildrenOfType<Menu.DrawableMenuItem>().Any(i => i.Item.Text.Value.ToString() == "Mini Preview"));

            bool initialState = false;
            AddStep("capture state", () => initialState = editor.MiniPreviewEnabled.Value);

            AddStep("click Mini Preview", () =>
            {
                var drawableItem = editor.ChildrenOfType<Menu.DrawableMenuItem>()
                                         .First(i => i.Item.Text.Value.ToString() == "Mini Preview");
                input.MoveMouseTo(drawableItem);
                input.Click(MouseButton.Left);
            });

            AddAssert("bindable flipped", () => editor.MiniPreviewEnabled.Value == !initialState);
        }

        [Test]
        public void TestPreviewSuspendsForTestModeAndRestores()
        {
            GarbusEditor editor = null!;
            ManualInputManager input = null!;
            ScreenStack stack = null!;

            AddStep("create editor with virtual track", () =>
            {
                editor = buildEditor();
                editor.TrackFactoryOverride = () => new TrackVirtual(60_000);

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = stack = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
                };
            });
            AddUntilStep("editor loaded", () => editor?.IsLoaded == true && editor.ChildrenOfType<ComposeTab>().Any());

            AddAssert("enabled initially", () => editor.MiniPreviewEnabled.Value);

            AddStep("press F5", () => input.Key(Key.F5));
            AddUntilStep("PlayScreen pushed and loaded", () => stack.CurrentScreen is PlayScreen ps && ps.IsLoaded);

            AddAssert("disabled while suspended", () => !editor.MiniPreviewEnabled.Value);

            PlayScreen playScreen = null!;
            AddStep("capture PlayScreen and exit", () =>
            {
                playScreen = (PlayScreen)stack.CurrentScreen;
                playScreen.Exit();
            });

            AddUntilStep("back at editor", () => stack.CurrentScreen is GarbusEditor);
            AddAssert("restored to enabled on resume", () => editor.MiniPreviewEnabled.Value);
        }

        [Test]
        public void TestPreviewSuspendRestoresPriorDisabledValue()
        {
            GarbusEditor editor = null!;
            ManualInputManager input = null!;
            ScreenStack stack = null!;

            AddStep("create editor with virtual track", () =>
            {
                editor = buildEditor();
                editor.TrackFactoryOverride = () => new TrackVirtual(60_000);

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = stack = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
                };
            });
            AddUntilStep("editor loaded", () => editor?.IsLoaded == true && editor.ChildrenOfType<ComposeTab>().Any());

            AddStep("user disables preview before test mode", () => editor.MiniPreviewEnabled.Value = false);

            AddStep("press F5", () => input.Key(Key.F5));
            AddUntilStep("PlayScreen pushed and loaded", () => stack.CurrentScreen is PlayScreen ps && ps.IsLoaded);
            AddAssert("still disabled while suspended", () => !editor.MiniPreviewEnabled.Value);

            PlayScreen playScreen = null!;
            AddStep("capture PlayScreen and exit", () =>
            {
                playScreen = (PlayScreen)stack.CurrentScreen;
                playScreen.Exit();
            });

            AddUntilStep("back at editor", () => stack.CurrentScreen is GarbusEditor);
            AddAssert("resume does NOT force-enable — user's prior disabled choice survives",
                () => !editor.MiniPreviewEnabled.Value);
        }

        // ------------------------------------------------------------------
        // Test host: wires the minimal editor DI graph MiniPreview needs
        // (EditorChart, EditorClock, BindableBeatDivisor) and hosts a real
        // MiniPreview as its child. GarbusScrollingInfo comes from the ambient
        // GarbusTestScene/GarbusGameBase graph — no need to cache it here.
        // ------------------------------------------------------------------

        private partial class MiniPreviewTestHost : CompositeDrawable
        {
            public MiniPreview Preview { get; private set; } = null!;
            public EditorChart EditorChart { get; private set; } = null!;
            public EditorClock EditorClock { get; private set; } = null!;

            private CardinalNote? lastAdded;
            private DependencyContainer dependencies = null!;
            private readonly Vector2? previewSize;

            // previewSize: when set, hosts the MiniPreview at a fixed centred size (for the content-scale
            // test); otherwise the preview fills the host (RelativeSizeAxes.Both), as the panel does.
            public MiniPreviewTestHost(Vector2? previewSize = null)
            {
                this.previewSize = previewSize;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var chart = new GarbusChart();
                chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                EditorChart = new EditorChart(chart);

                var beatDivisor = new BindableBeatDivisor(4);
                EditorClock = new EditorClock(EditorChart.ControlPointInfo, 60000, beatDivisor);
                EditorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(EditorChart);
                dependencies.Cache(EditorClock);
                dependencies.Cache(beatDivisor);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.Both;

                // MiniPreview always fills its parent (its load() sets RelativeSizeAxes.Both), exactly as it
                // does inside the 190px InlineChartPreviewPanel. To exercise the content-scaling in a panel-
                // sized box, wrap it in a fixed-size centred container rather than sizing the preview itself.
                Preview = new MiniPreview { RelativeSizeAxes = Axes.Both };

                InternalChild = previewSize is { } sz
                    ? new Container { Size = sz, Anchor = Anchor.Centre, Origin = Anchor.Centre, Child = Preview }
                    : Preview;

                // EditorClock must be in the hierarchy to tick; add it alongside the preview.
                AddInternal(EditorClock);
            }

            public void AddNote(double time)
            {
                lastAdded = new CardinalNote { StartTime = time, AngleDeg = 90 };
                EditorChart.Add(lastAdded);
            }

            /// <summary>Adds a cardinal note at a given time/angle without disturbing lastAdded tracking.</summary>
            public CardinalNote AddCardinal(double time, int angle)
            {
                var note = new CardinalNote { StartTime = time, AngleDeg = angle };
                EditorChart.Add(note);
                return note;
            }

            private SliderBody? lastSlider;

            /// <summary>Adds a two-link slider at the given time/side and tracks it for a later Side flip.</summary>
            public SliderBody AddSlider(double time, HorizontalDirection side)
            {
                lastSlider = new SliderBody
                {
                    StartTime = time,
                    AngleDeg = 90,
                    Side = side,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>([
                            new GarbusPathControlPoint { RotationOffset = 0, TimeOffset = 250 },
                            new GarbusPathControlPoint { RotationOffset = 30, TimeOffset = 500 },
                        ]),
                    },
                };
                EditorChart.Add(lastSlider);
                return lastSlider;
            }

            /// <summary>Flips the most recently added slider's Side and re-applies it live (as the editor does).</summary>
            public void FlipLastSliderSide()
            {
                if (lastSlider == null) return;

                lastSlider.Side = lastSlider.Side == HorizontalDirection.Left ? HorizontalDirection.Right : HorizontalDirection.Left;
                EditorChart.Update(lastSlider);
            }

            /// <summary>Adds a cardinal hold note spanning [time, time + duration].</summary>
            public CardinalHoldNote AddHold(double time, double duration)
            {
                var hold = new CardinalHoldNote { StartTime = time, AngleDeg = 90, Duration = duration };
                EditorChart.Add(hold);
                return hold;
            }

            public void RemoveLastAddedNote()
            {
                if (lastAdded != null)
                    EditorChart.Remove(lastAdded);
            }

            public void MoveLastAddedNoteTo(double time)
            {
                if (lastAdded == null) return;

                lastAdded.StartTime = time;
                EditorChart.Update(lastAdded);
            }
        }

        // ------------------------------------------------------------------
        // Panel harness: wires the same minimal editor DI graph as
        // MiniPreviewTestHost (InlineChartPreviewPanel.SetVisible(true) builds a
        // MiniPreview that needs it), but wraps a ManualInputManager so panel drag
        // tests can drive real mouse input instead of only mutating state directly.
        // ------------------------------------------------------------------

        private partial class InlineChartPreviewPanelHarness : Container
        {
            public InlineChartPreviewPanel Panel { get; private set; } = null!;
            public ManualInputManager Input { get; private set; } = null!;
            public Container Workspace { get; private set; } = null!;
            public EditorClock EditorClock { get; private set; } = null!;

            private readonly Vector2 workspaceSize;
            private DependencyContainer dependencies = null!;

            public InlineChartPreviewPanelHarness(Vector2 workspaceSize)
            {
                this.workspaceSize = workspaceSize;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var chart = new GarbusChart();
                chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                var editorChart = new EditorChart(chart);

                var beatDivisor = new BindableBeatDivisor(4);
                EditorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                EditorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(EditorClock);
                dependencies.Cache(beatDivisor);

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = Input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = Workspace = new Container
                    {
                        Size = workspaceSize,
                        Child = Panel = new InlineChartPreviewPanel(),
                    },
                };
                AddInternal(EditorClock);
            }
        }
    }
}
