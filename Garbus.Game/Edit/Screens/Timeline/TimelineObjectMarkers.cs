// Bespoke for Garbus — non-interactive hit object markers in the timeline strip.
// Draws a 4px dot at each hit object's start time; IHasDuration objects get a wider bar spanning
// the full duration. Rebuilds on EditorChart.HitObjectAdded/Removed/Updated events.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Screens.Timeline
{
    /// <summary>
    /// Non-interactive markers in the timeline strip: a 4px dot per hit object at its time position,
    /// or a wider bar for objects that implement <see cref="IHasDuration"/>.
    /// Subscribes to <see cref="EditorChart"/> add/remove/update events and rebuilds on change.
    /// </summary>
    public partial class TimelineObjectMarkers : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly List<Drawable> markerPool = new List<Drawable>();
        private bool dirty = true;

        /// <summary>Count of currently visible markers (alpha &gt; 0).</summary>
        public int VisibleMarkerCount => markerPool.Count(m => m.Alpha > 0);

        public TimelineObjectMarkers()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            editorChart.HitObjectAdded += onHitObjectChanged;
            editorChart.HitObjectRemoved += onHitObjectChanged;
            editorChart.HitObjectUpdated += onHitObjectChanged;
        }

        private void onHitObjectChanged(GarbusHitObject _) => dirty = true;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            dirty = true;
        }

        protected override void Update()
        {
            base.Update();

            if (dirty)
            {
                dirty = false;
                rebuild();
            }
        }

        private void rebuild()
        {
            double trackLength = editorClock.TrackLength;
            if (trackLength <= 0) return;

            int idx = 0;

            foreach (var hitObject in editorChart.HitObjects)
            {
                double startTime = hitObject.StartTime;
                float xStart = (float)(startTime / trackLength);

                if (hitObject is IHasDuration dur && dur.Duration > 0)
                {
                    // Duration bar: spans from start to end.
                    float xEnd = (float)(dur.EndTime / trackLength);

                    var bar = getOrCreateMarker<Box>(idx++);
                    bar.RelativePositionAxes = Axes.X;
                    bar.RelativeSizeAxes = Axes.Y;
                    bar.Anchor = Anchor.CentreLeft;
                    bar.Origin = Anchor.CentreLeft;
                    bar.X = xStart;
                    ((Box)bar).Width = Math.Max(1f, (xEnd - xStart) * DrawWidth);
                    ((Box)bar).Height = 0.5f;
                    bar.Colour = new Color4(100, 200, 255, 200);
                    bar.Alpha = 1;
                }
                else
                {
                    // Point dot.
                    var dot = getOrCreateMarker<Box>(idx++);
                    dot.RelativePositionAxes = Axes.X;
                    dot.RelativeSizeAxes = Axes.Y;
                    dot.Anchor = Anchor.CentreLeft;
                    dot.Origin = Anchor.Centre;
                    dot.X = xStart;
                    ((Box)dot).Width = 4f;
                    ((Box)dot).Height = 0.6f;
                    dot.Colour = new Color4(255, 200, 80, 220);
                    dot.Alpha = 1;
                }
            }

            // Hide unused markers.
            for (int i = idx; i < markerPool.Count; i++)
                markerPool[i].Alpha = 0;
        }

        private T getOrCreateMarker<T>(int index) where T : Drawable, new()
        {
            if (index < markerPool.Count)
            {
                if (markerPool[index] is T existing)
                    return existing;

                // Wrong type in pool — replace it.
                var old = markerPool[index];
                RemoveInternal(old, true);
                markerPool.RemoveAt(index);
            }

            var newMarker = new T();
            if (index < markerPool.Count)
                markerPool.Insert(index, newMarker);
            else
                markerPool.Add(newMarker);
            AddInternal(newMarker);
            return newMarker;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorChart != null)
            {
                editorChart.HitObjectAdded -= onHitObjectChanged;
                editorChart.HitObjectRemoved -= onHitObjectChanged;
                editorChart.HitObjectUpdated -= onHitObjectChanged;
            }
        }
    }
}
