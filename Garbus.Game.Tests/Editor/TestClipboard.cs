// Tests for EditorClipboard: copy, cut, paste (shift math + fresh instances + selection swap),
// clone (clipboard not disturbed), paste as one undo step, snap-all, preview point set + undo.
// Plain NUnit — no game host required. EditorClipboard is tested via its internal Func<double>
// constructor so the osu-framework EditorClock component is never loaded.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestClipboard
    {
        private GarbusChart garbusChart = null!;
        private EditorChart editorChart = null!;
        private GarbusChartChangeHandler changeHandler = null!;
        private BindableBeatDivisor beatDivisor = null!;
        private EditorClipboard clipboard = null!;
        private double playheadTime;

        [SetUp]
        public void SetUp()
        {
            garbusChart = new GarbusChart();
            // BeatLength = 1000 ms (60 BPM), divisor = 4 → snap grid every 250 ms: 0, 250, 500, …
            garbusChart.ControlPointInfo!.Add(0, new TimingControlPoint { BeatLength = 1000 });

            editorChart = new EditorChart(garbusChart);
            changeHandler = new GarbusChartChangeHandler(editorChart);
            beatDivisor = new BindableBeatDivisor(4);
            playheadTime = 0;

            // Use the internal Func<double> constructor to avoid needing the full EditorClock.
            clipboard = new EditorClipboard(
                editorChart,
                () => playheadTime,
                garbusChart.ControlPointInfo,
                beatDivisor);
        }

        // ── Copy ─────────────────────────────────────────────────────────────────

        [Test]
        public void TestCopyPopulatesContent()
        {
            var note = new CardinalNote { StartTime = 1000, AngleDeg = 90 };
            editorChart.Add(note);
            editorChart.SelectedHitObjects.Add(note);

            clipboard.Copy();

            Assert.That(clipboard.Content.Value, Is.Not.Empty);
        }

        [Test]
        public void TestCopyDoesNothingWhenNothingSelected()
        {
            var note = new CardinalNote { StartTime = 1000, AngleDeg = 90 };
            editorChart.Add(note);
            // No selection — CanCopy is false.

            clipboard.Copy();

            Assert.That(clipboard.Content.Value, Is.Empty);
        }

        // ── Paste — shift math ────────────────────────────────────────────────────

        [Test]
        public void TestPasteAt4000_EarliestLandsOnSnappedPlayhead()
        {
            // Two notes: earliest at 1000, second at 1300 (300 ms relative offset).
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            var n2 = new CardinalNote { StartTime = 1300, AngleDeg = 90 };
            editorChart.Add(n1);
            editorChart.Add(n2);
            editorChart.SelectedHitObjects.Add(n1);
            editorChart.SelectedHitObjects.Add(n2);

            clipboard.Copy();

            // Playhead at 4000 ms — on the 250 ms grid, so snap = 4000.
            playheadTime = 4000;
            clipboard.Paste();

            var newObjects = editorChart.HitObjects.Where(h => h.StartTime >= 4000).ToList();
            Assert.That(newObjects, Has.Count.EqualTo(2));

            double earliest = newObjects.Min(h => h.StartTime);
            double latest = newObjects.Max(h => h.StartTime);

            Assert.That(earliest, Is.EqualTo(4000).Within(0.01),
                "Earliest pasted note must land exactly on the snapped playhead (4000)");
            Assert.That(latest, Is.EqualTo(4300).Within(0.01),
                "Relative offset of 300 ms must be preserved");
        }

        [Test]
        public void TestPasteOriginalsRemainIntact()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            var n2 = new CardinalNote { StartTime = 1300, AngleDeg = 90 };
            editorChart.Add(n1);
            editorChart.Add(n2);
            editorChart.SelectedHitObjects.Add(n1);
            editorChart.SelectedHitObjects.Add(n2);

            clipboard.Copy();
            playheadTime = 4000;
            clipboard.Paste();

            Assert.That(editorChart.HitObjects.Any(h => h.StartTime == 1000), Is.True,
                "Original n1 must still be present after paste");
            Assert.That(editorChart.HitObjects.Any(h => h.StartTime == 1300), Is.True,
                "Original n2 must still be present after paste");
        }

        [Test]
        public void TestPastedObjectsAreFreshInstances()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            editorChart.Add(n1);
            editorChart.SelectedHitObjects.Add(n1);

            clipboard.Copy();
            playheadTime = 4000;
            clipboard.Paste();

            // The pasted object must be a different reference (deep-cloned through the serializer).
            var pasted = editorChart.HitObjects.FirstOrDefault(h => h.StartTime >= 4000);
            Assert.That(pasted, Is.Not.Null);
            Assert.That(pasted, Is.Not.SameAs(n1), "Pasted object must be a fresh instance");
        }

        [Test]
        public void TestPastedSetIsSelected()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            var n2 = new CardinalNote { StartTime = 1300, AngleDeg = 90 };
            editorChart.Add(n1);
            editorChart.Add(n2);
            editorChart.SelectedHitObjects.Add(n1);
            editorChart.SelectedHitObjects.Add(n2);

            clipboard.Copy();
            playheadTime = 4000;
            clipboard.Paste();

            // Selection must now be only the two pasted objects.
            Assert.That(editorChart.SelectedHitObjects, Has.Count.EqualTo(2));
            Assert.That(editorChart.SelectedHitObjects.All(h => h.StartTime >= 4000), Is.True,
                "Selection after paste must contain only the newly pasted objects");
        }

        [Test]
        public void TestPasteSnapsPlayheadToGrid()
        {
            // Playhead at 4100 ms → nearest grid point at 4000 (grid every 250 ms).
            var n1 = new CardinalNote { StartTime = 500, AngleDeg = 0 };
            editorChart.Add(n1);
            editorChart.SelectedHitObjects.Add(n1);

            clipboard.Copy();
            playheadTime = 4100; // Off-grid; GetClosestSnappedTime(4100, 4) = 4000.
            clipboard.Paste();

            var pasted = editorChart.HitObjects.Where(h => h.StartTime != 500).ToList();
            Assert.That(pasted, Has.Count.EqualTo(1));
            Assert.That(pasted[0].StartTime, Is.EqualTo(4000).Within(0.01),
                "Paste must snap the earliest note to the nearest grid position");
        }

        // ── Cut ──────────────────────────────────────────────────────────────────

        [Test]
        public void TestCutRemovesOriginals()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            var n2 = new CardinalNote { StartTime = 1300, AngleDeg = 90 };
            editorChart.Add(n1);
            editorChart.Add(n2);
            editorChart.SelectedHitObjects.Add(n1);
            editorChart.SelectedHitObjects.Add(n2);

            clipboard.Cut();

            Assert.That(editorChart.HitObjects, Is.Empty, "Cut must remove the selected objects");
            Assert.That(clipboard.Content.Value, Is.Not.Empty, "Cut must populate clipboard content");
        }

        [Test]
        public void TestCutThenPasteRestoresObjects()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 45 };
            editorChart.Add(n1);
            editorChart.SelectedHitObjects.Add(n1);

            clipboard.Cut();
            playheadTime = 2000;
            clipboard.Paste();

            Assert.That(editorChart.HitObjects, Has.Count.EqualTo(1));
            Assert.That(editorChart.HitObjects[0].StartTime, Is.EqualTo(2000).Within(0.01));
        }

        // ── Paste is one undo step ────────────────────────────────────────────────

        [Test]
        public void TestPasteIsOneUndoStep()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            var n2 = new CardinalNote { StartTime = 1300, AngleDeg = 90 };
            editorChart.Add(n1);
            editorChart.Add(n2);
            editorChart.SelectedHitObjects.Add(n1);
            editorChart.SelectedHitObjects.Add(n2);

            clipboard.Copy();
            playheadTime = 4000;
            clipboard.Paste();

            Assert.That(editorChart.HitObjects, Has.Count.EqualTo(4));

            // One undo must remove both pasted objects at once.
            changeHandler.Undo();

            Assert.That(editorChart.HitObjects, Has.Count.EqualTo(2),
                "One undo step must remove all pasted objects together");
        }

        // ── Clone ─────────────────────────────────────────────────────────────────

        [Test]
        public void TestCloneDoesNotDisturbClipboard()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            editorChart.Add(n1);
            editorChart.SelectedHitObjects.Add(n1);

            clipboard.Copy();
            string contentBeforeClone = clipboard.Content.Value;

            // Now change selection to a different note for the clone.
            editorChart.SelectedHitObjects.Clear();
            var n2 = new CardinalNote { StartTime = 2000, AngleDeg = 45 };
            editorChart.Add(n2);
            editorChart.SelectedHitObjects.Add(n2);

            playheadTime = 3000;
            clipboard.Clone();

            Assert.That(clipboard.Content.Value, Is.EqualTo(contentBeforeClone),
                "Clone must not disturb the clipboard Content");
        }

        [Test]
        public void TestClonePastesAtPlayhead()
        {
            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            editorChart.Add(n1);
            editorChart.SelectedHitObjects.Add(n1);

            playheadTime = 3000;
            clipboard.Clone();

            Assert.That(editorChart.HitObjects, Has.Count.EqualTo(2));
            Assert.That(editorChart.HitObjects.Any(h => h.StartTime == 3000), Is.True,
                "Clone must place a copy at the snapped playhead (3000)");
            // Original must still be there.
            Assert.That(editorChart.HitObjects.Any(h => h.StartTime == 1000), Is.True,
                "Clone must not remove the original");
        }

        // ── Snap-all ──────────────────────────────────────────────────────────────

        [Test]
        public void TestSnapAllMovesOffGridNote()
        {
            // Note at 1050 ms — off the 250 ms grid (nearest grid = 1000 ms).
            var n1 = new CardinalNote { StartTime = 1050, AngleDeg = 0 };
            editorChart.Add(n1);

            // Simulate the "Snap all notes" transaction.
            editorChart.BeginChange();
            foreach (var h in editorChart.HitObjects.ToList())
            {
                h.StartTime = editorChart.ControlPointInfo.GetClosestSnappedTime(h.StartTime, beatDivisor.Value);
                editorChart.Update(h);
            }
            editorChart.EndChange();

            Assert.That(editorChart.HitObjects[0].StartTime, Is.EqualTo(1000).Within(0.01),
                "Snap-all must move the off-grid note to the nearest grid position (1000)");
        }

        [Test]
        public void TestSnapAllIsUndoable()
        {
            var n1 = new CardinalNote { StartTime = 1050, AngleDeg = 0 };
            editorChart.Add(n1);

            editorChart.BeginChange();
            foreach (var h in editorChart.HitObjects.ToList())
            {
                h.StartTime = editorChart.ControlPointInfo.GetClosestSnappedTime(h.StartTime, beatDivisor.Value);
                editorChart.Update(h);
            }
            editorChart.EndChange();

            Assert.That(editorChart.HitObjects[0].StartTime, Is.EqualTo(1000).Within(0.01));

            changeHandler.Undo();

            Assert.That(editorChart.HitObjects[0].StartTime, Is.EqualTo(1050).Within(0.01),
                "Undo after snap-all must restore original off-grid StartTime");
        }

        // ── Preview point ─────────────────────────────────────────────────────────

        [Test]
        public void TestSetPreviewPointUpdatesChart()
        {
            editorChart.BeginChange();
            editorChart.Chart.PreviewTime = 2500;
            editorChart.SaveState();
            editorChart.EndChange();

            Assert.That(editorChart.Chart.PreviewTime, Is.EqualTo(2500).Within(0.01));
        }

        [Test]
        public void TestSetPreviewPointIsUndoable()
        {
            editorChart.BeginChange();
            editorChart.Chart.PreviewTime = 1000;
            editorChart.SaveState();
            editorChart.EndChange();

            editorChart.BeginChange();
            editorChart.Chart.PreviewTime = 2500;
            editorChart.SaveState();
            editorChart.EndChange();

            Assert.That(editorChart.Chart.PreviewTime, Is.EqualTo(2500).Within(0.01));

            changeHandler.Undo();

            Assert.That(editorChart.Chart.PreviewTime, Is.EqualTo(1000).Within(0.01),
                "Undo must restore previous preview time");
        }

        [Test]
        public void TestSetPreviewPointNullIsUndoable()
        {
            Assert.That(editorChart.Chart.PreviewTime, Is.Null);

            editorChart.BeginChange();
            editorChart.Chart.PreviewTime = 3000;
            editorChart.SaveState();
            editorChart.EndChange();

            changeHandler.Undo();

            Assert.That(editorChart.Chart.PreviewTime, Is.Null,
                "Undo must restore null preview time when prior value was null");
        }

        // ── CanCut / CanCopy / CanPaste guards ───────────────────────────────────

        [Test]
        public void TestCanGuards()
        {
            Assert.That(clipboard.CanCut, Is.False, "CanCut false when no selection");
            Assert.That(clipboard.CanCopy, Is.False, "CanCopy false when no selection");
            Assert.That(clipboard.CanPaste, Is.False, "CanPaste false when clipboard empty");

            var n1 = new CardinalNote { StartTime = 1000, AngleDeg = 0 };
            editorChart.Add(n1);
            editorChart.SelectedHitObjects.Add(n1);

            Assert.That(clipboard.CanCut, Is.True, "CanCut true with selection");
            Assert.That(clipboard.CanCopy, Is.True, "CanCopy true with selection");
            Assert.That(clipboard.CanPaste, Is.False, "CanPaste still false — clipboard empty");

            clipboard.Copy();
            Assert.That(clipboard.CanPaste, Is.True, "CanPaste true after copy");
        }
    }
}
