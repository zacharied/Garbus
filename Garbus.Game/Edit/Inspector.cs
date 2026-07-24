// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/HitObjectInspector.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: EditorBeatmap → EditorChart; OsuTextFlowContainer/OsuFont/OverlayColourProvider dropped in
// favour of plain TextFlowContainer + hard-coded colours; adds multi-value-aware dropdowns that osu's plain-text
// inspector doesn't have — a Side dropdown when every selected object is IHasSide, a Direction dropdown when every
// selected object is a GarbusSlamEdge, and a SweepEasing dropdown when one or more slider control-point nodes are
// picked in a SliderSelectionBlueprint. Each shows "<multiple>" when the selection's values disagree and applies an
// edit to the whole selection as one undo step. Node selection isn't in EditorChart.SelectedHitObjects — polled via
// the composer's SelectionHandler alongside the 250ms rolling refresh.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Edit.Blueprints;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Objects;
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

        private FillFlowContainer flow = null!;
        private TextFlowContainer inspectorText = null!;
        private FillFlowContainer controlsFlow = null!;

        private ScheduledDelegate? rollingUpdate;
        private readonly List<Func<bool>> menuOpenChecks = new();

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
                    controlsFlow = new FillFlowContainer
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

            editorChart.SelectedHitObjects.CollectionChanged += (_, _) => rebuild();
            editorChart.HitObjectUpdated += _ => rebuild();
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

        private void rebuild()
        {
            if (IsHovered || menuOpenChecks.Any(isOpen => isOpen()))
            {
                rollingUpdate?.Cancel();
                rollingUpdate = Scheduler.AddDelayed(rebuild, 250);
                return;
            }

            inspectorText.Clear();
            controlsFlow.Clear();
            menuOpenChecks.Clear();

            rollingUpdate?.Cancel();
            rollingUpdate = null;

            var objects = editorChart.SelectedHitObjects.ToArray();
            var selectedNodes = collectSelectedNodes();

            lastNodeSelectionSnapshot.Clear();
            foreach (var n in selectedNodes) lastNodeSelectionSnapshot.Add(n);

            lastHeadSelectionSnapshot.Clear();
            foreach (var h in collectHeadSelectedSliders()) lastHeadSelectionSnapshot.Add(h);

            writeSummary(objects, selectedNodes);
            addControls(objects, selectedNodes);

            // Middle-ground rolling refresh (osu's HitObjectInspector does the same) — catches value changes
            // from drags/other panels without binding to every property.
            if (objects.Length > 0 || selectedNodes.Count > 0 || collectHeadSelectedSliders().Count > 0)
                rollingUpdate ??= Scheduler.AddDelayed(rebuild, 250);
        }

        private void writeSummary(GarbusHitObject[] objects, HashSet<GarbusPathControlPoint> selectedNodes)
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

            int headCount = collectHeadSelectedSliders().Count;
            if (selectedNodes.Count + headCount > 0)
            {
                addHeader("Selected Nodes");
                addValue($"{selectedNodes.Count + headCount}");
            }
        }

        private void addControls(GarbusHitObject[] objects, HashSet<GarbusPathControlPoint> selectedNodes)
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

            // Easing: shown whenever one or more slider control-point nodes are picked.
            if (selectedNodes.Count > 0)
            {
                var nodes = selectedNodes.ToArray();
                var state = MultiValue.Aggregate(nodes, n => n.SweepEasing);

                var affectedSliders = editorChart.HitObjects.OfType<SliderBody>()
                    .Where(s => s.Path.ControlPoints.Any(cp => selectedNodes.Contains(cp)))
                    .ToArray();

                addMultiValueDropdown("Easing", state, value =>
                {
                    if (!state.IsMixed && EqualityComparer<Easing>.Default.Equals(state.Value, value))
                        return;

                    changeHandler?.BeginChange();
                    foreach (var n in nodes) n.SweepEasing = value;
                    foreach (var s in affectedSliders) editorChart.Update(s);
                    changeHandler?.EndChange();
                });
            }
        }

        private void addMultiValueDropdown<T>(string label, MultiValue<T> state, Action<T> onChange)
            where T : struct, Enum
        {
            var dropdown = new InspectorEnumDropdown<T>(state, onChange)
            {
                RelativeSizeAxes = Axes.X,
            };
            menuOpenChecks.Add(() => dropdown.MenuOpen);

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

        private partial class InspectorEnumDropdown<T> : MultiValueEnumDropdown<T>
            where T : struct, Enum
        {
            public bool MenuOpen => Menu.State == MenuState.Open;

            public InspectorEnumDropdown(MultiValue<T> state, Action<T> onChange)
                : base(state, onChange)
            {
                Menu.MaxHeight = 160;
            }
        }

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
