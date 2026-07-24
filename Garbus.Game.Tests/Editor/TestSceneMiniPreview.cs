// The editor Mini preview: a non-interactive autoHit playfield hosted over the compose workspace,
// mirroring the editor's live hit objects on a clock slaved to the EditorClock.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Configuration;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
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
                InternalChild = Preview = new MiniPreview { RelativeSizeAxes = Axes.Both };
                // EditorClock must be in the hierarchy to tick; add it alongside the preview.
                AddInternal(EditorClock);
            }

            public void AddNote(double time)
            {
                lastAdded = new CardinalNote { StartTime = time, AngleDeg = 90 };
                EditorChart.Add(lastAdded);
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
