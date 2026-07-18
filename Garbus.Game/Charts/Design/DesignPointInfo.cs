// Sorted container of DesignPoints (by StartTime) — the design-side peer of ControlPointInfo.
// Raises DesignPointsChanged on any STRUCTURAL change (add, remove, clear, or a positional move via
// MoveDesignPoint) so the tab list, undo/redo rebuild, and timeline overlay refresh off one event.
// In-place edits of effect parameters (e.g. TutorialMessage.Text) deliberately do NOT raise it.

using System;
using System.Collections.Generic;

namespace Garbus.Game.Charts.Design
{
    public class DesignPointInfo
    {
        private readonly List<DesignPoint> designPoints = new List<DesignPoint>();

        public IReadOnlyList<DesignPoint> DesignPoints => designPoints;

        public event Action? DesignPointsChanged;

        public void Add(DesignPoint point)
        {
            insertSorted(point);
            DesignPointsChanged?.Invoke();
        }

        public void Remove(DesignPoint point)
        {
            if (designPoints.Remove(point))
                DesignPointsChanged?.Invoke();
        }

        public void Clear()
        {
            if (designPoints.Count == 0)
                return;

            designPoints.Clear();
            DesignPointsChanged?.Invoke();
        }

        /// <summary>
        /// Structurally moves a point: updates its start/end, re-sorts, then raises the change event.
        /// Editing position through here (rather than the bindables directly) keeps the sorted order
        /// and lets the single event drive every consumer. Analog of TimingPointChanges.MoveGroup.
        /// </summary>
        public void MoveDesignPoint(DesignPoint point, double newStartTime, double newEndTime)
        {
            point.StartTime = newStartTime;
            point.EndTime = newEndTime;
            designPoints.Remove(point);
            insertSorted(point);
            DesignPointsChanged?.Invoke();
        }

        // Stable insert: append after all points with an equal-or-earlier start time.
        private void insertSorted(DesignPoint point)
        {
            int i = 0;
            while (i < designPoints.Count && designPoints[i].StartTime <= point.StartTime)
                i++;
            designPoints.Insert(i, point);
        }
    }
}
