// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/ComposeBlueprintContainer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: namespace Garbus.Game.Edit.Compose; HitObject → GarbusHitObject;
// EditorBeatmap → EditorChart; DrawableRulesetDependenciesProvidingContainer removed (no
// DrawableRuleset in Garbus editor); EditorScreenWithTimeline dependency removed (Garbus manages
// its own layout); ALL sample/bank/new-combo ternary state stripped (HitSampleInfo, IHasComboInformation,
// SampleBankTernaryButton, DrawableTernaryButton, NewComboTernaryButton, Humanizer — none present
// in Garbus); MainTernaryStates / SampleBankTernaryStates / NewCombo / CreateTernaryButtons() removed;
// RightClickAlwaysQuickDeletes wired via EditorSelectionHandler still set on tool change;
// ApplySelectionOrder sorts by distance to editor clock time; placement lifecycle fully preserved:
// CurrentPlacement, refreshPlacement, ensurePlacementCreated, CommitIfPlacementActive;
// hitObjectAdded callback refreshes placement; Beatmap.SnapTime → EditorClock.CurrentTime (Garbus
// has no beat-snap provider yet; Task 17 may improve); paste/duplicate hooks stripped cleanly.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A blueprint container generally displayed as an overlay to a ruleset's playfield.
    /// </summary>
    public abstract partial class ComposeBlueprintContainer : EditorBlueprintContainer
    {
        private readonly Container<PlacementBlueprint> placementBlueprintContainer;

        protected new EditorSelectionHandler SelectionHandler => (EditorSelectionHandler)base.SelectionHandler;

        public PlacementBlueprint? CurrentPlacement { get; private set; }

        /// <summary>
        /// Positional input must be received outside the container's bounds,
        /// in order to handle composer blueprints which are partially offscreen.
        /// </summary>
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        protected override IEnumerable<SelectionBlueprint<GarbusHitObject>> ApplySelectionOrder(IEnumerable<SelectionBlueprint<GarbusHitObject>> blueprints) =>
            base.ApplySelectionOrder(blueprints)
                .OrderBy(b => Math.Min(Math.Abs(EditorClock.CurrentTime - b.Item.GetEndTime()), Math.Abs(EditorClock.CurrentTime - b.Item.StartTime)));

        protected ComposeBlueprintContainer(HitObjectComposer? composer)
            : base(composer)
        {
            placementBlueprintContainer = new Container<PlacementBlueprint>
            {
                RelativeSizeAxes = Axes.Both
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(placementBlueprintContainer);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            EditorChart.HitObjectAdded += hitObjectAdded;
        }

        protected override void TransferBlueprintFor(GarbusHitObject hitObject, DrawableHitObject drawableObject)
        {
            base.TransferBlueprintFor(hitObject, drawableObject);

            var blueprint = (HitObjectSelectionBlueprint)GetBlueprintFor(hitObject);
            blueprint.DrawableObject = drawableObject;
        }

        #region Placement

        /// <summary>
        /// Refreshes the current placement tool.
        /// </summary>
        private void refreshPlacement()
        {
            CurrentPlacement?.EndPlacement(false);
            CurrentPlacement?.Expire();
            CurrentPlacement = null;

            ensurePlacementCreated();
        }

        private void updatePlacementTimeAndPosition()
        {
            CurrentPlacement!.UpdateTimeAndPosition(InputManager.CurrentState.Mouse.Position, EditorClock.CurrentTime);
        }

        #endregion

        protected override void Update()
        {
            base.Update();

            if (CurrentPlacement != null)
            {
                switch (CurrentPlacement.PlacementActive)
                {
                    case PlacementBlueprint.PlacementState.Waiting:
                        if (Composer == null || !Composer.CursorInPlacementArea)
                            CurrentPlacement.Hide();
                        else
                            CurrentPlacement.Show();

                        break;

                    case PlacementBlueprint.PlacementState.Active:
                        CurrentPlacement.Show();
                        break;

                    case PlacementBlueprint.PlacementState.Finished:
                        refreshPlacement();
                        break;
                }

                // updates the placement with the latest editor clock time.
                updatePlacementTimeAndPosition();
            }
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            // updates the placement with the latest mouse position.
            if (CurrentPlacement != null)
                updatePlacementTimeAndPosition();

            return base.OnMouseMove(e);
        }

        protected sealed override SelectionBlueprint<GarbusHitObject>? CreateBlueprintFor(GarbusHitObject item)
        {
            DrawableHitObject? drawable = null;

            if (Composer != null)
                drawable = Composer.HitObjects.FirstOrDefault(d => d.HitObject == item);

            if (drawable == null)
                return null;

            return CreateHitObjectBlueprintFor(item)?.With(b => b.DrawableObject = drawable);
        }

        public virtual HitObjectSelectionBlueprint? CreateHitObjectBlueprintFor(GarbusHitObject hitObject) => null;

        private void hitObjectAdded(GarbusHitObject obj)
        {
            // refresh the tool to handle the case of placement completing.
            refreshPlacement();
        }

        private void ensurePlacementCreated()
        {
            if (CurrentPlacement != null) return;

            var blueprint = CurrentTool?.CreatePlacementBlueprint();

            if (blueprint != null)
            {
                placementBlueprintContainer.Child = CurrentPlacement = blueprint;

                // Fixes a 1-frame position discrepancy due to the first mouse move event happening in the next frame
                updatePlacementTimeAndPosition();
            }
        }

        public void CommitIfPlacementActive()
        {
            CurrentPlacement?.EndPlacement(CurrentPlacement.PlacementActive == PlacementBlueprint.PlacementState.Active);
            refreshPlacement();
        }

        private CompositionTool? currentTool;

        /// <summary>
        /// The current placement tool.
        /// </summary>
        public CompositionTool? CurrentTool
        {
            get => currentTool;

            set
            {
                if (currentTool == value)
                    return;

                currentTool = value;

                SelectionHandler.RightClickAlwaysQuickDeletes = currentTool is not SelectTool;

                // As per stable editor, when changing tools, we should forcefully commit any pending placement.
                CommitIfPlacementActive();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            EditorChart.HitObjectAdded -= hitObjectAdded;
        }
    }
}
