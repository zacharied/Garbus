// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/HitObjectPlacementBlueprint.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: IPlacementHandler indirection replaced with direct EditorChart calls (osu's
// handler ultimately routes to the same operations via ComposeBlueprintContainer — keeping the shape
// without the extra interface is fine here); HitObject field became a read-only property (osu:
// `public readonly HitObject HitObject` → Garbus: `public GarbusHitObject HitObject { get; }`);
// EndPlacement else-branch (osu's `placementHandler.HidePlacement()`) removed; disposal is now
// entirely the blueprint container's responsibility (disposes on PlacementState.Finished regardless
// of commit); sample bank/combo auto-assignment stripped (Garbus has no per-chart banks or combos);
// ApplyDefaults takes no arguments (Garbus hit windows are fixed, no ControlPointInfo/Difficulty
// needed); auto-seek on placement uses a plain Bindable<bool> defaulting true — Task 17 wires it to
// the GarbusHitObjectComposer.AutoSeekOnPlacement DI-cached bindable so the View menu config toggle
// propagates; namespace Garbus.Game.Edit.Compose.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using Garbus.Game.Objects;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A blueprint which governs the creation of a new <see cref="GarbusHitObject"/> through to
    /// its placement in the chart.
    /// </summary>
    public abstract partial class HitObjectPlacementBlueprint : PlacementBlueprint
    {
        /// <summary>
        /// The <see cref="GarbusHitObject"/> that is being placed.
        /// </summary>
        public GarbusHitObject HitObject { get; }

        /// <summary>
        /// Whether the editor should automatically seek to the placement time after committing.
        /// Defaults to <c>true</c>; bound to <see cref="GarbusHitObjectComposer.AutoSeekOnPlacement"/>
        /// via DI when the composer is in the hierarchy.
        /// </summary>
        protected readonly Bindable<bool> AutoSeekOnPlacement = new Bindable<bool>(true);

        [Resolved(CanBeNull = true)]
        private GarbusHitObjectComposer? composer { get; set; }

        [Resolved]
        protected EditorClock EditorClock { get; private set; } = null!;

        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler? changeHandler { get; set; }

        /// <summary>
        /// Acceptable leniency to account for rounding errors and minor unsnaps.
        /// </summary>
        private const double placement_replace_start_time_leniency_ms = 2;

        protected override bool IsValidForPlacement
        {
            get
            {
                // Reject negative times outright — the compose hit zone now extends below the
                // judgement line into already-past time, so near the track start the cursor can map to
                // a negative StartTime. Objects before time zero are never valid (see the Verify tab's
                // CheckObjectsBeforeTimeZero).
                if (HitObject.StartTime < 0)
                    return false;

                var firstTimingPoint = editorChart.ControlPointInfo.TimingPoints.FirstOrDefault();
                return firstTimingPoint == null || HitObject.StartTime >= firstTimingPoint.Time;
            }
        }

        protected HitObjectPlacementBlueprint(GarbusHitObject hitObject)
        {
            HitObject = hitObject;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Bind to the composer's AutoSeekOnPlacement when available (DI-resolved above).
            // This wires the View menu config toggle through to each placement blueprint instance.
            if (composer != null)
                AutoSeekOnPlacement.BindTo(composer.AutoSeekOnPlacement);
        }

        /// <summary>
        /// Re-applies the hit object's defaults, regenerating nested objects and derived geometry.
        /// Osu's overload takes the beatmap's ControlPointInfo + Difficulty; Garbus hit windows are fixed,
        /// so <see cref="Gameplay.Objects.HitObject.ApplyDefaults"/> takes no such arguments.
        /// </summary>
        protected void ApplyDefaultsToHitObject() => HitObject.ApplyDefaults();

        protected override void Update()
        {
            base.Update();

            Colour = IsValidForPlacement ? Colour4.White : Colour4.Red;
        }

        /// <summary>
        /// Signals that the placement has finished. Commits the hit object to the chart if valid.
        /// </summary>
        public override void EndPlacement(bool commit)
        {
            base.EndPlacement(commit);

            if (IsValidForPlacement && commit)
            {
                changeHandler?.BeginChange();

                // Remove any existing objects this placement replaces.
                var toRemove = editorChart.HitObjects.Where(h => ReplacesExistingObject(h)).ToArray();
                foreach (var h in toRemove)
                    editorChart.Remove(h);

                editorChart.Add(HitObject);

                changeHandler?.EndChange();

                if (AutoSeekOnPlacement.Value)
                    EditorClock.SeekSmoothlyTo(HitObject.StartTime);
            }
        }

        /// <summary>
        /// Updates the time and position of this <see cref="HitObjectPlacementBlueprint"/>.
        /// Writes <c>StartTime</c> onto the hit object while in the Waiting state.
        /// </summary>
        public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
        {
            if (PlacementActive == PlacementState.Waiting)
                HitObject.StartTime = fallbackTime;

            return new SnapResult(screenSpacePosition, fallbackTime);
        }

        /// <summary>
        /// Whether an existing <see cref="GarbusHitObject"/> should be removed because
        /// <see cref="HitObject"/> is being placed on top of it.
        /// </summary>
        /// <remarks>
        /// By default, it matches when start times are within ±<see cref="placement_replace_start_time_leniency_ms"/> ms.
        /// </remarks>
        public virtual bool ReplacesExistingObject(GarbusHitObject existing)
            => Precision.AlmostEquals(existing.StartTime, HitObject.StartTime, placement_replace_start_time_leniency_ms);

    }
}
