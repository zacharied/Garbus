// The Timing tab's waveform comparison display: one waveform slice per beat, stacked vertically
// under a centre playhead (osu's WaveformComparisonDisplay shape).
//
// Regression guard: this element used to be a placeholder that drew eight vertical coloured bars and
// never touched a WaveformGraph at all — the header claimed WaveformGraph needed osu.Game's
// WorkingBeatmap, when it is an osu-framework drawable fed by an osu.Framework.Audio.Track.Waveform.
// The tests below pin what that placeholder could not do: real waveform content in every row, rows
// tiling the display as horizontal slices, and consecutive rows sitting exactly one beat apart.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Garbus.Game.Charts;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Timing;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneWaveformComparison : GarbusTestScene
    {
        [Resolved]
        private Storage storage { get; set; } = null!;

        private GarbusEditor editor = null!;
        private SongFile songFile = null!;

        /// <summary>The seeded song runs at 120 BPM, so one beat is 500ms.</summary>
        private const double beat_length = 500;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("open editor on a song with audio", () =>
            {
                songFile = TestSongWithAudio.Create(storage, "waveform-comparison-test", beat_length);
                editor = new GarbusEditor(songFile);
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both };
            });

            AddUntilStep("editor loaded", () => editor.IsLoaded);
            AddStep("switch to Timing tab", () => editor.Tab.Value = EditorTab.Timing);
            AddUntilStep("comparison display loaded", () =>
                editor.ChildrenOfType<WaveformComparisonDisplay>().Any()
                && display().IsLoaded);
            AddUntilStep("track length known", () =>
                editor.ChildrenOfType<EditorClock>().First().TrackLength > 0);
        }

        private WaveformComparisonDisplay display() =>
            editor.ChildrenOfType<WaveformComparisonDisplay>().Single();

        private List<WaveformGraph> graphs() =>
            display().ChildrenOfType<WaveformGraph>().ToList();

        /// <summary>The rows in beat order — top row is the earliest beat.</summary>
        private List<Drawable> rowsTopToBottom() =>
            graphs().Select(g => (Drawable)g.Parent!)
                    .OrderBy(r => r.ScreenSpaceDrawQuad.TopLeft.Y)
                    .ToList();

        [Test]
        public void TestEveryRowRendersTheSongWaveform()
        {
            AddUntilStep("every row has a waveform", () =>
                graphs().Count > 0 && graphs().All(g => g.Waveform != null));

            // Decoding is expensive in CPU and memory, so the rows share one instance rather than
            // each decoding the track for itself.
            AddAssert("all rows share one decoded waveform", () =>
                graphs().Select(g => g.Waveform).Distinct().Count() == 1);

            // A non-null Waveform only proves the wiring; decoding happens on a background task, so
            // read its points back to confirm the seeded audio actually produced samples to draw.
            Task<Waveform.Point[]> points = null!;

            AddStep("request decoded points", () => points = graphs().First().Waveform!.GetPointsAsync());
            AddUntilStep("decode finished", () => points.IsCompleted);
            AddAssert("waveform has sample points", () => points.Result.Length > 0);
        }

        [Test]
        public void TestRowsTileTheDisplayAsHorizontalSlices()
        {
            // The placeholder drew full-height vertical bars side by side; the osu shape is
            // full-width horizontal slices stacked top to bottom with no gap and no overlap.
            AddUntilStep("rows span the full width", () =>
            {
                var quad = display().ScreenSpaceDrawQuad.AABBFloat;
                return rowsTopToBottom().Count > 1
                       && rowsTopToBottom().All(r =>
                           r.ScreenSpaceDrawQuad.AABBFloat.Width >= quad.Width - 1);
            });

            AddAssert("rows stack without gap or overlap", () =>
            {
                var rows = rowsTopToBottom();

                for (int i = 1; i < rows.Count; i++)
                {
                    float previousBottom = rows[i - 1].ScreenSpaceDrawQuad.AABBFloat.Bottom;
                    float top = rows[i].ScreenSpaceDrawQuad.AABBFloat.Top;

                    if (Math.Abs(top - previousBottom) > 1)
                        return false;
                }

                return true;
            });

            AddAssert("rows cover the display top to bottom", () =>
            {
                var rows = rowsTopToBottom();
                var quad = display().ScreenSpaceDrawQuad.AABBFloat;

                return Math.Abs(rows.First().ScreenSpaceDrawQuad.AABBFloat.Top - quad.Top) <= 1
                       && Math.Abs(rows.Last().ScreenSpaceDrawQuad.AABBFloat.Bottom - quad.Bottom) <= 1;
            });
        }

        [Test]
        public void TestConsecutiveRowsAreOneBeatApart()
        {
            // Each row shows a window of `visible_window` ms and the graph's X is relative to the row
            // width, so stepping one beat between rows shifts the graph by beat_length / visible_window
            // row-widths. At 500ms beats in a 250ms window that is exactly 2 row-widths, leftwards
            // (later beats sit further into the track, so the graph slides left).
            const float visible_window = 250;
            const float expected_step = (float)beat_length / visible_window;

            AddStep("narrow the window to 250ms", () => display().VisibleWidth = visible_window);

            AddUntilStep("rows step one beat each, leftwards", () =>
            {
                var offsets = rowsTopToBottom()
                              .Select(r => r.ChildrenOfType<WaveformGraph>().Single().X)
                              .ToList();

                if (offsets.Count < 2)
                    return false;

                for (int i = 1; i < offsets.Count; i++)
                {
                    float step = offsets[i - 1] - offsets[i];

                    if (Math.Abs(step - expected_step) > 0.01f)
                        return false;
                }

                return true;
            });
        }

        [Test]
        public void TestRowsAreLabelledFromFourBeatsBeforeThePlayhead()
        {
            // Eight rows centred on the playhead's beat means the labels run from four beats before
            // it to three after. Seeking mid-beat (beat 4 spans 2000-2500ms) keeps the expectation
            // clear of any rounding at the beat boundary.
            AddStep("seek into beat 4", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(2250));

            AddUntilStep("labels run 0 through 7", () =>
            {
                var labels = rowsTopToBottom()
                             .Select(r => r.ChildrenOfType<SpriteText>().Single().Text.ToString())
                             .ToList();

                return labels.SequenceEqual(new[] { "0", "1", "2", "3", "4", "5", "6", "7" });
            });
        }
    }
}
