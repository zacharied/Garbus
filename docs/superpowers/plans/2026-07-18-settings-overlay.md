# Settings Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a lightweight osu-style settings overlay exposing master/music/hitsound volume and scroll speed, reachable via a top-left gear button or gamepad button 9, on the main menu and song select only.

**Architecture:** A bespoke `SettingsOverlay` (a framework `VisibilityContainer`) plus a `SettingsGearButton` are hosted by a `GlobalSettingsContainer` that `GarbusGame` mounts *above* the `ScreenStack`. The container gates the gear/overlay to screens implementing a new `IAllowSettings` marker. Volume rows bind directly to the framework `AudioManager` bindables (already persisted by `FrameworkConfigManager`); scroll speed binds to a new `GarbusSetting.ScrollSpeed`, mapped to `GarbusScrollingInfo.TimeRange` and cached at the game-base level so gameplay resolves it.

**Tech Stack:** C# / osu-framework (no osu.Game dependency), NUnit + framework `TestScene` for headless visual tests.

## Global Constraints

- Nullability is enabled solution-wide. DI-resolved / BDL-initialised fields use `= null!`.
- Terminology: osu's "beatmap" is "chart"; `Bac*`/osu classes ported become `Garbus*`.
- No backwards-compatibility layers; no version bumps. This is experimental.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- New game source lives under `Garbus.Game/`; tests under `Garbus.Game.Tests/` (`Visual/` for `TestScene`-based scenes, repo root for pure NUnit fixtures).
- Controller "Button 9" maps to `osu.Framework.Input.JoystickButton.Button9`, kept in a single named constant so it is trivial to change.
- Scroll-speed calibration: speed `10` must reproduce the historical `700 ms` `GarbusScrollingInfo.TimeRange` default; higher speed = shorter time range.

---

## File Structure

**New files:**
- `Garbus.Game/Settings/ScrollSpeedMapping.cs` — pure static map: scroll speed (1–20) → `TimeRange` ms.
- `Garbus.Game/Settings/SettingsSlider.cs` — reusable labelled slider row bound to a `Bindable<double>`.
- `Garbus.Game/Settings/SettingsOverlay.cs` — the slide-in panel with four rows.
- `Garbus.Game/Settings/SettingsGearButton.cs` — clickable cog button.
- `Garbus.Game/Settings/GlobalSettingsContainer.cs` — hosts gear + overlay, screen gating, Button 9 / Escape input.
- `Garbus.Game/Screens/IAllowSettings.cs` — marker interface for settings-allowed screens.
- `Garbus.Game.Tests/ScrollSpeedMappingTest.cs` — pure unit test.
- `Garbus.Game.Tests/Visual/TestSceneScrollSpeed.cs` — config → gameplay `TimeRange` wiring.
- `Garbus.Game.Tests/Visual/TestSceneSettingsSlider.cs` — slider binding.
- `Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs` — overlay show/hide + row bindings.
- `Garbus.Game.Tests/Visual/TestSceneGlobalSettings.cs` — gating + toggle triggers.

**Modified files:**
- `Garbus.Game/Configuration/GarbusSetting.cs` — add `ScrollSpeed`.
- `Garbus.Game/Configuration/GarbusConfigManager.cs` — default + range for `ScrollSpeed`.
- `Garbus.Game/GarbusGameBase.cs` — cache config-driven `GarbusScrollingInfo`; remove the `Audio.Volume = 0.01` pin.
- `Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs` — resolve `GarbusScrollingInfo` from DI (fallback default) + expose `CurrentTimeRange`.
- `Garbus.Game/GarbusGame.cs` — mount `GlobalSettingsContainer` above the screen stack.
- `Garbus.Game/Screens/MainMenuScreen.cs` — implement `IAllowSettings`.
- `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs` — implement `IAllowSettings`.

---

### Task 1: Scroll-speed setting + mapping

**Files:**
- Modify: `Garbus.Game/Configuration/GarbusSetting.cs`
- Modify: `Garbus.Game/Configuration/GarbusConfigManager.cs:28-29` (after the Song select block)
- Create: `Garbus.Game/Settings/ScrollSpeedMapping.cs`
- Test: `Garbus.Game.Tests/ScrollSpeedMappingTest.cs`

**Interfaces:**
- Produces: `GarbusSetting.ScrollSpeed`; `ScrollSpeedMapping.ToTimeRange(double speed) -> double`; constants `ScrollSpeedMapping.MIN_SPEED = 1`, `MAX_SPEED = 20`, `DEFAULT_SPEED = 10`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/ScrollSpeedMappingTest.cs`:

```csharp
using Garbus.Game.Settings;
using NUnit.Framework;

namespace Garbus.Game.Tests
{
    [TestFixture]
    public class ScrollSpeedMappingTest
    {
        [Test]
        public void SpeedTenReproducesDefaultTimeRange()
        {
            Assert.That(ScrollSpeedMapping.ToTimeRange(10), Is.EqualTo(700).Within(0.001));
        }

        [Test]
        public void HigherSpeedGivesShorterTimeRange()
        {
            Assert.That(ScrollSpeedMapping.ToTimeRange(20), Is.LessThan(ScrollSpeedMapping.ToTimeRange(10)));
            Assert.That(ScrollSpeedMapping.ToTimeRange(1), Is.GreaterThan(ScrollSpeedMapping.ToTimeRange(10)));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~ScrollSpeedMappingTest`
Expected: FAIL — build error, `ScrollSpeedMapping` does not exist.

- [ ] **Step 3: Create the mapping helper**

Create `Garbus.Game/Settings/ScrollSpeedMapping.cs`:

```csharp
namespace Garbus.Game.Settings
{
    /// <summary>
    /// Maps the user-facing "scroll speed" (higher = faster) onto the gameplay
    /// <see cref="Gameplay.UI.Scrolling.GarbusScrollingInfo.TimeRange"/> in milliseconds
    /// (higher = slower). Calibrated so speed 10 reproduces the historical 700 ms default.
    /// </summary>
    public static class ScrollSpeedMapping
    {
        public const double MIN_SPEED = 1;
        public const double MAX_SPEED = 20;
        public const double DEFAULT_SPEED = 10;

        // TimeRange = BASELINE / speed, so speed 10 -> 700 ms, 20 -> 350 ms, 1 -> 7000 ms.
        private const double baseline = 7000.0;

        public static double ToTimeRange(double speed) => baseline / speed;
    }
}
```

- [ ] **Step 4: Add the config setting**

In `Garbus.Game/Configuration/GarbusSetting.cs`, add a new member after `SongSelectGrouped` (inside the enum):

```csharp
        // --- Gameplay ---

        /// <summary>Scroll speed (higher = faster). Maps to gameplay TimeRange via ScrollSpeedMapping.</summary>
        ScrollSpeed,
```

In `Garbus.Game/Configuration/GarbusConfigManager.cs`, add to `InitialiseDefaults()` after the Song select line:

```csharp
            // Gameplay.
            SetDefault(GarbusSetting.ScrollSpeed, ScrollSpeedMapping.DEFAULT_SPEED, ScrollSpeedMapping.MIN_SPEED, ScrollSpeedMapping.MAX_SPEED);
```

Add `using Garbus.Game.Settings;` to the top of `GarbusConfigManager.cs`.

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~ScrollSpeedMappingTest`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Settings/ScrollSpeedMapping.cs Garbus.Game/Configuration/GarbusSetting.cs Garbus.Game/Configuration/GarbusConfigManager.cs Garbus.Game.Tests/ScrollSpeedMappingTest.cs
git commit -m "feat: scroll speed config setting + time-range mapping"
```

---

### Task 2: Wire scroll speed into gameplay

**Files:**
- Modify: `Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs:35,47-54`
- Modify: `Garbus.Game/GarbusGameBase.cs:39-62`
- Test: `Garbus.Game.Tests/Visual/TestSceneScrollSpeed.cs`

**Interfaces:**
- Consumes: `ScrollSpeedMapping.ToTimeRange`; `GarbusSetting.ScrollSpeed`; `GarbusScrollingInfo` (existing, `Gameplay/UI/Scrolling/`).
- Produces: `GarbusScrollingHitObjectContainer.CurrentTimeRange -> double` (internal); a cached `GarbusScrollingInfo` in the game-base DI container whose `TimeRange` follows `GarbusSetting.ScrollSpeed`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneScrollSpeed.cs`:

```csharp
using Garbus.Game.Configuration;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Utils;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneScrollSpeed : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private GarbusScrollingHitObjectContainer container = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset speed", () => config.SetValue(GarbusSetting.ScrollSpeed, 10.0));
            AddStep("create container", () => Child = container = new GarbusScrollingHitObjectContainer { RelativeSizeAxes = Axes.Both });
        }

        [Test]
        public void TestConfigDrivesTimeRange()
        {
            AddAssert("default 700ms", () => Precision.AlmostEquals(container.CurrentTimeRange, 700, 0.001));
            AddStep("speed 20", () => config.SetValue(GarbusSetting.ScrollSpeed, 20.0));
            AddAssert("timerange 350ms", () => Precision.AlmostEquals(container.CurrentTimeRange, 350, 0.001));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneScrollSpeed`
Expected: FAIL — `CurrentTimeRange` does not exist (build error).

- [ ] **Step 3: Make the container resolve the scrolling info**

In `Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs`, replace the field at line 35:

```csharp
    private GarbusScrollingInfo scrollingInfo { get; set; } = new GarbusScrollingInfo();
```

with a DI-resolved reference plus a private fallback, and expose the current time range:

```csharp
    [Resolved(CanBeNull = true)]
    private GarbusScrollingInfo? scrollingInfo { get; set; }

    private readonly GarbusScrollingInfo fallbackScrollingInfo = new GarbusScrollingInfo();

    /// <summary>The visible time range currently in effect (ms). Exposed for tests.</summary>
    internal double CurrentTimeRange => timeRange.Value;
```

Then in `load()` (lines 47-54) bind from the resolved-or-fallback instance:

```csharp
    [BackgroundDependencyLoader]
    private void load()
    {
        var info = scrollingInfo ?? fallbackScrollingInfo;

        timeRange.BindTo(info.TimeRange);
        algorithm.BindTo(info.Algorithm);

        timeRange.ValueChanged += _ => layoutCache.Invalidate();
        algorithm.ValueChanged += _ => layoutCache.Invalidate();
```

Add `using osu.Framework.Allocation;` if not already present (it is, for `[BackgroundDependencyLoader]`). Ensure the class's namespace can see `GarbusScrollingInfo` (existing `using Garbus.Game.Gameplay.UI.Scrolling;` — keep it).

- [ ] **Step 4: Cache the config-driven scrolling info and drop the volume pin**

In `Garbus.Game/GarbusGameBase.cs`, add these usings at the top:

```csharp
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Settings;
using osu.Framework.Bindables;
```

Add a field next to `dependencies` (around line 24):

```csharp
        private Bindable<double> scrollSpeed = null!;
```

In `load(Storage storage)`, **delete** the line:

```csharp
            Audio.Volume.Value = 0.01;
```

and its preceding comment block ("Reduced master volume ..."). Then add, after `dependencies.Cache(new ChartStore(Resources));`:

```csharp
            // Scroll speed -> gameplay TimeRange. Cached here so the gameplay scrolling container
            // resolves a config-driven GarbusScrollingInfo. Speed 10 reproduces the historical 700 ms.
            var scrollingInfo = new GarbusScrollingInfo();
            scrollSpeed = LocalConfig.GetBindable<double>(GarbusSetting.ScrollSpeed);
            scrollSpeed.BindValueChanged(v => scrollingInfo.TimeRange.Value = ScrollSpeedMapping.ToTimeRange(v.NewValue), true);
            dependencies.Cache(scrollingInfo);
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneScrollSpeed`
Expected: PASS (1 test, 2 asserts).

- [ ] **Step 6: Run the gameplay tests to confirm no regression**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneGameplay`
Expected: PASS (unchanged — speed 10 still yields 700 ms).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/UI/GarbusScrollingHitObjectContainer.cs Garbus.Game/GarbusGameBase.cs Garbus.Game.Tests/Visual/TestSceneScrollSpeed.cs
git commit -m "feat: drive gameplay scroll speed from config; remove startup volume pin"
```

---

### Task 3: SettingsSlider control

**Files:**
- Create: `Garbus.Game/Settings/SettingsSlider.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneSettingsSlider.cs`

**Interfaces:**
- Produces: `new SettingsSlider(string label, Bindable<double> current, Func<double,string> format)` — a `CompositeDrawable` (`RelativeSizeAxes = Axes.X`, auto Y) containing a `BasicSliderBar<double>` whose `Current` is bound to `current`. `current` MUST be a `BindableNumber<double>` with `MinValue`/`MaxValue` set (all config/audio volume bindables are).

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneSettingsSlider.cs`:

```csharp
using System.Linq;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSettingsSlider : GarbusTestScene
    {
        [Test]
        public void TestSliderTracksBindable()
        {
            var value = new BindableDouble(0.5) { MinValue = 0, MaxValue = 1 };
            SettingsSlider slider = null!;

            AddStep("create slider", () => Child = new Container
            {
                Width = 300,
                AutoSizeAxes = Axes.Y,
                Child = slider = new SettingsSlider("Test", value, v => v.ToString("0.00")),
            });

            AddStep("set bindable to 0.8", () => value.Value = 0.8);
            AddAssert("slider current updated", () =>
                slider.ChildrenOfType<BasicSliderBar<double>>().Single().Current.Value == 0.8);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneSettingsSlider`
Expected: FAIL — `SettingsSlider` does not exist (build error).

- [ ] **Step 3: Implement the control**

Create `Garbus.Game/Settings/SettingsSlider.cs`:

```csharp
using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// A labelled slider row: name on the left, a live value readout on the right, and a slider
    /// bar bound to <c>current</c>.
    /// </summary>
    public partial class SettingsSlider : CompositeDrawable
    {
        private readonly Bindable<double> current;
        private readonly Func<double, string> format;
        private SpriteText valueText = null!;

        public SettingsSlider(string label, Bindable<double> current, Func<double, string> format)
        {
            this.current = current;
            this.format = format;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Anchor = Anchor.TopLeft,
                                Origin = Anchor.TopLeft,
                                Text = label,
                                Font = FontUsage.Default.With(size: 18),
                                Colour = Color4.White,
                            },
                            valueText = new SpriteText
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Font = FontUsage.Default.With(size: 14),
                                Colour = new Color4(180, 180, 200, 255),
                            },
                        },
                    },
                    new BasicSliderBar<double>
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 12,
                        Current = current,
                        BackgroundColour = new Color4(60, 60, 74, 255),
                        SelectionColour = new Color4(120, 160, 255, 255),
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            current.BindValueChanged(v => valueText.Text = format(v.NewValue), true);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneSettingsSlider`
Expected: PASS (1 test).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Settings/SettingsSlider.cs Garbus.Game.Tests/Visual/TestSceneSettingsSlider.cs
git commit -m "feat: reusable labelled settings slider control"
```

---

### Task 4: SettingsOverlay panel

**Files:**
- Create: `Garbus.Game/Settings/SettingsOverlay.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs`

**Interfaces:**
- Consumes: `SettingsSlider`; `GarbusSetting.ScrollSpeed`; framework `AudioManager` (`Volume`, `VolumeTrack`, `VolumeSample`); `GarbusConfigManager`.
- Produces: `SettingsOverlay : VisibilityContainer` with the standard `Show()`/`Hide()`/`ToggleVisibility()`/`State` API. Rows in order: master volume, music volume, hitsound volume, scroll speed. Click outside the panel dismisses.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs`:

```csharp
using System.Linq;
using Garbus.Game.Configuration;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSettingsOverlay : GarbusTestScene
    {
        [Resolved]
        private AudioManager audio { get; set; } = null!;

        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private SettingsOverlay overlay = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create overlay", () => Child = overlay = new SettingsOverlay());
        }

        [Test]
        public void TestShowHide()
        {
            AddStep("show", () => overlay.Show());
            AddUntilStep("visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("hide", () => overlay.Hide());
            AddUntilStep("hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestVolumeRowBoundToMaster()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set master 0.3", () => audio.Volume.Value = 0.3);
            AddAssert("first slider tracks master", () =>
                overlay.ChildrenOfType<BasicSliderBar<double>>().ElementAt(0).Current.Value == 0.3);
        }

        [Test]
        public void TestScrollSpeedRowBoundToConfig()
        {
            AddStep("show", () => overlay.Show());
            AddStep("set speed 15", () => config.SetValue(GarbusSetting.ScrollSpeed, 15.0));
            AddAssert("last slider tracks speed", () =>
                overlay.ChildrenOfType<BasicSliderBar<double>>().ElementAt(3).Current.Value == 15.0);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneSettingsOverlay`
Expected: FAIL — `SettingsOverlay` does not exist (build error).

- [ ] **Step 3: Implement the overlay**

Create `Garbus.Game/Settings/SettingsOverlay.cs`:

```csharp
using System;
using System.Globalization;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Audio;
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
                            new SettingsSlider("Master volume", audio.Volume, percent),
                            new SettingsSlider("Music volume", audio.VolumeTrack, percent),
                            new SettingsSlider("Hitsound volume", audio.VolumeSample, percent),
                            new SettingsSlider("Scroll speed", config.GetBindable<double>(GarbusSetting.ScrollSpeed), speed),
                        },
                    },
                },
            };
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
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneSettingsOverlay`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Settings/SettingsOverlay.cs Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs
git commit -m "feat: settings overlay panel with volume + scroll speed rows"
```

---

### Task 5: Gear button, global host, screen gating & input

**Files:**
- Create: `Garbus.Game/Screens/IAllowSettings.cs`
- Create: `Garbus.Game/Settings/SettingsGearButton.cs`
- Create: `Garbus.Game/Settings/GlobalSettingsContainer.cs`
- Modify: `Garbus.Game/Screens/MainMenuScreen.cs:20`
- Modify: `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs:29`
- Modify: `Garbus.Game/GarbusGame.cs:13-18`
- Test: `Garbus.Game.Tests/Visual/TestSceneGlobalSettings.cs`

**Interfaces:**
- Consumes: `SettingsOverlay`; `ScreenStack.CurrentScreen`; `IAllowSettings`; `JoystickButton.Button9`.
- Produces: `IAllowSettings` (empty marker); `SettingsGearButton` (`CompositeDrawable` with `Action? Action`); `new GlobalSettingsContainer(ScreenStack screenStack)` — shows the gear + enables toggling only when `screenStack.CurrentScreen is IAllowSettings`, toggled by gear click, `Escape`, or `JoystickButton.Button9`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Visual/TestSceneGlobalSettings.cs`:

```csharp
using System.Linq;
using Garbus.Game.Screens;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneGlobalSettings : GarbusTestScene
    {
        private ScreenStack stack = null!;
        private GlobalSettingsContainer global = null!;

        private SettingsGearButton gear => global.ChildrenOfType<SettingsGearButton>().Single();
        private SettingsOverlay overlay => global.ChildrenOfType<SettingsOverlay>().Single();

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create host", () => Children = new Drawable[]
            {
                stack = new ScreenStack { RelativeSizeAxes = Axes.Both },
                global = new GlobalSettingsContainer(stack) { RelativeSizeAxes = Axes.Both },
            });
        }

        [Test]
        public void TestGearGatedByScreen()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("push disallowed screen", () => stack.Push(new DisallowedScreen()));
            AddUntilStep("gear hidden", () => gear.Alpha == 0);
        }

        [Test]
        public void TestGearClickTogglesOverlay()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("click gear", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("click gear again", () => gear.TriggerClick());
            AddUntilStep("overlay hidden", () => overlay.State.Value == Visibility.Hidden);
        }

        [Test]
        public void TestButton9TogglesOverlay()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("press button 9", () =>
                global.TriggerEvent(new JoystickPressEvent(new InputState(), JoystickButton.Button9)));
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestDisallowedScreenForcesOverlayClosed()
        {
            AddStep("push allowed screen", () => stack.Push(new AllowedScreen()));
            AddUntilStep("gear visible", () => gear.Alpha == 1);
            AddStep("open overlay", () => gear.TriggerClick());
            AddUntilStep("overlay visible", () => overlay.State.Value == Visibility.Visible);
            AddStep("push disallowed screen", () => stack.Push(new DisallowedScreen()));
            AddUntilStep("overlay force-closed", () => overlay.State.Value == Visibility.Hidden);
        }

        private partial class AllowedScreen : Screen, IAllowSettings
        {
        }

        private partial class DisallowedScreen : Screen
        {
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneGlobalSettings`
Expected: FAIL — `IAllowSettings`, `SettingsGearButton`, `GlobalSettingsContainer` do not exist (build error).

- [ ] **Step 3: Add the marker interface**

Create `Garbus.Game/Screens/IAllowSettings.cs`:

```csharp
namespace Garbus.Game.Screens
{
    /// <summary>Marker for screens on which the global settings gear/overlay is available.</summary>
    public interface IAllowSettings
    {
    }
}
```

- [ ] **Step 4: Add the gear button**

Create `Garbus.Game/Settings/SettingsGearButton.cs`:

```csharp
using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    public partial class SettingsGearButton : CompositeDrawable
    {
        public Action? Action { get; set; }

        public SettingsGearButton()
        {
            Size = new Vector2(44);
            CornerRadius = 6;
            Masking = true;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(30, 30, 40, 200),
                },
                new SpriteIcon
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(24),
                    Icon = FontAwesome.Solid.Cog,
                    Colour = Color4.White,
                },
            };
        }

        protected override bool OnClick(ClickEvent e)
        {
            Action?.Invoke();
            return true;
        }
    }
}
```

- [ ] **Step 5: Add the global host container**

Create `Garbus.Game/Settings/GlobalSettingsContainer.cs`:

```csharp
using Garbus.Game.Screens;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;

namespace Garbus.Game.Settings
{
    /// <summary>
    /// Hosts the settings gear + overlay above the screen stack. The gear is shown (and toggling
    /// enabled) only when the current screen implements <see cref="IAllowSettings"/>. Toggled by the
    /// gear, the Escape key, or gamepad button 9.
    /// </summary>
    public partial class GlobalSettingsContainer : CompositeDrawable
    {
        /// <summary>The gamepad button which toggles settings — button 9 on the target controller.</summary>
        private const JoystickButton toggle_button = JoystickButton.Button9;

        private readonly ScreenStack screenStack;

        private SettingsGearButton gear = null!;
        private SettingsOverlay overlay = null!;

        private IScreen? lastScreen;

        public GlobalSettingsContainer(ScreenStack screenStack)
        {
            this.screenStack = screenStack;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                overlay = new SettingsOverlay(),
                gear = new SettingsGearButton
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(10, 10),
                    Alpha = 0,
                    Action = toggle,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            var current = screenStack.CurrentScreen;
            if (ReferenceEquals(current, lastScreen))
                return;

            lastScreen = current;

            bool allowed = current is IAllowSettings;
            gear.FadeTo(allowed ? 1 : 0, 150, Easing.OutQuint);

            if (!allowed)
                overlay.Hide();
        }

        private void toggle()
        {
            if (screenStack.CurrentScreen is IAllowSettings)
                overlay.ToggleVisibility();
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.Escape && overlay.State.Value == Visibility.Visible)
            {
                overlay.Hide();
                return true;
            }

            return base.OnKeyDown(e);
        }

        protected override bool OnJoystickPress(JoystickPressEvent e)
        {
            if (e.Button == toggle_button)
            {
                toggle();
                return true;
            }

            return base.OnJoystickPress(e);
        }
    }
}
```

- [ ] **Step 6: Mark the allowed screens and mount the host**

In `Garbus.Game/Screens/MainMenuScreen.cs`, change the class declaration (line 20):

```csharp
    public partial class MainMenuScreen : Screen, IAllowSettings
```

In `Garbus.Game/Screens/SongSelect/SongSelectScreen.cs`, change the class declaration (line 29):

```csharp
    public partial class SongSelectScreen : Screen, IAllowSettings
```

`SongSelectScreen` is in namespace `Garbus.Game.Screens.SongSelect`; add `using Garbus.Game.Screens;` at the top so `IAllowSettings` resolves. (`MainMenuScreen` is already in `Garbus.Game.Screens`.)

In `Garbus.Game/GarbusGame.cs`, add `using Garbus.Game.Settings;` and replace the `load()` body (lines 13-18):

```csharp
        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                screenStack = new ScreenStack { RelativeSizeAxes = Axes.Both },
                new GlobalSettingsContainer(screenStack) { RelativeSizeAxes = Axes.Both },
            };
        }
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneGlobalSettings`
Expected: PASS (4 tests).

- [ ] **Step 8: Full build + full test run**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: All tests pass (existing suite + the new settings tests).

- [ ] **Step 9: Commit**

```bash
git add Garbus.Game/Screens/IAllowSettings.cs Garbus.Game/Settings/SettingsGearButton.cs Garbus.Game/Settings/GlobalSettingsContainer.cs Garbus.Game/Screens/MainMenuScreen.cs Garbus.Game/Screens/SongSelect/SongSelectScreen.cs Garbus.Game/GarbusGame.cs Garbus.Game.Tests/Visual/TestSceneGlobalSettings.cs
git commit -m "feat: global settings gear + overlay host with screen gating and button-9 toggle"
```

---

## Manual verification (after all tasks)

Run the game: `dotnet run --project Garbus.Desktop`

- On the main menu, a gear button appears top-left. Click it → the settings panel slides in from the left.
- Move each slider: master/music/hitsound volume change audibly; scroll speed changes are reflected in gameplay.
- Click outside the panel (or press Escape, or gamepad button 9) → panel slides out.
- Enter Play → song select still shows the gear. Start gameplay → gear is gone and the overlay cannot open.
- Open the editor → no gear (editor has its own chrome).
- Restart the app → volume and scroll speed persist (`%APPDATA%\Garbus\garbus.ini` for scroll speed; framework config for volumes).

## Notes & open items

- **Button 9 mapping** is assumed to be `JoystickButton.Button9`; if the target controller reports a different index, change the single `toggle_button` constant in `GlobalSettingsContainer`.
- **Audio offset** setting is intentionally deferred (pending investigation of osu's offset handling).
- Removing the `Audio.Volume = 0.01` startup pin means a brand-new config boots at the framework's default master volume; the user now owns it via the overlay, and it persists thereafter.
