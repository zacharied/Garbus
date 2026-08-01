using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Input;
using Garbus.Game.UI;
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
    /// frame-limiter and screen-mode display settings, grouped into Audio / Graphics / Gameplay
    /// sections under a scrollable view. Volume rows bind to the framework <see cref="AudioManager"/>
    /// bindables (persisted by the framework config); scroll speed binds to
    /// <see cref="GarbusSetting.ScrollSpeed"/>; the display rows bind straight to
    /// <see cref="FrameworkConfigManager"/>.
    ///
    /// A single <see cref="SettingsPanelHeader"/> floats over the scrolling content and is retargeted
    /// between the settings view and the controls sub-view, so rows pass beneath it and pick up its
    /// drop shadow.
    /// </summary>
    public partial class SettingsOverlay : VisibilityContainer
    {
        private const float panel_width = 350;
        private const float content_side_padding = 20;
        private const float content_bottom_padding = 40;

        // Clearance between the header's bottom edge and the first row, so the header's drop shadow
        // falls on empty panel rather than on the top row while the view sits unscrolled.
        private const float content_header_gap = 16;

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
        private Container contentArea = null!;
        private SettingsPanelHeader header = null!;

        private BasicScrollContainer settingsScroll = null!;
        private Container settingsContentPadding = null!;
        private BasicScrollContainer? controlsScroll;

        // Sits just right of the sliding panel; shown only while the Controls sub-view is up.
        private ButtonTestPanel buttonTestPanel = null!;

        // Teardown for the volume-row subscriptions to the long-lived AudioManager bindables.
        private Action? volumeCleanup;

        /// <summary>
        /// The floating header's height, and with it the top padding that keeps the first row clear of
        /// the header at rest. The controls sub-view reads this when it is next opened.
        /// </summary>
        public float HeaderHeight
        {
            get => header.Height;
            set
            {
                header.Height = value;
                settingsContentPadding.Padding = contentPadding(value);
            }
        }

        public SettingsOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Alpha = 0;

            // Front-first so an open dropdown menu in one section pops over the sections below it
            // rather than being drawn underneath them — see FrontFirstFillFlowContainer.
            var settingsView = new FrontFirstFillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Padding = new MarginPadding { Horizontal = content_side_padding },
                Spacing = new Vector2(0, 24),
                Children = buildSections(),
            };

            header = new SettingsPanelHeader();

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
                    // Keeps the header's drop shadow from spilling out past the panel edges.
                    Masking = true,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(20, 20, 28, 240),
                        },
                        contentArea = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Child = settingsScroll = createScroll("settings scroll", settingsView, out settingsContentPadding),
                        },
                        // Added last so it draws — and casts its shadow — over the scrolling content.
                        header,
                    },
                },
            };
        }

        private MarginPadding contentPadding(float headerHeight) => new MarginPadding
        {
            Top = headerHeight + content_header_gap,
            Bottom = content_bottom_padding,
        };

        /// <summary>
        /// Wraps <paramref name="content"/> in a full-height scroll container. The content sits inside
        /// a padding wrapper rather than the scroll container being inset, so rows scroll underneath
        /// the floating header instead of stopping short of it.
        /// </summary>
        private BasicScrollContainer createScroll(string name, Drawable content, out Container padding)
        {
            padding = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = contentPadding(header.Height),
                Child = content,
            };

            return new BasicScrollContainer
            {
                Name = name,
                RelativeSizeAxes = Axes.Both,
                ScrollbarVisible = true,
                Child = padding,
            };
        }

        /// <summary>
        /// The settings sections in display order. The screen-mode row is left out where the platform
        /// offers a single window mode (mobile is fullscreen-only), since it would present no choice.
        /// </summary>
        private List<Drawable> buildSections()
        {
            var graphicsRows = new List<Drawable>
            {
                new SettingsEnumDropdown<FrameSync>("Frame limiter",
                    frameworkConfig.GetBindable<FrameSync>(FrameworkSetting.FrameSync)),
            };

            // A headless host has no window at all; fall back to every mode so tests still get the row.
            var windowModes = (host.Window?.SupportedWindowModes ?? Enum.GetValues<WindowMode>()).ToArray();

            if (windowModes.Length > 1)
            {
                graphicsRows.Add(new SettingsEnumDropdown<WindowMode>("Screen mode",
                    frameworkConfig.GetBindable<WindowMode>(FrameworkSetting.WindowMode), windowModes));
            }

            return new List<Drawable>
            {
                new SettingsSection("Audio",
                    createVolumeRow("Master volume", audio.Volume),
                    createVolumeRow("Music volume", audio.VolumeTrack),
                    createVolumeRow("Hitsound volume", audio.VolumeSample)),
                new SettingsSection("Graphics", graphicsRows.ToArray()),
                new SettingsSection("Gameplay",
                    new SettingsSlider("Scroll speed",
                        config.GetBindable<double>(GarbusSetting.ScrollSpeed), ScrollSpeedMapping.FormatSpeed),
                    new ControlsButton(showControls)),
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
            settingsScroll.Hide();

            controlsScroll?.Expire();
            contentArea.Add(controlsScroll = createScroll("controls scroll", new ControlsPanel(keyBindings), out _));

            header.ShowAs("Controls", FontAwesome.Solid.ChevronLeft, showSettings);

            buttonTestPanel.FadeIn(200, Easing.OutQuint);
        }

        private void showSettings()
        {
            controlsScroll?.Expire();
            controlsScroll = null;
            settingsScroll.Show();

            header.ShowAs("Settings", FontAwesome.Solid.Times, Hide);

            buttonTestPanel.FadeOut(200, Easing.OutQuint);
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

            settingsScroll.ScrollToStart(false);

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
