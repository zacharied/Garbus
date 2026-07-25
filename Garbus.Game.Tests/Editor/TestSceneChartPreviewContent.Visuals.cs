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

public partial class TestSceneChartPreviewContent
{
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
            Assert.That(squarePreview.Replace(fullState(1, chart, [1, 2, 3, 4], startTimes[0] - 1000, 700)), Is.True);
            Assert.That(miniPreview.Replace(fullState(1, chart, [1, 2, 3, 4], startTimes[0] - 1000, 700)), Is.True);
        });
        AddUntilStep("first cardinal warning revealed", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == angles[0]
            && miniWarning.RevealedAngleDeg(HorizontalDirection.Left) == angles[0]
            && gameplayPlayfield.WarningIndicators.RevealedAngleDeg(HorizontalDirection.Left) == angles[0]);
        AddStep("advance first warning fade", () =>
        {
            gameplayClock.CurrentTime = startTimes[0] - 900;
            Assert.That(squarePreview.Apply(transportBatch(2, startTimes[0] - 900, false, 1, 0)), Is.True);
            Assert.That(miniPreview.Apply(transportBatch(2, startTimes[0] - 900, false, 1, 0)), Is.True);
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
                        transportBatch(index + 2, startTimes[index] - 1000, false, 1, 0)), Is.True);
                    Assert.That(miniPreview.Apply(
                        transportBatch(index + 2, startTimes[index] - 1000, false, 1, 0)), Is.True);
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
    public void TestMiniWarningAlphaAtBreatheBoundaries()
    {
        const int angle = 135;
        const double start_time = 5000;
        const double breathe_period = 1000d / 2.7d;
        const float breathe_min_alpha = 0.35f;
        double warningStart = start_time - WarningIndicatorDisplay.WARNING_TIME;
        WarningIndicatorDisplay warning = null!;

        AddStep("seek to exact warning start", () =>
        {
            Assert.That(preview.Replace(fullState(
                1,
                chartWith(warningSlider(angle, start_time)),
                [1],
                warningStart,
                700)), Is.True);
            warning = preview.PlayfieldForTests.WarningIndicators;
        });
        AddUntilStep("warning start is transparent", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == angle
            && warningEffectBuffer(warning).Alpha == 0);

        AddStep("seek to initial half-period", () => Assert.That(preview.Apply(
            transportBatch(2, warningStart + breathe_period / 2, false, 1, 0)), Is.True));
        AddUntilStep("initial half-period is full alpha", () =>
            warningEffectBuffer(warning).Alpha == 1);

        AddStep("seek to full-period boundary", () => Assert.That(preview.Apply(
            transportBatch(3, warningStart + breathe_period, false, 1, 0)), Is.True));
        AddUntilStep("full-period boundary is minimum alpha", () =>
            warningEffectBuffer(warning).Alpha == breathe_min_alpha);
    }

    [Test]
    public void TestMiniWarningAlphaIsIndependentOfSeekHistory()
    {
        const int angle = 135;
        const double start_time = 5000;
        const double first_sample_time = 3250;
        const double phase_sample_time = 4100;
        WarningIndicatorDisplay warning = null!;
        float firstSampleAlpha = 0;
        float rewindAlpha = 0;

        AddStep("seek stopped preview into warning", () =>
        {
            Assert.That(preview.Replace(fullState(
                1,
                chartWith(warningSlider(angle, start_time)),
                [1],
                first_sample_time,
                700)), Is.True);
            warning = preview.PlayfieldForTests.WarningIndicators;
        });
        AddUntilStep("interior warning revealed", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == angle);
        AddAssert("interior seek-in has alpha", () => warningEffectBuffer(warning).Alpha,
            () => Is.GreaterThan(0));
        AddStep("record first alpha", () => firstSampleAlpha = warningEffectBuffer(warning).Alpha);

        AddStep("seek before warning", () => Assert.That(preview.Apply(
            transportBatch(2, start_time - WarningIndicatorDisplay.WARNING_TIME - 1, false, 1, 0)), Is.True));
        AddUntilStep("before interval is transparent", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == null
            && warningEffectBuffer(warning).Alpha == 0);

        AddStep("seek back to first sample", () => Assert.That(preview.Apply(
            transportBatch(3, first_sample_time, false, 1, 0)), Is.True));
        AddUntilStep("first alpha restored exactly", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == angle
            && warningEffectBuffer(warning).Alpha == firstSampleAlpha);

        AddStep("seek after warning", () => Assert.That(preview.Apply(
            transportBatch(4, start_time + 1, false, 1, 0)), Is.True));
        AddUntilStep("after interval is transparent", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == null
            && warningEffectBuffer(warning).Alpha == 0);

        AddStep("rewind into warning", () => Assert.That(preview.Apply(
            transportBatch(5, phase_sample_time, false, 1, 0)), Is.True));
        AddUntilStep("rewind restores angle and phase", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == angle
            && warningEffectBuffer(warning).Alpha > 0);
        AddStep("record rewind alpha", () => rewindAlpha = warningEffectBuffer(warning).Alpha);

        AddStep("seek before warning again", () => Assert.That(preview.Apply(
            transportBatch(6, start_time - WarningIndicatorDisplay.WARNING_TIME - 1, false, 1, 0)), Is.True));
        AddUntilStep("warning clears again", () => warningEffectBuffer(warning).Alpha == 0);
        AddStep("advance to same phase sample", () => Assert.That(preview.Apply(
            transportBatch(7, phase_sample_time, false, 1, 0)), Is.True));
        AddUntilStep("forward and backward histories match", () =>
            warning.RevealedAngleDeg(HorizontalDirection.Left) == angle
            && warningEffectBuffer(warning).Alpha == rewindAlpha);
    }

    [Test]
    public void TestSameTypeUpsertRetainsDrawableAndRecalculatesRouting()
    {
        DrawableHitObject before = null!;
        AddStep("apply full state", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700)));
        AddUntilStep("drawable loaded", () => preview.PlayfieldForTests.AllHitObjects.SingleOrDefault()?.IsLoaded == true);
        AddStep("capture drawable", () => before = preview.PlayfieldForTests.AllHitObjects.Single());

        AddStep("upsert cardinal", () => Assert.That(preview.Apply(upsertBatch(
            2,
            7,
            GarbusChartCloner.CloneHitObject(new CardinalNote { StartTime = 2500, AngleDeg = 180 }))), Is.True));

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

        AddStep("apply left slider", () => preview.Replace(fullState(1, chartWith(new SliderBody
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

        AddStep("upsert right slider", () => Assert.That(preview.Apply(upsertBatch(
            2,
            7,
            GarbusChartCloner.CloneHitObject(new SliderBody
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

        AddStep("apply left slams", () => preview.Replace(fullState(1, chartWith(
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

        AddStep("upsert centred slam", () => Assert.That(preview.Apply(upsertBatch(
            2,
            7,
            GarbusChartCloner.CloneHitObject(new GarbusSlamCentered
            {
                StartTime = 2100,
                AngleDeg = 135,
                Side = HorizontalDirection.Right,
            }))), Is.True));
        AddStep("upsert edge slam", () => Assert.That(preview.Apply(upsertBatch(
            3,
            8,
            GarbusChartCloner.CloneHitObject(new GarbusSlamEdge
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

        AddStep("apply separate cardinals", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalHoldNote { StartTime = 2100, AngleDeg = 180, Duration = 500 }), [7, 8], 1900, 700)));
        AddUntilStep("cardinals loaded white", () => preview.PlayfieldForTests.AllHitObjects
                                                         .Count(d => d.IsLoaded && d.Colour.Equals((ColourInfo)Colour4.White)) == 2);
        AddStep("capture cardinals", () => before = preview.PlayfieldForTests.AllHitObjects.ToArray());

        AddStep("move one cardinal into chord", () => Assert.That(preview.Apply(upsertBatch(
            2,
            8,
            GarbusChartCloner.CloneHitObject(new CardinalHoldNote
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

        AddStep("apply fresh note and hold chord", () => Assert.That(preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalHoldNote { StartTime = 2000, AngleDeg = 180, Duration = 500 }), [7, 8], 1900, 700)), Is.True));
        AddUntilStep("fresh chord roots loaded", () =>
            preview.DrawableForTests(new PreviewObjectId(7)) is DrawableCardinalNote { IsLoaded: true } loadedNote
            && preview.DrawableForTests(new PreviewObjectId(8)) is DrawableCardinalHoldNote { IsLoaded: true } loadedHold
            && (note = loadedNote) != null
            && (hold = loadedHold) != null);
        AddAssert("fresh chord roots are yellow", () => new[] { note.Colour, hold.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));

        AddStep("seek through both chord results", () =>
            Assert.That(preview.Apply(transportBatch(2, 2500, false, 1, 0)), Is.True));
        AddUntilStep("both chord roots hit", () => note.Judged && hold.Judged);
        AddAssert("first chord results are exact", () => new[] { note.Result.RawTime, hold.Result.RawTime },
            () => Is.EqualTo(new[] { 2000, 2500 }));
        AddAssert("chord roots stay yellow through results", () => new[] { note.Colour, hold.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));

        AddStep("rewind before chord", () =>
            Assert.That(preview.Apply(transportBatch(3, 1900, false, 1, 0)), Is.True));
        AddUntilStep("both chord roots rewind", () => !note.Judged && !hold.Judged);
        AddAssert("chord roots stay yellow after rewind", () => new[] { note.Colour, hold.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));

        AddStep("reapply both chord results", () =>
            Assert.That(preview.Apply(transportBatch(4, 2500, false, 1, 0)), Is.True));
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

        AddStep("apply one cardinal", () => Assert.That(preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True));
        AddUntilStep("existing cardinal loaded white", () =>
            preview.DrawableForTests(new PreviewObjectId(7)) is DrawableCardinalNote { IsLoaded: true } loaded
            && loaded.Colour.Equals((ColourInfo)Colour4.White)
            && (existing = loaded) != null);

        AddStep("upsert fresh chord member", () => Assert.That(preview.Apply(upsertBatch(
            2,
            8,
            GarbusChartCloner.CloneHitObject(new CardinalNote { StartTime = 2000, AngleDeg = 180 }))), Is.True));
        AddUntilStep("fresh chord member loaded", () =>
            preview.DrawableForTests(new PreviewObjectId(8)) is DrawableCardinalNote { IsLoaded: true } loaded
            && (fresh = loaded) != null);
        AddAssert("existing and fresh chord members are yellow", () => new[] { existing.Colour, fresh.Colour },
            () => Is.All.EqualTo((ColourInfo)ChordColours.Highlight));
        AddAssert("chord connector indexes both members", () =>
            preview.ChildrenOfType<ChordConnectorOverlay>().Single()
                   .ChildrenOfType<SmoothPath>().SingleOrDefault() is { IsPresent: true });

        AddStep("seek through live chord", () =>
            Assert.That(preview.Apply(transportBatch(3, 2000, false, 1, 0)), Is.True));
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
            Assert.That(preview.Replace(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddStep("remove pending drawable", () =>
            Assert.That(preview.Apply(removeBatch(2, 7)), Is.True));
        AddAssert("removal clears pending ownership", () => pendingVisualRefreshes(preview), () => Is.Empty);

        AddStep("add replacement work to advance updates", () =>
            Assert.That(preview.Apply(upsertBatch(3, 8,
                GarbusChartCloner.CloneHitObject(new CardinalNote { StartTime = 2500, AngleDeg = 90 }))), Is.True));
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
            Assert.That(preview.Replace(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddStep("replace same id with pending unloaded shoulder", () =>
        {
            Assert.That(preview.Apply(upsertBatch(
                2,
                7,
                GarbusChartCloner.CloneHitObject(new ShoulderNote
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
            Assert.That(preview.Replace(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1900, 700)), Is.True);
            Assert.That(generations, Has.Count.EqualTo(1));
            Assert.That(generations[0].Drawable.IsLoaded, Is.False);
            Assert.That(pendingVisualRefreshes(preview)[7], Is.SameAs(generations[0].Drawable));
        });

        AddStep("apply authoritative unloaded replacement full state", () =>
        {
            Assert.That(preview.Replace(fullState(2, chartWith(
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
            Assert.That(preview.Replace(fullState(1, chartWith(
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
            Assert.That(preview.Replace(fullState(1, chartWith(
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
            Assert.That(preview.Replace(fullState(1, chartWith(
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
            Assert.That(preview.Apply(transportBatch(2, 1950, false, 1, 0)), Is.True));
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
        AddStep("apply first chart chord", () => preview.Replace(fullState(1, chartWith(
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

        AddStep("apply future instant chord", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 2000, AngleDeg = 0 },
            new CardinalNote { StartTime = 2000, AngleDeg = 180 }), [7, 8], 1900, 700)));
        AddUntilStep("connector visible before chord", () =>
            preview.ChildrenOfType<ChordConnectorOverlay>().Single()
                   .ChildrenOfType<SmoothPath>().SingleOrDefault() is { IsPresent: true } path
            && path.Alpha == 1
            && (connector = path) != null);

        AddStep("seek stopped past connector fade", () =>
            preview.Apply(transportBatch(2, 2201, false, 1, 0)));
        AddAssert("connector hidden at seek destination", () => connector.IsPresent, () => Is.False);
        AddAssert("connector alpha resolved at seek destination", () => connector.Alpha, () => Is.Zero);

        AddStep("rewind stopped before chord", () =>
            preview.Apply(transportBatch(3, 1900, false, 1, 0)));
        AddUntilStep("connector returns after rewind", () => connector.IsPresent && connector.Alpha == 1);
    }

    [Test]
    public void TestPreviewHoldConnectorResolvesAtSharedHead()
    {
        SmoothPath connector = null!;
        Ring ring = null!;
        DrawableCardinalHoldNote hold = null!;

        AddStep("apply future hold chord", () => preview.Replace(fullState(1, chartWith(
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
            preview.Apply(transportBatch(2, 2100, false, 1, 0)));
        AddAssert("hold remains alive through head fade", () => ring.AliveHitObjects
                                                                    .Any(drawable => ReferenceEquals(drawable.HitObject, hold.HitObject)));
        AddAssert("hold connector uses head fade", () => connector.Alpha,
            () => Is.EqualTo(0.03125f).Within(0.001f));
        AddAssert("hold connector resolves at ring", () => connector.Vertices
                                                                    .All(vertex => Math.Abs(vertex.Length - ring.ScrollingContainer.ScrollLength) < 0.001f));

        AddStep("seek stopped past head fade", () =>
            preview.Apply(transportBatch(3, 2201, false, 1, 0)));
        AddAssert("hold tail remains pending", () => hold.IsAlive && !hold.Judged && hold.HitObject.GetEndTime() == 3000);
        AddAssert("hold connector hidden after head fade", () => connector.IsPresent, () => Is.False);

        AddStep("rewind stopped before hold head", () =>
            preview.Apply(transportBatch(4, 1900, false, 1, 0)));
        AddUntilStep("hold connector returns after rewind", () => connector.IsPresent && connector.Alpha == 1);
    }

    [Test]
    public void TestScrollSpeedDoesNotExtendPreviewHitLifetime()
    {
        DrawableCardinalNote drawable = null!;
        double hitLifetimeEnd = 0;

        AddStep("apply preview note", () => preview.Replace(fullState(1, chartWith(
            new CardinalNote { StartTime = 1000, AngleDeg = 0 }), [7], 900, 700)));
        AddUntilStep("preview note loaded", () =>
            preview.PlayfieldForTests.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault() is { IsLoaded: true } loaded
            && (drawable = loaded) != null);

        AddStep("seek to note start", () => preview.Apply(transportBatch(2, 1000, false, 1, 0)));
        AddUntilStep("preview note hits", () => drawable.State.Value == ArmedState.Hit);
        AddStep("capture hit lifetime", () => hitLifetimeEnd = drawable.LifetimeEnd);

        AddStep("change scroll speed after hit", () => preview.Apply(rangeBatch(3, 1400)));
        AddAssert("scroll speed preserves hit lifetime", () => drawable.LifetimeEnd, () => Is.EqualTo(hitLifetimeEnd));

        AddStep("seek beyond hit lifetime", () => preview.Apply(transportBatch(4, hitLifetimeEnd + 1, false, 1, 0)));
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

        AddStep("apply active duration objects", () => preview.Replace(fullState(1, chartWith(
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

        AddStep("apply hold and slider", () => preview.Replace(fullState(1, chartWith(
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

        AddStep("seek to hold head", () => preview.Apply(transportBatch(2, 1000, false, 1, 0)));
        AddUntilStep("hold head hits", () => holdHead.State.Value == ArmedState.Hit);
        AddAssert("hold head gets maximum result", () => holdHead.Result.Type,
            () => Is.EqualTo(holdHead.HitObject.Judgement.MaxResult));
        AddAssert("hold head result time is exact", () => holdHead.Result.RawTime, () => Is.EqualTo(1000));
        AddAssert("hold body remains idle at head", () => !hold.Judged && hold.State.Value == ArmedState.Idle);

        AddStep("seek before hold end", () => preview.Apply(transportBatch(3, 1499, false, 1, 0)));
        AddAssert("hold body remains idle before end", () => !hold.Judged && hold.State.Value == ArmedState.Idle);
        AddStep("seek to hold end", () => preview.Apply(transportBatch(4, 1500, false, 1, 0)));
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

        AddStep("seek to slider head", () => preview.Apply(transportBatch(5, 2000, false, 1, 0)));
        AddUntilStep("slider head hits", () => sliderHead.State.Value == ArmedState.Hit);
        AddAssert("slider head gets maximum result", () => sliderHead.Result.Type,
            () => Is.EqualTo(sliderHead.HitObject.Judgement.MaxResult));
        AddAssert("slider head result time is exact", () => sliderHead.Result.RawTime, () => Is.EqualTo(2000));
        AddAssert("slider body remains idle at head", () => !slider.Judged && slider.State.Value == ArmedState.Idle);

        AddStep("seek before first control point", () => preview.Apply(transportBatch(6, 2499, false, 1, 0)));
        AddAssert("first control point remains idle before its time", () => !sliderChildren[0].Judged);
        AddStep("seek to first control point", () => preview.Apply(transportBatch(7, 2500, false, 1, 0)));
        AddUntilStep("first control point hits", () => sliderChildren[0].State.Value == ArmedState.Hit);
        AddAssert("first control point gets maximum result", () => sliderChildren[0].Result.Type,
            () => Is.EqualTo(sliderChildren[0].HitObject.Judgement.MaxResult));
        AddAssert("first control point result time is exact", () => sliderChildren[0].Result.RawTime, () => Is.EqualTo(2500));
        AddAssert("slider body remains idle after first control point", () => !slider.Judged && slider.State.Value == ArmedState.Idle);

        AddStep("seek before slider end", () => preview.Apply(transportBatch(8, 2999, false, 1, 0)));
        AddAssert("last control point and body remain idle before end", () =>
            !sliderChildren[1].Judged && !slider.Judged && slider.State.Value == ArmedState.Idle);
        AddStep("seek to slider end", () => preview.Apply(transportBatch(9, 3000, false, 1, 0)));
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
            Assert.That(preview.Replace(fullState(1, chartWith(
                new CardinalNote { StartTime = 2000, AngleDeg = 0 }), [7], 1200, 700)), Is.True);
            drawable = (DrawableCardinalNote)preview.DrawableForTests(new PreviewObjectId(7));
        });
        AddAssert("future note starts before lifetime", () => drawable.IsAlive, () => Is.False);

        AddStep("cross future note lifetime boundary", () =>
            preview.Apply(transportBatch(2, 1300, false, 1, 0)));
        AddUntilStep("future note becomes alive", () => drawable.IsAlive);
        AddAssert("preview skips spawn tween", () =>
            drawable.ChildrenOfType<Sprite>().Single().Scale,
            () => Is.EqualTo(Vector2.One));
    }
}
