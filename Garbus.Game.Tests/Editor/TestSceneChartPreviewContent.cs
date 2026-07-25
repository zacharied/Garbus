using System.Collections.Immutable;
using Garbus.Game.Gameplay.Audio;
using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Format;
using Garbus.Game.Core;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using Garbus.Game.Objects.Drawables;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public partial class TestSceneChartPreviewContent : Visual.GarbusTestScene
{
    protected override double TimePerAction => 0;

    private ChartPreviewContent preview = null!;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create preview content", () => Child = preview = new ChartPreviewContent
        {
            Size = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
        });
        AddUntilStep("preview content loaded", () => preview.IsLoaded);
    }

    [Test]
    public void TestFullStateRendersCleanReadOnlyPlayfield()
    {
        AddStep("apply full state", () => Assert.That(preview.Replace(fullState(1, previewChart(), [10, 20], 1200, 900)), Is.True));
        AddUntilStep("objects loaded", () => preview.ObjectCountForTests == 2
                                                && preview.PlayfieldForTests.AllHitObjects.All(d => d.IsLoaded));

        AddAssert("one playfield", () => preview.ChildrenOfType<GarbusPlayfield>().Count(), () => Is.EqualTo(1));
        AddAssert("one design overlay", () => preview.ChildrenOfType<DesignOverlay>().Count(), () => Is.EqualTo(1));
        AddAssert("no key input manager", () => preview.ChildrenOfType<GarbusInputManager>().Any(), () => Is.False);
        AddAssert("no analog input manager", () => preview.ChildrenOfType<AnalogInputManager>().Any(), () => Is.False);
        AddAssert("no analog input manager constructed", () => preview.PlayfieldForTests.HasAnalogInputManagerForTests, () => Is.False);
        AddAssert("no gameplay HUD or results text", () => preview.ChildrenOfType<SpriteText>()
                                                              .Select(t => t.Text.ToString().ToLowerInvariant())
                                                              .Any(t => t.Contains("score:")
                                                                        || t.Contains("accuracy:")
                                                                        || t.Contains("chart complete")), () => Is.False);
        AddAssert("square preview size", () => preview.Size, () => Is.EqualTo(new Vector2(768)));
        AddAssert("preview time applied", () => preview.ClockTimeForTests, () => Is.EqualTo(1200).Within(0.001));
        AddAssert("tutorial follows preview time", () => preview.DesignOverlayForTests.MessageVisibleForTests
                                                          && preview.DesignOverlayForTests.MessageTextForTests == "Preview tutorial");
        AddAssert("judgements hidden", () => preview.PlayfieldForTests.DisplayJudgements.Value, () => Is.False);
        AddAssert("all input recursively disabled", () => preview.PlayfieldForTests.AllHitObjects
                                                               .SelectMany(withNested)
                                                               .All(d => !d.HandleUserInput));

        AddStep("seek slider consumers without analog input", () =>
            Assert.That(preview.Apply(transportBatch(2, 2200, false, 1, 0)), Is.True));
        AddUntilStep("slider consumers update without analog input", () =>
            preview.ClockTimeForTests == 2200
            && preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault() is { IsLoaded: true } slider
            && slider.NestedHitObjects.All(d => d.IsLoaded));
    }

    [Test]
    public void TestObjectBatchRefreshesGlobalStateOnceAtEnd()
    {
        DrawableHitObject[] before = null!;
        bool refreshedBetweenDeltas = false;

        AddStep("apply separate cardinals", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalNote { StartTime = 2100, AngleDeg = 180 }), [7, 8], 1900, 700)));
        AddUntilStep("cardinals loaded white", () => preview.PlayfieldForTests.AllHitObjects
                                                         .Count(d => d.IsLoaded && d.Colour.Equals((ColourInfo)Colour4.White)) == 2);
        AddStep("capture cardinals", () => before = preview.PlayfieldForTests.AllHitObjects.ToArray());

        AddStep("apply multi-object batch", () => Assert.That(preview.Apply(batch(
            2,
            upserts:
            [
                state(7, new CardinalNote { StartTime = 2200, AngleDeg = 0 }),
                state(8, new CardinalNote { StartTime = 2200, AngleDeg = 180 }),
            ])), Is.True));

        AddAssert("no global refresh between deltas", () => refreshedBetweenDeltas, () => Is.False);
        AddUntilStep("final chord refreshed", () => before.All(d => d.Colour.Equals((ColourInfo)ChordColours.Highlight)));
        AddAssert("first drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Contains(before[0]));
        AddAssert("second drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Contains(before[1]));
    }

    [Test]
    public void TestScrollRangeReachesEveryScrollingContainer()
    {
        AddStep("apply full state", () => preview.Replace(fullState(1, previewChart(), [10, 20], 0, 700)));
        AddUntilStep("scroll containers loaded", () => preview.ChildrenOfType<GarbusScrollingHitObjectContainer>().Any(c => c.IsLoaded));

        AddStep("change time range", () => Assert.That(preview.Apply(rangeBatch(2, 2400)), Is.True));

        AddUntilStep("view range changed", () => preview.CurrentTimeRangeForTests == 2400);
        AddAssert("scrolling containers exist", () => preview.ChildrenOfType<GarbusScrollingHitObjectContainer>().Any());
        AddAssert("all scrolling ranges changed",
            () => preview.ChildrenOfType<GarbusScrollingHitObjectContainer>().Select(c => c.CurrentTimeRange).ToArray(),
            () => Is.All.EqualTo(2400));
    }

    [Test]
    public void TestStaleScrollRangeIsRejectedAndRequestsResync()
    {
        int resyncRequests = 0;
        AddStep("listen for resync", () => preview.ResyncRequested += () => resyncRequests++);
        AddStep("apply full state", () => Assert.That(preview.Replace(fullState(5, previewChart(), [10, 20], 0, 700)), Is.True));

        AddStep("apply stale scroll range", () => Assert.That(preview.Apply(rangeBatch(4, 2400)), Is.False));

        AddAssert("scroll range unchanged", () => preview.CurrentTimeRangeForTests, () => Is.EqualTo(700));
        AddAssert("resync requested", () => resyncRequests, () => Is.EqualTo(1));
    }

    [Test]
    public void TestRevisionStreamIsMonotonicAcrossMessageDomains()
    {
        int resyncRequests = 0;
        AddStep("listen for resync", () => preview.ResyncRequested += () => resyncRequests++);
        AddStep("apply full state", () => Assert.That(preview.Replace(fullState(5, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)), Is.True));
        AddStep("apply newer transport", () => Assert.That(preview.Apply(transportBatch(6, 1500, false, 1, 0)), Is.True));

        AddStep("reject older model update", () => Assert.That(preview.Apply(upsertBatch(
            5,
            7,
            GarbusChartCloner.CloneHitObject(new CardinalNote { StartTime = 2000, AngleDeg = 180 }))), Is.False));
        AddAssert("model unchanged", () => preview.PlayfieldForTests.AllHitObjects.Single().HitObject.StartTime, () => Is.EqualTo(1000));

        AddStep("apply newer scroll range", () => Assert.That(preview.Apply(rangeBatch(7, 2400)), Is.True));
        AddStep("reject older transport", () => Assert.That(preview.Apply(transportBatch(7, 9000, false, 1, 0)), Is.False));

        AddAssert("transport unchanged", () => preview.ClockTimeForTests, () => Is.EqualTo(1500).Within(0.001));
        AddAssert("scroll range retained", () => preview.CurrentTimeRangeForTests, () => Is.EqualTo(2400));
        AddAssert("stale batch requested resync", () => resyncRequests, () => Is.EqualTo(1));
    }

    [Test]
    public void TestEqualRevisionTransportIsIgnoredWithoutRequestingResync()
    {
        int resyncRequests = 0;
        AddStep("listen for resync", () => preview.ResyncRequested += () => resyncRequests++);
        AddStep("apply full state", () => Assert.That(preview.Replace(fullState(5, previewChart(), [10, 20], 1500, 700)), Is.True));
        AddUntilStep("initial transport applied", () => preview.ClockTimeForTests == 1500);

        AddStep("apply duplicate transport", () => Assert.That(
            preview.Apply(transportBatch(5, 9000, false, 1, 0)), Is.False));

        AddAssert("transport unchanged", () => preview.ClockTimeForTests, () => Is.EqualTo(1500).Within(0.001));
        AddAssert("no resync requested", () => resyncRequests, () => Is.Zero);
    }

    [Test]
    public void TestStructuralStateReplacesTutorialOverlay()
    {
        AddStep("apply full state", () => preview.Replace(fullState(1, previewChart(), [10, 20], 1200, 700)));
        AddUntilStep("old tutorial visible", () => preview.DesignOverlayForTests.MessageTextForTests == "Preview tutorial");

        AddStep("replace structural state", () =>
        {
            var replacement = new GarbusChart();
            replacement.DesignPointInfo.Add(new TutorialMessage
            {
                StartTime = 1000,
                EndTime = 2000,
                Text = "Replacement tutorial",
            });
            Assert.That(preview.Apply(structureBatch(2, GarbusChartSerializer.Encode(replacement))), Is.True);
        });

        AddUntilStep("replacement tutorial visible", () => preview.DesignOverlayForTests.MessageVisibleForTests
                                                               && preview.DesignOverlayForTests.MessageTextForTests == "Replacement tutorial");
        AddAssert("still one overlay", () => preview.ChildrenOfType<DesignOverlay>().Count(), () => Is.EqualTo(1));
        AddAssert("objects retained", () => preview.ObjectCountForTests, () => Is.EqualTo(2));
    }

    private static ChartPreviewSnapshot fullState(long revision, GarbusChart chart, long[] ids, double time, double timeRange) =>
        snapshot(revision, chart, ids, time, timeRange);

    private static ChartPreviewBatch transportBatch(long revision, double time, bool isRunning, double rate, long timestamp) =>
        batch(revision, transport: new PreviewTransportState(time, isRunning, rate, timestamp));

    private static ChartPreviewBatch upsertBatch(long revision, long id, GarbusHitObject hitObject) =>
        batch(revision, upserts: [state(id, hitObject)]);

    private static ChartPreviewBatch removeBatch(long revision, long id) =>
        batch(revision, removes: [new PreviewObjectId(id)]);

    private static ChartPreviewBatch rangeBatch(long revision, double timeRange) =>
        batch(revision, timeRange: timeRange);

    private ChartPreviewBatch structureBatch(long revision, string structureJson)
    {
        GarbusChart detached = GarbusChartSerializer.Decode(structureJson);
        return batch(revision, structure: new PreviewChartStructure(
            preview.CurrentChart.ChartId,
            detached.Metadata,
            detached.PreviewTime,
            detached.ControlPointInfo!,
            detached.DesignPointInfo));
    }

    private static GarbusChart previewChart()
    {
        var chart = chartWith(
            new CardinalNote { StartTime = 1500, AngleDeg = 90 },
            new SliderBody
            {
                StartTime = 2000,
                AngleDeg = 0,
                Side = Core.HorizontalDirection.Left,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 90 },
                    },
                },
            });
        chart.DesignPointInfo.Add(new TutorialMessage
        {
            StartTime = 1000,
            EndTime = 2000,
            Text = "Preview tutorial",
        });
        return chart;
    }

    private static GarbusChart chartWith(params GarbusHitObject[] hitObjects)
    {
        var chart = new GarbusChart();
        chart.HitObjects.AddRange(hitObjects);
        return chart;
    }

    private static SliderBody warningSlider(int angle, double startTime) => new SliderBody
    {
        AngleDeg = angle,
        Side = HorizontalDirection.Left,
        StartTime = startTime,
        Path = new GarbusPath
        {
            ControlPoints = new BindableList<GarbusPathControlPoint>
            {
                new GarbusPathControlPoint { TimeOffset = 200, RotationOffset = 0 },
            },
        },
    };

    private static Circle warningRingMask(WarningIndicatorDisplay warning) =>
        warning.ChildrenOfType<Circle>().First();

    private static BufferedContainer warningEffectBuffer(WarningIndicatorDisplay warning) =>
        (BufferedContainer)warningRingMask(warning).Parent!;

    private static Arc warningArc(WarningIndicatorDisplay warning) =>
        warning.ChildrenOfType<Arc>().First();

    private static BufferedContainer warningBlurBuffer(WarningIndicatorDisplay warning) =>
        (BufferedContainer)warningArc(warning).Parent!;

    private static SmoothPath warningArcPath(WarningIndicatorDisplay warning) =>
        warningArc(warning).ChildrenOfType<SmoothPath>().Single();

    private static float warningRayAlignment(WarningIndicatorDisplay warning, int angle)
    {
        SmoothPath path = warningArcPath(warning);

        if (path.Vertices.Count == 0)
            return -1;

        Vector2 direction = directionAt(angle);
        Vector2 generatedDirection = generatedCentreVertex(path) - warningRingMask(warning).ScreenSpaceDrawQuad.Centre;
        return Vector2.Dot(generatedDirection / generatedDirection.Length, direction);
    }

    private static float visibleWarningSpanAt(WarningIndicatorDisplay warning, int angle)
    {
        SmoothPath path = warningArcPath(warning);

        if (path.Vertices.Count == 0)
            return 0;

        Vector2 direction = directionAt(angle);
        Vector2 centre = warningRingMask(warning).ScreenSpaceDrawQuad.Centre;
        Vector2 localVertex = path.PositionInBoundingBox(path.Vertices[path.Vertices.Count / 2]);
        Vector2 screenVertex = path.ToScreenSpace(localVertex);
        float centrelineDistance = Vector2.Dot(screenVertex - centre, direction);
        float screenPathRadius = (path.ToScreenSpace(localVertex + new Vector2(path.PathRadius, 0)) - screenVertex).Length;
        float clipDistance = MathF.Min(
            distanceToCardinalEdge(warningEffectBuffer(warning), centre, angle),
            distanceToCardinalEdge(warningBlurBuffer(warning), centre, angle));
        float maskDistance = distanceToCardinalEdge(warningRingMask(warning), centre, angle);

        return MathF.Min(centrelineDistance + screenPathRadius, clipDistance)
               - MathF.Max(centrelineDistance - screenPathRadius, maskDistance);
    }

    private static float visibleWarningAlpha(WarningIndicatorDisplay warning)
    {
        SmoothPath path = warningArcPath(warning);
        BufferedContainer blur = warningBlurBuffer(warning);
        BufferedContainer effect = warningEffectBuffer(warning);

        return (path.FrameBufferDrawColour?.Colour.MaxAlpha ?? 0)
               * (blur.FrameBufferDrawColour?.Colour.MaxAlpha ?? 0)
               * blur.EffectColour.MaxAlpha
               * (effect.FrameBufferDrawColour?.Colour.MaxAlpha ?? 0)
               * effect.EffectColour.MaxAlpha;
    }

    private static bool warningRenderedPathMatches(
        WarningIndicatorDisplay gameplay,
        WarningIndicatorDisplay candidate)
    {
        Arc gameplayArc = warningArc(gameplay);
        Arc candidateArc = warningArc(candidate);
        SmoothPath gameplayPath = warningArcPath(gameplay);
        SmoothPath candidatePath = warningArcPath(candidate);
        float gameplayDiameter = warningRingMask(gameplay).DrawWidth;
        float candidateDiameter = warningRingMask(candidate).DrawWidth;

        if (gameplayPath.Vertices.Count != candidatePath.Vertices.Count
            || Math.Abs(gameplayArc.StartRadians.Value - candidateArc.StartRadians.Value) > 0.0001f
            || Math.Abs(gameplayArc.EndRadians.Value - candidateArc.EndRadians.Value) > 0.0001f
            || Math.Abs(gameplayPath.PathRadius / gameplayDiameter - candidatePath.PathRadius / candidateDiameter) > 0.0001f
            || warningBlurBuffer(gameplay).BlurSigma != warningBlurBuffer(candidate).BlurSigma
            || !warningRingMask(gameplay).Blending.Equals(warningRingMask(candidate).Blending))
            return false;

        for (int i = 0; i < gameplayPath.Vertices.Count; i++)
        {
            if ((gameplayPath.Vertices[i] / gameplayDiameter
                 - candidatePath.Vertices[i] / candidateDiameter).Length > 0.0001f)
                return false;
        }

        return visibleWarningAlpha(gameplay) > 0 && visibleWarningAlpha(candidate) > 0;
    }

    private static Vector2 generatedCentreVertex(SmoothPath path)
    {
        Vector2 vertex = path.Vertices[path.Vertices.Count / 2];
        return path.ToScreenSpace(path.PositionInBoundingBox(vertex));
    }

    private static Vector2 directionAt(int angle)
    {
        float radians = angle * MathF.PI / 180;
        return new Vector2(MathF.Cos(radians), -MathF.Sin(radians));
    }

    private static float distanceToCardinalEdge(Drawable drawable, Vector2 centre, int angle)
    {
        var bounds = drawable.ScreenSpaceDrawQuad.AABBFloat;

        return angle switch
        {
            0 => bounds.Right - centre.X,
            90 => centre.Y - bounds.Top,
            180 => centre.X - bounds.Left,
            270 => bounds.Bottom - centre.Y,
            _ => throw new ArgumentOutOfRangeException(nameof(angle)),
        };
    }

    private static System.Collections.Generic.IEnumerable<DrawableHitObject> withNested(DrawableHitObject drawable)
    {
        yield return drawable;
        foreach (DrawableHitObject nested in drawable.NestedHitObjects.SelectMany(withNested))
            yield return nested;
    }

    private static Dictionary<Drawable, int> disposalCountsFor(IEnumerable<Drawable> drawables)
    {
        var counts = drawables.ToDictionary(drawable => drawable, _ => 0);
        var addOnDispose = typeof(Drawable).GetEvent("OnDispose",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetAddMethod(true)!;

        foreach (Drawable drawable in counts.Keys)
        {
            Drawable tracked = drawable;
            addOnDispose.Invoke(drawable, [(Action)(() => counts[tracked]++)]);
        }

        return counts;
    }

    private static bool isDisposed(Drawable drawable) =>
        (bool)typeof(Drawable).GetProperty("IsDisposed",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
            .GetValue(drawable)!;

    private static int internalChildIndex(CompositeDrawable parent, Drawable child) =>
        (int)typeof(CompositeDrawable).GetMethod("IndexOfInternal",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(parent, [child])!;

    private void useGatedDrawables(List<IGatedDrawable> generations)
    {
        AddStep("create preview with gated drawables", () => Child = preview = createGatedPreview(generations));
        AddUntilStep("gated preview content loaded", () => preview.IsLoaded);
    }

    private static ChartPreviewContent createGatedPreview(List<IGatedDrawable> generations) => new(
        hitObject =>
        {
            IGatedDrawable generation = hitObject switch
            {
                CardinalNote note => new GatedDrawableCardinalNote(note),
                CardinalHoldNote hold => new GatedDrawableCardinalHoldNote(hold),
                ShoulderNote note => new GatedDrawableShoulderNote(note),
                _ => throw new ArgumentOutOfRangeException(nameof(hitObject)),
            };
            generations.Add(generation);
            return generation.Drawable;
        },
        drawable => drawable.IsLoaded
                    && generations.Single(generation => ReferenceEquals(generation.Drawable, drawable)).LoadReleased)
    {
        Size = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
    };

    private static Dictionary<long, DrawableHitObject> pendingVisualRefreshes(ChartPreviewContent content) =>
        content.PendingVisualRefreshesForTests.ToDictionary(pair => pair.Key.Value, pair => pair.Value);

    private interface IGatedDrawable
    {
        DrawableHitObject Drawable { get; }

        int VisualApplyCount { get; }

        bool LoadReleased { get; }

        void ReleaseLoad();
    }

    private partial class GatedDrawableCardinalNote : DrawableCardinalNote, IGatedDrawable
    {
        public DrawableHitObject Drawable => this;

        public int VisualApplyCount { get; private set; }

        public bool LoadReleased { get; private set; }

        public GatedDrawableCardinalNote(CardinalNote hitObject)
            : base(hitObject)
        {
        }

        protected override void OnApply()
        {
            base.OnApply();
            VisualApplyCount++;
        }

        public void ReleaseLoad() => LoadReleased = true;
    }

    private partial class GatedDrawableShoulderNote : DrawableShoulderNote, IGatedDrawable
    {
        public DrawableHitObject Drawable => this;

        public int VisualApplyCount { get; private set; }

        public bool LoadReleased { get; private set; }

        public GatedDrawableShoulderNote(ShoulderNote hitObject)
            : base(hitObject)
        {
        }

        protected override void OnApply()
        {
            base.OnApply();
            VisualApplyCount++;
        }

        public void ReleaseLoad() => LoadReleased = true;
    }

    private partial class GatedDrawableCardinalHoldNote : DrawableCardinalHoldNote, IGatedDrawable
    {
        public DrawableHitObject Drawable => this;

        public int VisualApplyCount { get; private set; }

        public bool LoadReleased { get; private set; }

        public GatedDrawableCardinalHoldNote(CardinalHoldNote hitObject)
            : base(hitObject)
        {
        }

        protected override void OnApply()
        {
            base.OnApply();
            VisualApplyCount++;
        }

        public void ReleaseLoad() => LoadReleased = true;
    }

    private static bool hasRightColour(Drawable drawable) => drawable.Colour.Equals(Constants.RightColour);

    [Test]
    public void TestTypedMultiObjectBatchConsumesOneRevision()
    {
        AddStep("replace typed state", () => Assert.That(preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 2000, AngleDeg = 90 }), [7, 8], 500, 700)), Is.True));

        AddStep("apply two-object batch", () => Assert.That(preview.Apply(batch(
            2,
            upserts:
            [
                state(7, new CardinalNote { StartTime = 1100, AngleDeg = 45 }),
                state(8, new CardinalNote { StartTime = 2100, AngleDeg = 135 }),
            ])), Is.True));

        AddAssert("batch consumes one revision", () => preview.AcceptedRevision, () => Is.EqualTo(2));
        AddAssert("both objects committed", () => preview.CurrentChart.HitObjects.Select(hitObject => hitObject.StartTime),
            () => Is.EqualTo(new[] { 1100, 2100 }));
    }

    [Test]
    public void TestTypedRevisionGapRejectsWholeBatch()
    {
        DrawableHitObject original = null!;

        AddStep("replace typed state", () => preview.Replace(snapshot(3, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture drawable", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("reject revision gap", () => Assert.That(preview.Apply(batch(
            5,
            upserts: [state(7, new CardinalNote { StartTime = 9000, AngleDeg = 180 })],
            timeRange: 2400,
            transport: new PreviewTransportState(9000, false, 1, 0))), Is.False));

        AddAssert("gap changes nothing", () => preview.AcceptedRevision == 3
                                                   && preview.CurrentChart.HitObjects.Single().StartTime == 1000
                                                   && ReferenceEquals(preview.DrawableForTests(new PreviewObjectId(7)), original)
                                                   && preview.CurrentTimeRangeForTests == 700
                                                   && preview.ClockTimeForTests == 500);
    }

    [Test]
    public void TestTypedInvalidCollectionsAndValuesReject()
    {
        AddStep("replace typed state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));

        AddStep("reject duplicate snapshot ids", () => Assert.That(preview.Replace(snapshot(2, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 90 },
            new CardinalNote { StartTime = 3000, AngleDeg = 180 }), [8, 8], 9000, 2400)), Is.False));
        AddStep("reject default collections", () => Assert.That(preview.Apply(new ChartPreviewBatch(
            2,
            default,
            ImmutableArray<PreviewObjectState>.Empty,
            null,
            null,
            null)), Is.False));
        AddStep("reject nonpositive id", () => Assert.That(preview.Apply(batch(
            2,
            upserts: [state(0, new CardinalNote { AngleDeg = 0 })])), Is.False));
        AddStep("reject duplicate upsert ids", () => Assert.That(preview.Apply(batch(2, upserts:
        [
            state(8, new CardinalNote { AngleDeg = 0 }),
            state(8, new CardinalNote { AngleDeg = 0 }),
        ])), Is.False));
        AddStep("reject missing removal", () => Assert.That(preview.Apply(batch(2, removes: [new PreviewObjectId(99)])), Is.False));
        AddStep("reject remove upsert overlap", () => Assert.That(preview.Apply(batch(
            2,
            removes: [new PreviewObjectId(7)],
            upserts: [state(7, new CardinalNote { AngleDeg = 0 })])), Is.False));
        AddStep("reject invalid range", () => Assert.That(preview.Apply(batch(2, timeRange: double.NaN)), Is.False));
        AddStep("reject invalid rate", () => Assert.That(preview.Apply(batch(
            2,
            transport: new PreviewTransportState(500, false, double.PositiveInfinity, 0))), Is.False));
        AddStep("reject mismatched chart identity", () =>
        {
            GarbusChart current = preview.CurrentChart;
            Assert.That(preview.Apply(batch(2, structure: new PreviewChartStructure(
                Guid.NewGuid(),
                current.Metadata,
                current.PreviewTime,
                current.ControlPointInfo!,
                current.DesignPointInfo))), Is.False);
        });
        AddStep("reject unsupported object", () => Assert.That(preview.Apply(batch(
            2,
            upserts: [state(8, new UnsupportedHitObject())])), Is.False));
        AddStep("reject derived supported object", () => Assert.That(preview.Apply(batch(
            2,
            upserts: [state(8, new DerivedCardinalNote { AngleDeg = 0 })])), Is.False));

        AddAssert("all invalid batches leave initial state", () => preview.AcceptedRevision == 1
                                                               && preview.ObjectCountForTests == 1
                                                               && preview.CurrentChart.HitObjects.Single().StartTime == 1000
                                                               && preview.CurrentTimeRangeForTests == 700
                                                               && preview.ClockTimeForTests == 500);
    }

    [Test]
    public void TestTypedInvalidLaterUpsertMutatesNothing()
    {
        DrawableHitObject original = null!;

        AddStep("replace typed state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture drawable", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("reject invalid later upsert", () => Assert.That(preview.Apply(batch(
            2,
            upserts:
            [
                state(7, new CardinalNote { StartTime = 2000, AngleDeg = 90 }),
                state(8, new UnsupportedHitObject { StartTime = 3000 }),
            ],
            timeRange: 2400,
            transport: new PreviewTransportState(9000, false, 1, 0))), Is.False));

        AddAssert("later failure is atomic", () => preview.AcceptedRevision == 1
                                                      && preview.ObjectCountForTests == 1
                                                      && preview.CurrentChart.HitObjects.Single().StartTime == 1000
                                                      && ReferenceEquals(preview.DrawableForTests(new PreviewObjectId(7)), original)
                                                      && original.HitObject.StartTime == 1000
                                                      && preview.CurrentTimeRangeForTests == 700
                                                      && preview.ClockTimeForTests == 500);
    }

    [Test]
    public void TestTypedNewerSnapshotAuthoritativelyReplacesState()
    {
        DrawableHitObject original = null!;
        var replacement = new ShoulderNote { StartTime = 3000, Side = Core.HorizontalDirection.Right };

        AddStep("replace initial state", () => preview.Replace(snapshot(2, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture initial drawable", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("replace authoritative state", () => Assert.That(preview.Replace(snapshot(
            8, chartWith(replacement), [12], 4000, 2400)), Is.True));

        AddAssert("snapshot owns all new state", () => preview.AcceptedRevision == 8
                                                        && preview.ObjectCountForTests == 1
                                                        && preview.CurrentChart.HitObjects.Single() is ShoulderNote { StartTime: 3000 }
                                                        && preview.DrawableForTests(new PreviewObjectId(12)).HitObject is ShoulderNote
                                                        && preview.CurrentTimeRangeForTests == 2400
                                                        && preview.ClockTimeForTests == 4000);
        AddAssert("old snapshot drawable disposed", () => isDisposed(original));
    }

    [Test]
    public void TestTypedSameTypeUpsertRetainsRootAndAppliesIncomingObject()
    {
        DrawableHitObject original = null!;
        var incoming = new CardinalNote { StartTime = 2000, AngleDeg = 180 };

        AddStep("replace initial state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture root", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("apply same type", () => Assert.That(preview.Apply(batch(2, upserts: [state(7, incoming)])), Is.True));

        AddAssert("root retained", () => preview.DrawableForTests(new PreviewObjectId(7)), () => Is.SameAs(original));
        AddAssert("incoming object is content state", () => preview.CurrentChart.HitObjects.Single(), () => Is.SameAs(incoming));
        AddAssert("incoming object applied to root", () => original.HitObject, () => Is.SameAs(incoming));
    }

    [Test]
    public void TestTypedTypeChangingUpsertReplacesAndDisposesRoot()
    {
        DrawableHitObject original = null!;

        AddStep("replace initial state", () => preview.Replace(snapshot(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)));
        AddStep("capture root", () => original = preview.DrawableForTests(new PreviewObjectId(7)));
        AddStep("apply type replacement", () => Assert.That(preview.Apply(batch(2, upserts:
        [
            state(7, new ShoulderNote { StartTime = 2000, Side = Core.HorizontalDirection.Left }),
        ])), Is.True));

        AddAssert("root replaced", () => preview.DrawableForTests(new PreviewObjectId(7)), () => Is.Not.SameAs(original));
        AddAssert("old root disposed", () => isDisposed(original));
    }

    [Test]
    public void TestTypedEqualValuedObjectsRemainDistinctById()
    {
        var first = new CardinalNote { StartTime = 1000, AngleDeg = 90 };
        var second = new CardinalNote { StartTime = 1000, AngleDeg = 90 };

        AddStep("replace equal valued objects", () => Assert.That(preview.Replace(snapshot(
            1, chartWith(first, second), [7, 8], 500, 700)), Is.True));

        AddAssert("both ids retained", () => preview.ObjectCountForTests, () => Is.EqualTo(2));
        AddAssert("roots are distinct", () => preview.DrawableForTests(new PreviewObjectId(7)),
            () => Is.Not.SameAs(preview.DrawableForTests(new PreviewObjectId(8))));
    }

    private static ChartPreviewSnapshot snapshot(
        long revision,
        GarbusChart source,
        long[] ids,
        double time,
        double timeRange)
    {
        GarbusChart detached = GarbusChartSerializer.Decode(GarbusChartSerializer.Encode(source));
        var structure = new PreviewChartStructure(
            source.ChartId,
            detached.Metadata,
            detached.PreviewTime,
            detached.ControlPointInfo!,
            detached.DesignPointInfo);
        ImmutableArray<PreviewObjectState> objects = detached.HitObjects
                                                             .Select((hitObject, index) => state(ids[index], hitObject))
                                                             .ToImmutableArray();
        return new ChartPreviewSnapshot(
            revision,
            structure,
            objects,
            timeRange,
            new PreviewTransportState(time, false, 1, 0));
    }

    private static ChartPreviewBatch batch(
        long revision,
        ImmutableArray<PreviewObjectId> removes = default,
        ImmutableArray<PreviewObjectState> upserts = default,
        PreviewChartStructure? structure = null,
        double? timeRange = null,
        PreviewTransportState? transport = null)
        => new(
            revision,
            removes.IsDefault ? ImmutableArray<PreviewObjectId>.Empty : removes,
            upserts.IsDefault ? ImmutableArray<PreviewObjectState>.Empty : upserts,
            structure,
            timeRange,
            transport);

    private static PreviewObjectState state(long id, GarbusHitObject hitObject) => new(new PreviewObjectId(id), hitObject);

    private sealed class UnsupportedHitObject : GarbusHitObject
    {
        public override HitsoundFamily Hitsounds { get; } = new();
    }

    private sealed class DerivedCardinalNote : CardinalNote
    {
    }
}
