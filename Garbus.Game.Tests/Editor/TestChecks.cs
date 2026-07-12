// Pure NUnit tests for the four ICheck implementations — no game host required.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Edit.Screens.Verify;
using Garbus.Game.Edit.Screens.Verify.Checks;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestChecks
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Returns a ChartFile whose Directory resolves to an existing temp directory.
        /// The directory is created fresh for each call; the caller owns cleanup.
        /// </summary>
        private static (ChartFile chartFile, string dir) MakeSavedChartFile(GarbusChart chart)
        {
            string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            System.IO.Directory.CreateDirectory(dir);

            // Write a placeholder .garbus file so FilePath is non-null.
            string filePath = Path.Combine(dir, "test.garbus");
            File.WriteAllText(filePath, "{}");

            return (new ChartFile(chart, filePath), dir);
        }

        /// <summary>Unsaved ChartFile — Directory is null.</summary>
        private static ChartFile MakeUnsavedChartFile(GarbusChart chart) => new ChartFile(chart);

        private static CheckContext MakeContext(GarbusChart chart, ChartFile chartFile, double trackLength = 120000)
            => new CheckContext(chart, chartFile, trackLength);

        // -------------------------------------------------------------------------
        // CheckAudioPresent
        // -------------------------------------------------------------------------

        [Test]
        public void CheckAudioPresent_EmptyField_ReturnsOneIssue()
        {
            var chart = new GarbusChart();
            chart.Metadata.AudioFile = string.Empty;
            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));

            var issues = new CheckAudioPresent().Run(ctx).ToList();

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Time, Is.Null);
            Assert.That(issues[0].CheckName, Is.EqualTo("Audio Present"));
        }

        [Test]
        public void CheckAudioPresent_FilePresent_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            var (chartFile, dir) = MakeSavedChartFile(chart);

            try
            {
                // Create the audio file in the chart directory.
                string audioName = "song.ogg";
                File.WriteAllText(Path.Combine(dir, audioName), "fake-audio");
                chart.Metadata.AudioFile = audioName;

                var ctx = MakeContext(chart, chartFile);
                var issues = new CheckAudioPresent().Run(ctx).ToList();

                Assert.That(issues, Is.Empty);
            }
            finally
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void CheckAudioPresent_FileMissing_ReturnsOneIssue()
        {
            var chart = new GarbusChart();
            var (chartFile, dir) = MakeSavedChartFile(chart);

            try
            {
                chart.Metadata.AudioFile = "missing.ogg";

                var ctx = MakeContext(chart, chartFile);
                var issues = new CheckAudioPresent().Run(ctx).ToList();

                Assert.That(issues, Has.Count.EqualTo(1));
                Assert.That(issues[0].Time, Is.Null);
                Assert.That(issues[0].CheckName, Is.EqualTo("Audio Present"));
            }
            finally
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        /// <summary>
        /// Unsaved chart with a non-empty audio field: directory is null so we cannot
        /// check disk — no issue should be reported.
        /// </summary>
        [Test]
        public void CheckAudioPresent_UnsavedChartNonEmptyField_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            chart.Metadata.AudioFile = "song.ogg"; // field set, but chart not saved
            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));

            var issues = new CheckAudioPresent().Run(ctx).ToList();

            Assert.That(issues, Is.Empty);
        }

        // -------------------------------------------------------------------------
        // CheckBackgroundPresent
        // -------------------------------------------------------------------------

        [Test]
        public void CheckBackgroundPresent_EmptyField_ReturnsOneIssue()
        {
            var chart = new GarbusChart();
            chart.Metadata.BackgroundFile = string.Empty;
            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));

            var issues = new CheckBackgroundPresent().Run(ctx).ToList();

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Time, Is.Null);
            Assert.That(issues[0].CheckName, Is.EqualTo("Background Present"));
        }

        [Test]
        public void CheckBackgroundPresent_FilePresent_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            var (chartFile, dir) = MakeSavedChartFile(chart);

            try
            {
                string bgName = "background.png";
                File.WriteAllText(Path.Combine(dir, bgName), "fake-image");
                chart.Metadata.BackgroundFile = bgName;

                var ctx = MakeContext(chart, chartFile);
                var issues = new CheckBackgroundPresent().Run(ctx).ToList();

                Assert.That(issues, Is.Empty);
            }
            finally
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void CheckBackgroundPresent_FileMissing_ReturnsOneIssue()
        {
            var chart = new GarbusChart();
            var (chartFile, dir) = MakeSavedChartFile(chart);

            try
            {
                chart.Metadata.BackgroundFile = "missing.png";

                var ctx = MakeContext(chart, chartFile);
                var issues = new CheckBackgroundPresent().Run(ctx).ToList();

                Assert.That(issues, Has.Count.EqualTo(1));
                Assert.That(issues[0].Time, Is.Null);
                Assert.That(issues[0].CheckName, Is.EqualTo("Background Present"));
            }
            finally
            {
                System.IO.Directory.Delete(dir, recursive: true);
            }
        }

        [Test]
        public void CheckBackgroundPresent_UnsavedChartNonEmptyField_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            chart.Metadata.BackgroundFile = "bg.png";
            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));

            var issues = new CheckBackgroundPresent().Run(ctx).ToList();

            Assert.That(issues, Is.Empty);
        }

        // -------------------------------------------------------------------------
        // CheckObjectsBeyondTrackEnd
        // -------------------------------------------------------------------------

        /// <summary>
        /// A plain note (no duration) at 70000 ms with a 60000 ms track → 1 issue at 70000.
        /// </summary>
        [Test]
        public void CheckObjectsBeyondTrackEnd_NoteAfterEnd_ReturnsOneIssue()
        {
            var chart = new GarbusChart();
            chart.HitObjects.Add(new CardinalNote { StartTime = 70000, AngleDeg = 0 });

            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart), trackLength: 60000);
            var issues = new CheckObjectsBeyondTrackEnd().Run(ctx).ToList();

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Time, Is.EqualTo(70000));
            Assert.That(issues[0].CheckName, Is.EqualTo("Objects Beyond Track End"));
        }

        /// <summary>
        /// A hold note whose tail ends past the track → 1 issue. StartTime is before track end
        /// but EndTime is beyond.
        /// </summary>
        [Test]
        public void CheckObjectsBeyondTrackEnd_HoldNoteTailAfterEnd_ReturnsOneIssue()
        {
            var chart = new GarbusChart();
            // Hold starts at 50000, lasts 15000 → EndTime = 65000 > 60000
            chart.HitObjects.Add(new CardinalHoldNote { StartTime = 50000, AngleDeg = 0, Duration = 15000 });

            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart), trackLength: 60000);
            var issues = new CheckObjectsBeyondTrackEnd().Run(ctx).ToList();

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Time, Is.EqualTo(50000)); // issue at StartTime
            Assert.That(issues[0].CheckName, Is.EqualTo("Objects Beyond Track End"));
        }

        /// <summary>
        /// All notes within track length → no issues.
        /// </summary>
        [Test]
        public void CheckObjectsBeyondTrackEnd_AllWithinTrack_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            chart.HitObjects.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 });
            chart.HitObjects.Add(new CardinalNote { StartTime = 30000, AngleDeg = 90 });

            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart), trackLength: 60000);
            var issues = new CheckObjectsBeyondTrackEnd().Run(ctx).ToList();

            Assert.That(issues, Is.Empty);
        }

        /// <summary>
        /// Object exactly at track end is fine (not strictly greater).
        /// </summary>
        [Test]
        public void CheckObjectsBeyondTrackEnd_NoteExactlyAtEnd_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            chart.HitObjects.Add(new CardinalNote { StartTime = 60000, AngleDeg = 0 });

            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart), trackLength: 60000);
            var issues = new CheckObjectsBeyondTrackEnd().Run(ctx).ToList();

            Assert.That(issues, Is.Empty);
        }

        // -------------------------------------------------------------------------
        // CheckObjectsBeforeTimeZero
        // -------------------------------------------------------------------------

        [Test]
        public void CheckObjectsBeforeTimeZero_NegativeStartTime_ReturnsOneIssue()
        {
            var chart = new GarbusChart();
            chart.HitObjects.Add(new CardinalNote { StartTime = -100, AngleDeg = 0 });

            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));
            var issues = new CheckObjectsBeforeTimeZero().Run(ctx).ToList();

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Time, Is.EqualTo(-100));
            Assert.That(issues[0].CheckName, Is.EqualTo("Objects Before Time Zero"));
        }

        [Test]
        public void CheckObjectsBeforeTimeZero_MultipleNegative_ReturnsOneIssueEach()
        {
            var chart = new GarbusChart();
            chart.HitObjects.Add(new CardinalNote { StartTime = -500, AngleDeg = 0 });
            chart.HitObjects.Add(new CardinalNote { StartTime = -200, AngleDeg = 90 });

            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));
            var issues = new CheckObjectsBeforeTimeZero().Run(ctx).ToList();

            Assert.That(issues, Has.Count.EqualTo(2));
            Assert.That(issues[0].Time, Is.EqualTo(-500));
            Assert.That(issues[1].Time, Is.EqualTo(-200));
        }

        [Test]
        public void CheckObjectsBeforeTimeZero_AllAtOrAfterZero_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            chart.HitObjects.Add(new CardinalNote { StartTime = 0, AngleDeg = 0 });
            chart.HitObjects.Add(new CardinalNote { StartTime = 1000, AngleDeg = 90 });

            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));
            var issues = new CheckObjectsBeforeTimeZero().Run(ctx).ToList();

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void CheckObjectsBeforeTimeZero_EmptyChart_ReturnsNoIssues()
        {
            var chart = new GarbusChart();
            var ctx = MakeContext(chart, MakeUnsavedChartFile(chart));
            var issues = new CheckObjectsBeforeTimeZero().Run(ctx).ToList();
            Assert.That(issues, Is.Empty);
        }
    }
}
