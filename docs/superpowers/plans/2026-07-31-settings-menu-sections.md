# Scrollable Sectioned Settings Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the settings overlay into a scrollable menu whose rows are grouped into Audio /
Graphics / Gameplay sections, with the title and dismiss button floating above the content in a
distinct colour, casting a drop shadow onto the rows scrolling beneath.

**Architecture:** `SettingsOverlay.panel` gains three layers — background Box, a `contentArea`
holding a full-height `BasicScrollContainer`, and a `SettingsPanelHeader` added last so it draws on
top. The scroll container spans the whole panel and its content carries top padding equal to the
header height, so rows scroll *under* the header rather than stopping below it. One shared header is
retargeted between the settings view and the controls sub-view.

**Tech Stack:** C#, osu-framework (`BasicScrollContainer`, `EdgeEffectParameters`,
`FillFlowContainer`), NUnit visual test scenes.

## Global Constraints

- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- **Do not add new warnings — including in tests.** No unused fields, no unused usings.
- **Bare styling values are never asserted in tests** (colours, glyph icons, alphas, layout offsets).
  Assert relations instead.
- **New visual elements ship with a Tuning scene** in `Garbus.Game.Tests/Tuning/`.
- Nullability is enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- No historical context in docs — present tense, no version bumps, no compatibility layers.
- Update `docs/agents/screens.md` as the work lands.

---

### Task 1: `SettingsSection`

**Files:**
- Create: `Garbus.Game/Settings/SettingsSection.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `public partial class SettingsSection : CompositeDrawable` with constructor
  `SettingsSection(string title, params Drawable[] rows)`, `Name` set to `title`, and settable
  `Color4 LabelColour` / `Color4 DividerColour` properties for the tuning scene.

- [ ] **Step 1: Write the file**

```csharp
using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// A labelled group of settings rows: an uppercase section title over a divider rule, then the
    /// rows themselves. Located by <see cref="Drawable.Name"/> (the title), never by layout position.
    /// </summary>
    public partial class SettingsSection : CompositeDrawable
    {
        private static readonly Color4 default_label_colour = new Color4(150, 150, 175, 255);
        private static readonly Color4 default_divider_colour = new Color4(90, 90, 115, 120);

        private readonly SpriteText label;
        private readonly Box divider;

        /// <summary>The section title's colour. Exposed so the tuning scene can drive it live.</summary>
        public Color4 LabelColour
        {
            get => label.Colour;
            set => label.Colour = value;
        }

        /// <summary>The divider rule's colour. Exposed so the tuning scene can drive it live.</summary>
        public Color4 DividerColour
        {
            get => divider.Colour;
            set => divider.Colour = value;
        }

        public SettingsSection(string title, params Drawable[] rows)
        {
            ArgumentNullException.ThrowIfNull(title);

            Name = title;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 14),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 5),
                        Children = new Drawable[]
                        {
                            label = new SpriteText
                            {
                                Text = title.ToUpperInvariant(),
                                Font = FontUsage.Default.With(size: 14),
                                Colour = default_label_colour,
                            },
                            divider = new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 1,
                                Colour = default_divider_colour,
                            },
                        },
                    },
                },
            };

            foreach (var row in rows)
                flow.Add(row);

            InternalChild = flow;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: succeeds, no new warnings.

- [ ] **Step 3: Commit**

```bash
git add Garbus.Game/Settings/SettingsSection.cs
git commit -m "feat: add a labelled settings section container"
```

---

### Task 2: `SettingsPanelHeader`

**Files:**
- Create: `Garbus.Game/Settings/SettingsPanelHeader.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public const string ActionButtonName = "settings header action"` — how tests locate the button.
  - `public LocalisableString Title { get; }` — the current title text.
  - `public void ShowAs(string title, IconUsage icon, Action onClick)` — retargets the header.
  - Settable `Color4 BackgroundColour`, `Color4 ShadowColour`, `float ShadowRadius`,
    `float ShadowOffsetY` for the tuning scene.

Children are built in the constructor, not `[BackgroundDependencyLoader]`, so `ShowAs` works before
the drawable loads (`SettingsOverlay.PopIn` may run early).

- [ ] **Step 1: Write the file**

```csharp
using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// The bar floating over the top of the settings panel: an icon button and a title, on an opaque
    /// background a shade lighter than the panel, casting a drop shadow onto the rows that scroll
    /// beneath it. One instance is shared by the settings view and the controls sub-view — call
    /// <see cref="ShowAs"/> to retarget it.
    /// </summary>
    public partial class SettingsPanelHeader : CompositeDrawable
    {
        /// <summary>
        /// The action button's <see cref="Drawable.Name"/>. Tests locate the button by this rather
        /// than by its glyph (cosmetic) or its type (which would mean widening visibility for tests).
        /// </summary>
        public const string ActionButtonName = "settings header action";

        private static readonly Color4 default_background_colour = new Color4(34, 34, 48, 255);

        private readonly Box background;
        private readonly SpriteText titleText;
        private readonly ActionButton actionButton;

        /// <summary>The title currently displayed.</summary>
        public LocalisableString Title => titleText.Text;

        public Color4 BackgroundColour
        {
            get => background.Colour;
            set => background.Colour = value;
        }

        public Color4 ShadowColour
        {
            get => EdgeEffect.Colour;
            set
            {
                var effect = EdgeEffect;
                effect.Colour = value;
                EdgeEffect = effect;
            }
        }

        public float ShadowRadius
        {
            get => EdgeEffect.Radius;
            set
            {
                var effect = EdgeEffect;
                effect.Radius = value;
                EdgeEffect = effect;
            }
        }

        public float ShadowOffsetY
        {
            get => EdgeEffect.Offset.Y;
            set
            {
                var effect = EdgeEffect;
                effect.Offset = new Vector2(0, value);
                EdgeEffect = effect;
            }
        }

        public SettingsPanelHeader()
        {
            RelativeSizeAxes = Axes.X;
            Height = 56;

            // EdgeEffect only renders on a masking drawable.
            Masking = true;
            EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Shadow,
                Colour = new Color4(0, 0, 0, 140),
                Radius = 12,
                Offset = new Vector2(0, 3),
            };

            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = default_background_colour,
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Y,
                    AutoSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
                    Padding = new MarginPadding { Left = 20 },
                    Children = new Drawable[]
                    {
                        actionButton = new ActionButton
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                        },
                        titleText = new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 28),
                            Colour = Color4.White,
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Points the header at a view: its title, and the icon and action of the button beside it.
        /// </summary>
        public void ShowAs(string title, IconUsage icon, Action onClick)
        {
            titleText.Text = title;
            actionButton.SetAction(icon, onClick);
        }

        // The icon button at the left of the header. Dismisses the overlay on the settings view and
        // returns from the sub-view on the controls view — whichever action ShowAs last handed it.
        private partial class ActionButton : CompositeDrawable
        {
            private readonly SpriteIcon icon;

            private Action? onClick;

            public ActionButton()
            {
                Name = ActionButtonName;

                Size = new Vector2(28);
                CornerRadius = 6;
                Masking = true;

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    icon = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(16),
                        Colour = Color4.White,
                    },
                };
            }

            public void SetAction(IconUsage newIcon, Action action)
            {
                icon.Icon = newIcon;
                onClick = action;
            }

            protected override bool OnClick(ClickEvent e)
            {
                onClick?.Invoke();
                return true;
            }
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: succeeds, no new warnings.

- [ ] **Step 3: Commit**

```bash
git add Garbus.Game/Settings/SettingsPanelHeader.cs
git commit -m "feat: add the floating settings panel header"
```

---

### Task 3: Rewire `SettingsOverlay` and `ControlsPanel`

**Files:**
- Modify: `Garbus.Game/Settings/SettingsOverlay.cs` (whole-file restructure)
- Modify: `Garbus.Game/Settings/ControlsPanel.cs` (drop its inline back link and title)

**Interfaces:**
- Consumes: `SettingsSection(string, params Drawable[])`, `SettingsPanelHeader.ShowAs(string, IconUsage, Action)`,
  `SettingsPanelHeader.ActionButtonName`, `SettingsPanelHeader.Title`.
- Produces:
  - `public const string SettingsScrollName = "settings scroll"` on `SettingsOverlay` — how tests find
    the settings scroll container without catching the scroll containers inside dropdown menus.
  - `public float HeaderHeight { get; set; }` on `SettingsOverlay` — sets the header height and the
    matching content top padding together.
  - `ControlsPanel(KeyBindingStore store)` — the `onBack` constructor parameter is gone; the shared
    header owns that affordance now.

- [ ] **Step 1: Rewrite `SettingsOverlay.cs`**

```csharp
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
        /// <summary>
        /// The settings scroll container's <see cref="Drawable.Name"/>. Dropdown menus bring their own
        /// <see cref="BasicScrollContainer"/>s, so tests match on this rather than on type alone.
        /// </summary>
        public const string SettingsScrollName = "settings scroll";

        private const float panel_width = 350;
        private const float content_side_padding = 20;
        private const float content_bottom_padding = 40;

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

            var settingsView = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
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
                            Child = settingsScroll = createScroll(SettingsScrollName, settingsView, out settingsContentPadding),
                        },
                        // Added last so it draws — and casts its shadow — over the scrolling content.
                        header,
                    },
                },
            };
        }

        private MarginPadding contentPadding(float headerHeight) => new MarginPadding
        {
            Top = headerHeight,
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

            header.ShowAs("Settings", FontAwesome.Solid.SignOutAlt, Hide);

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
```

Note the settings flow's own side padding: `SettingsSection` rows are relative-width, so the flow
needs horizontal padding. Add it on the `settingsView` flow by setting
`Padding = new MarginPadding { Horizontal = content_side_padding }` in the `settingsView`
initialiser above (alongside `Spacing`).

- [ ] **Step 2: Trim `ControlsPanel.cs`**

Replace the whole file with:

```csharp
// The rebind sub-view: one KeyBindingRow per GarbusAction and a Reset-to-defaults button. The title
// and the back affordance live on the shared SettingsPanelHeader, not here. Given the store by its
// host (SettingsOverlay) so it stays test-constructible without DI.

using System;
using Garbus.Game.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    public partial class ControlsPanel : CompositeDrawable
    {
        private readonly KeyBindingStore store;

        public ControlsPanel(KeyBindingStore store)
        {
            this.store = store;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding { Horizontal = 20 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            foreach (GarbusAction action in Enum.GetValues<GarbusAction>())
                flow.Add(new KeyBindingRow(store, action));

            flow.Add(new ClickableText("Reset to defaults", store.ResetToDefaults));

            InternalChild = flow;
        }

        // A minimal text button: a label that runs an action on click.
        private partial class ClickableText : CompositeDrawable
        {
            private readonly string label;
            private readonly Action action;

            public ClickableText(string label, Action action)
            {
                this.label = label;
                this.action = action;

                RelativeSizeAxes = Axes.X;
                Height = 28;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = FontUsage.Default.With(size: 15),
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                action();
                return true;
            }
        }
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: succeeds, no new warnings. `TestSceneSettingsOverlay` will not compile yet — that is
Task 4. If the tests project is in the solution filter and blocks the build, proceed to Task 4 and
build both together.

- [ ] **Step 4: Commit**

```bash
git add Garbus.Game/Settings/SettingsOverlay.cs Garbus.Game/Settings/ControlsPanel.cs
git commit -m "feat: scroll the settings menu under a floating sectioned header"
```

---

### Task 4: Update `TestSceneSettingsOverlay`

**Files:**
- Modify: `Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs`

**Interfaces:**
- Consumes: `SettingsOverlay.SettingsScrollName`, `SettingsPanelHeader.ActionButtonName`,
  `SettingsPanelHeader.Title`, `SettingsSection`.
- Produces: nothing.

The scene now constrains the overlay to a 320px-tall container so the content genuinely overflows
and the scroll behaviour is exercised rather than assumed.

- [ ] **Step 1: Rewrite the file**

```csharp
using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osu.Framework.Utils;
using osuTK.Input;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSettingsOverlay : GarbusTestScene
    {
        // Short enough that the sections overflow the panel, so the scroll behaviour is exercised
        // rather than assumed. A full-height window would leave nothing to scroll.
        private const float panel_height = 320;

        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private SettingsOverlay overlay = null!;
        private ManualInputManager manual = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay", () => Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = panel_height,
                Child = manual = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = overlay = new SettingsOverlay(),
                },
            });
        }

        [Test]
        public void TestShowHide()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("hide", () => overlay.Hide());
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        // Located by name rather than by glyph or by type: which icon the button wears is a cosmetic
        // detail, and matching on type would mean widening a nested type's visibility for the test.
        private Drawable headerButton =>
            overlay.ChildrenOfType<Drawable>().Single(d => d.Name == SettingsPanelHeader.ActionButtonName);

        private SettingsPanelHeader header => overlay.ChildrenOfType<SettingsPanelHeader>().Single();

        // Dropdown menus carry their own scroll containers, so match on the name too.
        private BasicScrollContainer settingsScroll =>
            overlay.ChildrenOfType<BasicScrollContainer>().Single(s => s.Name == SettingsOverlay.SettingsScrollName);

        private SettingsSection section(string title) =>
            overlay.ChildrenOfType<SettingsSection>().Single(s => s.Name == title);

        private BasicSliderBar<double> sliderFor(string label) =>
            overlay.ChildrenOfType<SettingsSlider>().Single(s => s.Name == label)
                   .ChildrenOfType<BasicSliderBar<double>>().Single();

        [Test]
        public void TestMasterVolumeUsesLogarithmicTaper()
        {
            AddStep("show", () => overlay.Show());

            // Actual gain -> slider position: 3% gain sits at ~30% slider position.
            AddStep("set master gain 0.03", () => audio.Volume.Value = 0.03);
            AddAssert("master slider ~0.30", () =>
                Precision.AlmostEquals(sliderFor("Master volume").Current.Value, 0.30, 0.01));

            // Slider position -> actual gain: dragging to 30% outputs ~3% gain.
            AddStep("set slider position 0.30", () =>
                sliderFor("Master volume").Current.Value = 0.30);
            AddAssert("master gain ~0.03", () => Precision.AlmostEquals(audio.Volume.Value, 0.03, 0.01));
        }

        [Test]
        public void TestAllVolumeRowsUseTaper()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set all gains 0.03", () =>
            {
                audio.Volume.Value = 0.03;
                audio.VolumeTrack.Value = 0.03;
                audio.VolumeSample.Value = 0.03;
            });
            AddAssert("all three volume sliders ~0.30", () =>
                Precision.AlmostEquals(sliderFor("Master volume").Current.Value, 0.30, 0.01)
                && Precision.AlmostEquals(sliderFor("Music volume").Current.Value, 0.30, 0.01)
                && Precision.AlmostEquals(sliderFor("Hitsound volume").Current.Value, 0.30, 0.01));
        }

        [Test]
        public void TestScrollSpeedRowBoundToConfig()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set speed 15", () => config.SetValue(GarbusSetting.ScrollSpeed, 15.0));
            AddAssert("scroll speed slider tracks speed", () =>
                sliderFor("Scroll speed").Current.Value == 15.0);
        }

        /// <summary>
        /// The rows are grouped into the three sections, in order.
        /// </summary>
        [Test]
        public void TestRowsGroupedIntoSections()
        {
            AddStep("show", () => overlay.Show());

            AddAssert("sections in order", () =>
                overlay.ChildrenOfType<SettingsSection>().Select(s => s.Name)
                       .SequenceEqual(new[] { "Audio", "Graphics", "Gameplay" }));

            AddAssert("audio section holds the volume rows", () =>
                section("Audio").ChildrenOfType<SettingsSlider>().Select(s => s.Name)
                                .SequenceEqual(new[] { "Master volume", "Music volume", "Hitsound volume" }));

            AddAssert("gameplay section holds scroll speed", () =>
                section("Gameplay").ChildrenOfType<SettingsSlider>().Select(s => s.Name)
                                   .SequenceEqual(new[] { "Scroll speed" }));
        }

        /// <summary>
        /// The header floats: scrolling moves the rows beneath it while the header itself stays put.
        /// </summary>
        [Test]
        public void TestHeaderStaysPutWhileContentScrolls()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("panel slid in", () => headerButton.ScreenSpaceDrawQuad.TopLeft.X > 0);

            AddAssert("content overflows the panel", () => settingsScroll.ScrollableExtent > 0);

            float headerY = 0;
            float audioY = 0;

            AddStep("record positions", () =>
            {
                headerY = header.ScreenSpaceDrawQuad.TopLeft.Y;
                audioY = section("Audio").ScreenSpaceDrawQuad.TopLeft.Y;
            });

            AddStep("scroll to end", () => settingsScroll.ScrollToEnd(false));

            AddUntilStep("rows moved up", () =>
                section("Audio").ScreenSpaceDrawQuad.TopLeft.Y < audioY);

            AddAssert("header did not move", () =>
                Precision.AlmostEquals(header.ScreenSpaceDrawQuad.TopLeft.Y, headerY, 0.5f));
        }

        /// <summary>
        /// The header button dismisses the overlay from the settings view, mirroring Escape /
        /// clicking outside the panel.
        /// </summary>
        [Test]
        public void TestLeaveButtonHidesOverlay()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);

            // Wait for the slide-in to bring the button onscreen — a click that misses it would also
            // dismiss the overlay (click-outside), which must not be what this test ends up exercising.
            AddUntilStep("leave button onscreen", () => headerButton.ScreenSpaceDrawQuad.TopLeft.X > 0);

            AddStep("click leave button", () =>
            {
                manual.MoveMouseTo(headerButton);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestControlsButtonShowsRebindPanel()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("panel slid in", () => headerButton.ScreenSpaceDrawQuad.TopLeft.X > 0);
            AddAssert("no controls panel yet", () => !overlay.ChildrenOfType<ControlsPanel>().Any());

            // The Controls row sits in the last section, below the fold of the shortened panel.
            AddStep("scroll to end", () => settingsScroll.ScrollToEnd(false));

            AddStep("click Controls", () =>
            {
                var controls = overlay.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "Controls…");
                manual.MoveMouseTo(controls);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("controls panel visible", () => overlay.ChildrenOfType<ControlsPanel>().Any());
            AddUntilStep("header retargeted", () => header.Title.ToString() == "Controls");

            AddStep("click header button", () =>
            {
                manual.MoveMouseTo(headerButton);
                manual.Click(MouseButton.Left);
            });
            AddUntilStep("controls panel gone", () => !overlay.ChildrenOfType<ControlsPanel>().Any());
            AddUntilStep("header back to Settings", () => header.Title.ToString() == "Settings");
        }
    }
}
```

- [ ] **Step 2: Run the tests, expect them to pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneSettingsOverlay`
Expected: all tests pass.

If `TestHeaderStaysPutWhileContentScrolls` fails on `content overflows the panel`, the sections are
shorter than 320px — lower `panel_height` until `ScrollableExtent > 0`, do not delete the assertion.

- [ ] **Step 3: Run the whole suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: no regressions.

- [ ] **Step 4: Commit**

```bash
git add Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs
git commit -m "test: cover the sectioned scrolling settings menu"
```

---

### Task 5: Tuning scene and docs

**Files:**
- Create: `Garbus.Game.Tests/Tuning/TestSceneSettingsPanelTuning.cs`
- Modify: `docs/agents/screens.md` (the "Settings overlay" section)

**Interfaces:**
- Consumes: `SettingsOverlay.HeaderHeight`, `SettingsPanelHeader.BackgroundColour` /
  `ShadowColour` / `ShadowRadius` / `ShadowOffsetY`, `SettingsSection.LabelColour` / `DividerColour`.
- Produces: nothing.

- [ ] **Step 1: Write the tuning scene**

```csharp
// Interactive tuning scene for the settings panel's floating header and section styling: every
// visual parameter is a slider in the test browser's step sidebar and applies live to the open
// overlay. [Explicit] so it never runs in a headless "run all"; pick it in the test browser.

using System.Linq;
using Garbus.Game.Settings;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osuTK.Graphics;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneSettingsPanelTuning : GarbusTestScene
    {
        // Short enough that the content overflows, so the shadow has rows to fall on.
        private const float panel_height = 380;

        private SettingsOverlay overlay = null!;

        private float headerHeight = 56;
        private float headerBrightness = 34;
        private float shadowRadius = 12;
        private float shadowOffsetY = 3;
        private float shadowAlpha = 0.55f;
        private float labelBrightness = 150;
        private float dividerAlpha = 0.47f;

        public TestSceneSettingsPanelTuning()
        {
            AddSliderStep("header height", 32f, 120f, headerHeight, v => { headerHeight = v; apply(); });
            AddSliderStep("header brightness", 0f, 120f, headerBrightness, v => { headerBrightness = v; apply(); });
            AddSliderStep("shadow radius", 0f, 40f, shadowRadius, v => { shadowRadius = v; apply(); });
            AddSliderStep("shadow offset Y", -10f, 20f, shadowOffsetY, v => { shadowOffsetY = v; apply(); });
            AddSliderStep("shadow alpha", 0f, 1f, shadowAlpha, v => { shadowAlpha = v; apply(); });
            AddSliderStep("section label brightness", 60f, 255f, labelBrightness, v => { labelBrightness = v; apply(); });
            AddSliderStep("divider alpha", 0f, 1f, dividerAlpha, v => { dividerAlpha = v; apply(); });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = panel_height,
                Child = overlay = new SettingsOverlay(),
            };

            overlay.Show();
            Schedule(apply);
        }

        private void apply()
        {
            if (!IsLoaded)
                return;

            overlay.HeaderHeight = headerHeight;

            foreach (var header in overlay.ChildrenOfType<SettingsPanelHeader>())
            {
                // The header reads a touch bluer than neutral, matching the panel body's tint.
                header.BackgroundColour = new Color4(
                    (byte)headerBrightness,
                    (byte)headerBrightness,
                    (byte)System.Math.Min(255, headerBrightness * 1.4f),
                    255);
                header.ShadowColour = new Color4(0, 0, 0, (byte)(shadowAlpha * 255));
                header.ShadowRadius = shadowRadius;
                header.ShadowOffsetY = shadowOffsetY;
            }

            foreach (var section in overlay.ChildrenOfType<SettingsSection>())
            {
                section.LabelColour = new Color4(
                    (byte)labelBrightness,
                    (byte)labelBrightness,
                    (byte)System.Math.Min(255, labelBrightness * 1.17f),
                    255);
                section.DividerColour = new Color4(90, 90, 115, (byte)(dividerAlpha * 255));
            }
        }
    }
}
```

- [ ] **Step 2: Build and confirm the scene is discovered**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: succeeds, no new warnings.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: passes; the `[Explicit]` tuning scene is skipped in the headless run.

- [ ] **Step 3: Update `docs/agents/screens.md`**

Replace the "Settings overlay — `Settings/`" section's first two paragraphs so they describe the new
structure. The replacement text:

```markdown
## Settings overlay — `Settings/`

`SettingsOverlay.cs` (a `VisibilityContainer`) opened by `SettingsGearButton` /
`GlobalSettingsContainer`. The panel is three layers: a background box, a full-height
`BasicScrollContainer` holding the rows, and a `SettingsPanelHeader` added last so it draws over
them. The scroll container spans the whole panel and its content carries top padding equal to the
header height, so rows scroll *underneath* the header and pick up its drop shadow rather than
stopping short of it. `panel.Masking` clips that shadow's spill past the panel edges.

Rows are grouped by `SettingsSection` (an uppercase title over a divider rule, located by `Name`):
**Audio** (master / music / hitsound volume), **Graphics** (frame limiter, screen mode) and
**Gameplay** (scroll speed, the Controls… button). `SettingsOverlay.buildSections()` assembles them
so the screen-mode row can be skipped where the platform has only one window mode to offer.

The header is shared by both views — `header.ShowAs(title, icon, action)` retargets its title and
its icon button, which dismisses the overlay on the settings view and returns from the sub-view on
the controls view. `ControlsPanel` therefore carries no title or back link of its own. Tests locate
the button by `SettingsPanelHeader.ActionButtonName` and the settings scroll container by
`SettingsOverlay.SettingsScrollName` (dropdown menus bring their own scroll containers).

Panels: `ControlsPanel` (key rebinding UI over `KeyBindingStore` — see [input.md](input.md) — with
`KeyBindingRow`), `ButtonTestPanel` (live input feedback), `SettingsSlider` + `VolumeCurve` /
`ScrollSpeedMapping` (audio volumes, scroll speed, offset). These back the config settings in
`Configuration/GarbusConfigManager.cs`. `SettingsEnumDropdown<T>` is the dropdown counterpart to
`SettingsSlider` (item text uses each enum value's `[Description]`; pass `items` to offer a subset of
the enum instead of all of it). Two rows use it to bind straight to framework settings, persisted to
`framework.ini` with no `GarbusSetting` behind them: "Frame limiter" (`FrameworkSetting.FrameSync`)
and "Screen mode" (`FrameworkSetting.WindowMode`).
```

Also add to that doc's **Gotchas** list:

```markdown
- **An open dropdown menu near the bottom of the settings panel is clipped by the scroll
  container's masking.** Same as osu.Game's settings panel; the flow's bottom padding reduces how
  often it bites but does not eliminate it.
```

- [ ] **Step 4: Commit**

```bash
git add Garbus.Game.Tests/Tuning/TestSceneSettingsPanelTuning.cs docs/agents/screens.md
git commit -m "test: add a settings panel tuning scene, document the rework"
```

---

## Self-review notes

- Spec coverage: layout (Task 3), sections table (Task 3), `SettingsPanelHeader` (Task 2),
  `SettingsSection` (Task 1), `Name`-based button lookup (Tasks 2 + 4), scroll-under behaviour
  (Task 3, asserted in Task 4), known limitations (documented in Task 5), tuning scene (Task 5),
  docs (Task 5).
- The `out Container padding` parameter on `createScroll` is discarded (`out _`) for the controls
  view, which is deliberate: only the settings view's padding is retargeted live by `HeaderHeight`,
  and the controls view is rebuilt from the current height each time it opens.
