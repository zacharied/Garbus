// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/HitObjectComposer.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus (the biggest structural deviation from osu): Garbus has NO DrawableRuleset, so this
// composer hosts the playfield directly rather than through a DrawableEditorRulesetWrapper. The osu file
// splits into a generic HitObjectComposer<TObject> (ruleset/DrawableRuleset-bound) and a non-generic
// HitObjectComposer base; here the base carries the whole surface and the ruleset/config/ternary/sample
// machinery is dropped. Ruleset, IBeatSnapProvider, OverlayColourProvider, ExpandableSpriteText, sample
// banks, toggles (Q~P), composer-focus fade, and IPlacementHandler are all removed. The tool radio
// collection, number-key selection, LeftToolbox/RightToolbox columns, blueprint overlay, snapping, and
// beat-snap-grid update remain.
//
// CurrentTool/ActiveTool reconciliation: Task 12's stub exposed an abstract `CurrentTool` on the composer
// that NO consumer actually reads (the blueprint containers read HitObjects/Playfield/CursorInPlacementArea,
// and hold their OWN settable ComposeBlueprintContainer.CurrentTool). This composer replaces that unused
// abstract with the brief's `ActiveTool` bindable (the source of truth), and exposes a `CurrentTool`
// convenience getter (= ActiveTool.Value) for callers. Tool selection writes ActiveTool AND pushes into
// BlueprintContainer.CurrentTool, which is what actually drives placement.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Timing;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI;
using Garbus.Game.Gameplay.UI.Scrolling;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// Top level container for editor compose mode. Hosts the playfield directly (no DrawableRuleset),
    /// overlays the blueprint container, provides snapping, and glues the toolbox radio buttons together.
    /// </summary>
    [Cached]
    public abstract partial class HitObjectComposer : CompositeDrawable
    {
        public const float TOOLBOX_WIDTH_LEFT = 200;
        public const float TOOLBOX_WIDTH_RIGHT = 200;

        [Resolved]
        protected EditorChart EditorChart { get; private set; } = null!;

        [Resolved]
        protected EditorClock EditorClock { get; private set; } = null!;

        /// <summary>
        /// The composer's playfield. Concrete subclasses create it (see <see cref="ScrollingHitObjectComposer{T}"/>).
        /// </summary>
        public abstract Playfield Playfield { get; }

        /// <summary>
        /// All currently-displayed <see cref="DrawableHitObject"/>s in the playfield.
        /// </summary>
        public abstract IEnumerable<DrawableHitObject> HitObjects { get; }

        /// <summary>
        /// Whether the user's cursor is currently in an area valid for placement.
        /// </summary>
        public abstract bool CursorInPlacementArea { get; }

        /// <summary>
        /// The currently-active composition tool. Source of truth for tool selection.
        /// </summary>
        public readonly Bindable<CompositionTool?> ActiveTool = new Bindable<CompositionTool?>();

        /// <summary>Convenience accessor for <see cref="ActiveTool"/>'s value.</summary>
        public CompositionTool? CurrentTool => ActiveTool.Value;

        /// <summary>
        /// Defines all available composition tools, listed on the left side of the editor screen.
        /// A "select" tool is automatically prepended as the first tool.
        /// </summary>
        protected abstract IReadOnlyList<CompositionTool> CompositionTools { get; }

        /// <summary>Construct the blueprint container managing selection/placement input and display.</summary>
        protected abstract ComposeBlueprintContainer CreateBlueprintContainer();

        /// <summary>Construct an optional beat snap grid.</summary>
        protected virtual BeatSnapGrid? CreateBeatSnapGrid() => null;

        /// <summary>The blueprint container overlaying the playfield.</summary>
        public ComposeBlueprintContainer BlueprintContainer => blueprintContainer;
        private ComposeBlueprintContainer blueprintContainer = null!;

        /// <summary>The left-hand toolbox column (tools).</summary>
        protected ExpandingToolboxContainer LeftToolbox { get; private set; } = null!;

        /// <summary>The right-hand toolbox column (inspector / ruleset-specific groups).</summary>
        protected ExpandingToolboxContainer RightToolbox { get; private set; } = null!;

        /// <summary>Houses the playfield and its blueprint overlay.</summary>
        protected Container PlayfieldContentContainer { get; private set; } = null!;

        /// <summary>Playfield-scoped overlays drawn after the blueprint content.</summary>
        protected Container PlayfieldOverlayContainer { get; private set; } = null!;

        protected InputManager InputManager { get; private set; } = null!;

        private EditorRadioButtonCollection toolboxCollection = null!;

        protected HitObjectComposer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var inspectorScrollClock = new FramedClock();
            var inspectorContentClock = new FramedClock();

            InternalChildren = new Drawable[]
            {
                // Offset the content region between the toolboxes via X + Width rather than Padding.
                // Padding would shift the blueprint container WITHIN this container's coordinate space,
                // but the framework computes mouse-event positions in Target.Parent space
                // (UIEvent.ToLocalSpace → Target.Parent.ToLocalSpace). The DragBox positions its box in the
                // blueprint container's own (child) space, so any padding here double-counts as an offset —
                // the drag-select box (and its selection quad) would be shifted right by TOOLBOX_WIDTH_LEFT.
                // osu avoids this the same way (HitObjectComposer sets PlayfieldContentContainer.X, never pads it).
                PlayfieldContentContainer = new Container
                {
                    Name = "Playfield content",
                    RelativeSizeAxes = Axes.Y,
                    X = TOOLBOX_WIDTH_LEFT,
                    // Clip the playfield and its blueprint overlay to the content region between the toolboxes.
                    // The blueprint container returns positional input over the WHOLE screen
                    // (ComposeBlueprintContainer.ReceivePositionalInputAt => true) so partially-offscreen
                    // blueprints stay selectable; the Garbus polar→x mapping lets an object's blueprint (e.g. a
                    // slider wrapping past 360°) spill past the playfield edges into the toolbox / inspector
                    // columns. Masking here gates the positional-input recursion (ReceivePositionalInputAtSubTree
                    // = !Masking || ReceivePositionalInputAt) at the content bounds, so the spilled outline can't
                    // steal clicks over the toolboxes. All legitimately-visible content (incl. the ghost wrap
                    // bands) maps to x within [0,1] of this region, so nothing intended is clipped.
                    Masking = true,
                    Children = new Drawable[]
                    {
                        Playfield,
                        blueprintContainer = CreateBlueprintContainer(),
                        PlayfieldOverlayContainer = new Container
                        {
                            Name = "Mini preview overlay",
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                },
                new Container
                {
                    Name = "Left toolbox",
                    RelativeSizeAxes = Axes.Y,
                    Width = TOOLBOX_WIDTH_LEFT,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Colour4(20, 20, 26, 255),
                        },
                        LeftToolbox = new ExpandingToolboxContainer(TOOLBOX_WIDTH_LEFT)
                        {
                            Child = new EditorToolboxGroup("toolbox (1-9)")
                            {
                                Child = toolboxCollection = new EditorRadioButtonCollection { RelativeSizeAxes = Axes.X },
                            },
                        },
                    },
                },
                new Container
                {
                    Name = "Right toolbox",
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Y,
                    Width = TOOLBOX_WIDTH_RIGHT,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Colour4(20, 20, 26, 255),
                        },
                        new BasicScrollContainer
                        {
                            Name = "Right toolbox scroll",
                            RelativeSizeAxes = Axes.Both,
                            ScrollbarOverlapsContent = false,
                            // Keep all inspector and menu scrolling/transforms on UI time. The playfield remains
                            // on the composer's editor clock through its separate content branch.
                            Clock = inspectorScrollClock,
                            Child = RightToolbox = new ExpandingToolboxContainer(TOOLBOX_WIDTH_RIGHT, scrollContent: true)
                            {
                                Clock = inspectorContentClock,
                            },
                        },
                    },
                },
            };

            var beatSnapGrid = CreateBeatSnapGrid();
            if (beatSnapGrid != null)
                AddInternal(beatSnapGrid);

            toolboxCollection.Items = CompositionTools.Prepend(new SelectTool())
                                                      .Select(t => new HitObjectCompositionToolButton(t, () => toolSelected(t)))
                                                      .ToList();

            setSelectTool();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            InputManager = GetContainingInputManager()!;

            EditorChart.SelectedHitObjects.CollectionChanged += onSelectedHitObjectsChanged;
        }

        protected override void Update()
        {
            base.Update();

            // Size the content region to sit between the fixed-width toolboxes (see the layout note above:
            // this replaces Padding, which would desync drag-select coordinates).
            PlayfieldContentContainer.Width = Math.Max(0, DrawWidth - TOOLBOX_WIDTH_LEFT - TOOLBOX_WIDTH_RIGHT);
        }

        private void onSelectedHitObjectsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Ensure in selection mode whenever a selection is made.
            if (EditorChart.SelectedHitObjects.Any())
                setSelectTool();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            EditorChart.SelectedHitObjects.CollectionChanged -= onSelectedHitObjectsChanged;
        }

        #region Tool selection

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.ControlPressed || e.SuperPressed)
                return false;

            if (checkToolboxMappingFromKey(e.Key, out int index))
            {
                var item = toolboxCollection.Items.ElementAtOrDefault(index);

                if (item != null)
                {
                    if (!item.Selected.Disabled)
                        item.Select();
                    return true;
                }
            }

            return base.OnKeyDown(e);
        }

        private static bool checkToolboxMappingFromKey(Key key, out int index)
        {
            if (key < Key.Number1 || key > Key.Number9)
            {
                index = -1;
                return false;
            }

            index = key - Key.Number1;
            return true;
        }

        private void setSelectTool() => toolboxCollection.Items.First().Select();

        private void toolSelected(CompositionTool tool)
        {
            ActiveTool.Value = tool;
            blueprintContainer.CurrentTool = tool;

            if (tool is not SelectTool)
                EditorChart.SelectedHitObjects.Clear();
        }

        #endregion

        #region Snapping

        /// <summary>
        /// Snaps a screen-space position to the nearest beat divisor time, mapping through the scrolling
        /// playfield: y → time via <see cref="ScrollingPlayfield.TimeAtScreenSpacePosition"/>, snapped via
        /// <see cref="Charts.Timing.ControlPointInfo.GetClosestSnappedTime(double,int,double?)"/>, then back
        /// to screen space via <see cref="ScrollingPlayfield.ScreenSpacePositionAtTime"/>. This reproduces
        /// the scrolling snap BAC's FindSnappedAngleTimeAndPosition builds on (including the recentre-x quirk
        /// it works around: ScreenSpacePositionAtTime returns the container's horizontal centre).
        /// </summary>
        public virtual SnapResult FindSnappedPositionAndTime(Vector2 screenSpacePosition)
        {
            if (Playfield is not ScrollingPlayfield scrollingPlayfield)
                return new SnapResult(screenSpacePosition, null, Playfield);

            double targetTime = scrollingPlayfield.TimeAtScreenSpacePosition(screenSpacePosition);

            targetTime = EditorChart.ControlPointInfo.GetClosestSnappedTime(targetTime, beatDivisor.Value);

            screenSpacePosition = scrollingPlayfield.ScreenSpacePositionAtTime(targetTime);

            return new SnapResult(screenSpacePosition, targetTime, scrollingPlayfield);
        }

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        #endregion
    }
}
