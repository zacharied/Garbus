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
bundled default). Also the target of the editor's F5/Test (an in-memory clone — see
[editor.md](editor.md)); on exit it stops and disposes its per-play track.

## Settings overlay — `Settings/`

`SettingsOverlay.cs` (a `VisibilityContainer`) opened by `SettingsGearButton` /
`GlobalSettingsContainer`. The panel is three layers: a background box, a full-height
`BasicScrollContainer` holding the rows, and a `SettingsPanelHeader` added last so it draws over
them. The scroll container spans the whole panel and its content carries top padding of the header
height *plus a gap*, so rows scroll *underneath* the header and pick up its drop shadow rather than
stopping short of it, while the top row still sits clear of the shadow when the view is unscrolled.
`panel.Masking` clips that shadow's spill past the panel edges. Both views share the one
`contentPadding()` helper, so the clearance is identical in the settings and controls views.

Rows are grouped by `SettingsSection` (an uppercase title over a divider rule, located by `Name`):
**Audio** (master / music / hitsound volume), **Graphics** (frame limiter, screen mode) and
**Gameplay** (scroll speed, the Controls… button). `SettingsOverlay.buildSections()` assembles them
so the screen-mode row can be skipped where the platform has only one window mode to offer.

Sectioning puts **two** vertical stacks between a dropdown and the rows it must pop over, so both are
`FrontFirstFillFlowContainer`s (see the dropdown note below): the flow holding the sections, so the
Graphics section's open menu covers the Gameplay section, and each `SettingsSection`'s own row flow,
so "Frame limiter" covers "Screen mode" inside the one section. Making only the outer one front-first
leaves a menu drawing underneath its own section's next row.

The header is shared by both views — `header.ShowAs(title, icon, action)` retargets its title and its
icon button, which dismisses the overlay on the settings view (the in-panel counterpart to Escape /
clicking outside the panel) and returns from the sub-view on the controls view. `ControlsPanel`
therefore carries no title or back link of its own. Tests locate the button by
`SettingsPanelHeader.ActionButtonName` and the settings scroll container by
`SettingsOverlay.SettingsScrollName` — dropdown menus bring their own `BasicScrollContainer`s, so
matching on type alone is ambiguous.

Panels: `ControlsPanel` (key rebinding UI over `KeyBindingStore` — see
[input.md](input.md) — with `KeyBindingRow`), `ButtonTestPanel` (live input feedback),
`SettingsSlider` + `VolumeCurve` / `ScrollSpeedMapping` (audio volumes, scroll speed, offset). These
back the config settings in `Configuration/GarbusConfigManager.cs`. `SettingsEnumDropdown<T>` is the
dropdown counterpart to `SettingsSlider` (item text uses each enum value's `[Description]`; pass
`items` to offer a subset of the enum instead of all of it). Two rows use it to bind straight to
framework settings, persisted to `framework.ini` with no `GarbusSetting` behind them: "Frame limiter"
(`FrameworkSetting.FrameSync`) and "Screen mode" (`FrameworkSetting.WindowMode`).

`Tuning/TestSceneSettingsPanelTuning.cs` drives the header height/colour, shadow radius/offset/alpha,
section label colour and divider alpha live.

Dropdown rows pop their open menu **over** the rows below, combo-box style, instead of growing the
row and reflowing the panel. Two shared pieces in `UI/` make that work everywhere (settings,
inspector, setup tab): `PopoverDropdown<T>` sets `Menu.BypassAutoSizeAxes = Axes.Y` so the open
menu doesn't contribute to its row's autosize, and `FrontFirstFillFlowContainer` draws earlier
children in front of later ones so the spilling menu covers the content below (a framework
`FillFlowContainer` flows by layout position and insertion order, never `Depth`, so draw order is
free to differ from flow order; input follows draw order, so the menu also wins clicks). Every
vertical stack that hosts a dropdown must be front-first — drop either piece and an open menu
pushes the content below down, or draws underneath it.

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
**and** its `ShowSettingsGear` (default true) is true **and** the overlay is closed — the gear yields
to the overlay it opens (fading out while it is up, returning on dismissal); the editor sets
`ShowSettingsGear` false and opens the overlay from a menu item instead. It also implements `ISettingsOverlayControl` (`OpenSettings()`), cached in
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
- **An open dropdown menu near the bottom of the settings panel is clipped by the scroll container's
  masking.** Same as osu.Game's settings panel; the flow's bottom padding reduces how often it bites
  but does not eliminate it.
- **The settings scrollbar runs the full panel height**, so its top few pixels sit behind the
  floating header. Insetting it would mean padding the scroll container itself, which would stop
  rows scrolling under the header and leave the drop shadow with nothing to fall on.
