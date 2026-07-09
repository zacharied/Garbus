// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/EditorBlueprintContainer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; HitObject → GarbusHitObject;
// EditorBeatmap → EditorChart; DrawableHitObject from Garbus.Game.Gameplay.Objects.Drawables;
// Playfield from Garbus.Game.Gameplay.UI; HitObjectUsageEventBuffer removed (Garbus editor does
// not pool drawables at editor time — blueprints are added via HitObjectAdded/Removed events only);
// TransferBlueprintFor kept as protected virtual stub (ComposeBlueprintContainer overrides);
// IBarLine filter removed (no bar-line concept in Garbus); nullable enabled.

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;

namespace Garbus.Game.Edit.Compose
{
    public abstract partial class EditorBlueprintContainer : BlueprintContainer<GarbusHitObject>
    {
        [Resolved]
        protected EditorClock EditorClock { get; private set; } = null!;

        [Resolved]
        protected EditorChart EditorChart { get; private set; } = null!;

        protected readonly HitObjectComposer? Composer;

        protected InputManager InputManager { get; private set; } = null!;

        protected EditorBlueprintContainer(HitObjectComposer? composer)
        {
            Composer = composer;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            SelectedItems.BindTo(EditorChart.SelectedHitObjects);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            InputManager = GetContainingInputManager();

            EditorChart.HitObjectAdded += AddBlueprintFor;
            EditorChart.HitObjectRemoved += RemoveBlueprintFor;
            EditorChart.SelectedHitObjects.CollectionChanged += updateSelectionLifetime;

            if (Composer != null)
            {
                foreach (var obj in Composer.HitObjects)
                {
                    if (obj.HitObject is GarbusHitObject garbusObj)
                        AddBlueprintFor(garbusObj);
                }
            }
        }

        protected override IEnumerable<SelectionBlueprint<GarbusHitObject>> SortForMovement(IReadOnlyList<SelectionBlueprint<GarbusHitObject>> blueprints)
            => blueprints.OrderBy(b => b.Item.StartTime);

        protected void ApplySnapResultTime(SnapResult result, double referenceTime)
        {
            if (!result.Time.HasValue)
                return;

            // Apply the start time at the newly snapped-to position
            double offset = result.Time.Value - referenceTime;

            if (offset != 0)
                EditorChart.PerformOnSelection(obj => obj.StartTime += offset);
        }

        /// <summary>
        /// Invoked when a <see cref="GarbusHitObject"/> has been transferred to another <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <param name="hitObject">The hit object which has been assigned to a new drawable.</param>
        /// <param name="drawableObject">The new drawable that is representing the hit object.</param>
        protected virtual void TransferBlueprintFor(GarbusHitObject hitObject, DrawableHitObject drawableObject)
        {
        }

        protected override void DragOperationCompleted()
        {
            base.DragOperationCompleted();

            // handle positional change etc.
            foreach (var blueprint in SelectionBlueprints)
                EditorChart.Update(blueprint.Item);
        }

        protected override bool OnDoubleClick(DoubleClickEvent e)
        {
            if (!base.OnDoubleClick(e))
                return false;

            if (ClickedBlueprint != null)
                EditorClock.SeekSmoothlyTo(ClickedBlueprint.Item.StartTime);
            return true;
        }

        protected override SelectionBlueprintContainer CreateSelectionBlueprintContainer() => new HitObjectOrderedSelectionContainer { RelativeSizeAxes = Axes.Both };

        protected override SelectionHandler<GarbusHitObject> CreateSelectionHandler() => new EditorSelectionHandler();

        protected override void SelectAll()
        {
            if (Composer != null)
                Composer.Playfield.KeepAllAlive();

            SelectedItems.AddRange(EditorChart.HitObjects.Except(SelectedItems).ToArray());
        }

        /// <summary>
        /// Ensures that newly-selected hit objects are kept alive
        /// and drops that keep-alive from newly-deselected objects.
        /// </summary>
        private void updateSelectionLifetime(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (Composer == null) return;

            if (e.NewItems != null)
            {
                foreach (GarbusHitObject newSelection in e.NewItems)
                    Composer.Playfield.SetKeepAlive(newSelection, true);
            }

            if (e.OldItems != null)
            {
                foreach (GarbusHitObject oldSelection in e.OldItems)
                    Composer.Playfield.SetKeepAlive(oldSelection, false);
            }
        }

        protected override void OnBlueprintSelected(SelectionBlueprint<GarbusHitObject> blueprint)
        {
            base.OnBlueprintSelected(blueprint);

            Composer?.Playfield.SetKeepAlive(blueprint.Item, true);
        }

        protected override void OnBlueprintDeselected(SelectionBlueprint<GarbusHitObject> blueprint)
        {
            base.OnBlueprintDeselected(blueprint);

            Composer?.Playfield.SetKeepAlive(blueprint.Item, false);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            EditorChart.HitObjectAdded -= AddBlueprintFor;
            EditorChart.HitObjectRemoved -= RemoveBlueprintFor;
            EditorChart.SelectedHitObjects.CollectionChanged -= updateSelectionLifetime;
        }
    }
}
