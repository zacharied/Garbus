using Garbus.Game.Configuration;
using Garbus.Game.Timing;
using osu.Framework.Allocation;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game
{
    /// <summary>
    /// Phase-1 walking skeleton: plays a track through the vendored gameplay clock stack and
    /// displays live timing/offset state, proving audio + clock parity with osu.
    /// </summary>
    public partial class MainScreen : Screen
    {
        private Track track = null!;
        private MasterGameplayClockContainer gameplayClock = null!;

        private SpriteText timeText = null!;
        private SpriteText trackText = null!;
        private SpriteText offsetText = null!;
        private SpriteText stateText = null!;
        private TextFlowContainer snapshotText = null!;

        private Bindable<double> userAudioOffset = null!;

        [BackgroundDependencyLoader]
        private void load(ITrackStore tracks, GarbusConfigManager config)
        {
            userAudioOffset = config.GetBindable<double>(GarbusSetting.AudioOffset);

            track = tracks.Get(@"sample-track");

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Colour = new Color4(30, 30, 40, 255),
                    RelativeSizeAxes = Axes.Both,
                },
                gameplayClock = new MasterGameplayClockContainer(track, 0),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding(20),
                    Spacing = new osuTK.Vector2(0, 6),
                    Children = new Drawable[]
                    {
                        new SpriteText { Text = @"Garbus — clock stack walking skeleton", Font = FontUsage.Default.With(size: 32) },
                        new SpriteText
                        {
                            Text = @"space: play/pause   r: reset   left/right: seek 2s   up/down: user offset 5ms",
                            Font = FontUsage.Default.With(size: 18),
                            Colour = Color4.Gray,
                        },
                        timeText = new SpriteText { Font = FontUsage.Default.With(size: 24) },
                        trackText = new SpriteText { Font = FontUsage.Default.With(size: 24) },
                        offsetText = new SpriteText { Font = FontUsage.Default.With(size: 24) },
                        stateText = new SpriteText { Font = FontUsage.Default.With(size: 24) },
                        snapshotText = new TextFlowContainer(t => t.Font = FontUsage.Default.With(size: 16))
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Colour = Color4.LightGray,
                            Margin = new MarginPadding { Top = 12 },
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Walking-skeleton behaviour: begin playing immediately.
            gameplayClock.Reset(startClock: true);
        }

        protected override void Update()
        {
            base.Update();

            timeText.Text = $"gameplay time: {gameplayClock.CurrentTime:N1} ms";
            trackText.Text = $"raw track time: {track.CurrentTime:N1} / {track.Length:N0} ms";
            offsetText.Text = $"user offset: {userAudioOffset.Value:+0.#;-0.#;0} ms   total applied offset: {gameplayClock.TotalAppliedOffset:+0.#;-0.#;0} ms";
            stateText.Text = $"running: {gameplayClock.IsRunning}   paused: {gameplayClock.IsPaused.Value}   rate: {gameplayClock.Rate:N2}   rewinding: {gameplayClock.IsRewinding}";
            snapshotText.Text = gameplayClock.GetClockSnapshot();
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
                    gameplayClock.Reset();
                    return true;

                case Key.Left:
                    gameplayClock.Seek(gameplayClock.CurrentTime - 2000);
                    return true;

                case Key.Right:
                    gameplayClock.Seek(gameplayClock.CurrentTime + 2000);
                    return true;

                case Key.Up:
                    userAudioOffset.Value += 5;
                    return true;

                case Key.Down:
                    userAudioOffset.Value -= 5;
                    return true;
            }

            return base.OnKeyDown(e);
        }
    }
}
