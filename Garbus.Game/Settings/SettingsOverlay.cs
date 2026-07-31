using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Input;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// A left-anchored slide-in panel exposing master/music/hitsound volume, scroll speed, and the
    /// frame-limiter and screen-mode display settings. Volume rows bind to the framework
    /// <see cref="AudioManager"/> bindables (persisted by the framework config); scroll speed binds to
    /// <see cref="GarbusSetting.ScrollSpeed"/>; the display rows bind straight to
    /// <see cref="FrameworkConfigManager"/>.
    /// </summary>
    public partial class SettingsOverlay : VisibilityContainer
    {
        private const float panel_width = 350;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [Resolved]
        private KeyBindingStore keyBindings { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private Container panel = null!;
        private FillFlowContainer settingsView = null!;
        private ControlsPanel? controlsView;

        // Sits just right of the sliding panel; shown only while the Controls sub-view is up.
        private ButtonTestPanel buttonTestPanel = null!;

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

            InternalChildren = new Drawable[]
            {
                buttonTestPanel = new ButtonTestPanel
                {
                    X = panel_width + 12,
                    Y = 12,
                    Alpha = 0,
                },
                panel = new Container
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
                    settingsView = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(20),
                        Spacing = new Vector2(0, 18),
                        Children = buildSettingsRows(),
                    },
                    },
                },
            };
        }

        /// <summary>
        /// The settings-view rows in display order. The screen-mode row is left out where the platform
        /// offers a single window mode (mobile is fullscreen-only), since it would present no choice.
        /// </summary>
        private List<Drawable> buildSettingsRows()
        {
            var rows = new List<Drawable>
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        new LeaveButton(Hide)
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = "Settings",
                            Font = FontUsage.Default.With(size: 28),
                            Colour = Color4.White,
                        },
                    },
                },
                createVolumeRow("Master volume", audio.Volume),
                createVolumeRow("Music volume", audio.VolumeTrack),
                createVolumeRow("Hitsound volume", audio.VolumeSample),
                new SettingsSlider("Scroll speed", config.GetBindable<double>(GarbusSetting.ScrollSpeed), ScrollSpeedMapping.FormatSpeed),
                new SettingsEnumDropdown<FrameSync>("Frame limiter", frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync)),
            };

            // A headless host has no window at all; fall back to every mode so tests still get the row.
            var windowModes = (host.Window?.SupportedWindowModes ?? Enum.GetValues<WindowMode>()).ToArray();

            if (windowModes.Length > 1)
            {
                rows.Add(new SettingsEnumDropdown<WindowMode>("Screen mode",
                    frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode), windowModes));
            }

            rows.Add(new ControlsButton(showControls));

            return rows;
        }

        /// <summary>
        /// Builds a volume row whose slider position runs through <see cref="VolumeCurve"/> before it
        /// reaches the actual <paramref name="gain"/> bindable, so the usable low end is spread across
        /// more of the slider. The readout shows the slider position, not the raw gain.
        /// </summary>
        private SettingsSlider createVolumeRow(string label, BindableNumber<double> gain)
        {
            var position = new BindableDouble(VolumeCurve.ToPosition(gain.Value)) { MinValue = 0, MaxValue = 1 };

            // Per-row guard so the two-way position<->gain sync can't feed back on itself. Kept local
            // to this row so the three rows stay fully independent even if they ever become coupled.
            bool syncing = false;

            void onPositionChanged(ValueChangedEvent<double> e)
            {
                if (syncing) return;

                syncing = true;
                gain.Value = VolumeCurve.ToGain(e.NewValue);
                syncing = false;
            }

            void onGainChanged(ValueChangedEvent<double> e)
            {
                if (syncing) return;

                syncing = true;
                position.Value = VolumeCurve.ToPosition(e.NewValue);
                syncing = false;
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

        private void showControls()
        {
            settingsView.Hide();

            controlsView?.Expire();
            panel.Add(controlsView = new ControlsPanel(keyBindings, showSettings));

            buttonTestPanel.FadeIn(200, Easing.OutQuint);
        }

        private void showSettings()
        {
            controlsView?.Expire();
            controlsView = null;
            settingsView.Show();

            buttonTestPanel.FadeOut(200, Easing.OutQuint);
        }

        // An icon button beside the title that dismisses the overlay.
        internal partial class LeaveButton : CompositeDrawable
        {
            private readonly Action onClick;

            public LeaveButton(Action onClick)
            {
                this.onClick = onClick;

                Size = new Vector2(28);
                CornerRadius = 6;
                Masking = true;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(16),
                        Icon = FontAwesome.Solid.Times,
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                onClick();
                return true;
            }
        }

        // A labelled row that opens the controls sub-view.
        private partial class ControlsButton : CompositeDrawable
        {
            private readonly Action onClick;

            public ControlsButton(Action onClick)
            {
                this.onClick = onClick;
                RelativeSizeAxes = Axes.X;
                Height = 30;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Padding = new MarginPadding { Left = 8 },
                        Text = "Controls…",
                        Font = FontUsage.Default.With(size: 18),
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                onClick();
                return true;
            }
        }

        protected override void PopIn()
        {
            // Always open on the settings view, never the controls sub-view.
            showSettings();

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
            // A click landing outside the panel (and the button-test panel beside it) dismisses the overlay.
            if (!panel.ReceivePositionalInputAt(e.ScreenSpaceMousePosition)
                && !buttonTestPanel.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
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
