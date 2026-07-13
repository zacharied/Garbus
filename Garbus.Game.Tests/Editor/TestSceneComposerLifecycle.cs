// Tests for ScrollingHitObjectComposer<T> drawable lifecycle:
// verifies that the drawableMap fix prevents ghost drawables (remove no-op) and
// progressive duplication (update adds duplicate without removing old drawable).

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneComposerLifecycle : GarbusTestScene
    {
        private ComposerTestHarness harness = null!;
        private EditorChart editorChart = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);

            Child = harness = new ComposerTestHarness(editorChart) { RelativeSizeAxes = Axes.Both };
        });

        [Test]
        public void TestAddCreatesExactlyOneDrawable()
        {
            var note = new MinimalNote { StartTime = 500 };
            AddStep("add hit object", () => editorChart.Add(note));
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);
            AddAssert("exactly 1 drawable", () => harness.Composer.Playfield.AllHitObjects.Count(), () => Is.EqualTo(1));
        }

        [Test]
        public void TestUpdateProducesExactlyOneDrawable()
        {
            var note = new MinimalNote { StartTime = 500 };
            AddStep("add hit object", () => editorChart.Add(note));
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);

            DrawableHitObject? drawableBeforeUpdate = null;
            AddStep("capture drawable", () => drawableBeforeUpdate = harness.Composer.Playfield.AllHitObjects.SingleOrDefault());

            AddStep("update hit object", () =>
            {
                note.StartTime = 600;
                editorChart.Update(note);
            });

            // Still exactly 1 drawable — no duplication.
            AddAssert("exactly 1 drawable after update", () => harness.Composer.Playfield.AllHitObjects.Count(), () => Is.EqualTo(1));
            // The SAME drawable instance refreshes in place (via HitObject.DefaultsApplied) — updates
            // must not tear down and rebuild drawables (see the slider node-drag GC storm, ISSUES.md).
            AddAssert("drawable is the same instance", () =>
                drawableBeforeUpdate != null &&
                ReferenceEquals(harness.Composer.Playfield.AllHitObjects.Single(), drawableBeforeUpdate));
        }

        [Test]
        public void TestRemoveLeaveZeroDrawables()
        {
            var note = new MinimalNote { StartTime = 500 };
            AddStep("add hit object", () => editorChart.Add(note));
            AddUntilStep("composer loaded", () => harness.Composer.IsLoaded);
            AddAssert("1 drawable before remove", () => harness.Composer.Playfield.AllHitObjects.Count(), () => Is.EqualTo(1));

            AddStep("remove hit object", () => editorChart.Remove(note));
            AddAssert("0 drawables after remove", () => harness.Composer.Playfield.AllHitObjects.Count(), () => Is.EqualTo(0));
        }

        // ---------------------------------------------------------------------------
        // Minimal concrete hit object — no audio, no graphics beyond what the base adds.
        // ---------------------------------------------------------------------------

        private class MinimalNote : GarbusHitObject
        {
            public override Judgement CreateJudgement() => new Judgement();

            public override HitsoundFamily Hitsounds => HitsoundFamilies.CardinalNote;
        }

        // ---------------------------------------------------------------------------
        // Minimal DrawableHitObject — only overrides the required abstract members.
        // DrawableHitObject<T> itself has no abstract methods; CheckForResult and
        // UpdateHitStateTransforms are virtual. We provide empty bodies so the compiler
        // is happy and so the base BDL (HitSoundContainer) completes without throwing.
        // ---------------------------------------------------------------------------

        private partial class MinimalDrawableNote : DrawableHitObject<MinimalNote>
        {
            public MinimalDrawableNote(MinimalNote hitObject)
                : base(hitObject)
            {
            }

            protected override void CheckForResult(bool userTriggered, double timeOffset) { }

            protected override void UpdateHitStateTransforms(ArmedState state) { }
        }

        // ---------------------------------------------------------------------------
        // Minimal scrolling playfield — concrete subclass of ScrollingPlayfield.
        // ---------------------------------------------------------------------------

        private partial class MinimalPlayfield : ScrollingPlayfield
        {
        }

        // ---------------------------------------------------------------------------
        // Minimal concrete composer — wires together MinimalNote → MinimalDrawableNote.
        // ---------------------------------------------------------------------------

        private partial class MinimalComposer : ScrollingHitObjectComposer<MinimalNote>
        {
            protected override IReadOnlyList<CompositionTool> CompositionTools => System.Array.Empty<CompositionTool>();

            protected override Playfield CreatePlayfield() => new MinimalPlayfield();

            protected override DrawableHitObject? CreateDrawableRepresentation(MinimalNote hitObject) =>
                new MinimalDrawableNote(hitObject);

            protected override ComposeBlueprintContainer CreateBlueprintContainer() =>
                new MinimalBlueprintContainer(this);
        }

        // ---------------------------------------------------------------------------
        // Minimal blueprint container — ComposeBlueprintContainer is abstract; we need
        // a trivial concrete subclass that satisfies the abstract surface.
        // ---------------------------------------------------------------------------

        private partial class MinimalBlueprintContainer : ComposeBlueprintContainer
        {
            public MinimalBlueprintContainer(HitObjectComposer composer)
                : base(composer)
            {
            }

            public override HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject) => null;

            protected override bool TryMoveBlueprints(
                osu.Framework.Input.Events.DragEvent e,
                IList<(SelectionBlueprint<GarbusHitObject> blueprint, osuTK.Vector2[] originalSnapPositions)> blueprints) => false;
        }

        // ---------------------------------------------------------------------------
        // Harness: a Container that caches all DI deps the composer tree requires,
        // then hosts the composer as its child.
        // ---------------------------------------------------------------------------

        private partial class ComposerTestHarness : Container
        {
            private readonly EditorChart editorChart;
            private DependencyContainer dependencies = null!;

            public MinimalComposer Composer { get; private set; } = null!;

            public ComposerTestHarness(EditorChart editorChart)
            {
                this.editorChart = editorChart;
            }

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var beatDivisor = new BindableBeatDivisor(4);
                var editorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                editorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(editorClock);
                dependencies.Cache(beatDivisor);
                // IEditorChangeHandler is [Resolved(CanBeNull = true)] in SelectionHandler — omit it.

                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = Composer = new MinimalComposer { RelativeSizeAxes = Axes.Both };
                // EditorClock must be in the hierarchy to tick; add it alongside the composer.
                AddInternal(dependencies.Get<EditorClock>());
            }
        }
    }
}
