// Editor clipboard for cut/copy/paste/clone operations on hit objects.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts.Format;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Objects;
using osu.Framework.Bindables;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Provides clipboard operations (Cut / Copy / Paste / Clone) for <see cref="GarbusHitObject"/>s
    /// in the editor.
    /// <para>
    /// <list type="bullet">
    /// <item><description><b>Copy</b> — encodes the current selection to <see cref="Content"/> (clipboard stays populated after paste).</description></item>
    /// <item><description><b>Cut</b> — Copy + RemoveRange in one transaction.</description></item>
    /// <item><description><b>Paste</b> — decodes <see cref="Content"/>, shifts all objects so that the
    /// earliest lands exactly on the snapped playhead time, AddRanges them, and swaps the selection to
    /// the pasted set.  Everything inside one transaction (one undo step).</description></item>
    /// <item><description><b>Clone</b> (Ctrl+D) — copy+paste at the playhead in one step <em>without</em>
    /// touching <see cref="Content"/>.  osu's clone behaviour: clipboard is intentionally <em>not</em>
    /// disturbed so a previous copy can still be pasted after cloning.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class EditorClipboard
    {
        /// <summary>The raw JSON payload; non-empty when something has been copied.</summary>
        public readonly Bindable<string> Content = new Bindable<string>(string.Empty);

        /// <summary>True when the selection is non-empty (cut/copy/clone are available).</summary>
        public bool CanCut => editorChart.SelectedHitObjects.Count > 0;

        /// <summary>True when the selection is non-empty (copy is available).</summary>
        public bool CanCopy => editorChart.SelectedHitObjects.Count > 0;

        /// <summary>True when <see cref="Content"/> is non-empty (paste is available).</summary>
        public bool CanPaste => !string.IsNullOrEmpty(Content.Value);

        private readonly EditorChart editorChart;
        private readonly Func<double> getPlayheadTime;
        private readonly BindableBeatDivisor beatDivisor;

        /// <summary>
        /// Constructs the clipboard.
        /// </summary>
        /// <param name="editorChart">The editor chart to operate on.</param>
        /// <param name="editorClock">The editor clock — <see cref="EditorClock.CurrentTime"/> is used as the paste target.</param>
        /// <param name="controlPointInfo">Timing info for beat-snap calculations.</param>
        /// <param name="beatDivisor">The current snap divisor.</param>
        public EditorClipboard(
            EditorChart editorChart,
            EditorClock editorClock,
            ControlPointInfo controlPointInfo,
            BindableBeatDivisor beatDivisor)
            : this(editorChart, () => editorClock.CurrentTime, controlPointInfo, beatDivisor)
        {
        }

        /// <summary>
        /// Internal constructor that accepts a raw time provider, used by unit tests to avoid
        /// the osu-framework component infrastructure that <see cref="EditorClock"/> requires.
        /// </summary>
        internal EditorClipboard(
            EditorChart editorChart,
            Func<double> getPlayheadTime,
            ControlPointInfo controlPointInfo,
            BindableBeatDivisor beatDivisor)
        {
            this.editorChart = editorChart;
            this.getPlayheadTime = getPlayheadTime;
            this.beatDivisor = beatDivisor;
        }

        // ── Public operations ────────────────────────────────────────────────────

        /// <summary>Encodes the current selection into <see cref="Content"/>.</summary>
        public void Copy()
        {
            if (!CanCopy) return;

            var selection = editorChart.SelectedHitObjects.OrderBy(h => h.StartTime).ToList();
            Content.Value = GarbusChartSerializer.EncodeHitObjects(selection);
        }

        /// <summary>Copy + remove originals in one transaction.</summary>
        public void Cut()
        {
            if (!CanCut) return;

            Copy();

            var toRemove = editorChart.SelectedHitObjects.ToList();
            editorChart.BeginChange();
            editorChart.RemoveRange(toRemove);
            editorChart.EndChange();
        }

        /// <summary>
        /// Paste from <see cref="Content"/> at the snapped playhead position.
        /// Earliest object lands exactly on the snap-rounded playhead; relative offsets are preserved.
        /// The selection is replaced with the pasted set.  One undo step.
        /// </summary>
        public void Paste()
        {
            if (!CanPaste) return;

            var decoded = GarbusChartSerializer.DecodeHitObjects(Content.Value);
            pasteObjects(decoded);
        }

        /// <summary>
        /// Clone: paste selection at the playhead <em>without</em> touching <see cref="Content"/>.
        /// Clipboard content is deliberately preserved so a prior Copy can still be Pasted afterwards.
        /// </summary>
        public void Clone()
        {
            if (!CanCopy) return;

            var selection = editorChart.SelectedHitObjects.OrderBy(h => h.StartTime).ToList();
            // Encode then decode to get deep-cloned fresh instances.
            string encoded = GarbusChartSerializer.EncodeHitObjects(selection);
            var cloned = GarbusChartSerializer.DecodeHitObjects(encoded);
            pasteObjects(cloned);
            // Content is NOT changed — intentional, matching osu's clone behaviour.
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        private void pasteObjects(IReadOnlyList<GarbusHitObject> objects)
        {
            if (objects.Count == 0) return;

            // Snap the playhead to the current divisor.
            double snappedPlayhead = editorChart.ControlPointInfo.GetClosestSnappedTime(
                getPlayheadTime(), beatDivisor.Value);

            // Shift all objects so the earliest lands exactly on the snapped playhead.
            double minStart = objects.Min(h => h.StartTime);
            double shift = snappedPlayhead - minStart;

            foreach (var h in objects)
                h.StartTime += shift;

            // One transaction: add + select pasted set.
            editorChart.BeginChange();

            editorChart.AddRange(objects);

            // Swap selection to the pasted set.
            editorChart.SelectedHitObjects.Clear();
            foreach (var h in objects)
                editorChart.SelectedHitObjects.Add(h);

            editorChart.EndChange();
        }
    }
}
