using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Tests.Visual;

[TestFixture]
public partial class TestSceneJudgementFeedback : GarbusTestScene
{
    protected override double TimePerAction => 0;

    private ManualClock manualClock = null!;
    private JudgementFeedbackDisplay display = null!;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create display", () =>
        {
            manualClock = new ManualClock();
            Child = new Container
            {
                Size = new Vector2(800),
                Clock = new FramedClock(manualClock),
                Child = display = new JudgementFeedbackDisplay(),
            };
        });

        AddUntilStep("display loaded", () => display.IsLoaded && display.DrawWidth == 800);
    }

    [Test]
    public void TestRankText()
    {
        var expected = new Dictionary<HitResult, string>
        {
            [HitResult.CriticalPerfect] = "CRITICAL PERFECT",
            [HitResult.Perfect] = "PERFECT",
            [HitResult.Near] = "NEAR",
            [HitResult.Bad] = "BAD",
            [HitResult.Miss] = "MISS",
        };

        foreach (var (result, text) in expected)
            AddAssert($"{result} text", () => createMessage(result, 0, true).RankText == text);
    }

    [Test]
    public void TestTimingDirection()
    {
        AddAssert("negative is early", () => createMessage(HitResult.Perfect, -18, true).TimingText == "EARLY");
        AddAssert("positive is late", () => createMessage(HitResult.Perfect, 18, true).TimingText == "LATE");
        AddAssert("zero has no direction", () => createMessage(HitResult.Perfect, 0, true).TimingText == string.Empty);
        AddAssert("floating point zero has no direction", () => createMessage(HitResult.Perfect, 1e-10, true).TimingText == string.Empty);
    }

    [Test]
    public void TestButtonCriticalPerfectIsSilent()
    {
        AddAssert("critical perfect rejected", () =>
        {
            var (drawable, result) = createResult(HitResult.CriticalPerfect, 0);
            return !display.Show(drawable, result);
        });
        AddAssert("no message created", () => display.ActiveMessages.Count == 0);
    }

    [Test]
    public void TestButtonPerfectShowsWhiteDirectionOnly()
    {
        AddStep("show early perfect", () => show(HitResult.Perfect, -18, 90));
        AddAssert("rank omitted", () => display.ActiveMessages.Single().RankText == string.Empty);
        AddAssert("early shown", () => display.ActiveMessages.Single().TimingText == "EARLY");
        AddAssert("timing is white", () => display.ActiveMessages.Single().TimingColour == Color4.White);

        AddStep("clear", () => display.ClearMessages());
        AddStep("show late perfect", () => show(HitResult.Perfect, 18, 90));
        AddAssert("late shown alone", () => display.ActiveMessages.Single().RankText == string.Empty && display.ActiveMessages.Single().TimingText == "LATE");
    }

    [Test]
    public void TestButtonPerfectWithoutDirectionIsSilent()
    {
        AddAssert("zero-offset perfect rejected", () =>
        {
            var (drawable, result) = createResult(HitResult.Perfect, 0);
            return !display.Show(drawable, result);
        });
        AddAssert("no empty message created", () => display.ActiveMessages.Count == 0);
    }

    [Test]
    public void TestButtonNearKeepsRankAndDirection()
    {
        AddStep("show late near", () => show(HitResult.Near, 32, 90));
        AddAssert("near rank retained", () => display.ActiveMessages.Single().RankText == "NEAR");
        AddAssert("late retained", () => display.ActiveMessages.Single().TimingText == "LATE");
    }

    [Test]
    public void TestButtonMissOmitsDirection()
    {
        AddStep("show early miss", () => show(HitResult.Miss, -150, 90));
        AddAssert("miss rank retained", () => display.ActiveMessages.Single().RankText == "MISS");
        AddAssert("early omitted", () => display.ActiveMessages.Single().TimingText == string.Empty);

        AddStep("clear", () => display.ClearMessages());
        AddStep("show late miss", () => show(HitResult.Miss, 110, 90));
        AddAssert("late omitted", () => display.ActiveMessages.Single().TimingText == string.Empty);
    }

    [Test]
    public void TestHoldAndSliderPerfectsAreSilent()
    {
        AddAssert("hold perfect rejected", () =>
        {
            var hitObject = new CardinalHoldNote
            {
                AngleDeg = 90,
                StartTime = 1000,
                Duration = 500,
            };
            hitObject.ApplyDefaults();
            var result = new JudgementResult(hitObject, hitObject.Judgement) { Type = HitResult.Perfect };
            return !display.Show(new DrawableCardinalHoldNote(hitObject), result);
        });

        AddAssert("slider head perfect rejected", () =>
        {
            var parent = createSliderBody();
            var hitObject = new SliderHead(parent) { StartTime = parent.StartTime };
            hitObject.ApplyDefaults();
            var result = new JudgementResult(hitObject, hitObject.Judgement) { Type = HitResult.Perfect };
            return !display.Show(new DrawableSliderHead(hitObject), result);
        });

        AddAssert("slider child perfect rejected", () =>
        {
            var parent = createSliderBody();
            var hitObject = parent.NestedHitObjects.OfType<SliderChild>().Single();
            var result = new JudgementResult(hitObject, hitObject.Judgement) { Type = HitResult.Perfect };
            return !display.Show(new DrawableSliderChild(hitObject), result);
        });

        AddAssert("no duration perfect messages", () => display.ActiveMessages.Count == 0);
    }

    [Test]
    public void TestDurationMessageOmitsDirection()
    {
        AddAssert("duration omits direction", () => createMessage(HitResult.Bad, 18, false).TimingText == string.Empty);

        AddAssert("hold tail declares duration semantics", () =>
            !new DrawableCardinalHoldNote(new CardinalHoldNote
            {
                AngleDeg = 90,
                StartTime = 1000,
                Duration = 500,
            }).DisplayTimingOffset);

        AddAssert("slider child declares duration semantics", () =>
        {
            var controlPoint = new GarbusPathControlPoint { TimeOffset = 500 };
            var parent = new SliderBody
            {
                AngleDeg = 0,
                Side = HorizontalDirection.Right,
                StartTime = 1000,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint> { controlPoint },
                },
            };

            parent.ApplyDefaults();
            return !new DrawableSliderChild(parent.NestedHitObjects.OfType<SliderChild>().Single()).DisplayTimingOffset;
        });
    }

    [Test]
    public void TestFiltersResults()
    {
        AddAssert("scorable angled result accepted", () =>
        {
            var (drawable, result) = createResult(HitResult.Near, 0);
            return display.Show(drawable, result);
        });
        AddAssert("one active", () => display.ActiveMessages.Count == 1);

        AddAssert("ignore hit rejected", () =>
        {
            var (drawable, result) = createResult(HitResult.IgnoreHit, 0);
            return !display.Show(drawable, result);
        });
        AddAssert("ignore miss rejected", () =>
        {
            var (drawable, result) = createResult(HitResult.IgnoreMiss, 0);
            return !display.Show(drawable, result);
        });
        AddAssert("opted out rejected", () =>
        {
            var (drawable, result) = createResult(HitResult.Perfect, 0, displayResult: false);
            return !display.Show(drawable, result);
        });
        AddAssert("non-angle rejected", () =>
        {
            var hitObject = new NonAngledHitObject { StartTime = 1000 };
            hitObject.ApplyDefaults();
            var drawable = new TestDrawable<NonAngledHitObject>(hitObject);
            var result = new JudgementResult(hitObject, hitObject.Judgement) { Type = HitResult.Perfect };
            return !display.Show(drawable, result);
        });
        AddAssert("still one active", () => display.ActiveMessages.Count == 1);
    }

    [Test]
    public void TestDisplayJudgementsClearsAndSuppresses()
    {
        AddStep("show result", () =>
        {
            var (drawable, result) = createResult(HitResult.Near, 0);
            display.Show(drawable, result);
        });
        AddAssert("one active", () => display.ActiveMessages.Count == 1);

        AddStep("disable", () => display.DisplayJudgements.Value = false);
        AddAssert("cleared", () => display.ActiveMessages.Count == 0);
        AddAssert("hidden", () => display.Alpha == 0);
        AddAssert("new result suppressed", () =>
        {
            var (drawable, result) = createResult(HitResult.Near, 0);
            return !display.Show(drawable, result);
        });

        AddStep("re-enable", () => display.DisplayJudgements.Value = true);
        AddAssert("visible", () => display.Alpha == 1);
        AddAssert("later result accepted", () =>
        {
            var (drawable, result) = createResult(HitResult.Near, 0);
            return display.Show(drawable, result);
        });
    }

    [Test]
    public void TestCardinalAnglePlacement()
    {
        AddStep("show cardinal angles", () =>
        {
            foreach (int angle in new[] { 0, 90, 180, 270 })
            {
                var (drawable, result) = createResult(HitResult.Near, 0, angleDeg: angle);
                display.Show(drawable, result);
            }
        });

        AddAssert("right", () => targetAt(0).X > 0 && nearZero(targetAt(0).Y));
        AddAssert("up", () => targetAt(90).Y < 0 && nearZero(targetAt(90).X));
        AddAssert("left", () => targetAt(180).X < 0 && nearZero(targetAt(180).Y));
        AddAssert("down", () => targetAt(270).Y > 0 && nearZero(targetAt(270).X));
        AddAssert("all upright", () => display.ActiveMessages.All(m => m.Rotation == 0));
    }

    [Test]
    public void TestAngleNormalisation()
    {
        AddStep("show 450 degrees", () =>
        {
            var (drawable, result) = createResult(HitResult.Near, 0, angleDeg: 450);
            display.Show(drawable, result);
        });

        AddAssert("normalised to 90", () => display.ActiveMessages.Single().AngleDeg == 90);
        AddAssert("placed up", () => display.ActiveMessages.Single().TargetPosition.Y < 0 && nearZero(display.ActiveMessages.Single().TargetPosition.X));
    }

    [Test]
    public void TestResizeReflowsRadius()
    {
        AddStep("show result", () => show(HitResult.Near, 0, 0));
        AddAssert("800px radius is 160", () => System.Math.Abs(display.ActiveMessages.Single().TargetPosition.Length - 160) < 0.001f);

        AddStep("resize parent to 500", () => display.Parent!.Size = new Vector2(500));
        AddUntilStep("radius reflowed to 100", () => System.Math.Abs(display.ActiveMessages.Single().TargetPosition.Length - 100) < 0.001f);
    }

    [Test]
    public void TestMissDirectionUsesSignedOffset()
    {
        AddAssert("early input miss", () => createMessage(HitResult.Miss, -150, true).TimingText == "EARLY");
        AddAssert("automatic late miss", () => createMessage(HitResult.Miss, 110, true).TimingText == "LATE");
    }

    [Test]
    public void TestRadialStackNewestFirstAndCap()
    {
        JudgementResult first = null!;
        JudgementResult second = null!;
        JudgementResult third = null!;
        JudgementFeedbackMessage firstMessage = null!;

        AddStep("show three at same angle", () =>
        {
            first = show(HitResult.Near, -30, 90);
            firstMessage = messageFor(first);
            second = show(HitResult.Perfect, -10, 90);
            third = show(HitResult.Miss, 10, 90);
        });

        AddAssert("three active", () => display.ActiveMessages.Count == 3);
        AddAssert("newest slot zero", () => messageFor(third).StackIndex == 0);
        AddAssert("middle slot one", () => messageFor(second).StackIndex == 1);
        AddAssert("oldest slot two", () => messageFor(first).StackIndex == 2);
        AddAssert("radii increase outward", () =>
            messageFor(third).TargetPosition.Length < messageFor(second).TargetPosition.Length &&
            messageFor(second).TargetPosition.Length < messageFor(first).TargetPosition.Length);

        AddStep("show fourth", () => show(HitResult.Near, 50, 90));
        AddAssert("still capped at three", () => display.ActiveMessages.Count == 3);
        AddAssert("oldest retired", () => display.ActiveMessages.All(m => !ReferenceEquals(m.Result, first)));
        AddAssert("oldest disposed", () => firstMessage.IsDisposedForTesting);
    }

    [Test]
    public void TestCircularSeamClusters()
    {
        JudgementResult at359 = null!;
        JudgementResult at1 = null!;

        AddStep("show seam pair", () =>
        {
            at359 = show(HitResult.Perfect, -10, 359);
            at1 = show(HitResult.Perfect, 10, 1);
        });

        AddAssert("newest seam result slot zero", () => messageFor(at1).StackIndex == 0);
        AddAssert("older seam result stacked", () => messageFor(at359).StackIndex == 1);
    }

    [Test]
    public void TestAnglesOutsideThresholdDoNotStack()
    {
        JudgementResult at0 = null!;
        JudgementResult at20 = null!;

        AddStep("show separated pair", () =>
        {
            at0 = show(HitResult.Perfect, -10, 0);
            at20 = show(HitResult.Perfect, 10, 20);
        });

        AddAssert("zero remains slot zero", () => messageFor(at0).StackIndex == 0);
        AddAssert("twenty remains slot zero", () => messageFor(at20).StackIndex == 0);
    }

    [Test]
    public void TestRevertUsesResultReferenceAndClosesGap()
    {
        JudgementResult oldest = null!;
        JudgementResult middle = null!;
        JudgementResult newest = null!;
        JudgementResult equivalent = null!;

        AddStep("show stack", () =>
        {
            oldest = show(HitResult.Near, -30, 180);
            middle = show(HitResult.Perfect, -10, 180);
            newest = show(HitResult.Miss, 10, 180);

            var hitObject = middle.HitObject;
            equivalent = new JudgementResult(hitObject, hitObject.Judgement) { Type = middle.Type };
            equivalent.TimeOffset = middle.TimeOffset;
        });

        AddStep("revert equivalent value", () => display.Revert(equivalent));
        AddAssert("equivalent does not remove", () => display.ActiveMessages.Count == 3);

        AddStep("revert exact middle", () => display.Revert(middle));
        AddAssert("middle removed", () => display.ActiveMessages.Count == 2 && display.ActiveMessages.All(m => !ReferenceEquals(m.Result, middle)));
        AddAssert("newest remains zero", () => messageFor(newest).StackIndex == 0);
        AddAssert("oldest closes to one", () => messageFor(oldest).StackIndex == 1);
    }

    [Test]
    public void TestMessagesExpireAndDispose()
    {
        JudgementFeedbackMessage message = null!;

        AddStep("show result", () =>
        {
            var result = show(HitResult.Perfect, -10, 270);
            message = messageFor(result);
        });
        AddAssert("active during readable lifetime", () => display.ActiveMessages.Count == 1);

        AddStep("advance beyond lifetime", () => manualClock.CurrentTime = JudgementFeedbackDisplay.TOTAL_LIFETIME + 1);
        AddUntilStep("removed after lifetime", () => display.ActiveMessages.Count == 0);
        AddAssert("expired message disposed", () => message.IsDisposedForTesting);
    }

    [Test]
    public void TestClearDisposesWhileAnimating()
    {
        JudgementFeedbackMessage message = null!;

        AddStep("show result", () =>
        {
            var result = show(HitResult.Perfect, -10, 270);
            message = messageFor(result);
        });
        AddStep("clear immediately", () => display.ClearMessages());
        AddAssert("active empty", () => display.ActiveMessages.Count == 0);
        AddAssert("message disposed", () => message.IsDisposedForTesting);
    }

    /// <summary>A frozen readable state for tuning the halo in the visual test browser.</summary>
    [Test]
    public void TestVisualHalo()
    {
        AddStep("show representative feedback", () =>
        {
            show(HitResult.CriticalPerfect, 0, 0);
            show(HitResult.Perfect, -18, 90);
            show(HitResult.Near, 32, 180);
            show(HitResult.Miss, 80, 270);

            show(HitResult.Bad, 0, 45, displayTimingOffset: false);
            show(HitResult.Perfect, -12, 45);
            show(HitResult.Near, 24, 45);

            show(HitResult.Perfect, -8, 359);
            show(HitResult.Perfect, 8, 1);
        });
        AddStep("freeze after fade in", () => manualClock.CurrentTime = JudgementFeedbackDisplay.FADE_IN_DURATION + 20);
        AddAssert("preview populated", () => display.ActiveMessages.Count > 0);
    }

    private static JudgementFeedbackMessage createMessage(HitResult type, double offset, bool displayTimingOffset)
    {
        var hitObject = new CardinalNote
        {
            AngleDeg = 90,
            StartTime = 1000,
        };
        hitObject.ApplyDefaults();

        var result = new JudgementResult(hitObject, hitObject.Judgement) { Type = type };
        result.TimeOffset = offset;

        return new JudgementFeedbackMessage(result, hitObject.AngleDeg, displayTimingOffset);
    }

    private static (TestDrawable<CardinalNote> Drawable, JudgementResult Result) createResult(
        HitResult type,
        double offset,
        bool displayResult = true,
        bool displayTimingOffset = true,
        int angleDeg = 90)
    {
        var hitObject = new CardinalNote
        {
            AngleDeg = angleDeg,
            StartTime = 1000,
        };
        hitObject.ApplyDefaults();

        var result = new JudgementResult(hitObject, hitObject.Judgement) { Type = type };
        result.TimeOffset = offset;
        return (new TestDrawable<CardinalNote>(hitObject, displayResult, displayTimingOffset), result);
    }

    private static SliderBody createSliderBody()
    {
        var hitObject = new SliderBody
        {
            AngleDeg = 0,
            Side = HorizontalDirection.Right,
            StartTime = 1000,
            Path = new GarbusPath
            {
                ControlPoints = new BindableList<GarbusPathControlPoint>
                {
                    new() { TimeOffset = 500 },
                },
            },
        };
        hitObject.ApplyDefaults();
        return hitObject;
    }

    private Vector2 targetAt(float angle) => display.ActiveMessages.Single(m => m.AngleDeg == angle).TargetPosition;

    private JudgementResult show(HitResult type, double offset, int angleDeg, bool displayTimingOffset = true)
    {
        var (drawable, result) = createResult(type, offset, displayTimingOffset: displayTimingOffset, angleDeg: angleDeg);
        display.Show(drawable, result);
        return result;
    }

    private JudgementFeedbackMessage messageFor(JudgementResult result)
        => display.ActiveMessages.Single(m => ReferenceEquals(m.Result, result));

    private static bool nearZero(float value) => System.Math.Abs(value) < 0.001f;

    private partial class TestDrawable<T> : DrawableGarbusHitObject<T>
        where T : GarbusHitObject
    {
        private readonly bool displayResult;
        private readonly bool displayTimingOffset;

        public override bool DisplayResult => displayResult;
        public override bool DisplayTimingOffset => displayTimingOffset;

        public TestDrawable(T hitObject, bool displayResult = true, bool displayTimingOffset = true)
            : base(hitObject)
        {
            this.displayResult = displayResult;
            this.displayTimingOffset = displayTimingOffset;
        }
    }

    private class NonAngledHitObject : GarbusHitObject
    {
        public override HitsoundFamily Hitsounds => HitsoundFamilies.CardinalNote;
    }
}
