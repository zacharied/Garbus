// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/UI/Playfield.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: mods, gameplay cursor, skinnable component quad and skinnable sample pooling
// removed (hit sounds play through HitSoundContainer on each DrawableHitObject instead).

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Bindables;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Allocation;
using Garbus.Game.Gameplay;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Objects.Pooling;
using osuTK;

namespace Garbus.Game.Gameplay.UI
{
    [Cached(typeof(IPooledHitObjectProvider))]
    [Cached]
    public abstract partial class Playfield : CompositeDrawable, IPooledHitObjectProvider
    {
        /// <summary>
        /// Invoked when a <see cref="DrawableHitObject"/> is judged.
        /// </summary>
        public event Action<DrawableHitObject, JudgementResult> NewResult;

        /// <summary>
        /// Invoked when a judgement result is reverted.
        /// </summary>
        public event Action<JudgementResult> RevertResult;

        /// <summary>
        /// The <see cref="DrawableHitObject"/> contained in this Playfield.
        /// </summary>
        public HitObjectContainer HitObjectContainer => hitObjectContainerLazy.Value;

        private readonly Lazy<HitObjectContainer> hitObjectContainerLazy;

        /// <summary>
        /// A function that converts gamefield coordinates to screen space.
        /// </summary>
        public Func<Vector2, Vector2> GamefieldToScreenSpace => HitObjectContainer.ToScreenSpace;

        /// <summary>
        /// A function that converts screen space coordinates to gamefield.
        /// </summary>
        public Func<Vector2, Vector2> ScreenSpaceToGamefield => HitObjectContainer.ToLocalSpace;

        /// <summary>
        /// All the <see cref="DrawableHitObject"/>s contained in this <see cref="Playfield"/> and all <see cref="NestedPlayfields"/>.
        /// </summary>
        public IEnumerable<DrawableHitObject> AllHitObjects
        {
            get
            {
                if (HitObjectContainer == null)
                    return Enumerable.Empty<DrawableHitObject>();

                var enumerable = HitObjectContainer.Objects;

                if (nestedPlayfields.Count != 0)
                    enumerable = enumerable.Concat(NestedPlayfields.SelectMany(p => p.AllHitObjects));

                return enumerable;
            }
        }

        /// <summary>
        /// All <see cref="Playfield"/>s nested inside this <see cref="Playfield"/>.
        /// </summary>
        public IEnumerable<Playfield> NestedPlayfields => nestedPlayfields;

        private readonly List<Playfield> nestedPlayfields = new List<Playfield>();

        /// <summary>
        /// Whether this <see cref="Playfield"/> is nested in another <see cref="Playfield"/>.
        /// </summary>
        public bool IsNested { get; private set; }

        /// <summary>
        /// Whether judgements should be displayed by this and and all nested <see cref="Playfield"/>s.
        /// </summary>
        public readonly BindableBool DisplayJudgements = new BindableBool(true);

        private readonly HitObjectEntryManager entryManager = new HitObjectEntryManager();

        private readonly List<HitObjectLifetimeEntry> judgedEntries;

        [Resolved(CanBeNull = true)]
        private IGameplayPresentationPolicy presentationPolicy { get; set; }

        /// <summary>
        /// Creates a new <see cref="Playfield"/>.
        /// </summary>
        protected Playfield()
        {
            RelativeSizeAxes = Axes.Both;

            hitObjectContainerLazy = new Lazy<HitObjectContainer>(() => CreateHitObjectContainer().With(h =>
            {
                h.NewResult += onNewResult;
                h.HitObjectUsageBegan += o => HitObjectUsageBegan?.Invoke(o);
                h.HitObjectUsageFinished += o => HitObjectUsageFinished?.Invoke(o);
            }));

            entryManager.OnEntryAdded += onEntryAdded;
            entryManager.OnEntryRemoved += onEntryRemoved;

            judgedEntries = new List<HitObjectLifetimeEntry>();
        }

        private void onNewDrawableHitObject(DrawableHitObject d)
        {
            d.OnNestedDrawableCreated += onNewDrawableHitObject;

            OnNewDrawableHitObject(d);

            Debug.Assert(!d.IsInitialized);
            d.IsInitialized = true;
        }

        /// <summary>
        /// Performs post-processing tasks (if any) after all DrawableHitObjects are loaded into this Playfield.
        /// </summary>
        public virtual void PostProcess() => NestedPlayfields.ForEach(p => p.PostProcess());

        /// <summary>
        /// Adds a DrawableHitObject to this Playfield.
        /// </summary>
        /// <param name="h">The DrawableHitObject to add.</param>
        public virtual void Add(DrawableHitObject h)
        {
            if (!h.IsInitialized)
                onNewDrawableHitObject(h);

            HitObjectContainer.Add(h);
            OnHitObjectAdded(h.HitObject);
        }

        /// <summary>
        /// Remove a DrawableHitObject from this Playfield.
        /// </summary>
        /// <param name="h">The DrawableHitObject to remove.</param>
        public virtual bool Remove(DrawableHitObject h)
        {
            if (!HitObjectContainer.Remove(h))
                return false;

            OnHitObjectRemoved(h.HitObject);
            return false;
        }

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> is added to this <see cref="Playfield"/>.
        /// </summary>
        /// <param name="hitObject">The added <see cref="HitObject"/>.</param>
        protected virtual void OnHitObjectAdded(HitObject hitObject)
        {
        }

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> is removed from this <see cref="Playfield"/>.
        /// </summary>
        /// <param name="hitObject">The removed <see cref="HitObject"/>.</param>
        protected virtual void OnHitObjectRemoved(HitObject hitObject)
        {
        }

        /// <summary>
        /// Invoked before a new <see cref="DrawableHitObject"/> is added to this <see cref="Playfield"/>.
        /// It is invoked only once even if the drawable is pooled and used multiple times for different <see cref="HitObject"/>s.
        /// </summary>
        /// <remarks>
        /// This is also invoked for nested <see cref="DrawableHitObject"/>s.
        /// </remarks>
        protected virtual void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
        }

        /// <summary>
        /// Registers a <see cref="Playfield"/> as a nested <see cref="Playfield"/>.
        /// This does not add the <see cref="Playfield"/> to the draw hierarchy.
        /// </summary>
        /// <param name="otherPlayfield">The <see cref="Playfield"/> to add.</param>
        protected void AddNested(Playfield otherPlayfield)
        {
            otherPlayfield.IsNested = true;

            otherPlayfield.DisplayJudgements.BindTo(DisplayJudgements);

            otherPlayfield.NewResult += (d, r) => NewResult?.Invoke(d, r);
            otherPlayfield.RevertResult += r => RevertResult?.Invoke(r);
            otherPlayfield.HitObjectUsageBegan += h => HitObjectUsageBegan?.Invoke(h);
            otherPlayfield.HitObjectUsageFinished += h => HitObjectUsageFinished?.Invoke(h);

            nestedPlayfields.Add(otherPlayfield);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // in the case a consumer forgets to add the HitObjectContainer, we will add it here.
            if (HitObjectContainer.Parent == null)
                AddInternal(HitObjectContainer);
        }

        protected override void Update()
        {
            base.Update();

            double currentTime = Time.Current;

            // When rewinding, revert future judgements in the reverse order.
            while (judgedEntries.Count > 0)
            {
                HitObjectLifetimeEntry entry = judgedEntries[judgedEntries.Count - 1];
                var result = entry.Result;
                Debug.Assert(result?.RawTime != null);

                if (currentTime >= result.RawTime.Value)
                    break;

                removeJudgedEntryAt(judgedEntries.Count - 1);
                revertResult(entry);
            }
        }

        /// <summary>
        /// Creates the container that will be used to contain the <see cref="DrawableHitObject"/>s.
        /// </summary>
        protected virtual HitObjectContainer CreateHitObjectContainer() => new HitObjectContainer();

        #region Pooling support

        private readonly Dictionary<Type, IDrawablePool> pools = new Dictionary<Type, IDrawablePool>();

        /// <summary>
        /// Adds a <see cref="HitObjectLifetimeEntry"/> for a pooled <see cref="HitObject"/> to this <see cref="Playfield"/>.
        /// </summary>
        /// <param name="hitObject"></param>
        public virtual void Add(HitObject hitObject)
        {
            var entry = CreateLifetimeEntry(hitObject);
            entryManager.Add(entry, null);
        }

        /// <summary>
        /// Removes a <see cref="HitObjectLifetimeEntry"/> for a pooled <see cref="HitObject"/> from this <see cref="Playfield"/>.
        /// </summary>
        /// <param name="hitObject"></param>
        /// <returns>Whether the <see cref="HitObject"/> was successfully removed.</returns>
        public virtual bool Remove(HitObject hitObject)
        {
            if (entryManager.TryGet(hitObject, out var entry))
            {
                entryManager.Remove(entry);
                return true;
            }

            return nestedPlayfields.Any(p => p.Remove(hitObject));
        }

        private void onEntryAdded(HitObjectLifetimeEntry entry, [CanBeNull] HitObject parentHitObject)
        {
            if (parentHitObject != null) return;

            HitObjectContainer.Add(entry);
            OnHitObjectAdded(entry.HitObject);
        }

        private void onEntryRemoved(HitObjectLifetimeEntry entry, [CanBeNull] HitObject parentHitObject)
        {
            if (presentationPolicy?.UsesExternalResults == true)
                untrackJudgedEntry(entry);

            if (parentHitObject != null) return;

            HitObjectContainer.Remove(entry);
            OnHitObjectRemoved(entry.HitObject);
        }

        /// <summary>
        /// Creates the <see cref="HitObjectLifetimeEntry"/> for a given <see cref="HitObject"/>.
        /// </summary>
        /// <remarks>
        /// This may be overridden to provide custom lifetime control (e.g. via <see cref="HitObjectLifetimeEntry.InitialLifetimeOffset"/>.
        /// </remarks>
        /// <param name="hitObject">The <see cref="HitObject"/> to create the entry for.</param>
        /// <returns>The <see cref="HitObjectLifetimeEntry"/>.</returns>
        [NotNull]
        protected virtual HitObjectLifetimeEntry CreateLifetimeEntry([NotNull] HitObject hitObject) => new HitObjectLifetimeEntry(hitObject);

        /// <summary>
        /// Registers a default <see cref="DrawableHitObject"/> pool with this playfield which is to be used whenever
        /// <see cref="DrawableHitObject"/> representations are requested for the given <typeparamref name="TObject"/> type.
        /// </summary>
        /// <param name="initialSize">The number of <see cref="DrawableHitObject"/>s to be initially stored in the pool.</param>
        /// <param name="maximumSize">
        /// The maximum number of <see cref="DrawableHitObject"/>s that can be stored in the pool.
        /// If this limit is exceeded, every subsequent <see cref="DrawableHitObject"/> will be created anew instead of being retrieved from the pool,
        /// until some of the existing <see cref="DrawableHitObject"/>s are returned to the pool.
        /// </param>
        /// <typeparam name="TObject">The <see cref="HitObject"/> type.</typeparam>
        /// <typeparam name="TDrawable">The <see cref="DrawableHitObject"/> receiver for <typeparamref name="TObject"/>s.</typeparam>
        public void RegisterPool<TObject, TDrawable>(int initialSize, int? maximumSize = null)
            where TObject : HitObject
            where TDrawable : DrawableHitObject, new()
            => RegisterPool<TObject, TDrawable>(new DrawablePool<TDrawable>(initialSize, maximumSize));

        /// <summary>
        /// Registers a custom <see cref="DrawableHitObject"/> pool with this playfield which is to be used whenever
        /// <see cref="DrawableHitObject"/> representations are requested for the given <typeparamref name="TObject"/> type.
        /// </summary>
        /// <param name="pool">The <see cref="DrawablePool{T}"/> to register.</param>
        /// <typeparam name="TObject">The <see cref="HitObject"/> type.</typeparam>
        /// <typeparam name="TDrawable">The <see cref="DrawableHitObject"/> receiver for <typeparamref name="TObject"/>s.</typeparam>
        protected void RegisterPool<TObject, TDrawable>([NotNull] DrawablePool<TDrawable> pool)
            where TObject : HitObject
            where TDrawable : DrawableHitObject, new()
        {
            pools[typeof(TObject)] = pool;
            AddInternal(pool);
        }

        DrawableHitObject IPooledHitObjectProvider.GetPooledDrawableRepresentation(HitObject hitObject, DrawableHitObject parent)
        {
            var pool = prepareDrawableHitObjectPool(hitObject);

            return (DrawableHitObject)pool?.Get(d =>
            {
                var dho = (DrawableHitObject)d;

                if (!dho.IsInitialized)
                    onNewDrawableHitObject(dho);

                if (!entryManager.TryGet(hitObject, out var entry))
                {
                    entry = CreateLifetimeEntry(hitObject);
                    entryManager.Add(entry, parent?.HitObject);
                }

                dho.ParentHitObject = parent;
                dho.Apply(entry);
            });
        }

        private IDrawablePool prepareDrawableHitObjectPool(HitObject hitObject)
        {
            var lookupType = hitObject.GetType();

            IDrawablePool pool;

            // Tests may add derived hitobject instances for which pools don't exist. Try to find any applicable pool and dynamically assign the type if the pool exists.
            if (!pools.TryGetValue(lookupType, out pool))
            {
                foreach (var (t, p) in pools)
                {
                    if (!t.IsInstanceOfType(hitObject))
                        continue;

                    pools[lookupType] = pool = p;
                    break;
                }
            }

            return pool;
        }

        #endregion

        private void onNewResult(DrawableHitObject drawable, JudgementResult result)
        {
            Debug.Assert(result != null && drawable.Entry?.Result == result && result.RawTime != null);
            HitObjectLifetimeEntry entry = drawable.Entry.AsNonNull();

            if (presentationPolicy?.UsesExternalResults != true)
                judgedEntries.Add(entry);
            else
                trackExternallyJudgedEntry(entry);

            NewResult?.Invoke(drawable, result);
        }

        private void trackExternallyJudgedEntry(HitObjectLifetimeEntry entry)
        {
            Debug.Assert(entry.Result?.RawTime != null);

            if (judgedEntries.Contains(entry))
                return;

            // Nested externally-resulted drawables are traversed by hierarchy; rewind follows exact chronology.
            insertJudgedEntryChronologically(entry);
            entry.Result.RawTimeChanged += onResultRawTimeChanged;
        }

        private void insertJudgedEntryChronologically(HitObjectLifetimeEntry entry)
        {
            var result = entry.Result;
            Debug.Assert(result?.RawTime != null);

            if (judgedEntries.Count == 0
                || judgedEntries[judgedEntries.Count - 1].Result.RawTime.Value <= result.RawTime.Value)
            {
                judgedEntries.Add(entry);
                return;
            }

            int lower = 0;
            int upper = judgedEntries.Count;

            while (lower < upper)
            {
                int middle = lower + (upper - lower) / 2;

                if (judgedEntries[middle].Result.RawTime.Value <= result.RawTime.Value)
                    lower = middle + 1;
                else
                    upper = middle;
            }

            judgedEntries.Insert(lower, entry);
        }

        private void onResultRawTimeChanged(JudgementResult result)
        {
            int index = judgedEntries.FindIndex(entry => ReferenceEquals(entry.Result, result));

            if (index < 0)
                return;

            HitObjectLifetimeEntry entry = judgedEntries[index];
            judgedEntries.RemoveAt(index);

            if (result.RawTime == null)
            {
                result.RawTimeChanged -= onResultRawTimeChanged;
                return;
            }

            insertJudgedEntryChronologically(entry);
        }

        private void untrackJudgedEntry(HitObjectLifetimeEntry entry)
        {
            int index = judgedEntries.IndexOf(entry);

            if (index >= 0)
                removeJudgedEntryAt(index);
        }

        private HitObjectLifetimeEntry removeJudgedEntryAt(int index)
        {
            HitObjectLifetimeEntry entry = judgedEntries[index];
            judgedEntries.RemoveAt(index);
            entry.Result.RawTimeChanged -= onResultRawTimeChanged;
            return entry;
        }

        internal void UntrackJudgedResult(DrawableHitObject drawable)
        {
            if (drawable.Entry == null)
                return;

            untrackJudgedEntryTree(drawable.Entry);

            foreach (Playfield nestedPlayfield in nestedPlayfields)
                nestedPlayfield.UntrackJudgedResult(drawable);
        }

        internal void TrackJudgedResult(DrawableHitObject drawable)
        {
            if (drawable.Entry == null)
                return;

            findOwningPlayfield(drawable)?.trackJudgedEntryTree(drawable.Entry);
        }

        private Playfield findOwningPlayfield(DrawableHitObject drawable)
        {
            if (HitObjectContainer.Objects.Contains(drawable))
                return this;

            foreach (Playfield nestedPlayfield in nestedPlayfields)
            {
                Playfield owner = nestedPlayfield.findOwningPlayfield(drawable);

                if (owner != null)
                    return owner;
            }

            return null;
        }

        private void trackJudgedEntryTree(HitObjectLifetimeEntry entry)
        {
            if (entry.Judged)
                trackExternallyJudgedEntry(entry);

            foreach (HitObjectLifetimeEntry nestedEntry in entry.NestedEntries)
                trackJudgedEntryTree(nestedEntry);
        }

        private void untrackJudgedEntryTree(HitObjectLifetimeEntry entry)
        {
            untrackJudgedEntry(entry);

            foreach (HitObjectLifetimeEntry nestedEntry in entry.NestedEntries)
                untrackJudgedEntryTree(nestedEntry);
        }

        private void revertResult(HitObjectLifetimeEntry entry)
        {
            var result = entry.Result;
            Debug.Assert(result != null);

            RevertResult?.Invoke(result);
            entry.OnRevertResult();

            result.Reset();
        }

        protected override void Dispose(bool isDisposing)
        {
            while (judgedEntries.Count > 0)
                removeJudgedEntryAt(judgedEntries.Count - 1);

            base.Dispose(isDisposing);
        }

        #region Editor logic

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> becomes used by a <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// If this <see cref="HitObjectContainer"/> uses pooled objects, this represents the time when the <see cref="HitObject"/>s become alive.
        /// </remarks>
        internal event Action<HitObject> HitObjectUsageBegan;

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> becomes unused by a <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// If this <see cref="HitObjectContainer"/> uses pooled objects, this represents the time when the <see cref="HitObject"/>s become dead.
        /// </remarks>
        internal event Action<HitObject> HitObjectUsageFinished;

        /// <summary>
        /// Sets whether to keep a given <see cref="HitObject"/> always alive within this or any nested <see cref="Playfield"/>.
        /// </summary>
        /// <param name="hitObject">The <see cref="HitObject"/> to set.</param>
        /// <param name="keepAlive">Whether to keep <paramref name="hitObject"/> always alive.</param>
        internal void SetKeepAlive(HitObject hitObject, bool keepAlive)
        {
            if (entryManager.TryGet(hitObject, out var entry))
            {
                entry.KeepAlive = keepAlive;
                return;
            }

            foreach (var p in nestedPlayfields)
                p.SetKeepAlive(hitObject, keepAlive);
        }

        /// <summary>
        /// Keeps all <see cref="HitObject"/>s alive within this and all nested <see cref="Playfield"/>s.
        /// </summary>
        internal void KeepAllAlive()
        {
            foreach (var entry in entryManager.AllEntries)
                entry.KeepAlive = true;

            foreach (var p in nestedPlayfields)
                p.KeepAllAlive();
        }

        /// <summary>
        /// The amount of time prior to the current time within which <see cref="HitObject"/>s should be considered alive.
        /// </summary>
        internal double PastLifetimeExtension
        {
            get => HitObjectContainer.PastLifetimeExtension;
            set
            {
                HitObjectContainer.PastLifetimeExtension = value;

                foreach (var nested in nestedPlayfields)
                    nested.PastLifetimeExtension = value;
            }
        }

        /// <summary>
        /// The amount of time after the current time within which <see cref="HitObject"/>s should be considered alive.
        /// </summary>
        internal double FutureLifetimeExtension
        {
            get => HitObjectContainer.FutureLifetimeExtension;
            set
            {
                HitObjectContainer.FutureLifetimeExtension = value;

                foreach (var nested in nestedPlayfields)
                    nested.FutureLifetimeExtension = value;
            }
        }

        #endregion
    }
}
