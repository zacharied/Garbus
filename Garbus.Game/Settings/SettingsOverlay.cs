using System;
using System.Globalization;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// A left-anchored slide-in panel exposing master/music/hitsound volume and scroll speed.
    /// Volume rows bind to the framework <see cref="AudioManager"/> bindables (persisted by the
    /// framework config); scroll speed binds to <see cref="GarbusSetting.ScrollSpeed"/>.
    /// </summary>
    public partial class SettingsOverlay : VisibilityContainer
    {
        private const float panel_width = 350;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private Container panel = null!;

        // Guards the two-way position<->gain sync from feeding back on itself. Value changes are
        // processed synchronously on the update thread, so a single shared flag is sufficient.
        private bool syncingVolume;

        // Teardown for the volume-row subscriptions to the long-lived AudioManager bindables.
        private Action? volumeCleanup;

        public SettingsOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Alpha = 0;

            InternalChild = panel = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Width = panel_width,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(20, 20, 28, 240),
                    },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(20),
                        Spacing = new Vector2(0, 18),
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Text = "Settings",
                                Font = FontUsage.Default.With(size: 28),
                                Colour = Color4.White,
                            },
                            createVolumeRow("Master volume", audio.Volume),
                            createVolumeRow("Music volume", audio.VolumeTrack),
                            createVolumeRow("Hitsound volume", audio.VolumeSample),
                            new SettingsSlider("Scroll speed", config.GetBindable<double>(GarbusSetting.ScrollSpeed), speed),
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Builds a volume row whose slider position runs through <see cref="VolumeCurve"/> before it
        /// reaches the actual <paramref name="gain"/> bindable, so the usable low end is spread across
        /// more of the slider. The readout shows the slider position, not the raw gain.
        /// </summary>
        private SettingsSlider createVolumeRow(string label, BindableNumber<double> gain)
        {
            var position = new BindableDouble(VolumeCurve.ToPosition(gain.Value)) { MinValue = 0, MaxValue = 1 };

            void onPositionChanged(ValueChangedEvent<double> e)
            {
                if (syncingVolume) return;

                syncingVolume = true;
                gain.Value = VolumeCurve.ToGain(e.NewValue);
                syncingVolume = false;
            }

            void onGainChanged(ValueChangedEvent<double> e)
            {
                if (syncingVolume) return;

                syncingVolume = true;
                position.Value = VolumeCurve.ToPosition(e.NewValue);
                syncingVolume = false;
            }

            position.ValueChanged += onPositionChanged;
            gain.ValueChanged += onGainChanged;
            volumeCleanup += () =>
            {
                position.ValueChanged -= onPositionChanged;
                gain.ValueChanged -= onGainChanged;
            };

            return new SettingsSlider(label, position, percent);
        }

        private static string percent(double v) => $"{Math.Round(v * 100)}%";
        private static string speed(double v) => Math.Round(v).ToString(CultureInfo.InvariantCulture);

        protected override void PopIn()
        {
            panel.MoveToX(-panel_width).MoveToX(0, 500, Easing.OutQuint);
            this.FadeIn(300, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            panel.MoveToX(-panel_width, 500, Easing.OutQuint);
            this.FadeOut(300, Easing.OutQuint);
        }

        protected override bool OnClick(ClickEvent e)
        {
            // A click landing outside the panel dismisses the overlay.
            if (!panel.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                Hide();

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            volumeCleanup?.Invoke();
            base.Dispose(isDisposing);
        }
    }
}
