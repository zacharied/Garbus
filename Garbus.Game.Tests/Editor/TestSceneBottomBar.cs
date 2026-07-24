// TDD tests for Task 18: bottom bar + transport controls.
//
// Contract under test:
//   1. Space key toggles EditorClock.IsRunning (play/pause).
//   2. Speed 0.5 halves the AudioAdjustments aggregate tempo.
//   3. SummaryTimeline click seeks (raw/unsnapped).
//   4. Arrow keys move playhead by one beat-divisor step.
//   5. Z key seeks to start; X plays from start; C pause/resume; V seeks to end.
//   6. ↑/↓ keys change beatDivisor.
//   7. TimelineStrip drag seeks raw; no snap on release.
//
// Harness: wraps GarbusEditor in a ScreenStack inside a ManualInputManager so transport
// keys can be injected into the same input pipeline that GarbusEditor.OnKeyDown handles.

using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Configuration;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.BottomBar;
using Garbus.Game.Edit.Screens.Timeline;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneBottomBar : GarbusTestScene
    {
        private GarbusEditor editor = null!;
        private ManualInputManager input = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        // Cached once waitForEditor() passes — safe because fields are set inside AddStep lambdas
        // which run on the game thread after SetUp.
        private EditorClock? editorClock;
        private BindableBeatDivisor? beatDivisor;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            editorClock = null;
            beatDivisor = null;

            resetMiniOffsetConfig();
            createEditor();
        });

        private void createEditor()
        {
            editorClock = null;
            beatDivisor = null;

            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 }); // 120 BPM

            var chartFile = new ChartFile(chart);
            string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName() + ".garbus");
            chartFile.Save(tempPath);

            Child = input = new ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor = new GarbusEditor(chartFile)) { RelativeSizeAxes = Axes.Both },
            };
        }

        private void resetMiniOffsetConfig()
        {
            config.SetValue(GarbusSetting.MiniPreviewX, 5f);
            config.SetValue(GarbusSetting.MiniPreviewY, 5f);
        }

        private float miniOffset(GarbusSetting setting) => config.GetBindable<float>(setting).Value;

        private InlineChartPreviewPanel miniPanel() => editor.ChildrenOfType<InlineChartPreviewPanel>().Single();

        private TimelineStrip composeTimeline() =>
            editor.ChildrenOfType<ComposeTab>().Single().ChildrenOfType<TimelineStrip>().Single();

        private Container namedContainer(string name) =>
            editor.ChildrenOfType<Container>().Single(container => container.Name == name);

        private void dragMiniTo(Vector2 screenSpacePosition)
        {
            var panel = miniPanel();
            input.MoveMouseTo(panel.ScreenSpaceDrawQuad.Centre);
            input.PressButton(MouseButton.Left);
            input.MoveMouseTo(screenSpacePosition);
            input.ReleaseButton(MouseButton.Left);
        }

        private static bool screenSpaceContains(Drawable outer, Drawable inner)
        {
            var outerBounds = outer.ScreenSpaceDrawQuad.AABBFloat;
            var innerBounds = inner.ScreenSpaceDrawQuad.AABBFloat;
            return innerBounds.Left >= outerBounds.Left - 0.01f
                   && innerBounds.Top >= outerBounds.Top - 0.01f
                   && innerBounds.Right <= outerBounds.Right + 0.01f
                   && innerBounds.Bottom <= outerBounds.Bottom + 0.01f;
        }

        private static bool screenSpaceOverlaps(Drawable first, Drawable second)
        {
            var firstBounds = first.ScreenSpaceDrawQuad.AABBFloat;
            var secondBounds = second.ScreenSpaceDrawQuad.AABBFloat;
            return firstBounds.Left < secondBounds.Right
                   && firstBounds.Right > secondBounds.Left
                   && firstBounds.Top < secondBounds.Bottom
                   && firstBounds.Bottom > secondBounds.Top;
        }

        private static Vector2 bottomRightOffset(InlineChartPreviewPanel panel)
        {
            var panelBounds = panel.ScreenSpaceDrawQuad.AABBFloat;
            var parentBounds = panel.Parent!.ScreenSpaceDrawQuad.AABBFloat;
            return new Vector2(parentBounds.Right - panelBounds.Right, parentBounds.Bottom - panelBounds.Bottom);
        }

        /// <summary>
        /// Wait for the editor and bottom bar to be fully loaded, then cache the clock and divisor.
        /// All tests call this before asserting clock state.
        /// </summary>
        private void waitForEditor()
        {
            AddUntilStep("editor + bottom bar loaded", () =>
                editor.IsLoaded && editor.ChildrenOfType<BottomBar>().Any());

            // Cache references inside a step so they execute after the editor is loaded.
            AddStep("cache clock + divisor", () =>
            {
                editorClock = editor.ChildrenOfType<EditorClock>().First();
                beatDivisor = editor.ChildrenOfType<TimelineStrip>().First().Dependencies.Get<BindableBeatDivisor>();
            });
        }

        [Test]
        public void TestInlinePreviewToggleAndLiveUpdate()
        {
            CardinalNote note = null!;

            waitForEditor();

            AddUntilStep("inline preview loaded", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()?.IsLoaded == true);
            AddAssert("inline preview attached to workspace overlay", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Parent?.Name,
                () => Is.EqualTo("Mini preview workspace overlay"));
            AddAssert("preview overlay belongs directly to compose popover", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Parent?.Parent,
                () => Is.TypeOf<PopoverContainer>());
            AddAssert("workspace overlay spans compose controls", () =>
            {
                var overlay = miniPanel().Parent!;
                return screenSpaceContains(overlay, composeTimeline())
                       && screenSpaceContains(overlay, namedContainer("Playfield content"))
                       && screenSpaceContains(overlay, namedContainer("Left toolbox"))
                       && screenSpaceContains(overlay, editor.ChildrenOfType<Inspector>().Single());
            });
            AddAssert("workspace overlay remains inside compose tab", () =>
                screenSpaceContains(editor.ChildrenOfType<ComposeTab>().Single(), miniPanel().Parent!));
            AddAssert("inline preview keeps fixed width", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().DrawWidth,
                () => Is.EqualTo(InlineChartPreviewPanel.SIZE).Within(0.01f));
            AddAssert("inline preview is square", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().DrawHeight,
                () => Is.EqualTo(InlineChartPreviewPanel.SIZE).Within(0.01f));
            AddAssert("inline preview defaults five pixels from workspace bottom right", () =>
            {
                var panel = editor.ChildrenOfType<InlineChartPreviewPanel>().Single();
                Vector2 offset = bottomRightOffset(panel);
                return System.Math.Abs(offset.X - 5) < 0.01
                       && System.Math.Abs(offset.Y - 5) < 0.01;
            });
            AddStep("drag inline preview over inspector", () =>
                dragMiniTo(editor.ChildrenOfType<Inspector>().Single().ScreenSpaceDrawQuad.Centre));
            AddAssert("inline preview overlaps inspector", () =>
                screenSpaceOverlaps(miniPanel(), editor.ChildrenOfType<Inspector>().Single()));
            AddStep("drag inline preview over compose timeline", () =>
                dragMiniTo(composeTimeline().ScreenSpaceDrawQuad.Centre));
            AddAssert("inline preview overlaps compose timeline", () =>
                screenSpaceOverlaps(miniPanel(), composeTimeline()));
            AddAssert("no bottom preview button", () =>
                editor.ChildrenOfType<BottomBar>().Single().ChildrenOfType<BasicButton>()
                      .All(button => button.Text != "Preview"));
            AddAssert("mini enabled by default", () => editor.MiniPreviewEnabled.Value);
            AddAssert("inline preview visible by default", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Alpha, () => Is.EqualTo(1));

            setMiniPreviewEnabledStep(false);
            AddUntilStep("inline preview hidden", () =>
                !editor.MiniPreviewEnabled.Value
                && editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Alpha == 0);
            setMiniPreviewEnabledStep(true);
            AddUntilStep("inline preview shown", () =>
                editor.MiniPreviewEnabled.Value
                && editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Alpha == 1);

            AddStep("add unsaved object", () =>
                editor.EditorChart.Add(note = new CardinalNote { StartTime = 1000, AngleDeg = 90 }));
            AddUntilStep("inline preview receives live object", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().ViewForTests.ObjectCountForTests == 1);

            AddStep("update unsaved object", () =>
            {
                note.StartTime = 1500;
                note.AngleDeg = 180;
                editor.EditorChart.Update(note);
            });
            AddUntilStep("inline preview receives live update", () =>
            {
                var previewObject = editor.ChildrenOfType<InlineChartPreviewPanel>().Single()
                                          .ViewForTests.PlayfieldForTests.AllHitObjects.Single().HitObject;
                return previewObject.StartTime == 1500 && ((CardinalNote)previewObject).AngleDeg == 180;
            });

            AddStep("scrub editor", () => editorClock!.Seek(1000));
            AddUntilStep("inline preview follows editor clock", () =>
                System.Math.Abs(editor.ChildrenOfType<InlineChartPreviewPanel>().Single().ViewForTests.ClockTimeForTests - 1000) < 1);

            AddStep("switch away from compose", () => editor.Tab.Value = EditorTab.Design);
            AddUntilStep("enabled preview hidden outside compose", () =>
                editor.MiniPreviewEnabled.Value
                && editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Alpha == 0);

            AddStep("remove object while outside compose", () => editor.EditorChart.Remove(note));
            AddAssert("hidden preview does not process edits", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().ViewForTests.ObjectCountForTests,
                () => Is.EqualTo(1));

            AddStep("return to compose", () => editor.Tab.Value = EditorTab.Compose);
            AddUntilStep("returning to compose resyncs authoritatively", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Alpha == 1
                && editor.ChildrenOfType<InlineChartPreviewPanel>().Single().ViewForTests.ObjectCountForTests == 0);

            setMiniPreviewEnabledStep(false);
            AddUntilStep("inline preview disabled and hidden", () =>
                !editor.MiniPreviewEnabled.Value
                && editor.ChildrenOfType<InlineChartPreviewPanel>().Single().Alpha == 0);
        }

        [Test]
        public void TestMiniPreviewRestoresOffsetsAndClampsToWorkspaceBounds()
        {
            AddStep("save offsets and recreate editor", () =>
            {
                config.SetValue(GarbusSetting.MiniPreviewX, 37f);
                config.SetValue(GarbusSetting.MiniPreviewY, 43f);
                createEditor();
            });
            waitForEditor();
            AddUntilStep("restored offsets applied", () =>
            {
                Vector2 offset = bottomRightOffset(miniPanel());
                return System.Math.Abs(offset.X - 37) < 0.01
                       && System.Math.Abs(offset.Y - 43) < 0.01;
            });

            AddStep("drag beyond top left", () =>
            {
                var panel = miniPanel();
                input.MoveMouseTo(panel.ScreenSpaceDrawQuad.Centre);
                input.PressButton(MouseButton.Left);
                input.MoveMouseTo(panel.Parent!.ScreenSpaceDrawQuad.TopLeft - new Vector2(500));
                input.ReleaseButton(MouseButton.Left);
            });
            AddAssert("clamped at top and left edges", () =>
            {
                var panelBounds = miniPanel().ScreenSpaceDrawQuad.AABBFloat;
                var parentBounds = miniPanel().Parent!.ScreenSpaceDrawQuad.AABBFloat;
                return System.Math.Abs(panelBounds.Left - parentBounds.Left) < 0.01
                       && System.Math.Abs(panelBounds.Top - parentBounds.Top) < 0.01;
            });

            AddStep("drag beyond bottom right", () =>
            {
                var panel = miniPanel();
                input.MoveMouseTo(panel.ScreenSpaceDrawQuad.Centre);
                input.PressButton(MouseButton.Left);
                input.MoveMouseTo(panel.Parent!.ScreenSpaceDrawQuad.BottomRight + new Vector2(500));
                input.ReleaseButton(MouseButton.Left);
            });
            AddAssert("clamped at bottom and right edges", () =>
            {
                Vector2 offset = bottomRightOffset(miniPanel());
                return System.Math.Abs(offset.X) < 0.01 && System.Math.Abs(offset.Y) < 0.01;
            });

            AddStep("park at top left before resize", () =>
            {
                var panel = miniPanel();
                input.MoveMouseTo(panel.ScreenSpaceDrawQuad.Centre);
                input.PressButton(MouseButton.Left);
                input.MoveMouseTo(panel.Parent!.ScreenSpaceDrawQuad.TopLeft - new Vector2(500));
                input.ReleaseButton(MouseButton.Left);
            });
            AddStep("shrink editor bounds", () =>
            {
                input.RelativeSizeAxes = Axes.None;
                input.Size = new Vector2(
                    System.Math.Max(700, input.DrawWidth - 250),
                    System.Math.Max(450, input.DrawHeight - 150));
            });
            AddUntilStep("reclamped after bounds change", () =>
            {
                var panelBounds = miniPanel().ScreenSpaceDrawQuad.AABBFloat;
                var parentBounds = miniPanel().Parent!.ScreenSpaceDrawQuad.AABBFloat;
                return System.Math.Abs(panelBounds.Left - parentBounds.Left) < 0.01
                       && System.Math.Abs(panelBounds.Top - parentBounds.Top) < 0.01
                       && panelBounds.Right <= parentBounds.Right + 0.01
                       && panelBounds.Bottom <= parentBounds.Bottom + 0.01;
            });
            AddStep("restore editor bounds and config", () =>
            {
                input.RelativeSizeAxes = Axes.Both;
                input.Size = Vector2.One;
                resetMiniOffsetConfig();
            });
        }

        [Test]
        public void TestMiniPreviewPreservesLoadedOffsetsUntilBoundsResolve()
        {
            AddStep("cold load editor without layout bounds", () =>
            {
                config.SetValue(GarbusSetting.MiniPreviewX, 37f);
                config.SetValue(GarbusSetting.MiniPreviewY, 43f);
                createEditor();
                input.RelativeSizeAxes = Axes.None;
                input.Size = Vector2.Zero;
            });
            AddUntilStep("mini loaded with unresolved parent", () =>
                miniPanel().IsLoaded
                && (miniPanel().Parent!.DrawWidth < InlineChartPreviewPanel.SIZE
                    || miniPanel().Parent!.DrawHeight < InlineChartPreviewPanel.SIZE));
            AddWaitStep("allow unresolved clamp attempts", 2);
            AddAssert("persisted offsets remain intact while unresolved", () =>
                miniOffset(GarbusSetting.MiniPreviewX), () => Is.EqualTo(37).Within(0.01));
            AddAssert("persisted vertical offset remains intact while unresolved", () =>
                miniOffset(GarbusSetting.MiniPreviewY), () => Is.EqualTo(43).Within(0.01));

            AddStep("resolve editor bounds", () => input.Size = new Vector2(1000, 700));
            AddUntilStep("loaded offsets apply after bounds resolve", () =>
            {
                Vector2 offset = bottomRightOffset(miniPanel());
                return System.Math.Abs(offset.X - 37) < 0.01
                       && System.Math.Abs(offset.Y - 43) < 0.01;
            });
            AddStep("restore mini config", resetMiniOffsetConfig);
        }

        [Test]
        public void TestMiniPreviewOwnsDragAndWheelButOverlayPassesUncoveredInput()
        {
            GarbusSlamCentered selected = null!;
            double capturedTime = 0;
            float capturedZoom = 0;
            Vector2 persistedOffsetBeforeDrag = default;

            MultiValueEnumDropdown<HorizontalDirection> sideDropdown() =>
                editor.ChildrenOfType<MultiValueEnumDropdown<HorizontalDirection>>().Single();
            DropdownHeader sideHeader() => sideDropdown().ChildrenOfType<DropdownHeader>().Single();
            Menu sideMenu() => sideDropdown().ChildrenOfType<Menu>().Single();

            waitForEditor();
            AddUntilStep("mini loaded", () => miniPanel().IsLoaded && miniPanel().Alpha == 1);
            AddStep("select object and park clock", () =>
            {
                editor.EditorChart.Add(selected = new GarbusSlamCentered
                {
                    StartTime = 1000,
                    AngleDeg = 90,
                    Side = HorizontalDirection.Left,
                });
                editor.EditorChart.SelectedHitObjects.Add(selected);
                editorClock!.Stop();
                editorClock.Seek(editorClock.TrackLength / 2);
                capturedTime = editorClock.CurrentTime;
                capturedZoom = composeTimeline().CurrentZoom.Value;
            });
            AddUntilStep("inspector side control loaded", () =>
                editor.ChildrenOfType<MultiValueEnumDropdown<HorizontalDirection>>().Any());

            AddStep("drag mini over inspector side control", () =>
                dragMiniTo(sideHeader().ScreenSpaceDrawQuad.Centre));
            AddAssert("mini covers inspector side control", () =>
                screenSpaceOverlaps(miniPanel(), sideHeader()));
            AddStep("click covered inspector side control", () =>
            {
                input.MoveMouseTo(sideHeader().ScreenSpaceDrawQuad.Centre);
                input.Click(MouseButton.Left);
            });
            AddAssert("covered inspector side control stays closed", () => sideMenu().State,
                () => Is.EqualTo(MenuState.Closed));

            AddStep("drag mini over timeline control", () =>
            {
                capturedTime = editorClock!.CurrentTime;
                var timelineBounds = composeTimeline().ScreenSpaceDrawQuad.AABBFloat;
                dragMiniTo(new Vector2(timelineBounds.Left + timelineBounds.Width * 0.25f, timelineBounds.Centre.Y));
            });
            AddAssert("mini covers timeline control", () => screenSpaceOverlaps(miniPanel(), composeTimeline()));
            AddAssert("mini drag did not seek covered timeline", () => editorClock!.CurrentTime,
                () => Is.EqualTo(capturedTime).Within(0.01));

            AddStep("open uncovered inspector side control", () =>
            {
                input.MoveMouseTo(sideHeader().ScreenSpaceDrawQuad.Centre);
                input.Click(MouseButton.Left);
            });
            AddUntilStep("uncovered inspector side control opens", () => sideMenu().State == MenuState.Open);
            AddStep("close inspector side control", () =>
            {
                input.MoveMouseTo(sideHeader().ScreenSpaceDrawQuad.Centre);
                input.Click(MouseButton.Left);
            });
            AddUntilStep("inspector side control closes", () => sideMenu().State == MenuState.Closed);

            AddStep("drag across covered timeline", () =>
            {
                capturedTime = editorClock!.CurrentTime;
                var overlapBounds = miniPanel().ScreenSpaceDrawQuad.AABBFloat;
                var timelineBounds = composeTimeline().ScreenSpaceDrawQuad.AABBFloat;
                var start = new Vector2(
                    (System.Math.Max(overlapBounds.Left, timelineBounds.Left) + System.Math.Min(overlapBounds.Right, timelineBounds.Right)) / 2,
                    (System.Math.Max(overlapBounds.Top, timelineBounds.Top) + System.Math.Min(overlapBounds.Bottom, timelineBounds.Bottom)) / 2);
                input.MoveMouseTo(start);
                input.PressButton(MouseButton.Left);
                input.MoveMouseTo(start + new Vector2(30, 0));
                input.ReleaseButton(MouseButton.Left);
            });
            AddAssert("covered timeline drag did not seek editor", () => editorClock!.CurrentTime,
                () => Is.EqualTo(capturedTime).Within(0.01));

            AddStep("move mini away from timeline", () =>
                dragMiniTo(editor.ChildrenOfType<Inspector>().Single().ScreenSpaceDrawQuad.Centre));
            AddAssert("timeline is uncovered", () => !screenSpaceOverlaps(miniPanel(), composeTimeline()));
            AddStep("drag uncovered timeline", () =>
            {
                capturedTime = editorClock!.CurrentTime;
                var timelineBounds = composeTimeline().ScreenSpaceDrawQuad.AABBFloat;
                var start = new Vector2(timelineBounds.Left + timelineBounds.Width * 0.25f, timelineBounds.Centre.Y);
                input.MoveMouseTo(start);
                input.PressButton(MouseButton.Left);
                input.MoveMouseTo(start + new Vector2(80, 0));
                input.ReleaseButton(MouseButton.Left);
            });
            AddUntilStep("uncovered timeline drag seeks editor", () =>
                System.Math.Abs(editorClock!.CurrentTime - capturedTime) > 10);

            AddStep("press and drag mini", () =>
            {
                var panel = miniPanel();
                persistedOffsetBeforeDrag = new Vector2(
                    miniOffset(GarbusSetting.MiniPreviewX),
                    miniOffset(GarbusSetting.MiniPreviewY));
                input.MoveMouseTo(panel.ScreenSpaceDrawQuad.Centre);
                input.PressButton(MouseButton.Left);
                input.MoveMouseTo(panel.ScreenSpaceDrawQuad.Centre - new Vector2(60, 40));
            });
            AddAssert("config unchanged during drag", () =>
                System.Math.Abs(miniOffset(GarbusSetting.MiniPreviewX) - persistedOffsetBeforeDrag.X) < 0.01
                && System.Math.Abs(miniOffset(GarbusSetting.MiniPreviewY) - persistedOffsetBeforeDrag.Y) < 0.01);
            AddAssert("blueprint drag box stayed hidden", () =>
                editor.ChildrenOfType<ScrollingDragBox>().Single().State, () => Is.EqualTo(Visibility.Hidden));
            AddAssert("selection retained under mini drag", () =>
                editor.EditorChart.SelectedHitObjects.Single(), () => Is.SameAs(selected));
            AddStep("release mini", () => input.ReleaseButton(MouseButton.Left));
            AddAssert("config persisted on release", () =>
            {
                Vector2 offset = bottomRightOffset(miniPanel());
                return System.Math.Abs(miniOffset(GarbusSetting.MiniPreviewX) - offset.X) < 0.01
                       && System.Math.Abs(miniOffset(GarbusSetting.MiniPreviewY) - offset.Y) < 0.01;
            });

            AddStep("wheel over mini", () =>
            {
                capturedTime = editorClock!.CurrentTime;
                input.MoveMouseTo(miniPanel().ScreenSpaceDrawQuad.Centre);
                input.ScrollVerticalBy(-1);
            });
            AddAssert("mini wheel did not seek editor", () => editorClock!.CurrentTime,
                () => Is.EqualTo(capturedTime).Within(0.01));

            AddStep("ctrl wheel over mini", () =>
            {
                capturedZoom = composeTimeline().CurrentZoom.Value;
                input.PressKey(Key.LControl);
                input.ScrollVerticalBy(1);
                input.ReleaseKey(Key.LControl);
            });
            AddAssert("mini wheel did not zoom compose", () =>
                composeTimeline().CurrentZoom.Value,
                () => Is.EqualTo(capturedZoom).Within(0.01));

            AddStep("wheel over uncovered playfield", () =>
            {
                capturedTime = editorClock!.CurrentTime;
                input.MoveMouseTo(namedContainer("Playfield content").ScreenSpaceDrawQuad.Centre);
                input.ScrollVerticalBy(-1);
            });
            AddUntilStep("uncovered playfield still seeks", () => editorClock!.CurrentTime > capturedTime + 10);

            AddStep("ctrl wheel over uncovered playfield", () =>
            {
                capturedZoom = composeTimeline().CurrentZoom.Value;
                input.MoveMouseTo(namedContainer("Playfield content").ScreenSpaceDrawQuad.Centre);
                input.PressKey(Key.LControl);
                input.ScrollVerticalBy(1);
                input.ReleaseKey(Key.LControl);
            });
            AddUntilStep("uncovered playfield still zooms", () =>
                composeTimeline().CurrentZoom.Value > capturedZoom + 0.01f);
            AddStep("reset mini config", resetMiniOffsetConfig);
        }

        [Test]
        public void TestInlinePreviewScalesVirtualCanvasIntoMiniPanel()
        {
            InlineChartPreviewPanel panel = null!;
            DrawableCardinalNote drawable = null!;

            waitForEditor();

            AddUntilStep("inline preview content loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            AddStep("add preview note", () =>
                editor.EditorChart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 }));
            AddUntilStep("preview note loaded", () =>
                panel.ViewForTests.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault()
                    is { IsLoaded: true } loaded
                && (drawable = loaded) != null);

            AddAssert("mini content keeps preview virtual size", () => panel.ViewForTests.DrawSize,
                () => Is.EqualTo(new Vector2(768)));
            AddAssert("virtual canvas fills mini panel", () =>
                    panel.ViewForTests.ScreenSpaceDrawQuad.Width / panel.ScreenSpaceDrawQuad.Width,
                () => Is.EqualTo(1).Within(0.001f));
            AddAssert("note scales into mini panel", () =>
                    drawable.ScreenSpaceDrawQuad.Width / panel.ScreenSpaceDrawQuad.Width,
                () => Is.EqualTo(80f / 768).Within(0.001f));

            AddAssert("warning effect buffer fills mini panel", () =>
                    warningEffectBuffer(panel).ScreenSpaceDrawQuad.Width / panel.ScreenSpaceDrawQuad.Width,
                () => Is.EqualTo(1).Within(0.001f));
            AddAssert("warning blur buffer fills mini panel", () =>
                    warningBlurBuffer(panel).ScreenSpaceDrawQuad.Width / panel.ScreenSpaceDrawQuad.Width,
                () => Is.EqualTo(1).Within(0.001f));
            AddAssert("warning ring keeps normalized preview radius", () =>
                    warningRingMask(panel).ScreenSpaceDrawQuad.Width / panel.ScreenSpaceDrawQuad.Width,
                () => Is.EqualTo(708f / 768).Within(0.001f));
            AddAssert("warning arc keeps normalized ring radius", () =>
                    warningArc(panel).ScreenSpaceDrawQuad.Width / warningRingMask(panel).ScreenSpaceDrawQuad.Width,
                () => Is.EqualTo(1.1f).Within(0.001f));
            AddAssert("warning geometry stays centred in mini panel", () =>
                new[]
                {
                    (warningEffectBuffer(panel).ScreenSpaceDrawQuad.Centre - panel.ScreenSpaceDrawQuad.Centre).Length,
                    (warningRingMask(panel).ScreenSpaceDrawQuad.Centre - panel.ScreenSpaceDrawQuad.Centre).Length,
                }, () => Is.All.LessThan(0.01f));
        }

        private static Circle warningRingMask(InlineChartPreviewPanel panel) =>
            panel.ViewForTests.PlayfieldForTests.WarningIndicators.ChildrenOfType<Circle>().First();

        private static BufferedContainer warningEffectBuffer(InlineChartPreviewPanel panel) =>
            (BufferedContainer)warningRingMask(panel).Parent!;

        private static Arc warningArc(InlineChartPreviewPanel panel) =>
            panel.ViewForTests.PlayfieldForTests.WarningIndicators.ChildrenOfType<Arc>().First();

        private static BufferedContainer warningBlurBuffer(InlineChartPreviewPanel panel) =>
            (BufferedContainer)warningArc(panel).Parent!;

        private void setMiniPreviewEnabledStep(bool enabled) =>
            AddStep($"set mini preview enabled to {enabled}", () => editor.MiniPreviewEnabled.Value = enabled);

        // ------------------------------------------------------------------
        // 1. Space key toggles clock.IsRunning
        // ------------------------------------------------------------------

        [Test]
        public void TestSpaceTogglesPlayback()
        {
            waitForEditor();

            AddAssert("clock stopped initially", () => !editorClock!.IsRunning);

            AddStep("press space", () => input.Key(Key.Space));
            AddUntilStep("clock running", () => editorClock!.IsRunning);

            AddStep("press space again", () => input.Key(Key.Space));
            AddUntilStep("clock stopped", () => !editorClock!.IsRunning);
        }

        // ------------------------------------------------------------------
        // 2. Speed 0.5 halves AudioAdjustments aggregate tempo
        // ------------------------------------------------------------------

        [Test]
        public void TestSpeedControlHalvesTempo()
        {
            waitForEditor();

            AddStep("select speed 0.5 via tab control", () =>
            {
                var playback = editor.ChildrenOfType<PlaybackControl>().First();
                var tabControl = playback.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicTabControl<double>>().First();
                tabControl.Current.Value = 0.5;
            });

            AddAssert("aggregate tempo ≈ 0.5", () =>
                System.Math.Abs(editorClock!.AudioAdjustments.AggregateTempo.Value - 0.5) < 0.01);

            AddStep("reset speed to 1.0", () =>
            {
                var playback = editor.ChildrenOfType<PlaybackControl>().First();
                var tabControl = playback.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicTabControl<double>>().First();
                tabControl.Current.Value = 1.0;
            });

            AddAssert("aggregate tempo ≈ 1.0", () =>
                System.Math.Abs(editorClock!.AudioAdjustments.AggregateTempo.Value - 1.0) < 0.01);
        }

        // ------------------------------------------------------------------
        // 3. SummaryTimeline click seeks (raw, unsnapped)
        // ------------------------------------------------------------------

        [Test]
        public void TestSummaryTimelineClickSeeks()
        {
            waitForEditor();

            AddUntilStep("summary timeline loaded", () =>
                editor.ChildrenOfType<SummaryTimeline>().Any());

            AddStep("seek clock to 0", () => editorClock!.Seek(0));

            // Click the SummaryTimeline at ~50% width → should seek to ~half of track length.
            AddStep("click middle of summary timeline", () =>
            {
                var summary = editor.ChildrenOfType<SummaryTimeline>().First();
                var centre = summary.ToScreenSpace(new Vector2(summary.DrawWidth * 0.5f, summary.DrawHeight * 0.5f));
                input.MoveMouseTo(centre);
                input.Click(MouseButton.Left);
            });

            AddUntilStep("clock seeked to roughly half track", () =>
            {
                double halfTrack = editorClock!.TrackLength / 2;
                return System.Math.Abs(editorClock.CurrentTime - halfTrack) < halfTrack * 0.15 + 200;
            });
        }

        // ------------------------------------------------------------------
        // 4. Arrow keys move playhead by one beat-divisor step
        // ------------------------------------------------------------------

        [Test]
        public void TestArrowKeysSeekByDivisorStep()
        {
            waitForEditor();

            AddStep("seek to 0 and stop", () => { editorClock!.Stop(); editorClock.Seek(0); });

            double capturedTime = 0;
            AddStep("capture position", () => capturedTime = editorClock!.CurrentTime);

            // BeatLength=500ms, divisor=4 → one step = 125ms.
            AddStep("press right arrow", () => input.Key(Key.Right));

            AddUntilStep("moved forward by at least 50ms", () =>
                editorClock!.CurrentTime > capturedTime + 50);

            AddStep("capture new position", () => capturedTime = editorClock!.CurrentTime);
            AddStep("press left arrow", () => input.Key(Key.Left));

            AddUntilStep("moved backward by at least 50ms", () =>
                editorClock!.CurrentTime < capturedTime - 50);
        }

        // ------------------------------------------------------------------
        // 5. Z/X/C/V transport keys
        // ------------------------------------------------------------------

        [Test]
        public void TestZSeeksToStart()
        {
            waitForEditor();

            AddStep("seek to 5000", () => editorClock!.Seek(5000));
            AddStep("press Z", () => input.Key(Key.Z));
            AddUntilStep("at start (< 100ms)", () => editorClock!.CurrentTime < 100);
        }

        [Test]
        public void TestXPlaysFromStart()
        {
            waitForEditor();

            AddStep("seek to 5000", () => editorClock!.Seek(5000));
            AddStep("press X", () => input.Key(Key.X));
            AddUntilStep("clock running from near start", () => editorClock!.IsRunning && editorClock.CurrentTime < 2000);
            AddStep("stop clock", () => editorClock!.Stop());
        }

        [Test]
        public void TestCPausesAndResumes()
        {
            waitForEditor();

            AddAssert("stopped initially", () => !editorClock!.IsRunning);
            AddStep("press C to start", () => input.Key(Key.C));
            AddUntilStep("running", () => editorClock!.IsRunning);
            AddStep("press C to stop", () => input.Key(Key.C));
            AddUntilStep("stopped", () => !editorClock!.IsRunning);
        }

        [Test]
        public void TestVSeeksToEnd()
        {
            waitForEditor();

            AddStep("seek to start", () => editorClock!.Seek(0));
            AddStep("press V", () => input.Key(Key.V));
            AddUntilStep("at end (within 200ms)", () =>
                System.Math.Abs(editorClock!.CurrentTime - editorClock.TrackLength) < 200);
        }

        // ------------------------------------------------------------------
        // 6. ↑/↓ change beat divisor
        // ------------------------------------------------------------------

        [Test]
        public void TestUpDownChangesDivisor()
        {
            waitForEditor();

            int startDivisor = 0;
            AddStep("capture initial divisor", () => startDivisor = beatDivisor!.Value);

            AddStep("press down (increase divisor)", () => input.Key(Key.Down));
            AddUntilStep("divisor increased", () => beatDivisor!.Value > startDivisor);

            AddStep("capture new divisor", () => startDivisor = beatDivisor!.Value);
            AddStep("press up (decrease divisor)", () => input.Key(Key.Up));
            AddUntilStep("divisor decreased", () => beatDivisor!.Value < startDivisor);
        }

        // ------------------------------------------------------------------
        // 8. Wheel seek direction
        //
        // osu-framework: positive ScrollDelta.Y = wheel-up.
        // Spec: wheel-down (negative Y) → forward; wheel-up (positive Y) → backward.
        // ------------------------------------------------------------------

        [Test]
        public void TestWheelDownSeeksForward()
        {
            waitForEditor();

            AddStep("stop clock and seek to mid-track", () =>
            {
                editorClock!.Stop();
                editorClock.Seek(editorClock.TrackLength / 2);
            });

            double capturedTime = 0;
            AddStep("capture current time", () => capturedTime = editorClock!.CurrentTime);

            AddStep("wheel down over compose area", () =>
            {
                var compose = editor.ChildrenOfType<ComposeTab>().First();
                input.MoveMouseTo(compose.ToScreenSpace(new Vector2(compose.DrawWidth * 0.5f, compose.DrawHeight * 0.5f)));
                input.ScrollVerticalBy(-1);
            });

            AddUntilStep("time increased (wheel-down = forward)", () => editorClock!.CurrentTime > capturedTime + 10);
        }

        [Test]
        public void TestWheelUpSeeksBackward()
        {
            waitForEditor();

            AddStep("stop clock and seek to mid-track", () =>
            {
                editorClock!.Stop();
                editorClock.Seek(editorClock.TrackLength / 2);
            });

            double capturedTime = 0;
            AddStep("capture current time", () => capturedTime = editorClock!.CurrentTime);

            AddStep("wheel up over compose area", () =>
            {
                var compose = editor.ChildrenOfType<ComposeTab>().First();
                input.MoveMouseTo(compose.ToScreenSpace(new Vector2(compose.DrawWidth * 0.5f, compose.DrawHeight * 0.5f)));
                input.ScrollVerticalBy(1);
            });

            AddUntilStep("time decreased (wheel-up = backward)", () => editorClock!.CurrentTime < capturedTime - 10);
        }

        // ------------------------------------------------------------------
        // 7. TimelineStrip raw drag; no snap on release
        //
        // The TimelineStrip drag mechanism: scroll position → time via TimeAtPosition(Current).
        // Dragging the waveform display must stay raw/unsnapped throughout, including on release —
        // matching SummaryTimeline's existing raw-seek behaviour.
        // ------------------------------------------------------------------

        [Test]
        public void TestTimelineStripDragDoesNotSnapOnRelease()
        {
            waitForEditor();

            AddUntilStep("timeline strip loaded", () => editor.ChildrenOfType<TimelineStrip>().Any());

            // Seek to a non-snapped position (e.g., 333ms — not a multiple of 125ms).
            // This simulates where the raw-drag seek might leave the playhead.
            AddStep("seek to non-snapped position 333ms", () =>
            {
                editorClock!.Stop();
                editorClock.Seek(333);
            });

            AddWaitStep("wait one frame for seek to settle", 2);

            AddStep("mouse-press on timeline strip (simulates drag start)", () =>
            {
                var strip = editor.ChildrenOfType<TimelineStrip>().First();
                // Position the mouse somewhere on the strip so OnMouseDown is triggered.
                float x = strip.DrawWidth * 0.10f;
                float y = strip.DrawHeight * 0.5f;
                input.MoveMouseTo(strip.ToScreenSpace(new Vector2(x, y)));
                input.PressButton(MouseButton.Left);
            });

            AddWaitStep("wait a frame", 2);

            double timeBeforeRelease = 0;
            AddStep("capture time just before release", () => timeBeforeRelease = editorClock!.CurrentTime);

            AddStep("release mouse", () => input.ReleaseButton(MouseButton.Left));

            AddWaitStep("wait a few frames", 3);

            // Releasing the drag must not move the playhead — no beat-snap correction on release.
            AddAssert("position unchanged by release (no snap applied)", () =>
                System.Math.Abs(editorClock!.CurrentTime - timeBeforeRelease) < 1.0);
        }
    }
}
