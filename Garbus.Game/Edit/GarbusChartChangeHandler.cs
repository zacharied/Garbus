// Ported for Garbus. Mirrors osu.Game's BeatmapEditorChangeHandler pattern
// (osu.Game/Screens/Edit/BeatmapEditorChangeHandler.cs) but serializes via GarbusChartSerializer and
// patches via an in-process JSON-identity diff rather than a legacy .osu line-diff.
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: BeatmapEditorChangeHandler → GarbusChartChangeHandler; LegacyEditorBeatmapPatcher
// replaced by per-object JSON-encoded identity diff; operates on EditorChart / GarbusChartSerializer.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Format;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Concrete <see cref="EditorChangeHandler"/> that snapshots the chart as UTF-8 JSON and applies
    /// state changes by diffing hit-object identity strings (encoded via
    /// <see cref="GarbusChartSerializer.EncodeHitObject"/>).
    /// </summary>
    public partial class GarbusChartChangeHandler : EditorChangeHandler
    {
        private readonly EditorChart editorChart;

        public GarbusChartChangeHandler(EditorChart editorChart)
        {
            this.editorChart = editorChart;

            // Capture the baseline (pre-edit) state before wiring any events.
            // This ensures ensureStateSaved() in BeginChange finds a state already saved,
            // so subsequent BeginChange calls do not overwrite it with the already-mutated chart.
            // (EditorChart.Add mutates its hitObjects list BEFORE firing TransactionBegan, so if we
            // waited for the first BeginChange the initial snapshot would be post-mutation.)
            SaveState();

            // Mirror the chart's transaction lifecycle so chart mutations become undo steps.
            editorChart.TransactionBegan += BeginChange;
            editorChart.TransactionEnded += EndChange;
            editorChart.SaveStateTriggered += SaveState;
        }

        protected override void WriteCurrentStateToStream(MemoryStream stream)
        {
            // EditorChart now aliases Chart.HitObjects directly, so Chart always reflects live state.
            // Serialize it as-is — no shadow copy reconstruction needed.
            byte[] bytes = Encoding.UTF8.GetBytes(GarbusChartSerializer.Encode(editorChart.Chart));
            stream.Write(bytes, 0, bytes.Length);
        }

        protected override void ApplyStateChange(byte[] previousState, byte[] newState)
        {
            // Decode the target state.
            string json = Encoding.UTF8.GetString(newState);
            GarbusChart targetChart = GarbusChartSerializer.Decode(json);

            // --- Hit-object diff ---
            // Build identity maps: encoded JSON string → first matching live object (or null).
            // Untouched objects keep their existing references; only deltas are removed/added.
            var currentObjects = editorChart.HitObjects.ToList();

            // Map: identity key → remaining pool of current objects (list to handle duplicates).
            var currentPool = new Dictionary<string, Queue<GarbusHitObject>>();
            foreach (var obj in currentObjects)
            {
                string key = GarbusChartSerializer.EncodeHitObject(obj);
                if (!currentPool.TryGetValue(key, out var q))
                    currentPool[key] = q = new Queue<GarbusHitObject>();
                q.Enqueue(obj);
            }

            // Walk target list; consume matching current objects or record new ones to add.
            var toAdd = new List<GarbusHitObject>();
            var consumed = new HashSet<GarbusHitObject>(ReferenceEqualityComparer.Instance);

            foreach (var targetObj in targetChart.HitObjects)
            {
                string key = GarbusChartSerializer.EncodeHitObject(targetObj);
                if (currentPool.TryGetValue(key, out var q) && q.Count > 0)
                {
                    // Reuse existing reference.
                    consumed.Add(q.Dequeue());
                }
                else
                {
                    toAdd.Add(targetObj);
                }
            }

            // Any current object not consumed must be removed.
            var toRemove = currentObjects.Where(o => !consumed.Contains(o)).ToList();

            foreach (var obj in toRemove)
                editorChart.Remove(obj);

            foreach (var obj in toAdd)
                editorChart.Add(obj);

            // --- Metadata ---
            editorChart.Metadata.Title = targetChart.Metadata.Title;
            editorChart.Metadata.Artist = targetChart.Metadata.Artist;
            editorChart.Metadata.Charter = targetChart.Metadata.Charter;
            editorChart.Metadata.ChartName = targetChart.Metadata.ChartName;
            editorChart.Metadata.RomanisedTitle = targetChart.Metadata.RomanisedTitle;
            editorChart.Metadata.RomanisedArtist = targetChart.Metadata.RomanisedArtist;
            editorChart.Metadata.Source = targetChart.Metadata.Source;
            editorChart.Metadata.Tags = targetChart.Metadata.Tags;
            editorChart.Metadata.AudioFile = targetChart.Metadata.AudioFile;
            editorChart.Metadata.BackgroundFile = targetChart.Metadata.BackgroundFile;
            editorChart.Metadata.Level = targetChart.Metadata.Level;
            editorChart.Metadata.Difficulty = targetChart.Metadata.Difficulty;

            // --- PreviewTime ---
            editorChart.Chart.PreviewTime = targetChart.PreviewTime;

            // --- ControlPointInfo ---
            // Rebuild the timing control points from the decoded target.
            editorChart.ControlPointInfo.Clear();
            foreach (var tp in targetChart.ControlPointInfo.TimingPoints)
            {
                editorChart.ControlPointInfo.Add(tp.Time, new TimingControlPoint
                {
                    BeatLength = tp.BeatLength,
                    TimeSignature = tp.TimeSignature,
                    OmitFirstBarLine = tp.OmitFirstBarLine,
                });
            }
        }
    }
}
