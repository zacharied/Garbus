// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/GroupSection.cs +
// TapTimingControl.cs (the group-move and BPM-set transactions).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: extracted into a shared static helper (osu inlines this in each caller); the
// adjust-objects flag is a parameter instead of an OsuConfigManager setting.

using System;
using System.Linq;
using Garbus.Game.Charts.Timing;
using osu.Framework.Utils;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// The shared undo transactions behind every timing point mutation the Timing tab offers.
    /// </summary>
    public static class TimingPointChanges
    {
        /// <summary>
        /// Moves a control point group to a new time inside one undo transaction, preserving its
        /// points. Optionally shifts the objects of the affected timing section by the same amount
        /// (computed from the point's OLD time, before the move). Returns the group now at the new time.
        /// </summary>
        public static ControlPointGroup MoveGroup(EditorChart chart, IEditorChangeHandler changeHandler,
                                                  ControlPointGroup group, double newTime, bool adjustObjects)
            => moveGroup(null, chart, changeHandler, group, newTime, adjustObjects);

        public static ControlPointGroup MoveGroup(EditorSong song, EditorChart chart, IEditorChangeHandler changeHandler,
                                                  ControlPointGroup group, double newTime, bool adjustObjects)
            => moveGroup(song, chart, changeHandler, group, newTime, adjustObjects);

        private static ControlPointGroup moveGroup(EditorSong? song, EditorChart chart, IEditorChangeHandler changeHandler,
                                                   ControlPointGroup group, double newTime, bool adjustObjects)
        {
            var currentItems = group.ControlPoints.ToArray();

            changeHandler.BeginChange();

            var tp = currentItems.OfType<TimingControlPoint>().FirstOrDefault();
            if (tp != null && adjustObjects)
            {
                var range = TimingSectionAdjustments.TimingRange(chart, tp);
                TimingSectionAdjustments.AdjustHitObjectOffset(chart, tp, newTime - group.Time);
                if (song?.Song.UsesSharedTiming == true)
                {
                    foreach (var other in song.Song.Charts.Where(c => !ReferenceEquals(c, chart.Chart)))
                        TimingSectionAdjustments.AdjustHitObjectOffset(other, range.start, range.end, newTime - group.Time);
                }
            }

            chart.ControlPointInfo.RemoveGroup(group);

            foreach (var cp in currentItems)
                chart.ControlPointInfo.Add(newTime, cp);

            chart.SaveState();
            changeHandler.EndChange();

            return chart.ControlPointInfo.GroupAt(newTime);
        }

        /// <summary>
        /// Sets a timing point's BPM inside one undo transaction, optionally keeping the affected
        /// section's objects on the same beats.
        /// </summary>
        public static void ChangeBpm(EditorChart chart, IEditorChangeHandler changeHandler,
                                     TimingControlPoint tp, double newBpm, bool adjustObjects)
            => changeBpm(null, chart, changeHandler, tp, newBpm, adjustObjects);

        public static void ChangeBpm(EditorSong song, EditorChart chart, IEditorChangeHandler changeHandler,
                                     TimingControlPoint tp, double newBpm, bool adjustObjects)
            => changeBpm(song, chart, changeHandler, tp, newBpm, adjustObjects);

        private static void changeBpm(EditorSong? song, EditorChart chart, IEditorChangeHandler changeHandler,
                                      TimingControlPoint tp, double newBpm, bool adjustObjects)
        {
            double oldBeatLength = tp.BeatLength;
            double newBeatLength = 60000.0 / newBpm;

            if (Precision.AlmostEquals(oldBeatLength, newBeatLength))
                return;

            changeHandler.BeginChange();
            tp.BeatLength = newBeatLength;

            if (adjustObjects)
            {
                var range = TimingSectionAdjustments.TimingRange(chart, tp);
                TimingSectionAdjustments.SetHitObjectBPM(chart, tp, oldBeatLength);
                if (song?.Song.UsesSharedTiming == true)
                {
                    foreach (var other in song.Song.Charts.Where(c => !ReferenceEquals(c, chart.Chart)))
                        TimingSectionAdjustments.SetHitObjectBPM(other, tp, oldBeatLength, range.start, range.end);
                }
            }

            chart.SaveState();
            changeHandler.EndChange();
        }
    }
}
