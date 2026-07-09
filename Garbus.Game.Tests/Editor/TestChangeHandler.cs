// Tests for EditorChangeHandler / GarbusChartChangeHandler: undo/redo, transaction grouping,
// reference preservation during diff, metadata undo, state hash.
// Plain NUnit — no game host required.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Edit;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestChangeHandler
    {
        private EditorChart chart = null!;
        private GarbusChartChangeHandler handler = null!;

        [SetUp]
        public void SetUp()
        {
            chart = new EditorChart(new GarbusChart());
            handler = new GarbusChartChangeHandler(chart);
        }

        [Test]
        public void TestUndoRedoPlacement()
        {
            chart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 90 });
            Assert.That(handler.CanUndo.Value, Is.True);

            handler.Undo();
            Assert.That(chart.HitObjects, Is.Empty);
            Assert.That(handler.CanRedo.Value, Is.True);

            handler.Redo();
            Assert.That(chart.HitObjects, Has.Count.EqualTo(1));
            Assert.That(((CardinalNote)chart.HitObjects[0]).AngleDeg, Is.EqualTo(90));
        }

        [Test]
        public void TestTransactionIsOneUndoStep()
        {
            chart.BeginChange();
            chart.Add(new CardinalNote { StartTime = 0, AngleDeg = 0 });
            chart.Add(new CardinalNote { StartTime = 500, AngleDeg = 0 });
            chart.EndChange();

            handler.Undo();
            Assert.That(chart.HitObjects, Is.Empty);
        }

        [Test]
        public void TestUndoPreservesUntouchedObjects()
        {
            var keep = new CardinalNote { StartTime = 0, AngleDeg = 0 };
            chart.Add(keep);
            chart.Add(new CardinalNote { StartTime = 500, AngleDeg = 0 });
            handler.Undo(); // removes only the second add
            Assert.That(chart.HitObjects.Single(), Is.SameAs(keep)); // same reference — diff didn't recreate it
        }

        [Test]
        public void TestMetadataUndo()
        {
            chart.BeginChange();
            chart.Metadata.Title = "changed";
            chart.SaveState();
            chart.EndChange();
            handler.Undo();
            Assert.That(chart.Metadata.Title, Is.Empty);
        }

        [Test]
        public void TestStateHashChangesWithEdits()
        {
            string before = handler.CurrentStateHash;
            chart.Add(new CardinalNote { StartTime = 0, AngleDeg = 0 });
            Assert.That(handler.CurrentStateHash, Is.Not.EqualTo(before));
        }

        /// <summary>
        /// Regression: after undo, Chart.HitObjects (the serialization source) must stay in sync
        /// with editorChart.HitObjects (the live view). Prior to the write-through fix they diverged.
        /// </summary>
        [Test]
        public void TestUndoKeepsChartHitObjectsInSync()
        {
            chart.Add(new CardinalNote { StartTime = 1000, AngleDeg = 90 });
            handler.Undo();

            // Both views must be empty after undo.
            Assert.That(chart.HitObjects, Is.Empty);
            Assert.That(chart.Chart.HitObjects, Is.Empty,
                "Chart.HitObjects must stay in sync with editorChart.HitObjects after undo");
        }

        [Test]
        public void TestRedoKeepsChartHitObjectsInSync()
        {
            var note = new CardinalNote { StartTime = 1000, AngleDeg = 90 };
            chart.Add(note);
            handler.Undo();
            handler.Redo();

            // Both views must have one item after redo.
            Assert.That(chart.HitObjects, Has.Count.EqualTo(1));
            Assert.That(chart.Chart.HitObjects, Is.EqualTo(chart.HitObjects),
                "Chart.HitObjects must stay in sync with editorChart.HitObjects after redo");
        }
    }
}
