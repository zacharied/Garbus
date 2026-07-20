using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Objects;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Layout;

namespace Garbus.Game.UI;

/// <summary>
/// Places short-lived judgement messages on an invisible circle around the gameplay centre.
/// </summary>
public sealed partial class JudgementFeedbackDisplay : CompositeDrawable
{
    public const double FADE_IN_DURATION = 100;
    public const double READ_DURATION = 450;
    public const double FADE_OUT_DURATION = 250;
    public const double TOTAL_LIFETIME = FADE_IN_DURATION + READ_DURATION + FADE_OUT_DURATION;

    internal const double COLLISION_ANGLE_DEG = 15;
    internal const int MAX_CLUSTER_MESSAGES = 3;
    internal const float STACK_SPACING = 34;

    private const double stack_transition_duration = 100;
    private const float radius_ratio = 0.20f;
    private const float minimum_radius = 96;
    private const float maximum_radius = 180;

    private readonly List<JudgementFeedbackMessage> activeMessages = new();
    private readonly LayoutValue layoutCache = new(Invalidation.DrawSize);
    private long nextSequence;

    public BindableBool DisplayJudgements { get; } = new(true);

    public IReadOnlyList<JudgementFeedbackMessage> ActiveMessages => activeMessages;

    public JudgementFeedbackDisplay()
    {
        RelativeSizeAxes = Axes.Both;
        AddLayout(layoutCache);
        DisplayJudgements.BindValueChanged(onDisplayJudgementsChanged, true);
    }

    /// <summary>
    /// Shows a result if it is scorable, opted in, and has an angle suitable for polar placement.
    /// </summary>
    public bool Show(DrawableHitObject drawable, JudgementResult result)
    {
        if (!DisplayJudgements.Value || !result.Type.IsScorable() || !drawable.DisplayResult || drawable.HitObject is not IHasAngle angled)
            return false;

        bool compactButtonFeedback = drawable.HitObject is Note && drawable.HitObject is not IHasDuration;
        bool holdOrSliderFeedback = drawable.HitObject is IHasDuration or SliderHead or SliderChild;

        if ((compactButtonFeedback && result.Type == HitResult.CriticalPerfect) ||
            (holdOrSliderFeedback && result.Type == HitResult.Perfect))
            return false;

        bool displayTimingOffset = drawable.DisplayTimingOffset && !(compactButtonFeedback && result.Type == HitResult.Miss);
        var message = new JudgementFeedbackMessage(result, angled.AngleDeg, displayTimingOffset, compactButtonFeedback);

        if (!message.HasContent)
        {
            message.Dispose();
            return false;
        }

        message.Sequence = nextSequence++;
        activeMessages.Add(message);
        AddInternal(message);
        message.SetPolarTarget(baseRadius, 0);
        message.BeginLifetime(FADE_IN_DURATION, READ_DURATION, FADE_OUT_DURATION, () => remove(message));
        reflow(stack_transition_duration);
        return true;
    }

    /// <summary>Removes the live message created by this exact result, if it has not expired already.</summary>
    public void Revert(JudgementResult result)
    {
        var message = activeMessages.FirstOrDefault(m => ReferenceEquals(m.Result, result));
        if (message != null)
            remove(message);
    }

    public void ClearMessages()
    {
        activeMessages.Clear();
        ClearInternal(true);
    }

    protected override void Update()
    {
        base.Update();

        if (layoutCache.IsValid)
            return;

        reflow(0);
        layoutCache.Validate();
    }

    private void onDisplayJudgementsChanged(ValueChangedEvent<bool> value)
    {
        Alpha = value.NewValue ? 1 : 0;
        if (!value.NewValue)
            ClearMessages();
    }

    private void reflow(double duration)
    {
        enforceClusterCap();

        foreach (var cluster in clusters())
        {
            var newestFirst = cluster.OrderByDescending(m => m.Sequence).ToArray();
            for (int i = 0; i < newestFirst.Length; i++)
            {
                newestFirst[i].StackIndex = i;
                newestFirst[i].SetPolarTarget(baseRadius + i * STACK_SPACING, duration);
            }
        }
    }

    private float baseRadius
    {
        get
        {
            float diameter = MathF.Min(DrawWidth, DrawHeight);
            return Math.Clamp(diameter * radius_ratio, minimum_radius, maximum_radius);
        }
    }

    private void enforceClusterCap()
    {
        while (true)
        {
            var retire = clusters()
                         .Where(c => c.Count > MAX_CLUSTER_MESSAGES)
                         .Select(c => c.OrderBy(m => m.Sequence).First())
                         .Distinct()
                         .ToArray();

            if (retire.Length == 0)
                return;

            foreach (var message in retire)
                removeInternal(message);
        }
    }

    private IReadOnlyList<List<JudgementFeedbackMessage>> clusters()
    {
        var remaining = new HashSet<JudgementFeedbackMessage>(activeMessages);
        var result = new List<List<JudgementFeedbackMessage>>();

        while (remaining.Count > 0)
        {
            var seed = remaining.First();
            remaining.Remove(seed);

            var cluster = new List<JudgementFeedbackMessage>();
            var pending = new Stack<JudgementFeedbackMessage>();
            pending.Push(seed);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                cluster.Add(current);

                foreach (var neighbour in remaining.Where(m => angularDifference(current.AngleDeg, m.AngleDeg) <= COLLISION_ANGLE_DEG).ToArray())
                {
                    remaining.Remove(neighbour);
                    pending.Push(neighbour);
                }
            }

            result.Add(cluster);
        }

        return result;
    }

    private static double angularDifference(double a, double b)
    {
        double difference = Math.Abs(a - b) % 360;
        return Math.Min(difference, 360 - difference);
    }

    private void remove(JudgementFeedbackMessage message)
    {
        if (!removeInternal(message))
            return;

        reflow(stack_transition_duration);
    }

    private bool removeInternal(JudgementFeedbackMessage message)
    {
        if (!activeMessages.Remove(message))
            return false;

        RemoveInternal(message, true);
        return true;
    }

    protected override void Dispose(bool isDisposing)
    {
        DisplayJudgements.UnbindAll();
        base.Dispose(isDisposing);
    }
}
