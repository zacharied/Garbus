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
        AddStep("apply full state", () => Assert.That(preview.Apply(fullState(1, previewChart(), [10, 20], 1200, 900)), Is.True));
        AddUntilStep("objects loaded", () => preview.ObjectCountForTests == 2
                                                && preview.PlayfieldForTests.AllHitObjects.All(d => d.IsLoaded));

        AddAssert("one playfield", () => preview.ChildrenOfType<GarbusPlayfield>().Count(), () => Is.EqualTo(1));
        AddAssert("one design overlay", () => preview.ChildrenOfType<DesignOverlay>().Count(), () => Is.EqualTo(1));
        AddAssert("no key input manager", () => preview.ChildrenOfType<GarbusInputManager>().Any(), () => Is.False);
        AddAssert("no analog input manager", () => preview.ChildrenOfType<AnalogInputManager>().Any(), () => Is.False);
        AddAssert("no analog input manager constructed", () =>
            typeof(GarbusPlayfield).GetProperty("analogInputManager",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(preview.PlayfieldForTests), () => Is.Null);
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
            Assert.That(preview.Apply(new ChartPreviewTransport(2, 2200, false, 1, 0)), Is.True));
        AddUntilStep("slider consumers update without analog input", () =>
            preview.ClockTimeForTests == 2200
            && preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault() is { IsLoaded: true } slider
            && slider.NestedHitObjects.All(d => d.IsLoaded));
    }

    [Test]
    public void TestSquarePreviewWarningKeepsCardinalHeadroom()
    {
        const float ring_diameter = ChartPreviewContent.TARGET_DRAW_SIZE - 60;
        int[] angles = [0, 90, 180, 270];
        double[] startTimes = [5000, 10000, 15000, 20000];
        ChartPreviewContent squarePreview = null!;
        ChartPreviewContent miniPreview = null!;
        WarningIndicatorDisplay warning = null!;
        WarningIndicatorDisplay miniWarning = null!;
        GarbusPlayfield gameplayPlayfield = null!;
        ManualClock gameplayClock = null!;
        Container miniHost = null!;

        AddStep("create gameplay, square preview, and mini preview", () =>
        {
            gameplayClock = new ManualClock();
            Child = new Container
            {
                Size = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
                Children =
                [
                    new Container
                    {
                        Size = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
                        Clock = new FramedClock(gameplayClock),
                        Child = new GarbusInputManager
                        {
                            Child = gameplayPlayfield = new GarbusPlayfield { Size = Vector2.One },
                        },
                    },
                    squarePreview = new ChartPreviewContent
                    {
                        Size = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
                    },
                    miniHost = new Container
                    {
                        Size = new Vector2(InlineChartPreviewPanel.SIZE),
                        Child = new DrawSizePreservingFillContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            TargetDrawSize = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
                            Child = miniPreview = new ChartPreviewContent
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                        },
                    },
                ],
            };
        });
        AddUntilStep("warning render branches loaded", () =>
            squarePreview.IsLoaded && miniPreview.IsLoaded && gameplayPlayfield.IsLoaded);
        AddStep("apply cardinal warning sliders", () =>
        {
            GarbusChart chart = chartWith(
                angles.Select((angle, index) => warningSlider(angle, startTimes[index])).ToArray());
            warning = squarePreview.PlayfieldForTests.WarningIndicators;
            miniWarning = miniPreview.PlayfieldForTests.WarningIndicators;
            gameplayPlayfield.SetHitObjects(chart.HitObjects);
            gameplayClock.CurrentTime = startTimes[0] - 1000;
            Assert.That(squarePreview.Apply(fullState(1, chart, [1, 2, 3, 4], startTimes[0] - 1000, 700)), Is.True);
            Assert.That(miniPreview.Apply(fullState(1, chart, [1, 2, 3, 4], startTimes[0] - 1000, 700)), Is.True);
        });
        AddUntilStep("first cardinal warning revealed", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == angles[0]
            && miniWarning.RevealedAngleDeg(HorizontalDirection.Left) == angles[0]
            && gameplayPlayfield.WarningIndicators.RevealedAngleDeg(HorizontalDirection.Left) == angles[0]);
        AddStep("advance first warning fade", () =>
        {
            gameplayClock.CurrentTime = startTimes[0] - 900;
            Assert.That(squarePreview.Apply(new ChartPreviewTransport(2, startTimes[0] - 900, false, 1, 0)), Is.True);
            Assert.That(miniPreview.Apply(new ChartPreviewTransport(2, startTimes[0] - 900, false, 1, 0)), Is.True);
        });
        AddUntilStep("first warning output visible", () =>
            visibleWarningAlpha(warning) > 0
            && visibleWarningAlpha(miniWarning) > 0
            && visibleWarningAlpha(gameplayPlayfield.WarningIndicators) > 0);

        AddAssert("preview effect buffer uses full square", () => warningEffectBuffer(warning).DrawSize,
            () => Is.EqualTo(new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE)));
        AddAssert("preview blur buffer uses full square", () => warningBlurBuffer(warning).DrawSize,
            () => Is.EqualTo(new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE)));
        AddAssert("preview mask retains ring diameter", () => warningRingMask(warning).DrawSize,
            () => Is.EqualTo(new Vector2(ring_diameter)));
        AddAssert("preview arc retains ring-relative diameter", () =>
                warningArc(warning).DrawWidth / warningRingMask(warning).DrawWidth,
            () => Is.EqualTo(1.1f).Within(0.001f));
        AddAssert("preview effect and ring stay centred", () =>
                (warningEffectBuffer(warning).ScreenSpaceDrawQuad.Centre
                 - warningRingMask(warning).ScreenSpaceDrawQuad.Centre).Length,
            () => Is.LessThan(0.01f));
        AddAssert("mini keeps normalized ring radius", () =>
                warningRingMask(miniWarning).ScreenSpaceDrawQuad.Width / miniHost.ScreenSpaceDrawQuad.Width,
            () => Is.EqualTo(ring_diameter / ChartPreviewContent.TARGET_DRAW_SIZE).Within(0.001f));

        for (int i = 0; i < angles.Length; i++)
        {
            int index = i;

            if (index > 0)
            {
                AddStep($"seek to {angles[index]} degree warning", () =>
                {
                    gameplayClock.CurrentTime = startTimes[index] - 1000;
                    Assert.That(squarePreview.Apply(
                        new ChartPreviewTransport(index + 2, startTimes[index] - 1000, false, 1, 0)), Is.True);
                    Assert.That(miniPreview.Apply(
                        new ChartPreviewTransport(index + 2, startTimes[index] - 1000, false, 1, 0)), Is.True);
                });
                AddUntilStep($"{angles[index]} degree warning revealed", () =>
                    warning.RevealedAngleDeg(HorizontalDirection.Left) == angles[index]
                    && miniWarning.RevealedAngleDeg(HorizontalDirection.Left) == angles[index]
                    && gameplayPlayfield.WarningIndicators.RevealedAngleDeg(HorizontalDirection.Left) == angles[index]);
            }

            AddAssert($"{angles[index]} degree preview path matches gameplay", () =>
                warningRenderedPathMatches(gameplayPlayfield.WarningIndicators, warning));
            AddAssert($"{angles[index]} degree mini path matches gameplay", () =>
                warningRenderedPathMatches(gameplayPlayfield.WarningIndicators, miniWarning));
            AddAssert($"{angles[index]} degree warning path has vertices", () =>
                warningArcPath(warning).Vertices.Count, () => Is.GreaterThan(0));
            AddAssert($"{angles[index]} degree warning path uses target ray", () =>
                warningRayAlignment(warning, angles[index]), () => Is.GreaterThan(0.999f));
            AddAssert($"{angles[index]} degree path stroke remains visible outside ring", () =>
                visibleWarningSpanAt(warning, angles[index]), () => Is.GreaterThan(0));
            AddAssert($"{angles[index]} degree warning output has visible alpha", () =>
                visibleWarningAlpha(warning), () => Is.GreaterThan(0));
            AddAssert($"{angles[index]} degree mini path uses target ray", () =>
                warningRayAlignment(miniWarning, angles[index]), () => Is.GreaterThan(0.999f));
            AddAssert($"{angles[index]} degree mini stroke remains visible outside ring", () =>
                visibleWarningSpanAt(miniWarning, angles[index]), () => Is.GreaterThan(0));
        }
    }

    [Test]
    public void TestSameTypeUpsertRetainsDrawableAndRecalculatesRouting()
    {
        DrawableHitObject before = null!;
        AddStep("apply full state", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700)));
        AddUntilStep("drawable loaded", () => preview.PlayfieldForTests.AllHitObjects.SingleOrDefault()?.IsLoaded == true);
        AddStep("capture drawable", () => before = preview.PlayfieldForTests.AllHitObjects.Single());

        AddStep("upsert cardinal", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            2,
            7,
            GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 2500, AngleDeg = 180 }))), Is.True));

        AddUntilStep("same drawable updated", () => preview.PlayfieldForTests.AllHitObjects.SingleOrDefault()?.HitObject.StartTime == 2500);
        AddAssert("drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Single(), () => Is.SameAs(before));
        AddAssert("model retained", () => preview.PlayfieldForTests.AllHitObjects.Single().HitObject, () => Is.SameAs(before.HitObject));
        AddAssert("angle updated", () => ((CardinalNote)before.HitObject).AngleDeg, () => Is.EqualTo(180));
        AddAssert("input remains disabled", () => before.HandleUserInput, () => Is.False);
    }

    [Test]
    public void TestSameTypeSliderUpsertRefreshesSideAndRetainsRenderResources()
    {
        DrawableSliderBody before = null!;
        Drawable bufferedPath = null!;

        AddStep("apply left slider", () => preview.Apply(fullState(1, chartWith(new SliderBody
        {
            StartTime = 2000,
            AngleDeg = 0,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 90 },
                },
            },
        }), [7], 1900, 700)));
        AddUntilStep("slider loaded", () => preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault()?.IsLoaded == true);
        AddStep("capture slider resources", () =>
        {
            before = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().Single();
            bufferedPath = before.ChildrenOfType<Container<SmoothPath>>()
                                 .Single(c => !ReferenceEquals(c.Parent, before))
                                 .Parent!;
        });

        AddStep("upsert right slider", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            2,
            7,
            GarbusChartSerializer.EncodeHitObject(new SliderBody
            {
                StartTime = 2100,
                AngleDeg = 180,
                Side = HorizontalDirection.Right,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 750, RotationOffset = -90 },
                    },
                },
            }))), Is.True));

        AddUntilStep("slider side visuals refreshed", () => sliderSideVisuals(before).All(hasRightColour));
        AddAssert("slider drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Single(), () => Is.SameAs(before));
        AddAssert("slider buffered path retained", () => before.ChildrenOfType<Container<SmoothPath>>()
                                                                    .Single(c => !ReferenceEquals(c.Parent, before))
                                                                    .Parent, () => Is.SameAs(bufferedPath));
        AddAssert("slider path geometry refreshed", () => before.AngleDegAt(2850), () => Is.EqualTo(90).Within(0.001));
    }

    [Test]
    public void TestSameTypeSlamUpsertsRefreshSideAngleAndDirectionVisuals()
    {
        DrawableSlamCentered centred = null!;
        DrawableSlamEdge edge = null!;

        AddStep("apply left slams", () => preview.Apply(fullState(1, chartWith(
            new GarbusSlamCentered { StartTime = 2000, AngleDeg = 45, Side = HorizontalDirection.Left },
            new GarbusSlamEdge
            {
                StartTime = 2500,
                AngleDeg = 90,
                Side = HorizontalDirection.Left,
                Direction = RotationalDirection.Clockwise,
            }), [7, 8], 2000, 700)));
        AddUntilStep("slams loaded", () => preview.PlayfieldForTests.AllHitObjects.All(d => d.IsLoaded));
        AddStep("capture slams", () =>
        {
            centred = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSlamCentered>().Single();
            edge = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSlamEdge>().Single();
        });

        AddStep("upsert centred slam", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            2,
            7,
            GarbusChartSerializer.EncodeHitObject(new GarbusSlamCentered
            {
                StartTime = 2100,
                AngleDeg = 135,
                Side = HorizontalDirection.Right,
            }))), Is.True));
        AddStep("upsert edge slam", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            3,
            8,
            GarbusChartSerializer.EncodeHitObject(new GarbusSlamEdge
            {
                StartTime = 2600,
                AngleDeg = 225,
                Side = HorizontalDirection.Right,
                Direction = RotationalDirection.Anticlockwise,
            }))), Is.True));

        AddUntilStep("slam visuals refreshed", () => centred.Rotation == -45
                                                        && edge.Rotation == 135
                                                        && centred.ChildrenOfType<Sprite>().Single().Colour.Equals(Constants.RightColour)
                                                        && edge.ChildrenOfType<Sprite>().Single().Colour.Equals(Constants.RightColour));
        AddAssert("centred drawable retained", () => preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSlamCentered>().Single(), () => Is.SameAs(centred));
        AddAssert("edge drawable retained", () => preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSlamEdge>().Single(), () => Is.SameAs(edge));
    }

    [Test]
    public void TestSameTypeUpsertRefreshesCardinalChordColours()
    {
        DrawableHitObject[] before = null!;

        AddStep("apply separate cardinals", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalHoldNote { StartTime = 2100, AngleDeg = 180, Duration = 500 }), [7, 8], 1900, 700)));
        AddUntilStep("cardinals loaded white", () => preview.PlayfieldForTests.AllHitObjects
                                                         .Count(d => d.IsLoaded && d.Colour.Equals((ColourInfo)Colour4.White)) == 2);
        AddStep("capture cardinals", () => before = preview.PlayfieldForTests.AllHitObjects.ToArray());

        AddStep("move one cardinal into chord", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            2,
            8,
            GarbusChartSerializer.EncodeHitObject(new CardinalHoldNote
            {
                StartTime = 2000,
                AngleDeg = 180,
                Duration = 500,
            }))), Is.True));

        AddUntilStep("both cardinals refreshed yellow", () => before.All(d => d.Colour.Equals((ColourInfo)ChordColours.Highlight)));
        AddAssert("first cardinal retained", () => preview.PlayfieldForTests.AllHitObjects.Contains(before[0]));
        AddAssert("second cardinal retained", () => preview.PlayfieldForTests.AllHitObjects.Contains(before[1]));
    }

    [Test]
    public void TestFullStateFreshChordColoursAfterLoadAndRewind()
    {
        DrawableCardinalNote note = null!;
        DrawableCardinalHoldNote hold = null!;

        AddStep("apply fresh note and hold chord", () => Assert.That(preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalHoldNote { StartTime = 2000, AngleDeg = 180, Duration = 500 }), [7, 8], 1900, 700)), Is.True));
        AddUntilStep("fresh chord roots loaded", () =>
            preview.DrawableForTests(7) is DrawableCardinalNote { IsLoaded: true } loadedNote
            && preview.DrawableForTests(8) is DrawableCardinalHoldNote { IsLoaded: true } loadedHold
            && (note = loadedNote) != null
            && (hold = loadedHold) != null);
        AddAssert("fresh chord roots are yellow", () => new[] { note.Colour, hold.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));

        AddStep("seek through both chord results", () =>
            Assert.That(preview.Apply(new ChartPreviewTransport(2, 2500, false, 1, 0)), Is.True));
        AddUntilStep("both chord roots hit", () => note.Judged && hold.Judged);
        AddAssert("first chord results are exact", () => new[] { note.Result.RawTime, hold.Result.RawTime },
            () => Is.EqualTo(new[] { 2000, 2500 }));
        AddAssert("chord roots stay yellow through results", () => new[] { note.Colour, hold.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));

        AddStep("rewind before chord", () =>
            Assert.That(preview.Apply(new ChartPreviewTransport(3, 1900, false, 1, 0)), Is.True));
        AddUntilStep("both chord roots rewind", () => !note.Judged && !hold.Judged);
        AddAssert("chord roots stay yellow after rewind", () => new[] { note.Colour, hold.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));

        AddStep("reapply both chord results", () =>
            Assert.That(preview.Apply(new ChartPreviewTransport(4, 2500, false, 1, 0)), Is.True));
        AddUntilStep("both chord roots hit again", () => note.Judged && hold.Judged);
        AddAssert("reapplied chord results are exact", () => new[] { note.Result.RawTime, hold.Result.RawTime },
            () => Is.EqualTo(new[] { 2000, 2500 }));
        AddAssert("chord roots stay yellow after reapply", () => new[] { note.Colour, hold.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));
    }

    [Test]
    public void TestNewObjectUpsertColoursFreshChordMember()
    {
        DrawableCardinalNote existing = null!;
        DrawableCardinalNote fresh = null!;

        AddStep("apply one cardinal", () => Assert.That(preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True));
        AddUntilStep("existing cardinal loaded white", () =>
            preview.DrawableForTests(7) is DrawableCardinalNote { IsLoaded: true } loaded
            && loaded.Colour.Equals((ColourInfo)Colour4.White)
            && (existing = loaded) != null);

        AddStep("upsert fresh chord member", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            2,
            8,
            GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 2000, AngleDeg = 180 }))), Is.True));
        AddUntilStep("fresh chord member loaded", () =>
            preview.DrawableForTests(8) is DrawableCardinalNote { IsLoaded: true } loaded
            && (fresh = loaded) != null);
        AddAssert("existing and fresh chord members are yellow", () => new[] { existing.Colour, fresh.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));
        AddAssert("chord connector indexes both members", () =>
            preview.ChildrenOfType<ChordConnectorOverlay>().Single()
                   .ChildrenOfType<SmoothPath>().SingleOrDefault() is { IsPresent: true });

        AddStep("seek through live chord", () =>
            Assert.That(preview.Apply(new ChartPreviewTransport(3, 2000, false, 1, 0)), Is.True));
        AddUntilStep("both live chord members hit", () => existing.Judged && fresh.Judged);
        AddAssert("both live chord results are exact", () => new[] { existing.Result.RawTime, fresh.Result.RawTime },
            () => Is.EqualTo(new[] { 2000, 2000 }));
    }

    [Test]
    public void TestPendingVisualRefreshIsClearedWhenObjectIsRemoved()
    {
        var generations = new List<IGatedDrawable>();
        useGatedDrawables(generations);

        AddStep("apply pending unloaded drawable", () =>
        {
            Assert.That(preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddStep("remove pending drawable", () =>
            Assert.That(preview.Apply(new ChartPreviewObjectRemove(2, 7)), Is.True));
        AddAssert("removal clears pending ownership", () => pendingVisualRefreshes(preview), () => Is.Empty);

        AddStep("add replacement work to advance updates", () =>
            Assert.That(preview.Apply(new ChartPreviewObjectUpsert(3, 8,
                GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 2500, AngleDeg = 90 }))), Is.True));
        AddUntilStep("current generation loads behind closed gate", () =>
            generations.Count == 2 && generations[1].Drawable.IsLoaded);
        AddStep("release all load gates", () => generations.ForEach(g => g.ReleaseLoad()));
        AddUntilStep("current generation refreshes", () =>
            generations.Count == 2 && generations[1].Drawable.IsLoaded && generations[1].VisualApplyCount == 2);
        AddAssert("removed generation never refreshes", () => generations[0].VisualApplyCount, () => Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void TestPendingVisualRefreshTracksOnlyCurrentSameIdTypeReplacement()
    {
        var generations = new List<IGatedDrawable>();
        useGatedDrawables(generations);

        AddStep("apply pending unloaded cardinal", () =>
        {
            Assert.That(preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddStep("replace same id with pending unloaded shoulder", () =>
        {
            Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
                2,
                7,
                GarbusChartSerializer.EncodeHitObject(new ShoulderNote
                {
                    StartTime = 2000,
                    Side = HorizontalDirection.Left,
                }))), Is.True);
            Assert.That(generations, Has.Count.EqualTo(2));
            Assert.That(generations[1].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[1].Drawable));
        });
        AddAssert("only replacement generation owns pending refresh", () =>
            generations.Count == 2
            && pendingVisualRefreshes(preview).Count == 1
            && pendingVisualRefreshes(preview).TryGetValue(7, out DrawableHitObject? pending)
            && ReferenceEquals(pending, generations[1].Drawable));

        AddUntilStep("current same-id generation loads behind closed gate", () => generations[1].Drawable.IsLoaded);
        AddStep("release both generations", () => generations.ForEach(g => g.ReleaseLoad()));
        AddUntilStep("current same-id generation refreshes", () =>
            generations[1].Drawable.IsLoaded && generations[1].VisualApplyCount == 2);
        AddAssert("old same-id generation never refreshes", () => generations[0].VisualApplyCount, () => Is.LessThanOrEqualTo(1));
        AddAssert("current generation refreshes exactly once", () => generations[1].VisualApplyCount, () => Is.EqualTo(2));
        AddAssert("completed replacement has no pending owner", () => pendingVisualRefreshes(preview), () => Is.Empty);
    }

    [Test]
    public void TestPendingVisualRefreshIsClearedByAuthoritativeFullState()
    {
        var generations = new List<IGatedDrawable>();
        useGatedDrawables(generations);

        AddStep("apply first pending unloaded full state", () =>
        {
            Assert.That(preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddStep("apply authoritative unloaded replacement full state", () =>
        {
            Assert.That(preview.Apply(fullState(2, chartWith(
                new CardinalNote { StartTime = 3000, AngleDeg = 180 }), [8], 2900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(2));
            Assert.That(generations[1].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[8], Is.SameAs(generations[1].Drawable));
        });
        AddAssert("only authoritative generation remains pending", () =>
            generations.Count == 2
            && pendingVisualRefreshes(preview).Count == 1
            && !pendingVisualRefreshes(preview).ContainsKey(7)
            && pendingVisualRefreshes(preview).TryGetValue(8, out DrawableHitObject? pending)
            && ReferenceEquals(pending, generations[1].Drawable));

        AddUntilStep("authoritative generation loads behind closed gate", () => generations[1].Drawable.IsLoaded);
        AddStep("release both full-state generations", () => generations.ForEach(g => g.ReleaseLoad()));
        AddUntilStep("authoritative generation refreshes", () =>
            generations[1].Drawable.IsLoaded && generations[1].VisualApplyCount == 2);
        AddAssert("replaced full-state generation never refreshes", () => generations[0].VisualApplyCount, () => Is.LessThanOrEqualTo(1));
        AddAssert("authoritative generation refreshes exactly once", () => generations[1].VisualApplyCount, () => Is.EqualTo(2));
        AddAssert("completed full state has no pending owners", () => pendingVisualRefreshes(preview), () => Is.Empty);
    }

    [Test]
    public void TestPendingVisualRefreshIsClearedOnContentDisposal()
    {
        var generations = new List<IGatedDrawable>();
        ChartPreviewContent disposedPreview = null!;
        IGatedDrawable disposedGeneration = null!;
        useGatedDrawables(generations);

        AddStep("apply pending unloaded drawable before disposal", () =>
        {
            Assert.That(preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddStep("dispose preview content", () =>
        {
            disposedPreview = preview;
            disposedGeneration = generations.Single();
            Child = new Container();
        });
        AddUntilStep("preview content disposed", () => isDisposed(disposedPreview));
        AddAssert("disposal clears pending ownership", () => pendingVisualRefreshes(disposedPreview), () => Is.Empty);

        AddStep("release disposed generation", () => generations[0].ReleaseLoad());
        AddStep("create update sentinel", () =>
        {
            var sentinelGenerations = new List<IGatedDrawable>();
            generations = sentinelGenerations;
            Child = preview = createGatedPreview(sentinelGenerations);
        });
        AddUntilStep("update sentinel preview loaded", () => preview.IsLoaded);
        AddStep("apply and release update sentinel", () =>
        {
            Assert.That(preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 3000, AngleDeg = 90 }), [8], 2900, 700)), Is.True);
            generations.Single().ReleaseLoad();
        });
        AddUntilStep("update sentinel refreshes", () =>
            generations.Single().Drawable.IsLoaded && generations.Single().VisualApplyCount == 2);
        AddAssert("disposed generation never refreshes", () =>
            pendingVisualRefreshes(disposedPreview).Count == 0
            && disposedGeneration.VisualApplyCount <= 1);
    }

    [Test]
    public void TestSuccessfulPendingVisualRefreshExecutesExactlyOnce()
    {
        var generations = new List<IGatedDrawable>();
        useGatedDrawables(generations);

        AddStep("apply genuinely pending unloaded drawable", () =>
        {
            Assert.That(preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(generations[0].VisualApplyCount, Is.EqualTo(1));
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddUntilStep("pending generation loads behind closed gate", () => generations[0].Drawable.IsLoaded);
        AddAssert("loaded generation remains pending behind gate", () => pendingVisualRefreshes(preview)[7],
            () => Is.SameAs(generations[0].Drawable));
        AddAssert("only initial visual apply ran", () => generations[0].VisualApplyCount, () => Is.EqualTo(1));
        AddStep("release pending generation", () => generations[0].ReleaseLoad());
        AddUntilStep("pending generation receives deferred refresh", () => generations[0].VisualApplyCount == 2);
        AddAssert("successful generation leaves pending ownership", () => pendingVisualRefreshes(preview), () => Is.Empty);

        AddStep("advance preview without visual refresh", () =>
            Assert.That(preview.Apply(new ChartPreviewTransport(2, 1950, false, 1, 0)), Is.True));
        AddAssert("deferred refresh executed exactly once", () => generations[0].VisualApplyCount, () => Is.EqualTo(2));
    }

    [Test]
    public void TestMiniConnectorDrawsAboveOverlappingInitialChord()
    {
        ChordConnectorOverlay connectorOverlay = null!;
        DrawableCardinalNote[] notes = null!;

        AddStep("create mini-scale preview", () => Child = new Container
        {
            Size = new Vector2(InlineChartPreviewPanel.SIZE),
            Child = new DrawSizePreservingFillContainer
            {
                RelativeSizeAxes = Axes.Both,
                TargetDrawSize = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
                Child = preview = new ChartPreviewContent { RelativeSizeAxes = Axes.Both },
            },
        });
        AddUntilStep("mini-scale preview loaded", () => preview.IsLoaded);
        AddStep("apply first chart chord", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 21333.333333333332, AngleDeg = 180 },
            new CardinalNote { StartTime = 21333.333333333332, AngleDeg = 0 }), [7, 8], 20700, 700)));
        AddUntilStep("first chord connector visible", () =>
            (connectorOverlay = preview.ChildrenOfType<ChordConnectorOverlay>().Single())
                .ChildrenOfType<SmoothPath>().SingleOrDefault() is { IsPresent: true, Alpha: 1 }
            && (notes = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>()
                               .Where(note => note.IsLoaded)
                               .OrderBy(note => note.ScreenSpaceDrawQuad.Centre.X)
                               .ToArray()).Length == 2);
        AddAssert("first connector is centred in ring", () =>
            Vector2.Distance(
                connectorOverlay.ChildrenOfType<SmoothPath>().Single().ScreenSpaceDrawQuad.Centre,
                preview.ChildrenOfType<Ring>().Single().ScreenSpaceDrawQuad.Centre),
            () => Is.LessThan(1));
        AddAssert("note bodies cover entire connector segment", () =>
        {
            var connectorBounds = connectorOverlay.ChildrenOfType<SmoothPath>().Single().ScreenSpaceDrawQuad.AABBFloat;
            var leftNoteBounds = notes[0].ScreenSpaceDrawQuad.AABBFloat;
            var rightNoteBounds = notes[1].ScreenSpaceDrawQuad.AABBFloat;
            return leftNoteBounds.Left <= connectorBounds.Left
                   && leftNoteBounds.Right >= connectorBounds.Centre.X
                   && rightNoteBounds.Left <= connectorBounds.Centre.X
                   && rightNoteBounds.Right >= connectorBounds.Right;
        });
        AddAssert("connector uses shared foreground layer", () =>
        {
            Ring ring = preview.ChildrenOfType<Ring>().Single();
            Drawable laneContainer = ring.ChildrenOfType<Lane>().First().Parent!;
            JudgementFeedbackDisplay feedback = ring.ChildrenOfType<JudgementFeedbackDisplay>().Single();
            Arc outerRing = ring.ChildrenOfType<Arc>().Single(arc => internalChildIndex(ring, arc) >= 0);
            int hitObjectsIndex = internalChildIndex(ring, ring.HitObjectContainer);
            int lanesIndex = internalChildIndex(ring, laneContainer);
            int connectorIndex = internalChildIndex(ring, connectorOverlay);
            int feedbackIndex = internalChildIndex(ring, feedback);
            int outerRingIndex = internalChildIndex(ring, outerRing);

            return ReferenceEquals(laneContainer.Parent, ring)
                   && hitObjectsIndex >= 0
                   && lanesIndex >= 0
                   && connectorIndex > hitObjectsIndex
                   && connectorIndex > lanesIndex
                   && feedbackIndex > connectorIndex
                   && outerRingIndex > connectorIndex;
        });
    }

    [Test]
    public void TestPreviewConnectorStoppedSeekUsesChordTimeAndRewinds()
    {
        SmoothPath connector = null!;

        AddStep("apply future instant chord", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalNote { StartTime = 2000, AngleDeg = 180 }), [7, 8], 1900, 700)));
        AddUntilStep("connector visible before chord", () =>
            preview.ChildrenOfType<ChordConnectorOverlay>().Single()
                   .ChildrenOfType<SmoothPath>().SingleOrDefault() is { IsPresent: true } path
            && path.Alpha == 1
            && (connector = path) != null);

        AddStep("seek stopped past connector fade", () =>
            preview.Apply(new ChartPreviewTransport(2, 2201, false, 1, 0)));
        AddAssert("connector hidden at seek destination", () => connector.IsPresent, () => Is.False);
        AddAssert("connector alpha resolved at seek destination", () => connector.Alpha, () => Is.Zero);

        AddStep("rewind stopped before chord", () =>
            preview.Apply(new ChartPreviewTransport(3, 1900, false, 1, 0)));
        AddUntilStep("connector returns after rewind", () => connector.IsPresent && connector.Alpha == 1);
    }

    [Test]
    public void TestPreviewHoldConnectorResolvesAtSharedHead()
    {
        SmoothPath connector = null!;
        Ring ring = null!;
        DrawableCardinalHoldNote hold = null!;

        AddStep("apply future hold chord", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalHoldNote { StartTime = 2000, AngleDeg = 180, Duration = 1000 }), [7, 8], 1900, 700)));
        AddUntilStep("hold chord loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalHoldNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && preview.ChildrenOfType<ChordConnectorOverlay>().Single()
                      .ChildrenOfType<SmoothPath>().SingleOrDefault() is { IsPresent: true } path
            && (hold = loaded) != null
            && (connector = path) != null
            && (ring = preview.ChildrenOfType<Ring>().Single()) != null);

        AddStep("seek stopped into head fade", () =>
            preview.Apply(new ChartPreviewTransport(2, 2100, false, 1, 0)));
        AddAssert("hold remains alive through head fade", () => ring.AliveHitObjects
                                                                    .Any(drawable => ReferenceEquals(drawable.HitObject, hold.HitObject)));
        AddAssert("hold connector uses head fade", () => connector.Alpha,
            () => Is.EqualTo(0.03125f).Within(0.001f));
        AddAssert("hold connector resolves at ring", () => connector.Vertices
                                                                    .All(vertex => Math.Abs(vertex.Length - ring.ScrollingContainer.ScrollLength) < 0.001f));

        AddStep("seek stopped past head fade", () =>
            preview.Apply(new ChartPreviewTransport(3, 2201, false, 1, 0)));
        AddAssert("hold tail remains pending", () => hold.IsAlive && !hold.Judged && hold.HitObject.GetEndTime() == 3000);
        AddAssert("hold connector hidden after head fade", () => connector.IsPresent, () => Is.False);

        AddStep("rewind stopped before hold head", () =>
            preview.Apply(new ChartPreviewTransport(4, 1900, false, 1, 0)));
        AddUntilStep("hold connector returns after rewind", () => connector.IsPresent && connector.Alpha == 1);
    }

    [Test]
    public void TestObjectBatchRefreshesGlobalStateOnceAtEnd()
    {
        DrawableHitObject[] before = null!;
        bool refreshedBetweenDeltas = false;

        AddStep("apply separate cardinals", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalNote { StartTime = 2100, AngleDeg = 180 }), [7, 8], 1900, 700)));
        AddUntilStep("cardinals loaded white", () => preview.PlayfieldForTests.AllHitObjects
                                                         .Count(d => d.IsLoaded && d.Colour.Equals((ColourInfo)Colour4.White)) == 2);
        AddStep("capture cardinals", () => before = preview.PlayfieldForTests.AllHitObjects.ToArray());

        AddStep("apply multi-object batch", () => preview.ApplyBatch(() =>
        {
            Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
                2,
                8,
                GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 2000, AngleDeg = 180 }))), Is.True);
            refreshedBetweenDeltas |= before.Any(d => d.Colour.Equals((ColourInfo)ChordColours.Highlight));

            Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
                3,
                7,
                GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 2200, AngleDeg = 0 }))), Is.True);
            refreshedBetweenDeltas |= before.Any(d => !d.Colour.Equals((ColourInfo)Colour4.White));

            Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
                4,
                8,
                GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 2200, AngleDeg = 180 }))), Is.True);
            refreshedBetweenDeltas |= before.Any(d => d.Colour.Equals((ColourInfo)ChordColours.Highlight));
        }));

        AddAssert("no global refresh between deltas", () => refreshedBetweenDeltas, () => Is.False);
        AddUntilStep("final chord refreshed", () => before.All(d => d.Colour.Equals((ColourInfo)ChordColours.Highlight)));
        AddAssert("first drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Contains(before[0]));
        AddAssert("second drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Contains(before[1]));
    }

    [Test]
    public void TestScrollRangeReachesEveryScrollingContainer()
    {
        AddStep("apply full state", () => preview.Apply(fullState(1, previewChart(), [10, 20], 0, 700)));
        AddUntilStep("scroll containers loaded", () => preview.ChildrenOfType<GarbusScrollingHitObjectContainer>().Any(c => c.IsLoaded));

        AddStep("change time range", () => Assert.That(preview.Apply(new ChartPreviewScrollSpeed(2, 2400)), Is.True));

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
        AddStep("apply full state", () => Assert.That(preview.Apply(fullState(5, previewChart(), [10, 20], 0, 700)), Is.True));

        AddStep("apply stale scroll range", () => Assert.That(preview.Apply(new ChartPreviewScrollSpeed(4, 2400)), Is.False));

        AddAssert("scroll range unchanged", () => preview.CurrentTimeRangeForTests, () => Is.EqualTo(700));
        AddAssert("resync requested", () => resyncRequests, () => Is.EqualTo(1));
    }

    [Test]
    public void TestFullStateWithHigherNestedRevisionIsRejectedAtomically()
    {
        testMismatchedFullStateRevision(7);
    }

    [Test]
    public void TestFullStateWithLowerNestedRevisionIsRejectedAtomically()
    {
        testMismatchedFullStateRevision(5);
    }

    [Test]
    public void TestRevisionStreamIsMonotonicAcrossMessageDomains()
    {
        int resyncRequests = 0;
        AddStep("listen for resync", () => preview.ResyncRequested += () => resyncRequests++);
        AddStep("apply full state", () => Assert.That(preview.Apply(fullState(5, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)), Is.True));
        AddStep("apply newer transport", () => Assert.That(preview.Apply(new ChartPreviewTransport(7, 1500, false, 1, 0)), Is.True));

        AddStep("reject older model update", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            6,
            7,
            GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 2000, AngleDeg = 180 }))), Is.False));
        AddAssert("model unchanged", () => preview.PlayfieldForTests.AllHitObjects.Single().HitObject.StartTime, () => Is.EqualTo(1000));

        AddStep("apply newer scroll range", () => Assert.That(preview.Apply(new ChartPreviewScrollSpeed(8, 2400)), Is.True));
        AddStep("reject older transport", () => Assert.That(preview.Apply(new ChartPreviewTransport(7, 9000, false, 1, 0)), Is.False));

        AddAssert("transport unchanged", () => preview.ClockTimeForTests, () => Is.EqualTo(1500).Within(0.001));
        AddAssert("scroll range retained", () => preview.CurrentTimeRangeForTests, () => Is.EqualTo(2400));
        AddAssert("both stale messages requested resync", () => resyncRequests, () => Is.EqualTo(2));
    }

    [Test]
    public void TestEqualRevisionTransportIsIgnoredWithoutRequestingResync()
    {
        int resyncRequests = 0;
        AddStep("listen for resync", () => preview.ResyncRequested += () => resyncRequests++);
        AddStep("apply full state", () => Assert.That(preview.Apply(fullState(5, previewChart(), [10, 20], 1500, 700)), Is.True));
        AddUntilStep("initial transport applied", () => preview.ClockTimeForTests == 1500);

        AddStep("apply duplicate transport", () => Assert.That(
            preview.Apply(new ChartPreviewTransport(5, 9000, false, 1, 0)), Is.False));

        AddAssert("transport unchanged", () => preview.ClockTimeForTests, () => Is.EqualTo(1500).Within(0.001));
        AddAssert("no resync requested", () => resyncRequests, () => Is.Zero);
    }

    [Test]
    public void TestStructuralStateReplacesTutorialOverlay()
    {
        AddStep("apply full state", () => preview.Apply(fullState(1, previewChart(), [10, 20], 1200, 700)));
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
            Assert.That(preview.Apply(new ChartPreviewStructuralState(2, GarbusChartSerializer.Encode(replacement))), Is.True);
        });

        AddUntilStep("replacement tutorial visible", () => preview.DesignOverlayForTests.MessageVisibleForTests
                                                               && preview.DesignOverlayForTests.MessageTextForTests == "Replacement tutorial");
        AddAssert("still one overlay", () => preview.ChildrenOfType<DesignOverlay>().Count(), () => Is.EqualTo(1));
        AddAssert("objects retained", () => preview.ObjectCountForTests, () => Is.EqualTo(2));
    }

    [Test]
    public void TestPreviewPolicyAppliesSilentMaximumResultWhileOrdinaryGameplayStillMisses()
    {
        ManualClock gameplayClock = null!;
        DrawableCardinalNote ordinary = null!;
        DrawableCardinalNote previewNote = null!;
        long transportRevision = 2;

        AddStep("create preview and ordinary note", () =>
        {
            preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700));

            gameplayClock = new ManualClock { CurrentTime = 900 };
            var ordinaryNote = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            ordinaryNote.ApplyDefaults();
            ordinary = new DrawableCardinalNote(ordinaryNote);
            var ordinaryPlayfield = new GarbusPlayfield { RelativeSizeAxes = Axes.Both };
            ordinaryPlayfield.Add(ordinary);
            Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = new FramedClock(gameplayClock),
                Child = ordinaryPlayfield,
            });
        });
        AddUntilStep("preview note loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && (previewNote = loaded) != null);
        AddUntilStep("advance both beyond miss window", () =>
        {
            gameplayClock.CurrentTime += 100;
            preview.Apply(new ChartPreviewTransport(transportRevision++, gameplayClock.CurrentTime, false, 1, 0));
            return gameplayClock.CurrentTime >= 3000;
        });

        AddUntilStep("ordinary gameplay note misses", () => ordinary.Judged);
        AddAssert("ordinary gets miss result", () => ordinary.Result.Type, () => Is.EqualTo(HitResult.Miss));
        AddUntilStep("preview note hits", () => previewNote.State.Value == ArmedState.Hit);
        AddAssert("preview gets maximum result", () => previewNote.Result.Type,
            () => Is.EqualTo(HitResult.CriticalPerfect));
        AddAssert("preview result occurs exactly at start", () => previewNote.Result.RawTime,
            () => Is.EqualTo(previewNote.HitObject.StartTime));
        AddAssert("preview hit remains silent", () => previewNote.SamplesPlayCount, () => Is.Zero);
    }

    [Test]
    public void TestPreviewHitLifecycleRewindsAndReappliesResult()
    {
        DrawableCardinalNote drawable = null!;
        double hitLifetimeEnd = 0;

        AddStep("apply preview note", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700)));
        AddUntilStep("preview note loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && (drawable = loaded) != null);

        AddStep("seek to note start", () => preview.Apply(new ChartPreviewTransport(2, 1000, false, 1, 0)));
        AddUntilStep("preview note hits", () => drawable.State.Value == ArmedState.Hit);
        AddAssert("first result is exact", () => drawable.Result.RawTime, () => Is.EqualTo(1000));
        AddStep("capture hit lifetime", () => hitLifetimeEnd = drawable.LifetimeEnd);

        AddStep("seek beyond hit lifetime", () => preview.Apply(new ChartPreviewTransport(3, hitLifetimeEnd + 1, false, 1, 0)));
        AddUntilStep("preview hit no longer alive", () => !drawable.IsAlive && !drawable.IsPresent);
        AddAssert("expired preview note remains hit", () => drawable.Judged && drawable.State.Value == ArmedState.Hit);

        AddStep("seek backward into hit lifetime", () => preview.Apply(new ChartPreviewTransport(4, 1200, false, 1, 0)));
        AddWaitStep("process backward seek", 2);
        AddAssert("preview clock rewinds", () => drawable.Clock.CurrentTime, () => Is.EqualTo(1200).Within(0.001));
        AddAssert("rewound time is inside hit lifetime", () => drawable.LifetimeEnd, () => Is.GreaterThan(1200));
        AddAssert("same preview hit is alive", () => drawable.IsAlive, () => Is.True);
        AddAssert("same preview hit is present", () => drawable.IsPresent, () => Is.True);
        AddAssert("preview result lifetime covers maximum animation", () => hitLifetimeEnd - drawable.HitStateUpdateTime,
            () => Is.EqualTo(1000));
        AddAssert("revived drawable retained", () => preview.PlayfieldForTests.AllHitObjects.Single(), () => Is.SameAs(drawable));
        AddAssert("revived result remains hit", () => drawable.Judged && drawable.State.Value == ArmedState.Hit);

        AddStep("seek before result", () => preview.Apply(new ChartPreviewTransport(5, 900, false, 1, 0)));
        AddUntilStep("preview result rewinds", () => !drawable.Judged && drawable.State.Value == ArmedState.Idle);

        AddStep("seek forward to note start again", () => preview.Apply(new ChartPreviewTransport(6, 1000, false, 1, 0)));
        AddUntilStep("preview note hits again", () => drawable.State.Value == ArmedState.Hit);
        AddAssert("reapplied result is exact", () => drawable.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("reapplied result remains silent", () => drawable.SamplesPlayCount, () => Is.Zero);
    }

    [Test]
    public void TestPreviewResultsApplyChronologicallyAcrossForwardAndBackwardSeeks()
    {
        DrawableCardinalNote earlier = null!;
        DrawableCardinalNote later = null!;
        int laterResultCount = 0;

        AddStep("apply same-lane preview notes", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [7, 8], 900, 700)));
        AddUntilStep("same-lane preview notes loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().Count(d => d.IsLoaded) == 2);
        AddStep("capture preview notes and results", () =>
        {
            DrawableCardinalNote[] notes = preview.PlayfieldForTests.AllHitObjects
                                                      .OfType<DrawableCardinalNote>()
                                                      .OrderBy(d => d.HitObject.StartTime)
                                                      .ToArray();
            earlier = notes[0];
            later = notes[1];
            preview.PlayfieldForTests.NewResult += (drawable, _) =>
            {
                if (ReferenceEquals(drawable, later))
                    laterResultCount++;
            };
        });

        AddStep("jump forward across both notes", () => preview.Apply(new ChartPreviewTransport(2, 1300, false, 1, 0)));
        AddUntilStep("both preview notes hit", () =>
            earlier.Judged && earlier.State.Value == ArmedState.Hit
                           && later.Judged && later.State.Value == ArmedState.Hit);
        AddAssert("earlier result time is exact", () => earlier.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddAssert("later result applied once", () => laterResultCount, () => Is.EqualTo(1));

        AddStep("rewind between notes", () => preview.Apply(new ChartPreviewTransport(3, 1100, false, 1, 0)));
        AddUntilStep("rewind transport applied", () => preview.ClockTimeForTests, () => Is.EqualTo(1100));
        AddAssert("later result becomes unjudged", () => later.Judged, () => Is.False);
        AddAssert("later state becomes idle", () => later.State.Value, () => Is.EqualTo(ArmedState.Idle));
        AddAssert("earlier result remains applied", () => earlier.Judged && earlier.State.Value == ArmedState.Hit);

        AddStep("move forward across later note", () => preview.Apply(new ChartPreviewTransport(4, 1300, false, 1, 0)));
        AddUntilStep("later result reapplies", () => later.Judged && later.State.Value == ArmedState.Hit);
        AddAssert("reapplied later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddWaitStep("hold after later result", 3);
        AddAssert("later result reapplied exactly once", () => laterResultCount, () => Is.EqualTo(2));
    }

    [Test]
    public void TestPreviewResultsRemainChronologicalAcrossLiveUpserts()
    {
        DrawableCardinalNote earlier = null!;
        DrawableCardinalNote later = null!;
        int laterResultCount = 0;

        AddStep("listen for later results", () => preview.PlayfieldForTests.NewResult += (_, result) =>
        {
            if (result.RawTime == 1200)
                laterResultCount++;
        });
        AddStep("apply only later note at 1300", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [8], 1300, 700)));
        AddUntilStep("later preview note loads and hits", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true, Judged: true } loaded
            && loaded.State.Value == ArmedState.Hit
            && (later = loaded) != null);
        AddAssert("initial later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddAssert("later result initially applied once", () => laterResultCount, () => Is.EqualTo(1));

        AddStep("upsert earlier note while still at 1300", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            2,
            7,
            GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 1000, AngleDeg = 0 }))), Is.True));
        AddUntilStep("both live-upserted notes load and hit", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>() is var notes
            && notes.Count(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit) == 2);
        AddStep("capture earlier note", () => earlier = preview.PlayfieldForTests.AllHitObjects
                                                                    .OfType<DrawableCardinalNote>()
                                                                    .Single(d => d.HitObject.StartTime == 1000));
        AddAssert("live-upserted earlier result time is exact", () => earlier.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("live-upserted later result time remains exact", () => later.Result.RawTime, () => Is.EqualTo(1200));

        AddStep("rewind between live-upserted notes", () => preview.Apply(new ChartPreviewTransport(3, 1100, false, 1, 0)));
        AddUntilStep("live-upsert rewind applied", () => preview.ClockTimeForTests, () => Is.EqualTo(1100));
        AddAssert("earlier live-upserted result remains hit", () => earlier.Judged && earlier.State.Value == ArmedState.Hit);
        AddAssert("later live-upserted result becomes idle and unjudged", () =>
            !later.Judged && later.State.Value == ArmedState.Idle);

        AddStep("move forward across later live-upserted note", () => preview.Apply(new ChartPreviewTransport(4, 1300, false, 1, 0)));
        AddUntilStep("later live-upserted result reapplies", () => later.Judged && later.State.Value == ArmedState.Hit);
        AddAssert("reapplied live-upserted later result time is exact", () => later.Result.RawTime, () => Is.EqualTo(1200));
        AddWaitStep("hold after live-upserted later result", 3);
        AddAssert("live-upserted later result reapplied exactly once", () => laterResultCount, () => Is.EqualTo(2));
    }

    [Test]
    public void TestPreviewReordersEditedResultTimesBeforeRewind()
    {
        DrawableCardinalNote edited = null!;
        DrawableCardinalNote unchanged = null!;

        AddStep("apply judged preview notes", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [7, 8], 1300, 700)));
        AddUntilStep("both preview notes load and hit", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>() is var notes
            && notes.Count(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit) == 2);
        AddStep("capture judged preview notes", () =>
        {
            edited = (DrawableCardinalNote)preview.DrawableForTests(7);
            unchanged = (DrawableCardinalNote)preview.DrawableForTests(8);
        });

        AddStep("move judged result after unchanged result", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            2,
            7,
            GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 1250, AngleDeg = 0 }))), Is.True));
        AddUntilStep("judged result time updates in place", () =>
            edited.Judged && edited.HitObject.StartTime == 1250 && edited.Result.RawTime == 1250);
        AddAssert("unchanged result stays exact", () => unchanged.Result.RawTime, () => Is.EqualTo(1200));

        AddStep("rewind between edited result times", () => preview.Apply(new ChartPreviewTransport(3, 1225, false, 1, 0)));
        AddUntilStep("edited-result rewind applied", () => preview.ClockTimeForTests, () => Is.EqualTo(1225));
        AddAssert("unchanged earlier result remains hit", () => unchanged.Judged && unchanged.State.Value == ArmedState.Hit);
        AddAssert("edited later result becomes idle and unjudged", () =>
            !edited.Judged && edited.State.Value == ArmedState.Idle);
    }

    [Test]
    public void TestPreviewRevertsEditedResultMovedBeyondStationaryTime()
    {
        DrawableCardinalNote edited = null!;
        DrawableCardinalNote unchanged = null!;
        int reappliedResultCount = 0;

        AddStep("apply judged preview notes at 1300", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 },
            new CardinalNote { StartTime = 1200, AngleDeg = 0 }), [7, 8], 1300, 700)));
        AddUntilStep("both preview notes load and hit", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>() is var notes
            && notes.Count(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit) == 2);
        AddStep("capture judged preview notes", () =>
        {
            edited = (DrawableCardinalNote)preview.DrawableForTests(7);
            unchanged = (DrawableCardinalNote)preview.DrawableForTests(8);
            preview.PlayfieldForTests.NewResult += (drawable, _) =>
            {
                if (ReferenceEquals(drawable, edited))
                    reappliedResultCount++;
            };
        });

        AddStep("move judged result beyond stationary time", () =>
        {
            Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
                2,
                7,
                GarbusChartSerializer.EncodeHitObject(new CardinalNote { StartTime = 1400, AngleDeg = 0 }))), Is.True);
            Assert.That(edited.HitObject.StartTime, Is.EqualTo(1400));
            Assert.That(edited.Result.RawTime, Is.EqualTo(1400));
        });
        AddAssert("preview time remains stationary", () => preview.ClockTimeForTests, () => Is.EqualTo(1300));
        AddUntilStep("future edited result promptly reverts", () =>
            !edited.Judged && edited.State.Value == ArmedState.Idle);
        AddAssert("unchanged earlier result remains hit", () =>
            unchanged.Judged && unchanged.State.Value == ArmedState.Hit);

        AddStep("move to edited result time", () => preview.Apply(new ChartPreviewTransport(3, 1400, false, 1, 0)));
        AddUntilStep("edited maximum result reapplies", () =>
            edited.Judged && edited.State.Value == ArmedState.Hit
                          && edited.Result.Type == HitResult.CriticalPerfect);
        AddAssert("reapplied edited result time is exact", () => edited.Result.RawTime, () => Is.EqualTo(1400));
        AddWaitStep("hold after edited result reapplies", 3);
        AddAssert("edited result reapplies exactly once", () => reappliedResultCount, () => Is.EqualTo(1));
    }

    [Test]
    public void TestPreviewJudgedHoldUpsertsRefreshResultTreeTracking()
    {
        DrawableCardinalHoldNote hold = null!;
        DrawableHitObject[] originalNested = null!;
        DrawableHitObject[] firstReplacementNested = null!;
        DrawableHitObject[] currentTree = null!;
        Dictionary<Drawable, int> holdDisposals = null!;
        Dictionary<Drawable, int> originalNestedDisposals = null!;
        Dictionary<Drawable, int> firstReplacementNestedDisposals = null!;
        JudgementResult rootResult = null!;
        JudgementResult[] currentResults = null!;
        JudgementResult[] judgedResultsBeforeRewind = null!;
        double[] updatedTimes = null!;
        var revertedResults = new List<JudgementResult>();
        var replayedResults = new List<(DrawableHitObject Drawable, JudgementResult Result)>();

        AddStep("apply preview hold", () => preview.Apply(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 1000 }), [7], 900, 700)));
        AddUntilStep("preview hold tree loads", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalHoldNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && withNested(loaded).Count() == 2
            && withNested(loaded).All(d => d.IsLoaded)
            && (hold = loaded) != null);
        AddStep("judge preview hold tree", () => preview.Apply(new ChartPreviewTransport(2, 2000, false, 1, 0)));
        AddUntilStep("preview hold tree hits", () =>
            withNested(hold).All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture original hold result tree", () =>
        {
            originalNested = hold.NestedHitObjects.ToArray();
            rootResult = hold.Result;
            holdDisposals = disposalCountsFor([hold]);
            originalNestedDisposals = disposalCountsFor(originalNested);
        });

        AddStep("upsert first judged hold rebuild", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            3,
            7,
            GarbusChartSerializer.EncodeHitObject(new CardinalHoldNote
            {
                StartTime = 1100,
                AngleDeg = 0,
                Duration = 900,
            }))), Is.True));
        AddStep("capture first replacement hold generation", () =>
        {
            firstReplacementNested = hold.NestedHitObjects.ToArray();
            firstReplacementNestedDisposals = disposalCountsFor(firstReplacementNested);
        });
        AddAssert("original hold generation disposed exactly once", () => originalNestedDisposals.Values, () => Is.All.EqualTo(1));
        AddAssert("retained hold root remains alive", () => holdDisposals.Values, () => Is.All.Zero);
        AddAssert("first replacement hold generation remains alive", () => firstReplacementNested.All(d => !isDisposed(d)));

        AddStep("upsert second judged hold rebuild", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            4,
            7,
            GarbusChartSerializer.EncodeHitObject(new CardinalHoldNote
            {
                StartTime = 1200,
                AngleDeg = 0,
                Duration = 800,
            }))), Is.True));
        AddUntilStep("current hold result tree loads and catches up", () =>
            withNested(hold).Count() == 2
            && withNested(hold).All(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture current hold result tree", () =>
        {
            currentTree = withNested(hold).ToArray();
            currentResults = currentTree.Select(d => d.Result).ToArray();
            judgedResultsBeforeRewind = currentTree.Where(d => d.Judged).Select(d => d.Result).ToArray();
            updatedTimes = currentTree.Select(d => d.HitObject.GetEndTime()).ToArray();

            Assert.That(preview.DrawableForTests(7), Is.SameAs(hold));
            Assert.That(hold.Result, Is.SameAs(rootResult));
            Assert.That(originalNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(firstReplacementNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(originalNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(firstReplacementNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(holdDisposals.Values, Is.All.Zero);
            Assert.That(currentTree.All(d => !isDisposed(d)), Is.True);
            Assert.That(currentTree, Has.Length.EqualTo(2));
            Assert.That(judgedResultsBeforeRewind, Is.EqualTo(currentResults));
            Assert.That(currentTree.Select(d => d.Result.RawTime), Is.EqualTo(updatedTimes));

            preview.PlayfieldForTests.RevertResult += revertedResults.Add;
            preview.PlayfieldForTests.NewResult += (drawable, result) => replayedResults.Add((drawable, result));
        });

        AddStep("rewind before current hold tree", () => preview.Apply(new ChartPreviewTransport(5, 900, false, 1, 0)));
        AddUntilStep("current hold tree rewinds", () =>
            preview.ClockTimeForTests == 900
            && currentTree.All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddAssert("only judged current hold results revert", () => revertedResults.Count, () => Is.EqualTo(judgedResultsBeforeRewind.Length));
        AddAssert("judged current hold result identities revert once", () => revertedResults, () => Is.EquivalentTo(judgedResultsBeforeRewind));
        AddAssert("current hold nested objects are idle and unjudged", () =>
            currentTree.Skip(1).All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddUntilStep("current hold nested objects load idle", () => currentTree.Skip(1).All(d => d.IsLoaded && !d.Judged));

        AddStep("replay current hold result tree", () => preview.Apply(new ChartPreviewTransport(6, 2000, false, 1, 0)));
        AddUntilStep("current hold tree reapplies", () =>
            currentTree.All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddWaitStep("hold after current hold replay", 3);
        AddAssert("only current hold results reapply", () => replayedResults.Count, () => Is.EqualTo(currentResults.Length));
        AddAssert("current hold result identities reapply once", () => replayedResults.Select(e => e.Result), () => Is.EquivalentTo(currentResults));
        AddAssert("current hold results reapply at updated exact times", () =>
            currentTree.Select(d => d.Result.RawTime), () => Is.EqualTo(updatedTimes));
    }

    [Test]
    public void TestPreviewJudgedSliderUpsertsRefreshResultTreeTracking()
    {
        DrawableSliderBody slider = null!;
        DrawableHitObject[] originalNested = null!;
        DrawableHitObject[] firstReplacementNested = null!;
        DrawableHitObject[] currentTree = null!;
        Dictionary<Drawable, int> sliderDisposals = null!;
        Dictionary<Drawable, int> originalNestedDisposals = null!;
        Dictionary<Drawable, int> firstReplacementNestedDisposals = null!;
        JudgementResult rootResult = null!;
        JudgementResult[] currentResults = null!;
        JudgementResult[] judgedResultsBeforeRewind = null!;
        double[] updatedTimes = null!;
        var revertedResults = new List<JudgementResult>();
        var replayedResults = new List<(DrawableHitObject Drawable, JudgementResult Result)>();

        AddStep("apply preview slider", () => preview.Apply(fullState(1, chartWith(new SliderBody
        {
            StartTime = 1000,
            AngleDeg = 0,
            Side = HorizontalDirection.Left,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>
                {
                    new GarbusPathControlPoint { TimeOffset = 800, RotationOffset = 45 },
                    new GarbusPathControlPoint { TimeOffset = 1600, RotationOffset = 90 },
                },
            },
        }), [7], 900, 700)));
        AddUntilStep("preview slider tree loads", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault() is { IsLoaded: true } loaded
            && withNested(loaded).Count() == 4
            && withNested(loaded).All(d => d.IsLoaded)
            && (slider = loaded) != null);
        AddStep("judge preview slider tree", () => preview.Apply(new ChartPreviewTransport(2, 2600, false, 1, 0)));
        AddUntilStep("preview slider tree hits", () =>
            withNested(slider).All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture original slider result tree", () =>
        {
            originalNested = slider.NestedHitObjects.ToArray();
            rootResult = slider.Result;
            sliderDisposals = disposalCountsFor([slider]);
            originalNestedDisposals = disposalCountsFor(originalNested);
        });

        AddStep("upsert first judged slider rebuild", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            3,
            7,
            GarbusChartSerializer.EncodeHitObject(new SliderBody
            {
                StartTime = 1100,
                AngleDeg = 45,
                Side = HorizontalDirection.Right,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 700, RotationOffset = 45 },
                        new GarbusPathControlPoint { TimeOffset = 1500, RotationOffset = 90 },
                    },
                },
            }))), Is.True));
        AddStep("capture first replacement slider generation", () =>
        {
            firstReplacementNested = slider.NestedHitObjects.ToArray();
            firstReplacementNestedDisposals = disposalCountsFor(firstReplacementNested);
        });
        AddAssert("original slider generation disposed exactly once", () => originalNestedDisposals.Values, () => Is.All.EqualTo(1));
        AddAssert("retained slider root remains alive", () => sliderDisposals.Values, () => Is.All.Zero);
        AddAssert("first replacement slider generation remains alive", () => firstReplacementNested.All(d => !isDisposed(d)));

        AddStep("upsert second judged slider rebuild", () => Assert.That(preview.Apply(new ChartPreviewObjectUpsert(
            4,
            7,
            GarbusChartSerializer.EncodeHitObject(new SliderBody
            {
                StartTime = 1200,
                AngleDeg = 90,
                Side = HorizontalDirection.Left,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 700, RotationOffset = 45 },
                        new GarbusPathControlPoint { TimeOffset = 1400, RotationOffset = 90 },
                    },
                },
            }))), Is.True));
        AddUntilStep("current slider result tree loads and catches up", () =>
            withNested(slider).Count() == 4
            && withNested(slider).All(d => d.IsLoaded && d.Judged && d.State.Value == ArmedState.Hit));
        AddStep("capture current slider result tree", () =>
        {
            currentTree = withNested(slider).ToArray();
            currentResults = currentTree.Select(d => d.Result).ToArray();
            judgedResultsBeforeRewind = currentTree.Where(d => d.Judged).Select(d => d.Result).ToArray();
            updatedTimes = currentTree.Select(d => d.HitObject.GetEndTime()).ToArray();

            Assert.That(preview.DrawableForTests(7), Is.SameAs(slider));
            Assert.That(slider.Result, Is.SameAs(rootResult));
            Assert.That(originalNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(firstReplacementNested.All(old => currentTree.Skip(1).All(current => !ReferenceEquals(old, current))), Is.True);
            Assert.That(originalNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(firstReplacementNestedDisposals.Values, Is.All.EqualTo(1));
            Assert.That(sliderDisposals.Values, Is.All.Zero);
            Assert.That(currentTree.All(d => !isDisposed(d)), Is.True);
            Assert.That(currentTree, Has.Length.EqualTo(4));
            Assert.That(judgedResultsBeforeRewind, Is.EqualTo(currentResults));
            Assert.That(currentTree.Select(d => d.Result.RawTime), Is.EqualTo(updatedTimes));

            preview.PlayfieldForTests.RevertResult += revertedResults.Add;
            preview.PlayfieldForTests.NewResult += (drawable, result) => replayedResults.Add((drawable, result));
        });

        AddStep("rewind before current slider tree", () => preview.Apply(new ChartPreviewTransport(5, 900, false, 1, 0)));
        AddUntilStep("current slider tree rewinds", () =>
            preview.ClockTimeForTests == 900
            && currentTree.All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddAssert("only judged current slider results revert", () => revertedResults.Count, () => Is.EqualTo(judgedResultsBeforeRewind.Length));
        AddAssert("judged current slider result identities revert once", () => revertedResults, () => Is.EquivalentTo(judgedResultsBeforeRewind));
        AddAssert("current slider nested objects are idle and unjudged", () =>
            currentTree.Skip(1).All(d => !d.Judged && d.State.Value == ArmedState.Idle));
        AddUntilStep("current slider nested objects load idle", () => currentTree.Skip(1).All(d => d.IsLoaded && !d.Judged));

        AddStep("replay current slider result tree", () => preview.Apply(new ChartPreviewTransport(6, 2600, false, 1, 0)));
        AddUntilStep("current slider tree reapplies", () =>
            currentTree.All(d => d.Judged && d.State.Value == ArmedState.Hit));
        AddWaitStep("hold after current slider replay", 3);
        AddAssert("only current slider results reapply", () => replayedResults.Count, () => Is.EqualTo(currentResults.Length));
        AddAssert("current slider result identities reapply once", () => replayedResults.Select(e => e.Result), () => Is.EquivalentTo(currentResults));
        AddAssert("current slider results reapply at updated exact times", () =>
            currentTree.Select(d => d.Result.RawTime), () => Is.EqualTo(updatedTimes));
    }

    [Test]
    public void TestScrollSpeedDoesNotExtendPreviewHitLifetime()
    {
        DrawableCardinalNote drawable = null!;
        double hitLifetimeEnd = 0;

        AddStep("apply preview note", () => preview.Apply(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700)));
        AddUntilStep("preview note loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && (drawable = loaded) != null);

        AddStep("seek to note start", () => preview.Apply(new ChartPreviewTransport(2, 1000, false, 1, 0)));
        AddUntilStep("preview note hits", () => drawable.State.Value == ArmedState.Hit);
        AddStep("capture hit lifetime", () => hitLifetimeEnd = drawable.LifetimeEnd);

        AddStep("change scroll speed after hit", () => preview.Apply(new ChartPreviewScrollSpeed(3, 1400)));
        AddAssert("scroll speed preserves hit lifetime", () => drawable.LifetimeEnd, () => Is.EqualTo(hitLifetimeEnd));

        AddStep("seek beyond hit lifetime", () => preview.Apply(new ChartPreviewTransport(4, hitLifetimeEnd + 1, false, 1, 0)));
        AddUntilStep("preview note expires using hit lifecycle", () => !drawable.IsAlive && !drawable.IsPresent);
        AddAssert("expired preview note remains judged", () => drawable.Judged);
    }

    [Test]
    public void TestPreviewPresentsActiveDurationObjectsAsSuccessful()
    {
        DrawableCardinalHoldNote cardinalHold = null!;
        DrawableShoulderHoldNote shoulderHold = null!;
        DrawableSliderBody slider = null!;
        DrawableSliderHead sliderHead = null!;
        DrawableSliderChild firstControlPoint = null!;

        AddStep("apply active duration objects", () => preview.Apply(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 2000, AngleDeg = 180, Duration = 1000 },
            new ShoulderHoldNote { StartTime = 2000, Side = HorizontalDirection.Right, Duration = 1000 },
            new SliderBody
            {
                StartTime = 2000,
                AngleDeg = 0,
                Side = HorizontalDirection.Left,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 },
                        new GarbusPathControlPoint { TimeOffset = 1000, RotationOffset = 90 },
                    },
                },
            }), [7, 8, 9], 2500, 700)));
        AddUntilStep("active duration objects loaded", () =>
        {
            cardinalHold = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalHoldNote>().SingleOrDefault()!;
            shoulderHold = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableShoulderHoldNote>().SingleOrDefault()!;
            slider = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault()!;
            sliderHead = slider?.NestedHitObjects.OfType<DrawableSliderHead>().SingleOrDefault()!;
            firstControlPoint = slider?.NestedHitObjects.OfType<DrawableSliderChild>()
                                      .OrderBy(child => child.HitObject.StartTime)
                                      .FirstOrDefault()!;
            return cardinalHold?.IsLoaded == true
                   && shoulderHold?.IsLoaded == true
                   && slider?.IsLoaded == true
                   && sliderHead?.IsLoaded == true
                   && firstControlPoint?.IsLoaded == true;
        });

        AddAssert("cardinal hold body is held", () => cardinalHold.ChildrenOfType<SmoothPath>().Single().Colour,
            () => Is.EqualTo((ColourInfo)Colour4.White));
        AddAssert("shoulder hold body is held", () => shoulderHold.ChildrenOfType<CircularProgress>().Single().Colour,
            () => Is.EqualTo((ColourInfo)Colour4.Purple));
        AddAssert("slider body is caught", () => slider.Alpha, () => Is.EqualTo(1));
        AddAssert("slider caught tip is visible", () => slider.ChildrenOfType<Box>()
                                                                 .Single(box => box.Size == new Vector2(46)).Alpha,
            () => Is.EqualTo(1));
        AddAssert("slider head has exact maximum result", () => sliderHead.Result.Type,
            () => Is.EqualTo(sliderHead.HitObject.Judgement.MaxResult));
        AddAssert("slider control point is presented caught", () => firstControlPoint.HeadStyleHit, () => Is.True);
        AddAssert("slider control point has exact maximum result", () => firstControlPoint.Result.Type,
            () => Is.EqualTo(firstControlPoint.HitObject.Judgement.MaxResult));
    }

    [Test]
    public void TestPreviewHoldAndSliderHitAtTheirOwnEndTimes()
    {
        DrawableCardinalHoldNote hold = null!;
        DrawableHoldNoteHead<HoldNoteHead<CardinalHoldNote>> holdHead = null!;
        DrawableSliderBody slider = null!;
        DrawableSliderHead sliderHead = null!;
        DrawableSliderChild[] sliderChildren = null!;

        AddStep("apply hold and slider", () => preview.Apply(fullState(1, chartWith(
            new CardinalHoldNote { StartTime = 1000, AngleDeg = 0, Duration = 500 },
            new SliderBody
            {
                StartTime = 2000,
                AngleDeg = 0,
                Side = HorizontalDirection.Left,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 500, RotationOffset = 45 },
                        new GarbusPathControlPoint { TimeOffset = 1000, RotationOffset = 90 },
                    },
                },
            }), [7, 8], 900, 700)));
        AddUntilStep("hold loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalHoldNote>().SingleOrDefault()?.IsLoaded == true);
        AddStep("capture hold drawables", () =>
        {
            hold = preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalHoldNote>().Single();
            holdHead = hold.NestedHitObjects.OfType<DrawableHoldNoteHead<HoldNoteHead<CardinalHoldNote>>>().Single();
        });
        AddAssert("preview judgements hidden", () => preview.PlayfieldForTests.DisplayJudgements.Value, () => Is.False);

        AddStep("seek to hold head", () => preview.Apply(new ChartPreviewTransport(2, 1000, false, 1, 0)));
        AddUntilStep("hold head hits", () => holdHead.State.Value == ArmedState.Hit);
        AddAssert("hold head gets maximum result", () => holdHead.Result.Type,
            () => Is.EqualTo(holdHead.HitObject.Judgement.MaxResult));
        AddAssert("hold head result time is exact", () => holdHead.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("hold body remains idle at head", () => !hold.Judged && hold.State.Value == ArmedState.Idle);

        AddStep("seek before hold end", () => preview.Apply(new ChartPreviewTransport(3, 1499, false, 1, 0)));
        AddAssert("hold body remains idle before end", () => !hold.Judged && hold.State.Value == ArmedState.Idle);
        AddStep("seek to hold end", () => preview.Apply(new ChartPreviewTransport(4, 1500, false, 1, 0)));
        AddUntilStep("hold body hits", () => hold.State.Value == ArmedState.Hit);
        AddAssert("hold body gets maximum result", () => hold.Result.Type,
            () => Is.EqualTo(hold.HitObject.Judgement.MaxResult));
        AddAssert("hold body result time is exact", () => hold.Result.RawTime, () => Is.EqualTo(1500));

        AddUntilStep("slider loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableSliderBody>().SingleOrDefault() is { IsLoaded: true } loaded
            && (slider = loaded) != null);
        AddStep("capture slider drawables", () =>
        {
            sliderHead = slider.NestedHitObjects.OfType<DrawableSliderHead>().Single();
            sliderChildren = slider.NestedHitObjects.OfType<DrawableSliderChild>().OrderBy(d => d.HitObject.StartTime).ToArray();
        });

        AddStep("seek to slider head", () => preview.Apply(new ChartPreviewTransport(5, 2000, false, 1, 0)));
        AddUntilStep("slider head hits", () => sliderHead.State.Value == ArmedState.Hit);
        AddAssert("slider head gets maximum result", () => sliderHead.Result.Type,
            () => Is.EqualTo(sliderHead.HitObject.Judgement.MaxResult));
        AddAssert("slider head result time is exact", () => sliderHead.Result.RawTime, () => Is.EqualTo(2000));
        AddAssert("slider body remains idle at head", () => !slider.Judged && slider.State.Value == ArmedState.Idle);

        AddStep("seek before first control point", () => preview.Apply(new ChartPreviewTransport(6, 2499, false, 1, 0)));
        AddAssert("first control point remains idle before its time", () => !sliderChildren[0].Judged);
        AddStep("seek to first control point", () => preview.Apply(new ChartPreviewTransport(7, 2500, false, 1, 0)));
        AddUntilStep("first control point hits", () => sliderChildren[0].State.Value == ArmedState.Hit);
        AddAssert("first control point gets maximum result", () => sliderChildren[0].Result.Type,
            () => Is.EqualTo(sliderChildren[0].HitObject.Judgement.MaxResult));
        AddAssert("first control point result time is exact", () => sliderChildren[0].Result.RawTime, () => Is.EqualTo(2500));
        AddAssert("slider body remains idle after first control point", () => !slider.Judged && slider.State.Value == ArmedState.Idle);

        AddStep("seek before slider end", () => preview.Apply(new ChartPreviewTransport(8, 2999, false, 1, 0)));
        AddAssert("last control point and body remain idle before end", () =>
            !sliderChildren[1].Judged && !slider.Judged && slider.State.Value == ArmedState.Idle);
        AddStep("seek to slider end", () => preview.Apply(new ChartPreviewTransport(9, 3000, false, 1, 0)));
        AddUntilStep("last control point and body hit", () =>
            sliderChildren[1].State.Value == ArmedState.Hit && slider.State.Value == ArmedState.Hit);
        AddAssert("last control point gets maximum result", () => sliderChildren[1].Result.Type,
            () => Is.EqualTo(sliderChildren[1].HitObject.Judgement.MaxResult));
        AddAssert("last control point result time is exact", () => sliderChildren[1].Result.RawTime, () => Is.EqualTo(3000));
        AddAssert("slider body gets maximum result", () => slider.Result.Type,
            () => Is.EqualTo(slider.HitObject.Judgement.MaxResult));
        AddAssert("slider body result time is exact", () => slider.Result.RawTime, () => Is.EqualTo(3000));
        AddAssert("preview hold and slider remain silent", () => new[]
        {
            hold.SamplesPlayCount,
            holdHead.SamplesPlayCount,
            slider.SamplesPlayCount,
            sliderHead.SamplesPlayCount,
            sliderChildren[0].SamplesPlayCount,
            sliderChildren[1].SamplesPlayCount,
        }, () => Is.All.EqualTo(0));
    }

    [Test]
    public void TestPreviewSkipsSpawnTweenWhenFutureNoteBecomesAlive()
    {
        DrawableCardinalNote drawable = null!;

        AddStep("apply future preview note", () =>
        {
            Assert.That(preview.Apply(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1200, 700)), Is.True);
            drawable = (DrawableCardinalNote)preview.DrawableForTests(7);
        });
        AddAssert("future note starts before lifetime", () => drawable.IsAlive, () => Is.False);

        AddStep("cross future note lifetime boundary", () =>
            preview.Apply(new ChartPreviewTransport(2, 1300, false, 1, 0)));
        AddUntilStep("future note becomes alive", () => drawable.IsAlive);
        AddAssert("preview skips spawn tween", () =>
            drawable.ChildrenOfType<Sprite>().Single().Scale,
            () => Is.EqualTo(Vector2.One));
    }

    private static ChartPreviewFullState fullState(long revision, GarbusChart chart, long[] ids, double time, double timeRange) =>
        new(revision, GarbusChartSerializer.Encode(chart), ids, timeRange,
            new ChartPreviewTransport(revision, time, false, 1, 0));

    private void testMismatchedFullStateRevision(long transportRevision)
    {
        int resyncRequests = 0;
        DrawableHitObject originalDrawable = null!;

        AddStep("listen for resync", () => preview.ResyncRequested += () => resyncRequests++);
        AddStep("apply initial full state", () => Assert.That(preview.Apply(fullState(5, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 500, 700)), Is.True));
        AddUntilStep("initial state applied", () => preview.ClockTimeForTests == 500
                                                   && preview.ObjectCountForTests == 1
                                                   && preview.DrawableForTests(7).IsLoaded);
        AddStep("capture original drawable", () => originalDrawable = preview.DrawableForTests(7));

        AddStep("reject mismatched full state", () => Assert.That(preview.Apply(new ChartPreviewFullState(
            6,
            GarbusChartSerializer.Encode(chartWith(new CardinalNote { StartTime = 9000, AngleDeg = 180 })),
            [8],
            2400,
            new ChartPreviewTransport(transportRevision, 9000, false, 1, 0))), Is.False));

        AddAssert("model and drawable unchanged", () => preview.ObjectCountForTests == 1
                                                        && preview.DrawableForTests(7).HitObject.StartTime == 1000
                                                        && ReferenceEquals(preview.DrawableForTests(7), originalDrawable));
        AddAssert("clock unchanged", () => preview.ClockTimeForTests, () => Is.EqualTo(500).Within(0.001));
        AddAssert("scroll range unchanged", () => preview.CurrentTimeRangeForTests, () => Is.EqualTo(700));
        AddAssert("accepted revision unchanged", () => preview.AcceptedRevisionForTests, () => Is.EqualTo(5));
        AddAssert("one resync requested", () => resyncRequests, () => Is.EqualTo(1));
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

    private static System.Collections.Generic.IEnumerable<Drawable> sliderSideVisuals(DrawableSliderBody slider)
    {
        foreach (var container in slider.ChildrenOfType<Container<SmoothPath>>())
            yield return container;

        yield return slider.ContactSpikes;
        yield return slider.ChildrenOfType<Box>().Single(b => b.Size == new Vector2(46));
        yield return slider.ChildrenOfType<Circle>().Single(c => c.Size == new Vector2(slider.Thickness)).Parent!;
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
        (Dictionary<long, DrawableHitObject>)typeof(ChartPreviewContent)
            .GetField("pendingVisualRefreshes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(content)!;

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

    private static bool hasRightColour(Drawable drawable) => drawable.Colour.Equals(Constants.RightColour);
}
