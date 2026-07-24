// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Objects/Drawables/DrawableHitObject.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: skinning removed (ISkinSource, combo colours, IAnimationTimeReference),
// positional hitsound balance removed, PausableSkinnableSound replaced by HitSoundContainer,
// samples are Garbus's GarbusHitSample (a sample-store lookup name) rather than osu's HitSampleInfo,
// GameplayRate reads the plain clock rate (no mod-adjusted "true" gameplay rate); result presentation
// contracts distinguish opted-out and duration-based feedback.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ListExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Lists;
using Garbus.Game.Gameplay.Audio;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects.Pooling;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Timing;
using osuTK.Graphics;

namespace Garbus.Game.Gameplay.Objects.Drawables
{
    [Cached(typeof(DrawableHitObject))]
    public abstract partial class DrawableHitObject : PoolableDrawableWithLifetime<HitObjectLifetimeEntry>
    {
        /// <summary>
        /// Invoked after this <see cref="DrawableHitObject"/>'s applied <see cref="HitObject"/> has had its defaults applied.
        /// </summary>
        public event Action<DrawableHitObject> DefaultsApplied;

        /// <summary>
        /// When set, this drawable is a presentation-only auto-hit: it plays its Hit animation at the
        /// hit time as a pure function of the clock, never produces a <see cref="JudgementResult"/>,
        /// never scores, and lets its scrolling container own its lifetime. Set at construction time
        /// (object initializer); read-only thereafter. Nested drawables inherit it via <see cref="AutoHitActive"/>.
        /// </summary>
        public bool AutoHit { get; init; }

        /// <summary>
        /// When set together with <see cref="AutoHit"/>, plays the hitsound once as the clock crosses the
        /// hit time going forward. A one-shot side effect: it does nothing on rewind or backward scrub.
        /// Set at construction time (object initializer); read-only thereafter. Off by default — wired to
        /// <c>false</c> by the silent gameplay preview, reserved for a future audible auto-hit mode.
        /// </summary>
        public bool AutoHitPlaysSamples { get; init; }

        /// <summary>The clock time observed on the previous <see cref="Update"/>, used to detect a forward
        /// crossing of the hit time for <see cref="AutoHitPlaysSamples"/>. Null until the first update.</summary>
        private double? autoHitLastTime;

        /// <summary>The effective auto-hit state, inherited by nested drawables from their parent.</summary>
        internal bool AutoHitActive => AutoHit || (ParentHitObject?.AutoHitActive ?? false);

        /// <summary>
        /// While an auto-hit drawable is at or past its start time it is treated as continuously and
        /// successfully <em>engaged</em> — held, caught, contacted — as a pure function of the clock, since
        /// auto-hit consumes no input. This is the root-level seam for auto-hit "on-hit" behaviour on
        /// durationed objects: families whose engagement is otherwise input-derived (hold notes' button
        /// hold, sliders' analog catcher) read this in their single engagement predicate so the
        /// deterministic auto-hit presentation replaces the live one. Instantaneous notes need only the
        /// forced Hit transform (see <see cref="updateStateFromResult"/>) and ignore this.
        /// </summary>
        protected bool AutoHitEngaged => AutoHitActive && Time.Current >= HitObject.StartTime;

        // Auto-hit drawables derive presence purely from time; the scrolling container owns their
        // lifetime window (GetEndTime() + timeRange — deterministic). Swallow drawable-side writes
        // from UpdateState / Expire so a scrub or rewind can't pin lifetime to a clock-moment value.
        public override double LifetimeEnd
        {
            get => base.LifetimeEnd;
            set
            {
                if (AutoHitActive)
                    return;

                base.LifetimeEnd = value;
            }
        }

        /// <summary>
        /// Invoked after a <see cref="HitObject"/> has been applied to this <see cref="DrawableHitObject"/>.
        /// </summary>
        public event Action<DrawableHitObject> HitObjectApplied;

        /// <summary>
        /// The <see cref="HitObject"/> currently represented by this <see cref="DrawableHitObject"/>.
        /// </summary>
        public HitObject HitObject => Entry?.HitObject;

        /// <summary>
        /// The parenting <see cref="DrawableHitObject"/>, if any.
        /// </summary>
        [CanBeNull]
        protected internal DrawableHitObject ParentHitObject { get; internal set; }

        /// <summary>
        /// The colour used for various elements of this DrawableHitObject.
        /// </summary>
        public readonly Bindable<Color4> AccentColour = new Bindable<Color4>(Color4.Gray);

        protected HitSoundContainer Samples { get; private set; }

        private bool samplesLoaded;

        public virtual IEnumerable<GarbusHitSample> GetSamples() => HitObject.Samples;

        private readonly List<DrawableHitObject> nestedHitObjects = new List<DrawableHitObject>();
        public SlimReadOnlyListWrapper<DrawableHitObject> NestedHitObjects => nestedHitObjects.AsSlimReadOnly();

        /// <summary>
        /// Whether this object should handle any user input events.
        /// </summary>
        public bool HandleUserInput { get; set; } = true;

        public override bool PropagatePositionalInputSubTree => HandleUserInput;

        public override bool PropagateNonPositionalInputSubTree => HandleUserInput;

        /// <summary>
        /// Invoked by this or a nested <see cref="DrawableHitObject"/> after a <see cref="JudgementResult"/> has been applied.
        /// </summary>
        public event Action<DrawableHitObject, JudgementResult> OnNewResult;

        /// <summary>
        /// Invoked by this or a nested <see cref="DrawableHitObject"/> prior to a <see cref="JudgementResult"/> being reverted.
        /// </summary>
        /// <remarks>
        /// This is only invoked if this <see cref="DrawableHitObject"/> is alive when the result is reverted.
        /// </remarks>
        public event Action<DrawableHitObject, JudgementResult> OnRevertResult;

        /// <summary>
        /// Invoked when a new nested hit object is created by <see cref="CreateNestedHitObject" />.
        /// </summary>
        internal event Action<DrawableHitObject> OnNestedDrawableCreated;

        /// <summary>
        /// Whether a visual indicator should be displayed when a scoring result occurs.
        /// </summary>
        public virtual bool DisplayResult => true;

        /// <summary>
        /// Whether this result's signed time offset represents player input timing and may be shown as
        /// early/late feedback. Duration judgements override this because their application time is not
        /// their quality measurement.
        /// </summary>
        public virtual bool DisplayTimingOffset => true;

        /// <summary>
        /// The scoring result of this <see cref="DrawableHitObject"/>.
        /// </summary>
        public JudgementResult Result => Entry?.Result;

        /// <summary>
        /// Whether this <see cref="DrawableHitObject"/> has been hit. This occurs if <see cref="Result"/> is hit.
        /// Note: This does NOT include nested hitobjects.
        /// </summary>
        public bool IsHit => Result?.IsHit ?? false;

        /// <summary>
        /// Whether this <see cref="DrawableHitObject"/> has been judged.
        /// Note: This does NOT include nested hitobjects.
        /// </summary>
        public bool Judged => Entry?.Judged ?? false;

        /// <summary>
        /// Whether this <see cref="DrawableHitObject"/> and all of its nested <see cref="DrawableHitObject"/>s have been judged.
        /// </summary>
        public bool AllJudged => Entry?.AllJudged ?? false;

        public readonly Bindable<double> StartTimeBindable = new Bindable<double>();
        private readonly BindableList<GarbusHitSample> samplesBindable = new BindableList<GarbusHitSample>();

        protected override bool RequiresChildrenUpdate => true;

        public override bool IsPresent => base.IsPresent || (State.Value == ArmedState.Idle && Clock.IsNotNull() && Clock.CurrentTime >= LifetimeStart);

        private readonly Bindable<ArmedState> state = new Bindable<ArmedState>();

        /// <summary>
        /// The state of this <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// For pooled hitobjects, <see cref="ApplyCustomUpdateState"/> is recommended to be used instead for better rewinding support.
        /// </remarks>
        public IBindable<ArmedState> State => state;

        [Resolved(CanBeNull = true)]
        private IPooledHitObjectProvider pooledObjectProvider { get; set; }

        /// <summary>
        /// Whether the initialization logic in <see cref="Playfield" /> has applied.
        /// </summary>
        internal bool IsInitialized;

        /// <summary>
        /// Creates a new <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <param name="initialHitObject">
        /// The <see cref="HitObject"/> to be initially applied to this <see cref="DrawableHitObject"/>.
        /// If <c>null</c>, a hitobject is expected to be later applied via <see cref="PoolableDrawableWithLifetime{TEntry}.Apply"/> (or automatically via pooling).
        /// </param>
        protected DrawableHitObject([CanBeNull] HitObject initialHitObject = null)
        {
            if (initialHitObject == null) return;

            Entry = new SyntheticHitObjectEntry(initialHitObject);
            ensureEntryHasResult();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Explicit non-virtual function call in case a DrawableHitObject overrides AddInternal.
            base.AddInternal(Samples = new HitSoundContainer());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            samplesBindable.BindCollectionChanged((_, _) =>
            {
                if (samplesLoaded)
                    LoadSamples();
            });

            // Apply transforms
            updateStateFromResult();
        }

        /// <summary>
        /// Applies a new <see cref="HitObject"/> to be represented by this <see cref="DrawableHitObject"/>.
        /// A new <see cref="HitObjectLifetimeEntry"/> is automatically created and applied to this <see cref="DrawableHitObject"/>.
        /// </summary>
        public void Apply([NotNull] HitObject hitObject)
        {
            ArgumentNullException.ThrowIfNull(hitObject);

            Apply(new SyntheticHitObjectEntry(hitObject));
        }

        protected sealed override void OnApply(HitObjectLifetimeEntry entry)
        {
            Debug.Assert(Entry != null);

            // LifetimeStart is already computed using HitObjectLifetimeEntry's InitialLifetimeOffset.
            // We override this with DHO's InitialLifetimeOffset for a non-pooled DHO.
            if (entry is SyntheticHitObjectEntry)
                LifetimeStart = HitObject.StartTime - InitialLifetimeOffset;

            ensureEntryHasResult();

            entry.RevertResult += onRevertResult;

            foreach (var h in HitObject.NestedHitObjects)
            {
                var pooledDrawableNested = pooledObjectProvider?.GetPooledDrawableRepresentation(h, this);
                var drawableNested = pooledDrawableNested
                                     ?? CreateNestedHitObject(h)
                                     ?? throw new InvalidOperationException($"{nameof(CreateNestedHitObject)} returned null for {h.GetType().ReadableName()}.");

                // Only invoke the event for non-pooled DHOs, otherwise the event will be fired by the playfield.
                if (pooledDrawableNested == null)
                    OnNestedDrawableCreated?.Invoke(drawableNested);

                drawableNested.OnNewResult += onNewResult;
                drawableNested.OnRevertResult += onNestedRevertResult;
                drawableNested.ApplyCustomUpdateState += onApplyCustomUpdateState;

                // This is only necessary for non-pooled DHOs. For pooled DHOs, this is handled inside GetPooledDrawableRepresentation().
                // Must be done before the nested DHO is added to occur before the nested Apply()!
                drawableNested.ParentHitObject = this;

                nestedHitObjects.Add(drawableNested);

                // assume that synthetic entries are not pooled and therefore need to be managed from within the DHO.
                // this is important for the correctness of value of flags such as `AllJudged`.
                if (drawableNested.Entry is SyntheticHitObjectEntry syntheticNestedEntry)
                    Entry.NestedEntries.Add(syntheticNestedEntry);

                AddNestedHitObject(drawableNested);
            }

            StartTimeBindable.BindTo(HitObject.StartTimeBindable);
            samplesBindable.BindTo(HitObject.SamplesBindable);
            HitObject.DefaultsApplied += onDefaultsApplied;

            OnApply();
            HitObjectApplied?.Invoke(this);

            // If not loaded, the state update happens in LoadComplete().
            if (IsLoaded)
                updateStateFromResult();
        }

        private void updateStateFromResult()
        {
            if (AutoHitActive)
            {
                // Presentation only: play the Hit animation at the natural hit time (HitStateUpdateTime
                // → GetEndTime() with no result). Forced, so no hitsound fires and no result is produced.
                //
                // Deferred one tick via ScheduleAfterChildren: originally this guarded against a race with
                // PoolableDrawable's own PrepareForUse() scheduling a same-target-member transform (e.g.
                // DrawableCardinalNote's spawn-in ScaleTo) that osu-framework's same-target-member transform
                // pruning would collide with the Hit-state transforms, aborting the whole chained
                // TransformSequence. As of Task 9 the spawn-in transform moved out of PrepareForUse into
                // UpdateInitialTransforms, so that collision can no longer occur. The deferral is kept
                // anyway as harmless belt-and-suspenders — the forced Hit transforms are absolute-anchored
                // regardless of ordering, so scheduling after children costs nothing and removing it is a
                // separate, unneeded change.
                ScheduleAfterChildren(() => UpdateState(ArmedState.Hit, true));
                return;
            }

            if (Result.IsHit)
                UpdateState(ArmedState.Hit, true);
            else if (Result.HasResult)
                UpdateState(ArmedState.Miss, true);
            else
                UpdateState(ArmedState.Idle, true);
        }

        protected sealed override void OnFree(HitObjectLifetimeEntry entry)
        {
            Debug.Assert(Entry != null);

            StartTimeBindable.UnbindFrom(HitObject.StartTimeBindable);
            samplesBindable.UnbindFrom(HitObject.SamplesBindable);

            // Release the samples for other hitobjects to use.
            samplesLoaded = false;
            Samples?.ClearSamples();

            foreach (var obj in nestedHitObjects)
            {
                obj.OnNewResult -= onNewResult;
                obj.OnRevertResult -= onNestedRevertResult;
                obj.ApplyCustomUpdateState -= onApplyCustomUpdateState;
            }

            nestedHitObjects.Clear();
            // clean up synthetic entries manually added in `Apply()`.
            Entry.NestedEntries.RemoveAll(nestedEntry => nestedEntry is SyntheticHitObjectEntry);
            ClearNestedHitObjects();

            // Changes to `HitObject` properties trigger default application, which triggers `State` updates.
            // When a new hitobject is applied, `OnApply()` automatically performs a state update.
            HitObject.DefaultsApplied -= onDefaultsApplied;

            entry.RevertResult -= onRevertResult;

            OnFree();

            ParentHitObject = null;

            clearExistingStateTransforms();
        }

        /// <summary>
        /// Invoked for this <see cref="DrawableHitObject"/> to take on any values from a newly-applied <see cref="HitObject"/>.
        /// This is also fired after any changes which occurred via an <see cref="Objects.HitObject.ApplyDefaults"/> call.
        /// </summary>
        protected virtual void OnApply()
        {
        }

        /// <summary>
        /// Invoked for this <see cref="DrawableHitObject"/> to revert any values previously taken on from the currently-applied <see cref="HitObject"/>.
        /// This is also fired after any changes which occurred via an <see cref="Objects.HitObject.ApplyDefaults"/> call.
        /// </summary>
        protected virtual void OnFree()
        {
        }

        /// <summary>
        /// Invoked by the base <see cref="DrawableHitObject"/> to populate samples, once on initial load and potentially again on any change to the samples collection.
        /// </summary>
        protected virtual void LoadSamples()
        {
            Samples.Samples = GetSamples().ToArray();
        }

        private void onNewResult(DrawableHitObject drawableHitObject, JudgementResult result) => OnNewResult?.Invoke(drawableHitObject, result);

        private void onRevertResult()
        {
            UpdateState(ArmedState.Idle);
            OnRevertResult?.Invoke(this, Result);
        }

        private void onNestedRevertResult(DrawableHitObject drawableHitObject, JudgementResult result) => OnRevertResult?.Invoke(drawableHitObject, result);

        private void onApplyCustomUpdateState(DrawableHitObject drawableHitObject, ArmedState state) => ApplyCustomUpdateState?.Invoke(drawableHitObject, state);

        private void onDefaultsApplied(HitObject hitObject)
        {
            Debug.Assert(Entry != null);
            Apply(Entry);

            // Applied defaults indicate a change in hit object state.
            // We need to update the judgement result time to the new end time
            // and update state to ensure the hit object fades out at the correct time.
            if (Result is not null)
            {
                Result.TimeOffset = 0;
                UpdateState(State.Value, true);
            }

            DefaultsApplied?.Invoke(this);
        }

        /// <summary>
        /// Invoked by the base <see cref="DrawableHitObject"/> to add nested <see cref="DrawableHitObject"/>s to the hierarchy.
        /// </summary>
        /// <param name="hitObject">The <see cref="DrawableHitObject"/> to be added.</param>
        protected virtual void AddNestedHitObject(DrawableHitObject hitObject)
        {
        }

        /// <summary>
        /// Invoked by the base <see cref="DrawableHitObject"/> to remove all previously-added nested <see cref="DrawableHitObject"/>s.
        /// </summary>
        protected virtual void ClearNestedHitObjects()
        {
        }

        /// <summary>
        /// Creates the drawable representation for a nested <see cref="HitObject"/>.
        /// </summary>
        /// <param name="hitObject">The <see cref="HitObject"/>.</param>
        /// <returns>The drawable representation for <paramref name="hitObject"/>.</returns>
        protected virtual DrawableHitObject CreateNestedHitObject(HitObject hitObject) => null;

        #region State / Transform Management

        /// <summary>
        /// Invoked by this or a nested <see cref="DrawableHitObject"/> to apply a custom state that can override the default implementation.
        /// </summary>
        public event Action<DrawableHitObject, ArmedState> ApplyCustomUpdateState;

        protected override void ClearInternal(bool disposeChildren = true) =>
            // See sample addition in load method.
            throw new InvalidOperationException(
                $"Should never clear a {nameof(DrawableHitObject)} as the base implementation adds components. If attempting to use {nameof(InternalChild)} or {nameof(InternalChildren)}, using {nameof(AddInternal)} or {nameof(AddRangeInternal)} instead.");

        protected void UpdateState(ArmedState newState, bool force = false)
        {
            if (State.Value == newState && !force)
                return;

            LifetimeEnd = double.MaxValue;

            clearExistingStateTransforms();

            double initialTransformsTime = HitObject.StartTime - InitialLifetimeOffset;

            using (BeginAbsoluteSequence(initialTransformsTime))
                UpdateInitialTransforms();

            using (BeginAbsoluteSequence(StateUpdateTime))
                UpdateStartTimeStateTransforms();

            using (BeginAbsoluteSequence(HitStateUpdateTime))
                UpdateHitStateTransforms(newState);

            state.Value = newState;

            if (LifetimeEnd == double.MaxValue && (state.Value != ArmedState.Idle || HitObject.HitWindows == null))
                LifetimeEnd = Math.Max(LatestTransformEndTime, HitStateUpdateTime + (Samples?.Length ?? 0));

            // apply any custom state overrides
            ApplyCustomUpdateState?.Invoke(this, newState);

            if (!force && newState == ArmedState.Hit)
                PlaySamples();
        }

        private void clearExistingStateTransforms()
        {
            base.ApplyTransformsAt(double.MinValue, true);

            // has to call this method directly (not ClearTransforms) to bypass the local ClearTransformsAfter override.
            base.ClearTransformsAfter(double.MinValue, true);
        }

        /// <summary>
        /// Reapplies the current <see cref="ArmedState"/>.
        /// </summary>
        public void RefreshStateTransforms() => UpdateState(State.Value, true);

        /// <summary>
        /// Apply (generally fade-in) transforms leading into the <see cref="HitObject"/> start time.
        /// By default, this will fade in the object from zero with no duration.
        /// </summary>
        /// <remarks>
        /// This is called once before every <see cref="UpdateHitStateTransforms"/>. This is to ensure a good state in the case
        /// the <see cref="JudgementResult.TimeOffset"/> was negative and potentially altered the pre-hit transforms.
        /// </remarks>
        protected virtual void UpdateInitialTransforms()
        {
            this.FadeInFromZero();
        }

        /// <summary>
        /// Apply passive transforms at the <see cref="HitObject"/>'s StartTime.
        /// This is called each time <see cref="State"/> changes.
        /// Previous states are automatically cleared.
        /// </summary>
        protected virtual void UpdateStartTimeStateTransforms()
        {
        }

        /// <summary>
        /// Apply transforms based on the current <see cref="ArmedState"/>. This call is offset by <see cref="HitStateUpdateTime"/> (HitObject.EndTime + Result.Offset), equivalent to when the user hit the object.
        /// If <see cref="Drawable.LifetimeEnd"/> was not set during this call, <see cref="Drawable.Expire"/> will be invoked.
        /// Previous states are automatically cleared.
        /// </summary>
        /// <param name="state">The new armed state.</param>
        protected virtual void UpdateHitStateTransforms(ArmedState state)
        {
        }

        public override void ClearTransformsAfter(double time, bool propagateChildren = false, string targetMember = null)
        {
            // Parent calls to this should be blocked for safety, as we are manually handling this in updateState.
        }

        public override void ApplyTransformsAt(double time, bool propagateChildren = false)
        {
            // Parent calls to this should be blocked for safety, as we are manually handling this in updateState.
        }

        #endregion

        /// <summary>
        /// Plays all the hit sounds for this <see cref="DrawableHitObject"/>.
        /// This is invoked automatically when this <see cref="DrawableHitObject"/> is hit.
        /// </summary>
        public virtual void PlaySamples() => Samples?.Play();

        /// <summary>
        /// Stops playback of all relevant samples. Generally only looping samples should be stopped by this, and the rest let to play out.
        /// Automatically called when <see cref="DrawableHitObject{TObject}"/>'s lifetime has been exceeded.
        /// </summary>
        /// <remarks>
        /// Garbus has no looping gameplay samples, so one-shot hit sounds are deliberately left to play out.
        /// </remarks>
        public virtual void StopAllSamples()
        {
        }

        protected override void Update()
        {
            // We use a flag here to load samples only when they are required to be played.
            // This is a best-effort optimisation (over loading in `OnApply`) to avoid de-pooling
            // many samples at once and causing a gameplay stutter.
            if (!samplesLoaded)
            {
                samplesLoaded = true;
                LoadSamples();
            }

            if (AutoHitActive && AutoHitPlaysSamples)
            {
                double hitTime = HitObject.GetEndTime();

                // Forward crossing only: previous frame strictly before the hit, this frame at/after it.
                // Guards against firing on rewind/backward scrub, and against repeat fires on later frames.
                //
                // Plays via Samples directly rather than the virtual PlaySamples(): concrete overrides
                // (e.g. DrawableGarbusHitObject<T>, DrawableSliderChild) route through
                // GarbusHitSoundPlayback.Play, which gates on a judged Hit JudgementResult to resolve an
                // accuracy-specific sample variant — a result that presentation-only autoHit drawables
                // never produce. There is no accuracy to resolve here, so play the object's assigned
                // samples as-is (the same behaviour the base, un-overridden PlaySamples() provides).
                if (autoHitLastTime is double prev && prev < hitTime && Time.Current >= hitTime)
                    Samples?.Play();

                autoHitLastTime = Time.Current;
            }

            base.Update();
        }

        public override bool UpdateSubTreeMasking() => false;

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            UpdateResult(false);
        }

        /// <summary>
        /// An offset prior to the start time of <see cref="HitObject"/> at which this <see cref="DrawableHitObject"/> may begin displaying contents.
        /// By default, <see cref="DrawableHitObject"/>s are assumed to display their contents within 10 seconds prior to the start time of <see cref="HitObject"/>.
        /// </summary>
        /// <remarks>
        /// The initial transformation (<see cref="UpdateInitialTransforms"/>) starts at this offset before the start time of <see cref="HitObject"/>.
        /// </remarks>
        protected virtual double InitialLifetimeOffset => 10000;

        /// <summary>
        /// The time at which state transforms should be applied that line up to <see cref="HitObject"/>'s StartTime.
        /// This is used to offset calls to <see cref="UpdateStartTimeStateTransforms"/>.
        /// </summary>
        public double StateUpdateTime => HitObject.StartTime;

        /// <summary>
        /// The time at which judgement dependent state transforms should be applied. This is equivalent of the (end) time of the object, in addition to any judgement offset.
        /// This is used to offset calls to <see cref="UpdateHitStateTransforms"/>.
        /// </summary>
        public double HitStateUpdateTime => Result?.TimeAbsolute ?? HitObject.GetEndTime();

        /// <summary>
        /// Will be called at least once after this <see cref="DrawableHitObject"/> has become not alive.
        /// </summary>
        public virtual void OnKilled()
        {
            foreach (var nested in NestedHitObjects)
                nested.OnKilled();

            // failsafe to ensure looping samples don't get stuck in a playing state.
            StopAllSamples();

            UpdateResult(false);
        }

        protected void ApplyMaxResult() => ApplyResult((r, _) => r.Type = r.Judgement.MaxResult);
        protected void ApplyMinResult() => ApplyResult((r, _) => r.Type = r.Judgement.MinResult);

        protected void ApplyResult(HitResult type) => ApplyResult(static (result, state) => result.Type = state, type);

        protected void ApplyResult(Action<JudgementResult, DrawableHitObject> application) => ApplyResult(application, this);

        /// <summary>
        /// Applies the <see cref="Result"/> of this <see cref="DrawableHitObject"/>, notifying responders of the <see cref="JudgementResult"/>.
        /// </summary>
        /// <param name="application">The callback that applies changes to the <see cref="JudgementResult"/>. Using a `static` delegate is recommended to avoid allocation overhead.</param>
        /// <param name="state">
        /// Use this parameter to pass any data that <paramref name="application"/> requires
        /// to apply a result, so that it can remain a `static` delegate and thus not allocate.
        /// </param>
        protected void ApplyResult<T>(Action<JudgementResult, T> application, T state)
        {
            if (Result.HasResult)
                throw new InvalidOperationException("Cannot apply result on a hitobject that already has a result.");

            application?.Invoke(Result, state);

            if (!Result.HasResult)
                throw new InvalidOperationException($"{GetType().ReadableName()} applied a {nameof(JudgementResult)} but did not update {nameof(JudgementResult.Type)}.");

            HitResultExtensions.ValidateHitResultPair(Result.Judgement.MaxResult, Result.Judgement.MinResult);

            if (!Result.Type.IsValidHitResult(Result.Judgement.MinResult, Result.Judgement.MaxResult))
            {
                throw new InvalidOperationException(
                    $"{GetType().ReadableName()} applied an invalid hit result (was: {Result.Type}, expected: [{Result.Judgement.MinResult} ... {Result.Judgement.MaxResult}]).");
            }

            Result.RawTime = Time.Current;
            Result.GameplayRate = Clock.Rate;

            if (Result.HasResult)
                UpdateState(Result.IsHit ? ArmedState.Hit : ArmedState.Miss);

            OnNewResult?.Invoke(this, Result);
        }

        /// <summary>
        /// Processes this <see cref="DrawableHitObject"/>, checking if a scoring result has occurred.
        /// </summary>
        /// <param name="userTriggered">Whether the user triggered this process.</param>
        /// <returns>Whether a scoring result has occurred from this <see cref="DrawableHitObject"/> or any nested <see cref="DrawableHitObject"/>.</returns>
        protected bool UpdateResult(bool userTriggered)
        {
            // Auto-hit drawables never go through the input / miss-check result path — no JudgementResult,
            // no scoring, no feedback. Presentation only, in every context.
            if (AutoHitActive)
                return false;

            // It's possible for input to get into a bad state when rewinding gameplay, so results should not be processed
            if ((Clock as IGameplayClock)?.IsRewinding == true)
                return false;

            if (Judged)
                return false;

            CheckForResult(userTriggered, Time.Current - HitObject.GetEndTime());

            return Judged;
        }

        /// <summary>
        /// Checks if a scoring result has occurred for this <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// If a scoring result has occurred, this method must invoke <see cref="ApplyResult{T}"/> to update the result and notify responders.
        /// </remarks>
        /// <param name="userTriggered">Whether the user triggered this check.</param>
        /// <param name="timeOffset">The offset from the end time of the <see cref="HitObject"/> at which this check occurred.
        /// A <paramref name="timeOffset"/> &gt; 0 implies that this check occurred after the end time of the <see cref="HitObject"/>. </param>
        protected virtual void CheckForResult(bool userTriggered, double timeOffset)
        {
        }

        /// <summary>
        /// Creates the <see cref="JudgementResult"/> that represents the scoring result for this <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <param name="judgement">The <see cref="Judgement"/> that provides the scoring information.</param>
        protected internal virtual JudgementResult CreateResult(Judgement judgement) => new JudgementResult(HitObject, judgement);

        private void ensureEntryHasResult()
        {
            Debug.Assert(Entry != null);
            Entry.Result ??= CreateResult(HitObject.Judgement)
                             ?? throw new InvalidOperationException($"{GetType().ReadableName()} must provide a {nameof(JudgementResult)} through {nameof(CreateResult)}.");
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (HitObject != null)
                HitObject.DefaultsApplied -= onDefaultsApplied;

            // Safeties against shooting in foot in cases where these are bound by external entities (like playfield) that don't clean up.
            OnNestedDrawableCreated = null;
            OnNewResult = null;
            OnRevertResult = null;
            DefaultsApplied = null;
            HitObjectApplied = null;
        }
    }

    public abstract partial class DrawableHitObject<TObject> : DrawableHitObject
        where TObject : HitObject
    {
        public new TObject HitObject => (TObject)base.HitObject;

        protected DrawableHitObject([CanBeNull] TObject hitObject)
            : base(hitObject)
        {
        }
    }
}
