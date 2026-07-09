// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Compose/Components/Timeline/TimelineTimingChangeDisplay.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: removed OsuColour/OsuSpriteText/Timeline dependencies; uses ControlPointInfo
// directly; draws a red vertical bar per TimingControlPoint; visibility driven by a passed-in Bindable.

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
    /// Draws a red vertical bar for each <see cref="TimingControlPoint"/> in the timeline content area.
    /// </summary>
    public partial class TimelineTimingChangeDisplay : CompositeDrawable
    {
        [Resolved]
        private ControlPointInfo controlPointInfo { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Cached groupCache = new Cached();
        private readonly List<Box> linePool = new List<Box>();

        public TimelineTimingChangeDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            controlPointInfo.ControlPointsChanged += () => groupCache.Invalidate();
        }

        protected override void Update()
        {
            base.Update();

            if (!groupCache.IsValid)
                recreateLines();
        }

        private void recreateLines()
        {
            double trackLength = editorClock.TrackLength;
            if (trackLength <= 0) return;

            int idx = 0;
            foreach (var tp in controlPointInfo.TimingPoints)
            {
                var box = getOrCreateLine(idx++);
                box.RelativePositionAxes = Axes.X;
                box.RelativeSizeAxes = Axes.Y;
                box.Anchor = Anchor.CentreLeft;
                box.Origin = Anchor.Centre;
                box.X = (float)(tp.Time / trackLength);
                box.Width = 2;
                box.Height = 1;
                box.Colour = new Color4(220, 60, 60, 200);
                box.Alpha = 1;
            }

            for (int i = idx; i < linePool.Count; i++)
                linePool[i].Alpha = 0;

            groupCache.Validate();
        }

        private Box getOrCreateLine(int index)
        {
            if (index < linePool.Count)
                return linePool[index];

            var box = new Box();
            linePool.Add(box);
            AddInternal(box);
            return box;
        }
    }
}
