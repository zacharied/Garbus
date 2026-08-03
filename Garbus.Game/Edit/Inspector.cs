// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/HitObjectInspector.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: EditorBeatmap → EditorChart; OsuTextFlowContainer/OsuFont/OverlayColourProvider dropped in
// favour of plain TextFlowContainer + hard-coded colours; adds multi-value-aware dropdowns that osu's plain-text
// inspector doesn't have — a Side dropdown when every selected object is IHasSide, a Direction dropdown when every
// selected object is a GarbusSlamEdge, and a SweepEasing dropdown plus a Smoothing checkbox when one or more slider
// control-point nodes are picked in a SliderSelectionBlueprint. Each shows "<multiple>" (a dash, for the checkbox)
// when the selection's values disagree and applies an edit to the whole selection as one undo step. Node selection
// isn't in EditorChart.SelectedHitObjects — polled via the composer's SelectionHandler alongside the 250ms rolling
// refresh. Unlike osu's text-only inspector this one builds real controls, so rebuilds are throttled: update events
// coalesce via Scheduler.AddOnce, and the controls block is reconstructed only when its rendered inputs
// (buildControlsSignature) change — the per-drag-event widget teardown was a GC storm otherwise.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Threading;
using osuTK;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Right-toolbox inspector: a text summary of the current selection plus editable Side/SweepEasing
    /// dropdowns for hit objects and slider control points that carry those properties.
    /// </summary>
    public partial class Inspector : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private GarbusHitObjectComposer composer { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IEditorChangeHandler? changeHandler { get; set; }

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        private FillFlowContainer flow = null!;
        private TextFlowContainer inspectorText = null!;
        private FillFlowContainer controlsFlow = null!;

        private ScheduledDelegate? rollingUpdate;

        // Tracked so a node-selection change (which isn't observable via events) triggers a rebuild.
        private readonly HashSet<GarbusPathControlPoint> lastNodeSelectionSnapshot = new HashSet<GarbusPathControlPoint>();

        // Tracked so a head-selection change (also not event-observable) triggers a rebuild.
        private readonly HashSet<SliderBody> lastHeadSelectionSnapshot = new HashSet<SliderBody>();

        public Inspector()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
                Children = new Drawable[]
                {
                    inspectorText = new TextFlowContainer(s =>
                    {
                        s.Font = FontUsage.Default.With(size: 12);
                        s.Colour = Colour4.White;
                    })
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                    // Front-first so an open dropdown menu pops over the control rows below it.
                    controlsFlow = new FrontFirstFillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 6),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // AddOnce: a drag updates every selected object every mouse-move event, so the raw events
            // arrive N-per-frame — coalesce to one rebuild per frame (same pattern as
            // EditorSelectionHandler's ternary-state refresh).
            editorChart.SelectedHitObjects.CollectionChanged += (_, _) => Scheduler.AddOnce(rebuild);
            editorChart.HitObjectUpdated += _ => Scheduler.AddOnce(rebuild);
            rebuild();
        }

        protected override void Update()
        {
            base.Update();

            // Node selection lives on SliderSelectionBlueprint (local HashSet, no event). Poll once per
            // frame — cheap set-equality — so picking/deselecting a node updates the dropdown immediately.
            var currentNodes = collectSelectedNodes();
            var currentHeads = collectHeadSelectedSliders();
            if (!currentNodes.SetEquals(lastNodeSelectionSnapshot) || !currentHeads.SetEquals(lastHeadSelectionSnapshot))
                rebuild();
        }

        private HashSet<GarbusPathControlPoint> collectSelectedNodes()
        {
            var set = new HashSet<GarbusPathControlPoint>();

            foreach (var blueprint in composer.BlueprintContainer.SelectionHandler.SelectedBlueprints)
            {
                if (blueprint is SliderSelectionBlueprint sliderBlueprint)
                {
                    foreach (var node in sliderBlueprint.SelectedNodes)
                        set.Add(node);
                }
            }

            return set;
        }

        private HashSet<SliderBody> collectHeadSelectedSliders()
        {
            var set = new HashSet<SliderBody>();

            foreach (var blueprint in composer.BlueprintContainer.SelectionHandler.SelectedBlueprints)
            {
                if (blueprint is SliderSelectionBlueprint { HeadSelected: true } sliderBlueprint
                    && sliderBlueprint.Item is SliderBody body)
                {
                    set.Add(body);
                }
            }

            return set;
        }

        // Flattened inputs of the controls block as last built — see buildControlsSignature.
        private readonly List<object?> controlsSignature = new List<object?>();

        private void rebuild()
        {
            rollingUpdate?.Cancel();
            rollingUpdate = null;

            var objects = editorChart.SelectedHitObjects.ToArray();
            var selectedNodes = collectSelectedNodes();
            var selectedHeads = collectHeadSelectedSliders();

            lastNodeSelectionSnapshot.Clear();
            foreach (var n in selectedNodes) lastNodeSelectionSnapshot.Add(n);

            lastHeadSelectionSnapshot.Clear();
            foreach (var h in selectedHeads) lastHeadSelectionSnapshot.Add(h);

            inspectorText.Clear();
            writeSummary(objects, selectedNodes, selectedHeads);

            // The controls (dropdowns with DI loads, buttons) are far more expensive to construct than
            // the text, and a drag fires an update per frame while changing nothing a control renders —
            // reconstruct them only when their rendered inputs actually differ, else the discarded
            // widget trees alone drive GC pressure at drag rates.
            var signature = buildControlsSignature(objects, selectedNodes, selectedHeads);
            if (!signature.SequenceEqual(controlsSignature))
            {
                controlsSignature.Clear();
                controlsSignature.AddRange(signature);

                controlsFlow.Clear();
                addControls(objects, selectedNodes, selectedHeads);
            }

            // Middle-ground rolling refresh (osu's HitObjectInspector does the same) — catches value changes
            // from drags/other panels without binding to every property.
            if (objects.Length > 0 || selectedNodes.Count > 0 || selectedHeads.Count > 0)
                rollingUpdate ??= Scheduler.AddDelayed(rebuild, 250);
        }

        /// <summary>
        /// Everything the controls block renders, flattened for equality: the selected objects, nodes
        /// and heads by identity, then each control's aggregate state and each button's eligibility.
        /// <see cref="rebuild"/> skips reconstructing the controls while this is unchanged, so any
        /// control added to <see cref="addControls"/> must contribute its inputs here or it goes stale.
        /// </summary>
        private List<object?> buildControlsSignature(GarbusHitObject[] objects, HashSet<GarbusPathControlPoint> selectedNodes, HashSet<SliderBody> selectedHeads)
        {
            var sig = new List<object?>();

            sig.AddRange(objects);
            sig.Add(null);
            sig.AddRange(selectedNodes);
            sig.Add(null);
            sig.AddRange(selectedHeads);
            sig.Add(null);

            if (objects.Length > 0 && objects.All(o => o is IHasSide))
                sig.Add(MultiValue.Aggregate(objects.Cast<IHasSide>().ToArray(), s => s.Side));

            if (objects.Length > 0 && objects.All(o => o is GarbusSlamEdge))
                sig.Add(MultiValue.Aggregate(objects.Cast<GarbusSlamEdge>().ToArray(), s => s.Direction));

            sig.Add(objects.Length >= 2 && objects.All(o => o is SliderBody)
                    && selectedNodes.Count == 0 && selectedHeads.Count == 0
                    && timeRangesDisjoint(objects.Cast<SliderBody>().ToArray()));

            if (selectedNodes.Count > 0)
            {
                var nodes = selectedNodes.ToArray();
                sig.Add(MultiValue.Aggregate(nodes, n => n.SweepEasing));
                sig.Add(MultiValue.Aggregate(nodes, n => n.Smooth));

                var eligibleNodes = ShapeOnlyEligible(nodes, slidersOwningSelectedNodes(selectedNodes));
                if (eligibleNodes.Length > 0)
                    sig.Add(MultiValue.Aggregate(eligibleNodes, n => n.ShapeOnly));
            }

            foreach (var s in objects.OfType<SliderBody>())
                sig.Add(s.Path.ControlPoints.Count > 0);

            return sig;
        }

        private void writeSummary(GarbusHitObject[] objects, HashSet<GarbusPathControlPoint> selectedNodes, HashSet<SliderBody> selectedHeads)
        {
            if (objects.Length == 0 && selectedNodes.Count == 0)
            {
                addValue("No selection");
                return;
            }

            switch (objects.Length)
            {
                case 1:
                    var selected = objects[0];

                    addHeader("Type");
                    addValue(readableTypeName(selected));

                    addHeader("Time");
                    addValue($"{selected.StartTime:#,0.##}ms");

                    if (selected is IHasAngle angle)
                    {
                        addHeader("Angle");
                        addValue($"{angle.AngleDeg}°");
                    }

                    if (selected is IHasDuration duration)
                    {
                        addHeader("End Time");
                        addValue($"{duration.EndTime:#,0.##}ms");
                        addHeader("Duration");
                        addValue($"{duration.Duration:#,0.##}ms");
                    }

                    if (selected is SliderBody slider)
                    {
                        addHeader("Nodes");
                        addValue($"{slider.Path.ControlPoints.Count}");
                    }

                    break;

                default:
                    if (objects.Length > 1)
                    {
                        addHeader("Selected Objects");
                        addValue($"{objects.Length}");

                        addHeader("Start Time");
                        addValue($"{objects.Min(o => o.StartTime):#,0.##}ms");

                        addHeader("End Time");
                        addValue($"{objects.Max(o => o.GetEndTime()):#,0.##}ms");
                    }
                    break;
            }

            if (selectedNodes.Count + selectedHeads.Count > 0)
            {
                addHeader("Selected Nodes");
                addValue($"{selectedNodes.Count + selectedHeads.Count}");
            }
        }

        private void addControls(GarbusHitObject[] objects, HashSet<GarbusPathControlPoint> selectedNodes, HashSet<SliderBody> selectedHeads)
        {
            // Side: every selected object must carry a mutable Side (slider + both slam types).
            if (objects.Length > 0 && objects.All(o => o is IHasSide))
            {
                var sided = objects.Cast<IHasSide>().ToArray();
                var state = MultiValue.Aggregate(sided, s => s.Side);

                addMultiValueDropdown("Side", state, value =>
                {
                    if (!state.IsMixed && EqualityComparer<HorizontalDirection>.Default.Equals(state.Value, value))
                        return;

                    changeHandler?.BeginChange();
                    foreach (var s in sided) s.Side = value;
                    foreach (var o in objects) editorChart.Update(o);
                    changeHandler?.EndChange();
                });
            }

            // Direction: every selected object must be a GarbusSlamEdge.
            if (objects.Length > 0 && objects.All(o => o is GarbusSlamEdge))
            {
                var slams = objects.Cast<GarbusSlamEdge>().ToArray();
                var state = MultiValue.Aggregate(slams, s => s.Direction);

                addMultiValueDropdown("Direction", state, value =>
                {
                    if (!state.IsMixed && EqualityComparer<RotationalDirection>.Default.Equals(state.Value, value))
                        return;

                    changeHandler?.BeginChange();
                    foreach (var s in slams) s.Direction = value;
                    foreach (var s in slams) editorChart.Update(s);
                    changeHandler?.EndChange();
                });
            }

            // Merge sliders: joins several disjoint sliders into one path, reparenting every other
            // slider's nodes (their heads included) onto the earliest slider. Shown only for a
            // homogeneous multi-slider selection with no node/head picks and no time-range overlap —
            // the same conditions the merge itself relies on to build a valid, non-decreasing path.
            if (objects.Length >= 2 && objects.All(o => o is SliderBody)
                && selectedNodes.Count == 0 && selectedHeads.Count == 0)
            {
                var sliders = objects.Cast<SliderBody>().ToArray();

                if (timeRangesDisjoint(sliders))
                {
                    controlsFlow.Add(new BasicButton
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 24,
                        Text = "Merge sliders",
                        BackgroundColour = new Colour4(40, 40, 48, 255),
                        Action = () => mergeSliders(sliders),
                    });
                }
            }

            // Easing / Smoothing: shown whenever one or more slider control-point nodes are picked.
            if (selectedNodes.Count > 0)
            {
                var nodes = selectedNodes.ToArray();
                var easingState = MultiValue.Aggregate(nodes, n => n.SweepEasing);
                var smoothState = MultiValue.Aggregate(nodes, n => n.Smooth);

                var affectedSliders = slidersOwningSelectedNodes(selectedNodes);

                addMultiValueDropdown("Easing", easingState, value =>
                {
                    if (!easingState.IsMixed && EqualityComparer<Easing>.Default.Equals(easingState.Value, value))
                        return;

                    changeHandler?.BeginChange();
                    foreach (var n in nodes) n.SweepEasing = value;
                    foreach (var s in affectedSliders) editorChart.Update(s);
                    changeHandler?.EndChange();
                });

                // A tri-state click always resolves to a value at least one node doesn't hold, so no
                // no-op guard is needed here (unlike the dropdown, which can be re-picked unchanged).
                addMultiValueCheckbox("Smoothing", smoothState, value =>
                {
                    changeHandler?.BeginChange();
                    foreach (var n in nodes) n.Smooth = value;
                    foreach (var s in affectedSliders) editorChart.Update(s);
                    changeHandler?.EndChange();
                });

                // Shape-only: a shape-only point shapes the sweep without being judged. Each slider's final
                // point is excluded (never shape-only), so select-all-then-toggle keeps the invariant.
                var eligibleNodes = ShapeOnlyEligible(nodes, affectedSliders);

                if (eligibleNodes.Length > 0)
                {
                    var shapeOnlyState = MultiValue.Aggregate(eligibleNodes, n => n.ShapeOnly);

                    addMultiValueCheckbox("Shape only", shapeOnlyState, value =>
                    {
                        changeHandler?.BeginChange();
                        foreach (var n in eligibleNodes) n.ShapeOnly = value;
                        foreach (var s in affectedSliders) editorChart.Update(s);
                        changeHandler?.EndChange();
                    });
                }
            }

            // Decompose into heads: offered when the selection holds any slider with a path to sample. A
            // head-only slider (no control points) has nothing to split, so it doesn't count.
            var decomposable = objects.OfType<SliderBody>().Where(s => s.Path.ControlPoints.Count > 0).ToArray();
            if (decomposable.Length > 0)
                addButton(new DecomposeSliderButton { Action = () => decomposeSliders(decomposable) });
        }

        private void decomposeSliders(SliderBody[] sliders)
        {
            changeHandler?.BeginChange();

            foreach (var slider in sliders)
            {
                // Step at the slider's own head time — the timing section it starts in governs its snap grid.
                double step = editorChart.ControlPointInfo.TimingPointAt(slider.StartTime).BeatLength / beatDivisor.Value;

                editorChart.Remove(slider);
                foreach (var head in SliderDecomposition.DecomposeIntoHeads(slider, step))
                    editorChart.Add(head);
            }

            changeHandler?.EndChange();
        }

        /// <summary>
        /// The sliders whose paths contain any of the <paramref name="selectedNodes"/> — the set the
        /// per-node controls edit and refresh. Shared by <see cref="addControls"/> and
        /// <see cref="buildControlsSignature"/> so the rendered state and its staleness signature
        /// cannot diverge.
        /// </summary>
        private SliderBody[] slidersOwningSelectedNodes(HashSet<GarbusPathControlPoint> selectedNodes)
            => editorChart.HitObjects.OfType<SliderBody>()
                          .Where(s => s.Path.ControlPoints.Any(cp => selectedNodes.Contains(cp)))
                          .ToArray();

        /// <summary>
        /// The selected nodes eligible for the Shape-only toggle: every node except its owning slider's
        /// final control point, which is never shape-only.
        /// </summary>
        public static GarbusPathControlPoint[] ShapeOnlyEligible(
            IReadOnlyCollection<GarbusPathControlPoint> nodes, IEnumerable<SliderBody> sliders)
        {
            var finals = sliders.Where(s => s.Path.ControlPoints.Count > 0)
                                .Select(s => s.Path.ControlPoints[^1])
                                .ToHashSet();
            return nodes.Where(n => !finals.Contains(n)).ToArray();
        }

        /// <summary>
        /// Whether the <paramref name="sliders"/>' [StartTime, EndTime] spans are pairwise non-overlapping.
        /// Sweeps in start-time order tracking the furthest end seen so far; touching endpoints (one slider
        /// ending exactly as the next begins) are allowed — only a strict overlap disqualifies the merge.
        /// </summary>
        private static bool timeRangesDisjoint(SliderBody[] sliders)
        {
            double maxEnd = double.NegativeInfinity;

            foreach (var slider in sliders.OrderBy(s => s.StartTime))
            {
                if (slider.StartTime < maxEnd)
                    return false;

                maxEnd = Math.Max(maxEnd, slider.EndTime);
            }

            return true;
        }

        /// <summary>
        /// Joins the selected sliders into one by reparenting every other slider's nodes (their heads
        /// included) onto the earliest slider as new control points, then removing the now-empty sliders —
        /// all in one undo transaction.
        ///
        /// The base is the earliest by start time; because the spans are disjoint (see
        /// <see cref="timeRangesDisjoint"/>), appending each joined slider's nodes in time order keeps the
        /// path's node times non-decreasing. A joined slider's head connects to the running frame by the
        /// minimal rotation (so a multi-turn base doesn't spin the long way round to reach it), and that
        /// slider's own internal winding is preserved by rebasing its offsets onto the head's new offset.
        /// </summary>
        private void mergeSliders(SliderBody[] sliders)
        {
            if (sliders.Length < 2)
                return;

            var ordered = sliders.OrderBy(s => s.StartTime).ToArray();
            var baseSlider = ordered[0];
            var baseControlPoints = baseSlider.Path.ControlPoints;

            // Running reference for cross-slider angle continuity: the base-relative rotation offset and the
            // absolute angle of the last node placed so far. Seed from the base slider's final node (its head
            // when it carries no control points).
            int previousOffset = baseControlPoints.Count > 0 ? baseControlPoints[^1].RotationOffset : 0;
            int previousAbsolute = EditorAngleMapping.NormalizeDeg(baseSlider.AngleDeg + previousOffset);

            changeHandler?.BeginChange();

            foreach (var slider in ordered.Skip(1))
            {
                int headAbsolute = EditorAngleMapping.NormalizeDeg(slider.AngleDeg);
                int headOffset = previousOffset + EditorAngleMapping.MinimalDiff(previousAbsolute, headAbsolute);

                // The joined slider's head has no incoming segment, so it keeps the segment defaults.
                baseControlPoints.Add(new GarbusPathControlPoint
                {
                    TimeOffset = slider.StartTime - baseSlider.StartTime,
                    RotationOffset = headOffset,
                });

                // Each of the slider's own nodes is relative to its head, so rebasing onto headOffset keeps
                // its internal shape (including any multi-turn winding) exactly.
                foreach (var cp in slider.Path.ControlPoints)
                {
                    baseControlPoints.Add(new GarbusPathControlPoint
                    {
                        TimeOffset = (slider.StartTime + cp.TimeOffset) - baseSlider.StartTime,
                        RotationOffset = headOffset + cp.RotationOffset,
                        Smooth = cp.Smooth,
                        SweepEasing = cp.SweepEasing,
                        ShapeOnly = cp.ShapeOnly,
                    });
                }

                // Advance the running reference to this slider's final node (its head when nodeless).
                var lastCp = slider.Path.ControlPoints.Count > 0 ? slider.Path.ControlPoints[^1] : null;
                previousOffset = lastCp != null ? headOffset + lastCp.RotationOffset : headOffset;
                previousAbsolute = EditorAngleMapping.NormalizeDeg(slider.AngleDeg + (lastCp?.RotationOffset ?? 0));
            }

            foreach (var slider in ordered.Skip(1))
                editorChart.Remove(slider);

            editorChart.Update(baseSlider);

            changeHandler?.EndChange();
        }

        private void addMultiValueDropdown<T>(string label, MultiValue<T> state, Action<T> onChange)
            where T : struct, Enum
        {
            var dropdown = new MultiValueEnumDropdown<T>(state, onChange)
            {
                RelativeSizeAxes = Axes.X,
            };

            controlsFlow.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = label,
                        Font = FontUsage.Default.With(size: 12),
                        Colour = new Colour4(180, 180, 190, 255),
                    },
                    dropdown,
                },
            });
        }

        // The checkbox paints its own inline label, so unlike a dropdown it needs no label wrapper.
        private void addMultiValueCheckbox(string label, MultiValue<bool> state, Action<bool> onChange)
            => controlsFlow.Add(new MultiValueCheckbox(label, state, onChange));

        private void addButton(Drawable button) => controlsFlow.Add(button);

        private static string readableTypeName(GarbusHitObject h) => h switch
        {
            CardinalNote => "Cardinal note",
            CardinalHoldNote => "Cardinal hold note",
            ShoulderNote => "Shoulder note",
            ShoulderHoldNote => "Shoulder hold note",
            SliderBody => "Slider",
            GarbusSlamCentered => "Slam (centered)",
            GarbusSlamEdge => "Slam (edge)",
            _ => h.GetType().Name,
        };

        private void addHeader(string header) => inspectorText.AddParagraph($"{header}:", s =>
        {
            s.Font = FontUsage.Default.With(size: 11);
            s.Colour = new Colour4(160, 160, 170, 255);
        });

        private void addValue(string value) => inspectorText.AddParagraph(value, s =>
        {
            s.Font = FontUsage.Default.With(size: 13);
            s.Colour = Colour4.White;
        });
    }
}
