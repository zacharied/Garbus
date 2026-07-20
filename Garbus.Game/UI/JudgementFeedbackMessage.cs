using System;
using System.Collections.Generic;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Utils;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.UI;

/// <summary>
/// One upright judgement label positioned on the playfield's invisible feedback halo.
/// </summary>
public sealed partial class JudgementFeedbackMessage : CompositeDrawable
{
    private const double zero_epsilon = 1e-6;
    private const float outward_drift = 8;

    private readonly FillFlowContainer content;

    public JudgementResult Result { get; }

    public float AngleDeg { get; }

    public string RankText { get; }

    public string TimingText { get; }

    public Color4 TimingColour { get; }

    public bool HasContent => RankText.Length > 0 || TimingText.Length > 0;

    public int StackIndex { get; internal set; }

    public Vector2 TargetPosition { get; private set; }

    internal long Sequence { get; set; }

    internal bool IsDisposedForTesting { get; private set; }

    public JudgementFeedbackMessage(JudgementResult result, float angleDeg, bool displayTimingOffset, bool compactButtonFeedback = false)
    {
        Result = result;
        AngleDeg = normalise(angleDeg);
        RankText = compactButtonFeedback && result.Type is (HitResult.CriticalPerfect or HitResult.Perfect)
            ? string.Empty
            : result.Type.GetDescription().ToUpperInvariant();
        TimingText = timingTextFor(result.TimeOffset, displayTimingOffset);
        TimingColour = compactButtonFeedback && result.Type == HitResult.Perfect
            ? Color4.White
            : new Color4(210, 215, 225, 255);

        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
        AutoSizeAxes = Axes.Both;

        var textChildren = new List<Drawable>();

        if (RankText.Length > 0)
        {
            textChildren.Add(new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = RankText,
                Font = new FontUsage("Inter-Bold").With(size: 22),
                Colour = colourFor(result.Type),
            });
        }

        if (TimingText.Length > 0)
        {
            textChildren.Add(new SpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Text = TimingText,
                Font = new FontUsage("Inter-Bold").With(size: compactButtonFeedback && result.Type == HitResult.Perfect ? 16 : 12),
                Colour = TimingColour,
                Alpha = compactButtonFeedback && result.Type == HitResult.Perfect ? 1 : 0.85f,
            });
        }

        InternalChild = content = new FillFlowContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 1),
            Children = textChildren.ToArray(),
        };
    }

    internal void BeginLifetime(double fadeInDuration, double readDuration, double fadeOutDuration, Action completion)
    {
        content.Alpha = 0;
        content.Scale = new Vector2(0.85f);
        content.Position = Vector2.Zero;

        content.FadeTo(1, fadeInDuration, Easing.OutQuint)
               .Then()
               .FadeTo(1, readDuration)
               .Then()
               .FadeOut(fadeOutDuration, Easing.OutQuint)
               .OnComplete(_ => completion());

        content.ScaleTo(1, fadeInDuration, Easing.OutQuint);

        float radians = MathUtils.DegToRad(AngleDeg);
        var drift = new Vector2(MathF.Cos(radians), -MathF.Sin(radians)) * outward_drift;
        content.MoveTo(Vector2.Zero, fadeInDuration + readDuration)
               .Then()
               .MoveTo(drift, fadeOutDuration, Easing.OutQuint);
    }

    internal void SetPolarTarget(float radius, double duration = 0)
    {
        float radians = MathUtils.DegToRad(AngleDeg);
        TargetPosition = new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius);
        ClearTransforms();

        if (duration <= 0)
            Position = TargetPosition;
        else
            this.MoveTo(TargetPosition, duration, Easing.OutQuint);
    }

    private static string timingTextFor(double offset, bool displayTimingOffset)
    {
        if (!displayTimingOffset || Math.Abs(offset) <= zero_epsilon)
            return string.Empty;

        return offset < 0 ? "EARLY" : "LATE";
    }

    private static float normalise(float angle)
    {
        angle %= 360;
        return angle < 0 ? angle + 360 : angle;
    }

    private static Color4 colourFor(HitResult result) => result switch
    {
        HitResult.CriticalPerfect => new Color4(255, 236, 165, 255),
        HitResult.Perfect => new Color4(110, 235, 255, 255),
        HitResult.Near => new Color4(255, 226, 92, 255),
        HitResult.Bad => new Color4(255, 151, 65, 255),
        HitResult.Miss => new Color4(255, 82, 92, 255),
        _ => Color4.White,
    };

    protected override void Dispose(bool isDisposing)
    {
        IsDisposedForTesting = true;
        base.Dispose(isDisposing);
    }
}
