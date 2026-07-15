// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Compose/Components/HitObjectInspector.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: EditorBeatmap → EditorChart; OsuTextFlowContainer/OsuFont/OverlayColourProvider dropped in
// favour of plain TextFlowContainer + hard-coded colours; adds two dropdowns that osu's plain-text inspector
// doesn't have — a Side dropdown when the selection is a single slider or slam, and a SweepEasing dropdown when
// one or more slider control-point nodes are picked in a SliderSelectionBlueprint. Node selection isn't in
// EditorChart.SelectedHitObjects — polled via the composer's SelectionHandler alongside the 250ms rolling refresh.

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

        // Tracked so a node-selection change (which isn't observable via events) triggers a rebuild.
        private readonly HashSet<GarbusPathControlPoint> lastNodeSelectionSnapshot = new HashSet<GarbusPathControlPoint>();

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
            if (!currentNodes.SetEquals(lastNodeSelectionSnapshot))
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

        private void rebuild()
        {
            inspectorText.Clear();
            controlsFlow.Clear();

            rollingUpdate?.Cancel();
            rollingUpdate = null;

            var objects = editorChart.SelectedHitObjects.ToArray();
            var selectedNodes = collectSelectedNodes();

            lastNodeSelectionSnapshot.Clear();
            foreach (var n in selectedNodes) lastNodeSelectionSnapshot.Add(n);

            writeSummary(objects, selectedNodes);
            addControls(objects, selectedNodes);

            // Middle-ground rolling refresh (osu's HitObjectInspector does the same) — catches value changes
            // from drags/other panels without binding to every property.
            if (objects.Length > 0 || selectedNodes.Count > 0)
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

            if (selectedNodes.Count > 0)
            {
                addHeader("Selected Nodes");
                addValue($"{selectedNodes.Count}");
            }
        }

        private void addControls(GarbusHitObject[] objects, HashSet<GarbusPathControlPoint> selectedNodes)
        {
            // Side / Direction dropdowns: single object selection only.
            if (objects.Length == 1)
            {
                var single = objects[0];

                // Side applies to every IHasSide object (slider, both slam types); shoulders carry a Side
                // but it's positional and intentionally not IHasSide, so no dropdown for them.
                if (single is IHasSide sided)
                {
                    addEnumDropdown(
                        "Side",
                        sided.Side,
                        value =>
                        {
                            if (sided.Side == value) return;
                            changeHandler?.BeginChange();
                            sided.Side = value;
                            editorChart.Update(single);
                            changeHandler?.EndChange();
                        });
                }

                if (single is GarbusSlamEdge slam)
                {
                    addEnumDropdown(
                        "Direction",
                        slam.Direction,
                        value =>
                        {
                            if (slam.Direction == value) return;
                            changeHandler?.BeginChange();
                            slam.Direction = value;
                            editorChart.Update(slam);
                            changeHandler?.EndChange();
                        });
                }
            }

            // SweepEasing dropdown: shown whenever one or more slider nodes are picked. Applied to every
            // picked node (the shared value is shown when they agree, else the first node's value).
            if (selectedNodes.Count > 0)
            {
                var firstNode = selectedNodes.First();
                var shared = selectedNodes.All(n => n.SweepEasing == firstNode.SweepEasing)
                    ? firstNode.SweepEasing
                    : firstNode.SweepEasing;

                // Find every slider whose control-point list contains any of the picked nodes — we need to
                // fire EditorChart.Update on each of them.
                var affectedSliders = editorChart.HitObjects.OfType<SliderBody>()
                    .Where(s => s.Path.ControlPoints.Any(cp => selectedNodes.Contains(cp)))
                    .ToArray();

                addEnumDropdown(
                    "Easing",
                    shared,
                    value =>
                    {
                        if (selectedNodes.All(n => n.SweepEasing == value)) return;
                        changeHandler?.BeginChange();
                        foreach (var n in selectedNodes)
                            n.SweepEasing = value;
                        foreach (var s in affectedSliders)
                            editorChart.Update(s);
                        changeHandler?.EndChange();
                    });
            }
        }

        private void addEnumDropdown<T>(string label, T current, Action<T> onChange) where T : struct, Enum
        {
            var dropdown = new BasicDropdown<T>
            {
                RelativeSizeAxes = Axes.X,
                Items = Enum.GetValues<T>(),
                Current = { Value = current },
            };

            dropdown.Current.BindValueChanged(v => onChange(v.NewValue));

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
