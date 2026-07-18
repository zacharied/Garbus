// Draws a translucent horizontal band per design point across the timeline content area, from its
// StartTime to EndTime (as fractions of the track). Modeled on TimelineTimingChangeDisplay, but a
// spanning region instead of a vertical line. Recreated on DesignPointInfo.DesignPointsChanged — that
// single event covers add/remove AND Start/End moves, because MoveDesignPoint is structural.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Caching;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;
using Garbus.Game.Charts.Design;

namespace Garbus.Game.Edit.Screens.Timeline
{
    public partial class TimelineDesignRegionDisplay : CompositeDrawable
    {
        [Resolved]
        private DesignPointInfo designPointInfo { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly Cached regionCache = new Cached();
        private readonly List<Box> boxPool = new List<Box>();

        private void onDesignPointsChanged() => regionCache.Invalidate();

        public TimelineDesignRegionDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            designPointInfo.DesignPointsChanged += onDesignPointsChanged;
        }

        protected override void Update()
        {
            base.Update();

            if (!regionCache.IsValid)
                recreateRegions();
        }

        private void recreateRegions()
        {
            double trackLength = editorClock.TrackLength;
            if (trackLength <= 0) return;

            int idx = 0;
            foreach (var dp in designPointInfo.DesignPoints)
            {
                var box = getOrCreateBox(idx++);
                box.RelativePositionAxes = Axes.X;
                box.RelativeSizeAxes = Axes.Both;
                box.Anchor = Anchor.TopLeft;
                box.Origin = Anchor.TopLeft;
                box.X = (float)(dp.StartTime / trackLength);
                box.Width = (float)Math.Max(0, (dp.EndTime - dp.StartTime) / trackLength);
                box.Height = 1;
                box.Colour = new Color4(90, 140, 220, 60);
                box.Alpha = 1;
            }

            for (int i = idx; i < boxPool.Count; i++)
                boxPool[i].Alpha = 0;

            regionCache.Validate();
        }

        private Box getOrCreateBox(int index)
        {
            if (index < boxPool.Count)
                return boxPool[index];

            var box = new Box();
            boxPool.Add(box);
            AddInternal(box);
            return box;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (designPointInfo != null)
                designPointInfo.DesignPointsChanged -= onDesignPointsChanged;
        }
    }
}
