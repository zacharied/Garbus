# Screens & chrome

## Purpose & scope

The screen flow outside the editor: main menu, song select, the play loop, and the settings overlay,
plus small chrome (build-info overlay, design overlay). The editor is its own domain
([editor.md](editor.md)); the play loop's gameplay internals are in [gameplay.md](gameplay.md); the
song/chart model song select reads is in [charts.md](charts.md).

## Flow

`MainMenuScreen` → song select → `PlayScreen`. Settings is an **overlay**, reachable from any screen
that implements `Screens/IAllowSettings.cs` (main menu, song select, and the editor) via the gear
button — not a separate screen. `Screens/DesignOverlay.cs` renders timed tutorial-message design
points during play.

## Song select — `Screens/SongSelect/`

- `SongSelectScreen.cs` — the screen. A grouped↔flat view toggle is persisted in
  `GarbusSetting.SongSelectGrouped`; selecting a chart loops an audio preview
  (`DrawableTrack` fade) and launching plays the chart.
- `ChartLibrary.cs` — unions charts from every `IChartSource` and either flattens them sorted by
  level (`AllCharts()`, flat view) or groups them by `(Source, SongLocator)` into `SongGroup`s sorted
  by title (`Scan()`, grouped view). A rescan is cheap enough to run on every screen entry.
- Sources: `IChartSource.cs`, `ResourceChartSource.cs` (bundled resource songs via
  `SongStore.GetAvailableSongs()`), `DirectoryChartSource.cs` (an on-disk `charts/` folder under
  AppData, recursive scan, `IDisposable`).
- Presentation: `ChartCard`, `ChartRow`, `ChartDetailPanel`, `SongGroup`.

## Play loop — `Screens/PlayScreen.cs`

The minimal game loop replacing osu's `Player`: the clock stack → `GarbusInputManager` → the
playfield, with score/combo/accuracy tallied with rewind-revert and an inline results summary once the
chart plays out. Controls: **Space** pauses/resumes, **R** restarts, **Escape** exits. Launched with a
chart + track (song select guards a missing-audio chart so it never silently falls back to the
bundled default). `PlayScreen` hosts a `JacketBackground` under the gameplay subtree: the song's jacket circle-clipped to the ring's disc plus a cached blurred color wash behind it; song select passes the jacket via `IChartSource.GetBackground`, and a null jacket leaves the flat base background. Also the target of the editor's F5/Test (an in-memory clone — see
[editor.md](editor.md)); on exit it stops and disposes its per-play track.

## Settings overlay — `Settings/`

`SettingsOverlay.cs` (a `VisibilityContainer`) opened by `SettingsGearButton` /
`GlobalSettingsContainer`. Panels: `ControlsPanel` (key rebinding UI over `KeyBindingStore` — see
[input.md](input.md) — with `KeyBindingRow`), `ButtonTestPanel` (live input feedback),
`SettingsSlider` + `VolumeCurve` / `ScrollSpeedMapping` (audio volumes, scroll speed, offset). These
back the config settings in `Configuration/GarbusConfigManager.cs`. `SettingsEnumDropdown<T>` is the
dropdown counterpart to `SettingsSlider` (item text uses each enum value's `[Description]`; pass
`items` to offer a subset of the enum instead of all of it). Two rows use it to bind straight to
framework settings, persisted to `framework.ini` with no `GarbusSetting` behind them: "Frame limiter"
(`FrameworkSetting.FrameSync`) and "Screen mode" (`FrameworkSetting.WindowMode`).

`SettingsOverlay.buildSettingsRows()` assembles the rows so the screen-mode row can be skipped where
the platform has only one window mode to offer.

### Screen mode

The three window modes are the framework's: `Windowed`, `Borderless` (windowed fullscreen) and
`Fullscreen` (exclusive). All three already existed before the dropdown — **Alt+Enter and F11 do not
toggle, they cycle** `Windowed → Borderless → Fullscreen → Windowed` (`FrameworkAction.ToggleFullscreen`
→ `IWindow.CycleMode()`), which is why a single press lands on borderless rather than exclusive
fullscreen. The dropdown and the key both drive `FrameworkSetting.WindowMode`, so they stay in sync
and neither needs to know about the other.

Item list comes from `IWindow.SupportedWindowModes`, not `Enum.GetValues` — mobile supports
`Fullscreen` only, and offering a mode the platform rejects would let the framework silently override
the selection. A headless host has no `Window`, so the row falls back to every mode and stays testable.

`GlobalSettingsContainer` shows the floating gear only when the current screen is `IAllowSettings`
**and** its `ShowSettingsGear` (default true) is true; the editor sets it false and opens the overlay
from a menu item instead. It also implements `ISettingsOverlayControl` (`OpenSettings()`), cached in
DI by `GarbusGame` so any screen can open the overlay without holding a container reference.

## Chrome

`BuildInfoOverlay.cs` / `BuildInfo.cs` show build/version info in-app.

## osu-framework background

`Screen`/`ScreenStack` and `IScreen` push/exit semantics; `VisibilityContainer` for overlays;
`OnKeyDown` for screen-level hotkeys; `DrawableTrack` for the preview audio. See
[osu-framework.md](osu-framework.md).

## Present-tense gaps

State these as current gaps, not "phase" items:

- **There is no dedicated results screen** — the play loop shows an inline results summary only.
- **There is no in-app updater screen** — release/update tooling is desktop/CI, not a game screen.
- Song select defers background rendering and a search box.

## Gotchas

- **Song select must guard a missing-audio chart before launching `PlayScreen`** so it never silently
  falls back to the bundled default track.
- **`PlayScreen` owns its track** — it stops and disposes a per-play instance on exit; tracks are
  never shared with the editor.
