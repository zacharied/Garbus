// Tests for EditorChangeHandler / GarbusChartChangeHandler: undo/redo, transaction grouping,
// reference preservation during diff, metadata undo, state hash.
// Plain NUnit — no game host required.

using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
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
        public void TestUndoRedoDesignPoint()
        {
            chart.BeginChange();
            chart.DesignPointInfo.Add(new TutorialMessage { StartTime = 1000, EndTime = 3000, Text = "hi" });
            chart.SaveState();
            chart.EndChange();

            Assert.That(chart.DesignPointInfo.DesignPoints, Has.Count.EqualTo(1));
            Assert.That(handler.CanUndo.Value, Is.True);

            handler.Undo();
            Assert.That(chart.DesignPointInfo.DesignPoints, Is.Empty);

            handler.Redo();
            Assert.That(chart.DesignPointInfo.DesignPoints, Has.Count.EqualTo(1));
            var tm = (TutorialMessage)chart.DesignPointInfo.DesignPoints[0];
            Assert.That(tm.StartTime, Is.EqualTo(1000));
            Assert.That(tm.EndTime, Is.EqualTo(3000));
            Assert.That(tm.Text, Is.EqualTo("hi"));
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

        [Test]
        public void TestWholeSongMetadataUndoRedo()
        {
            var song = GarbusSong.CreateDefault();
            var editorSong = new EditorSong(song);
            var editorChart = new EditorChart(editorSong.ActiveChart, editorSong.EffectiveControlPointInfo);
            var songHandler = new GarbusChartChangeHandler(editorSong, editorChart);

            songHandler.BeginChange();
            song.Metadata.Title = "Changed";
            editorSong.SaveState();
            songHandler.EndChange();

            songHandler.Undo();
            Assert.That(song.Metadata.Title, Is.Empty);
            songHandler.Redo();
            Assert.That(song.Metadata.Title, Is.EqualTo("Changed"));
        }

        [Test]
        public void TestWholeSongChartAddAndTimingOwnershipUndo()
        {
            var song = GarbusSong.CreateDefault();
            var editorSong = new EditorSong(song);
            var editorChart = new EditorChart(editorSong.ActiveChart, editorSong.EffectiveControlPointInfo);
            var songHandler = new GarbusChartChangeHandler(editorSong, editorChart);

            editorSong.AddChart();
            Assert.That(song.Charts, Has.Count.EqualTo(2));
            songHandler.Undo();
            Assert.That(song.Charts, Has.Count.EqualTo(1));
            songHandler.Redo();
            Assert.That(song.Charts, Has.Count.EqualTo(2));

            editorSong.UsePerChartTiming();
            Assert.That(song.ControlPointInfo, Is.Null);
            songHandler.Undo();
            Assert.Multiple(() =>
            {
                Assert.That(song.ControlPointInfo, Is.Not.Null);
                Assert.That(song.Charts.All(c => c.ControlPointInfo == null), Is.True);
            });
        }
    }
}
