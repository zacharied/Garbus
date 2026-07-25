// The minimal game loop replacing osu.Game's Player/DrawableRuleset/ScoreProcessor/HUD stack (see
// PLAN-port.md — Player is a 1.3k-line megaclass with realm/online/mod dependencies we don't want).
// Responsibilities: drive the vendored gameplay clock, host the playfield inside the action input
// manager, tally results into score/combo/accuracy, and show a summary once the chart has played out.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using Garbus.Game.Charts;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.Scoring;
using Garbus.Game.Configuration;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Objects.Drawables;
using Garbus.Game.Settings;
using Garbus.Game.Timing;
using Garbus.Game.UI;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Screens
{
    public partial class PlayScreen : Screen
    {
        /// <summary>
        /// Extra time after the last hit object's judgement window before the results overlay shows.
        /// </summary>
        private const double results_grace = 1000;

        private Track track = null!;
        private MasterGameplayClockContainer gameplayClock = null!;
        private GarbusPlayfield playfield = null!;
        private DesignOverlay designOverlay = null!;

        private GarbusChart chart = null!;
        private double chartEndTime;

        // Scoring state, reverted through JudgementResult's stored prior values on rewind.
        private readonly Dictionary<HitResult, int> resultCounts = new Dictionary<HitResult, int>();
        private long totalScore;
        private int combo;
        private int highestCombo;
        private double accuracyEarned;
        private double accuracyMax;

        private SpriteText comboText = null!;
        private SpriteText scoreText = null!;
        private SpriteText accuracyText = null!;
        private SpriteText lastJudgementText = null!;
        private FillFlowContainer resultsOverlay = null!;
        private TextFlowContainer resultsText = null!;
        private bool resultsShown;

        /// <summary>
        /// The chart file to load. Hardcoded until song select arrives in Phase 5.
        /// </summary>
        public const string DEFAULT_CHART = @"test-chart.garbus";

        // --- Editor test mode ---

        /// <summary>
        /// When non-null, this chart and track are used instead of the bundled default.
        /// Set by the editor test-mode constructor.
        /// </summary>
        private readonly PlayableChart? injectedPlayableChart;
        private readonly Track? injectedTrack;

        /// <summary>The gameplay start time passed from the editor (milliseconds). Exposed for tests.</summary>
        public double StartTime { get; }

        /// <summary>
        /// The gameplay clock time at which the player exited, or null if the screen has not yet exited.
        /// Set during <see cref="OnExiting"/> so the editor can seek back to the exit position.
        /// </summary>
        public double? ExitTime { get; private set; }

        /// <summary>The chart this screen is playing. Exposed for editor test-mode assertions.</summary>
        public GarbusChart? Chart => chart;

        public PlayableChart? PlayableChart { get; private set; }

        // ---

        /// <summary>
        /// Creates a <see cref="PlayScreen"/> that loads the bundled test chart.
        /// Used by the main menu "Play" button and <c>TestScenePlayScreen</c>.
        /// </summary>
        public PlayScreen()
        {
        }

        /// <summary>
        /// Creates a <see cref="PlayScreen"/> pre-loaded with the given chart and track, starting
        /// at the specified time. Used by song select (startTime 0) and the editor's Test mode
        /// (F5 / Test button, non-zero startTime).
        /// </summary>
        /// <param name="chart">A deep-cloned chart (no shared references with the editor).</param>
        /// <param name="track">A fresh track instance (never the editor's own track).</param>
        /// <param name="startTime">
        /// The intended gameplay start time in milliseconds — the editor playhead clamped to ≥ 0.
        /// The silent lead-in count-in is applied inside <see cref="MasterGameplayClockContainer"/>
        /// (the clock actually starts <c>LEAD_IN_TIME</c> before this), so no pre-roll offset is
        /// baked in here.
        /// </param>
        public PlayScreen(GarbusChart chart, Track track, double startTime = 0)
        {
            var bridgeSong = new GarbusSong { Charts = { chart } };
            injectedPlayableChart = new PlayableChart(
                bridgeSong,
                chart,
                chart.ControlPointInfo ?? new Charts.Timing.ControlPointInfo());
            injectedTrack = track;
            StartTime = startTime;
        }

        public PlayScreen(PlayableChart chart, Track track, double startTime = 0)
        {
            injectedPlayableChart = chart;
            injectedTrack = track;
            StartTime = startTime;
        }

        [BackgroundDependencyLoader]
        private void load(ITrackStore tracks, SongStore songs, GarbusConfigManager config)
        {
            if (injectedPlayableChart != null && injectedTrack != null)
            {
                // Editor test mode: use the pre-provided chart + track.
                PlayableChart = injectedPlayableChart;
                chart = injectedPlayableChart.Chart;
                track = injectedTrack;
            }
            else
            {
                // Normal (standalone) path: load the bundled song's first chart.
                var song = songs.Get(DEFAULT_CHART).Song;
                PlayableChart = song.CreatePlayableChart(song.Charts[0].ChartId);
                chart = PlayableChart.Chart;

                // The song references its audio by full filename (TrackStore only probes ".mp3" for
                // extension-less lookups).
                track = tracks.Get(song.Resources.Track);
            }

            chartEndTime = chart.HitObjects.Count == 0
                ? 0
                : chart.HitObjects.Max(h => h.GetEndTime() + h.MaximumJudgementOffset);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Colour = new Color4(18, 18, 26, 255),
                    RelativeSizeAxes = Axes.Both,
                },
                gameplayClock = new MasterGameplayClockContainer(track, StartTime)
                {
                    // The overlay must live inside the gameplay-clock subtree so it reads gameplay
                    // time (GameplayClockContainer sets Content.Clock), not the ambient wall clock.
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new GarbusInputManager
                            {
                                Child = playfield = new GarbusPlayfield
                                {
                                    Size = Vector2.One,
                                },
                            },
                            designOverlay = new DesignOverlay(chart)
                            {
                                RelativeSizeAxes = Axes.Both,
                            },
                            new HoldActivationDebugDisplay(playfield),
                        },
                    },
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(20),
                    Spacing = new Vector2(0, 4),
                    Children = new Drawable[]
                    {
                        scoreText = new SpriteText { Font = FontUsage.Default.With(size: 32) },
                        comboText = new SpriteText { Font = FontUsage.Default.With(size: 24) },
                        accuracyText = new SpriteText { Font = FontUsage.Default.With(size: 24) },
                        lastJudgementText = new SpriteText { Font = FontUsage.Default.With(size: 20), Colour = Color4.Gray },
                        new SpriteText
                        {
                            Text = @"space: pause   r: restart",
                            Font = FontUsage.Default.With(size: 14),
                            Colour = Color4.DimGray,
                        },
                    },
                },
                resultsOverlay = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 8),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = @"chart complete",
                            Font = FontUsage.Default.With(size: 40),
                        },
                        resultsText = new TextFlowContainer(t => t.Font = FontUsage.Default.With(size: 22))
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            AutoSizeAxes = Axes.Both,
                            TextAnchor = Anchor.TopCentre,
                        },
                    },
                },
                // Live scroll-speed control at the bottom-right. Bound to the same config value the
                // settings overlay uses, which flows into gameplay TimeRange (GarbusGameBase), so
                // dragging it retunes scroll speed in real time.
                new Container
                {
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    AutoSizeAxes = Axes.Y,
                    Width = 260,
                    Margin = new MarginPadding(20),
                    Child = new SettingsSlider("Scroll speed", config.GetBindable<double>(GarbusSetting.ScrollSpeed), ScrollSpeedMapping.FormatSpeed),
                },
            };

            foreach (var hitObject in chart.HitObjects)
                playfield.Add(CreateDrawableRepresentation(hitObject));

            playfield.SetHitObjects(chart.HitObjects);

            playfield.NewResult += onNewResult;
            playfield.ResultReverted += onRevertResult;
        }

        /// <summary>
        /// The drawable representation for each top-level hit object type, mirroring
        /// BAC's <c>DrawableBigAssCircleRuleset.CreateDrawableRepresentation</c>.
        /// </summary>
        public static DrawableHitObject CreateDrawableRepresentation(GarbusHitObject h) => h switch
        {
            SliderBody path => new DrawableSliderBody(path),
            CardinalNote button => new DrawableCardinalNote(button),
            CardinalHoldNote hold => new DrawableCardinalHoldNote(hold),
            ShoulderNote note => new DrawableShoulderNote(note),
            GarbusSlamCentered slamCentered => new DrawableSlamCentered(slamCentered),
            GarbusSlamEdge slamEdge => new DrawableSlamEdge(slamEdge),
            ShoulderHoldNote hold => new DrawableShoulderHoldNote(hold),
            _ => throw new ArgumentOutOfRangeException(nameof(h), h.GetType().Name, "no drawable representation")
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            gameplayClock.Reset(startClock: true);
        }

        protected override void Update()
        {
            base.Update();

            scoreText.Text = $"score: {totalScore:N0}";
            comboText.Text = $"combo: {combo}x   (best {highestCombo}x)";

            // Feed the centre-of-circle combo counter (drawn beneath the hit objects).
            playfield.Combo.Value = combo;
            accuracyText.Text = $"accuracy: {(accuracyMax > 0 ? accuracyEarned / accuracyMax : 1):P2}";

            if (!resultsShown && gameplayClock.CurrentTime > chartEndTime + results_grace)
                showResults();
        }

        private void onNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (!result.Type.IsScorable())
                return;

            result.ComboAtJudgement = combo;
            result.HighestComboAtJudgement = highestCombo;

            if (result.Type.BreaksCombo())
                combo = 0;
            else if (result.Type.IncreasesCombo())
                combo++;

            highestCombo = Math.Max(highestCombo, combo);
            result.ComboAfterJudgement = combo;
            result.HighestComboAfterJudgement = highestCombo;

            resultCounts[result.Type] = resultCounts.GetValueOrDefault(result.Type) + 1;
            totalScore += scoreFor(result.Type);

            if (result.Type.AffectsAccuracy())
            {
                accuracyEarned += scoreFor(result.Type);
                accuracyMax += scoreFor(result.Judgement.MaxResult);
            }

            lastJudgementText.Text = $"last: {result.Type} ({judgedObject.HitObject.GetType().Name})";
        }

        private void onRevertResult(JudgementResult result)
        {
            if (!result.Type.IsScorable())
                return;

            combo = result.ComboAtJudgement;
            highestCombo = result.HighestComboAtJudgement;

            resultCounts[result.Type] = resultCounts.GetValueOrDefault(result.Type) - 1;
            totalScore -= scoreFor(result.Type);

            if (result.Type.AffectsAccuracy())
            {
                accuracyEarned -= scoreFor(result.Type);
                accuracyMax -= scoreFor(result.Judgement.MaxResult);
            }
        }

        private static long scoreFor(HitResult result) => result switch
        {
            HitResult.CriticalPerfect => 320,
            HitResult.Perfect => 300,
            HitResult.Near => 200,
            HitResult.Bad => 100,
            _ => 0,
        };

        private void showResults()
        {
            resultsShown = true;

            string breakdown = string.Join("\n", resultCounts
                                                 .Where(kvp => kvp.Value > 0)
                                                 .OrderBy(kvp => kvp.Key.GetIndexForOrderedDisplay())
                                                 .Select(kvp => $"{kvp.Key}: {kvp.Value}"));

            resultsText.Text = $"score {totalScore:N0}   accuracy {(accuracyMax > 0 ? accuracyEarned / accuracyMax : 1):P2}   best combo {highestCombo}x\n{breakdown}";
            resultsOverlay.FadeIn(400, Easing.OutQuint);
        }

        private void hideResults()
        {
            resultsShown = false;
            resultsOverlay.FadeOut(100);
        }

        private void restart()
        {
            hideResults();
            lastJudgementText.Text = string.Empty;

            // Rewinding across judgements makes the playfield revert each result (restoring the score
            // state above), so a plain seek-to-start is a full restart.
            gameplayClock.Reset(startClock: true);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Space:
                    if (gameplayClock.IsPaused.Value)
                        gameplayClock.Start();
                    else
                        gameplayClock.Stop();
                    return true;

                case Key.R:
                    restart();
                    return true;

                case Key.Escape:
                    // Exit back to whatever pushed us (editor test mode / main menu).
                    this.Exit();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            // Record where gameplay was when the player exited so the editor can seek back there.
            ExitTime = gameplayClock?.CurrentTime;

            // The track is this screen's own instance (a fresh one per play) — stop it so audio
            // doesn't keep playing under the editor after exiting (ISSUES.md).
            track?.Stop();

            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (playfield.IsNotNull())
            {
                playfield.NewResult -= onNewResult;
                playfield.ResultReverted -= onRevertResult;
            }

            base.Dispose(isDisposing);

            // Track instances are per-Get (never shared with the store cache or the editor), so the
            // screen owns and disposes its own.
            track?.Dispose();
        }
    }
}
