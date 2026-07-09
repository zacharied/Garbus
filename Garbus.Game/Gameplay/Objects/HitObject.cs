// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Objects/HitObject.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: ControlPointInfo/Kiai and IBeatmapDifficultyInfo removed (ApplyDefaults takes no
// timing/difficulty arguments; hit windows are fixed), combo information and legacy sample helpers
// (CreateSlidingSamples/CreateHitSampleInfo) removed, nullability enabled.

using System;
using System.Collections.Generic;
using System.Threading;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ListExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Lists;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Scoring;

namespace Garbus.Game.Gameplay.Objects
{
    /// <summary>
    /// A HitObject describes an object in a chart.
    /// <para>
    /// HitObjects may contain more properties for which you should be checking through the IHas* types.
    /// </para>
    /// </summary>
    public class HitObject
    {
        /// <summary>
        /// Invoked after <see cref="ApplyDefaults"/> has completed on this <see cref="HitObject"/>.
        /// </summary>
        public event Action<HitObject>? DefaultsApplied;

        public readonly Bindable<double> StartTimeBindable = new BindableDouble();

        /// <summary>
        /// The time at which the HitObject starts.
        /// </summary>
        public virtual double StartTime
        {
            get => StartTimeBindable.Value;
            set => StartTimeBindable.Value = value;
        }

        public readonly BindableList<HitSampleInfo> SamplesBindable = new BindableList<HitSampleInfo>();

        /// <summary>
        /// The samples to be played when this hit object is hit.
        /// </summary>
        public IList<HitSampleInfo> Samples
        {
            get => SamplesBindable;
            set
            {
                SamplesBindable.Clear();
                SamplesBindable.AddRange(value);
            }
        }

        /// <summary>
        /// The hit windows for this <see cref="HitObject"/>.
        /// </summary>
        public HitWindows HitWindows { get; set; } = null!;

        private readonly List<HitObject> nestedHitObjects = new List<HitObject>();

        public SlimReadOnlyListWrapper<HitObject> NestedHitObjects => nestedHitObjects.AsSlimReadOnly();

        /// <summary>
        /// Applies default values to this HitObject.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public void ApplyDefaults(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ApplyDefaultsToSelf();

            nestedHitObjects.Clear();

            CreateNestedHitObjects(cancellationToken);

            nestedHitObjects.Sort((h1, h2) => h1.StartTime.CompareTo(h2.StartTime));

            foreach (var h in nestedHitObjects)
                h.ApplyDefaults(cancellationToken);

            // `ApplyDefaults()` may be called multiple times on a single hitobject.
            // to prevent subscribing to `StartTimeBindable.ValueChanged` multiple times with the same callback,
            // remove the previous subscription (if present) before (re-)registering.
            StartTimeBindable.ValueChanged -= onStartTimeChanged;

            // this callback must be (re-)registered after default application
            // to ensure that the read of `this.GetEndTime()` within `onStartTimeChanged` doesn't return an invalid value
            // if `StartTimeBindable` is changed prior to default application.
            StartTimeBindable.ValueChanged += onStartTimeChanged;

            DefaultsApplied?.Invoke(this);

            void onStartTimeChanged(ValueChangedEvent<double> time)
            {
                double offset = time.NewValue - time.OldValue;

                foreach (var nested in nestedHitObjects)
                    nested.StartTime += offset;
            }
        }

        protected virtual void ApplyDefaultsToSelf()
        {
            HitWindows ??= CreateHitWindows();
        }

        protected virtual void CreateNestedHitObjects(CancellationToken cancellationToken)
        {
        }

        protected void AddNested(HitObject hitObject) => nestedHitObjects.Add(hitObject);

        /// <summary>
        /// The <see cref="Judgements.Judgement"/> that represents the scoring information for this <see cref="HitObject"/>.
        /// </summary>
        public Judgement Judgement => judgement ??= CreateJudgement();

        private Judgement? judgement;

        /// <summary>
        /// Should be overridden to create a <see cref="Judgements.Judgement"/> that represents the scoring information for this <see cref="HitObject"/>.
        /// </summary>
        /// <remarks>
        /// For read access, use <see cref="Judgement"/> to avoid unnecessary allocations.
        /// </remarks>
        public virtual Judgement CreateJudgement() => new Judgement();

        /// <summary>
        /// Creates the <see cref="Scoring.HitWindows"/> for this <see cref="HitObject"/>.
        /// This can be null to indicate that the <see cref="HitObject"/> has no <see cref="Scoring.HitWindows"/> and timing errors should not be displayed to the user.
        /// <para>
        /// This will only be invoked if <see cref="HitWindows"/> hasn't been set externally.
        /// </para>
        /// </summary>
        protected virtual HitWindows CreateHitWindows() => new DefaultHitWindows();

        /// <summary>
        /// The maximum offset from the end time of <see cref="HitObject"/> at which this <see cref="HitObject"/> can be judged.
        /// <para>
        /// Defaults to the miss window.
        /// </para>
        /// </summary>
        public virtual double MaximumJudgementOffset => HitWindows?.WindowFor(HitResult.Miss) ?? 0;

        public override string ToString() => $"{GetType().ReadableName()} @ {StartTime}";
    }

    public static class HitObjectExtensions
    {
        /// <summary>
        /// Returns the end time of this object.
        /// </summary>
        /// <remarks>
        /// This returns the <see cref="Types.IHasDuration.EndTime"/> where available, falling back to <see cref="HitObject.StartTime"/> otherwise.
        /// </remarks>
        /// <param name="hitObject">The object.</param>
        /// <returns>The end time of this object.</returns>
        public static double GetEndTime(this HitObject hitObject) => (hitObject as Types.IHasDuration)?.EndTime ?? hitObject.StartTime;
    }
}
