# Settings Overlay — Design

**Status:** Approved design, ready for implementation planning.
**Scope:** Phase 5 (port plan). A lightweight, osu-style settings overlay exposing four
settings: master / music / hitsound volume, and scroll speed.

## Goal

Give the player a settings panel, drawn as a slide-in overlay like osu's, reachable from the
main menu and song select. Deliberately **not** a faithful port of osu's `SettingsOverlay`
subsystem (search sidebar, sections, keyboard-nav) — a purpose-built panel with four sliders,
styled to feel osu-ish. It stays trivial to extend later.

### Settings exposed

| Row | Bound to | Range / display |
|---|---|---|
| Master volume | framework `AudioManager.Volume` | 0–100% |
| Music volume | framework `AudioManager.VolumeTrack` | 0–100% |
| Hitsound volume | framework `AudioManager.VolumeSample` | 0–100% |
| Scroll speed | new `GarbusSetting.ScrollSpeed` | 1–20, higher = faster |

Offset is deliberately deferred (needs investigation of how osu handles it).

## Architecture & hosting

The overlay and a gear button are hosted **globally** by `GarbusGame`, layered *above* the
`ScreenStack` so they float over the active screen.

- A small controller (cached at game level) exposes `ToggleVisibility()` and a `Visible`
  bindable. The gear button and the two input triggers all drive this single entry point.
- **Screen gating (main menu + song select only):** a marker interface `IAllowSettings`
  implemented by `MainMenuScreen` and `SongSelect`. `GarbusGame` watches
  `ScreenStack.CurrentScreen` and shows the gear + enables the toggle only when the current
  screen implements `IAllowSettings`; on gameplay/editor the gear hides and the overlay
  force-closes. The rule lives in exactly one place — no per-screen gear duplication.

### `SettingsOverlay`

- Left-anchored panel (~350px wide, full height), slides in/out horizontally (osu feel:
  `MoveToX` + fade, ~500 ms `OutQuint`). Semi-transparent dark background.
- Derives from the framework `VisibilityContainer` so `Show()`/`Hide()`/`State` come for free.
- Dismiss via: gear click again, controller Button 9 again, `Escape`, or click outside the
  panel.
- Content: a vertical `FillFlowContainer` of four labeled slider rows, each a small reusable
  `SettingsSlider` control = label text + `BasicSliderBar<double>` + live value readout.

### Data flow

- The three volume rows resolve the framework `AudioManager` bindables (`Volume`,
  `VolumeTrack`, `VolumeSample`) and `BindTo` them directly. These are already persisted by
  the framework's `FrameworkConfigManager` (`VolumeUniversal` / `VolumeMusic` / `VolumeEffect`),
  so no extra persistence is needed.
- The scroll-speed row binds to the new `GarbusSetting.ScrollSpeed` (persisted in `garbus.ini`).

## Config & scroll-speed wiring

- **New setting:** `GarbusSetting.ScrollSpeed`, default `10`, range `1–20` (`double`), persisted
  in `garbus.ini` via `GarbusConfigManager`.
- **Mapping (scroll speed → `TimeRange`):** a small static helper, osu-mania-inspired:
  `TimeRange = baseline / speedFactor`, with constants chosen so that **speed 10 ≈ 700 ms**
  (the current `GarbusScrollingInfo` default) and higher speed = shorter time range = faster
  travel. Exact baseline/range constants locked during implementation.
- **Wiring into gameplay:** `GarbusGame` caches a `GarbusScrollingInfo` whose `TimeRange` is
  kept in sync with the config value (via `BindValueChanged` through the mapping).
  `GarbusScrollingHitObjectContainer.scrollingInfo` changes from a plain
  `new GarbusScrollingInfo()` field to `[Resolved(canBeNull: true)]` with the current
  `new GarbusScrollingInfo()` retained as the fallback default. Gameplay then picks up the
  cached (config-driven) instance, while existing tests that don't cache one keep working
  unchanged.
- **Startup volume pin removed:** the `Audio.Volume.Value = 0.01` line in `GarbusGameBase.load`
  is deleted — the settings screen now owns volume, and `FrameworkConfigManager` restores the
  persisted value on boot.

## Input

A global toggle, not the gameplay-scoped `GarbusInputManager`:

- **Gear button:** an `IconButton`/clickable at the top-left, click → `ToggleVisibility()`.
- **Controller Button 9:** SDL gamepad button 9 → `InputKey.Joystick9`, wired via a global
  `KeyBindingContainer` (or a `Handle(UIEvent)` at the game host level) so it fires on the
  menu / song-select screens where `GarbusInputManager` isn't present.

## Testing

Headless NUnit scenes:

- Overlay show/hide via the toggle controller.
- Gear visibility follows the current screen: present on `MainMenuScreen` / `SongSelect`,
  absent on `PlayScreen` (and the editor).
- Each volume slider moves its target `AudioManager` bindable; the scroll-speed slider moves
  the `GarbusSetting.ScrollSpeed` config value.
- Scroll-speed → `TimeRange` mapping: speed 10 ⇒ ~700 ms; higher speed ⇒ smaller `TimeRange`.
- Button-9 toggle simulated via `ManualInputManager` where feasible.

## Out of scope

- osu's full `SettingsOverlay` infrastructure (search, sections, sidebar).
- Audio offset setting (deferred pending investigation).
- Key rebinding UI (separate Phase 5 work).
