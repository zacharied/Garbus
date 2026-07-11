// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Compose/Components/Timeline/TimelineTickDisplay.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: removed OsuColour/OsuConfigManager/EditorBeatmap/Timeline dependencies;
// uses ControlPointInfo directly; colours are derived from divisor index without OsuColour;
// visibility is driven by an injected Bindable<bool> (EditorShowTicks config) rather than
// OsuConfig.EditorTimelineShowTicks; no PointVisualisation — uses plain Box drawables.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using Garbus.Game.Charts.Timing;

namespace Garbus.Game.Edit.Screens.Timeline
{
    /// <summary>
    /// Draws beat tick lines inside the <see cref="TimelineStrip"/> content area.
    /// Colours are determined by the beat divisor index (bar lines = white; other subdivisions
    /// cycle through a fixed palette keyed on the applicable divisor).
    /// </summary>
    public partial class TimelineTickDisplay : CompositeDrawable
    {
        [Resolved]
        private ControlPointInfo controlPointInfo { get; set; } = null!;

        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Cached tickCache = new Cached();
        private readonly List<Box> tickPool = new List<Box>();
        private int usedTicks;

        private void onControlPointsChanged() => tickCache.Invalidate();

        public TimelineTickDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            // Children use relative X positions within the content.
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            beatDivisor.BindValueChanged(_ => tickCache.Invalidate());
            controlPointInfo.ControlPointsChanged += onControlPointsChanged;
        }

        protected override void Update()
        {
            base.Update();

            if (!tickCache.IsValid)
                recreateTicks();
        }

        private void recreateTicks()
        {
            double trackLength = editorClock.TrackLength;
            if (trackLength <= 0 || DrawWidth <= 0)
                return;

            int idx = 0;

            for (int i = 0; i < controlPointInfo.TimingPoints.Count; i++)
            {
                var point = controlPointInfo.TimingPoints[i];
                double until = i + 1 < controlPointInfo.TimingPoints.Count
                    ? controlPointInfo.TimingPoints[i + 1].Time
                    : trackLength;

                int beat = 0;
                double step = point.BeatLength / beatDivisor.Value;

                for (double t = point.Time; t < until; t += step)
                {
                    if (t < 0) { beat++; continue; }

                    int divisor = BindableBeatDivisor.GetDivisorForBeatIndex(beat, beatDivisor.Value);
                    bool isBar = beat % point.TimeSignature.Numerator == 0 && divisor <= 1;

                    Color4 colour = BeatDivisorColours.ColourFor(isBar ? 0 : divisor);
                    float heightFrac = isBar ? 1.0f : BeatDivisorColours.HeightFor(divisor);

                    var box = getOrCreateTick(idx++);
                    box.RelativePositionAxes = Axes.X;
                    box.RelativeSizeAxes = Axes.Y;
                    box.Anchor = Anchor.CentreLeft;
                    box.Origin = Anchor.Centre;
                    box.X = (float)(t / trackLength);
                    box.Width = isBar ? 1.5f : 1f;
                    box.Height = heightFrac;
                    box.Colour = colour;
                    box.Alpha = 1;
                    beat++;
                }
            }

            usedTicks = idx;

            // Hide excess ticks from the pool.
            for (int i = usedTicks; i < tickPool.Count; i++)
                tickPool[i].Alpha = 0;

            tickCache.Validate();
        }

        private Box getOrCreateTick(int index)
        {
            if (index < tickPool.Count)
                return tickPool[index];

            var box = new Box();
            tickPool.Add(box);
            AddInternal(box);
            return box;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (controlPointInfo != null)
                controlPointInfo.ControlPointsChanged -= onControlPointsChanged;
        }
    }
}
