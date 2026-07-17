# Song Select — design

Phase 5 ("game chrome" in `PLAN-port.md`) splits into three independent sub-projects: **song select**,
settings screen, and results screen. This spec covers **song select only** — the first and most
architecturally consequential slice, because it establishes where playable charts live and how they are
discovered. Settings and results get their own spec → plan → implementation cycles later.

## Goal

Replace the hardcoded `PlayScreen` chart load (`test-chart.garbus` from bundled resources) with a real
song-select screen: the player browses the charts available to the game, hears a preview, and launches
one into gameplay.

## Chart discovery model

Charts come from **two sources**, unioned into one list:

1. **Bundled resource charts** — `.garbus` files shipped read-only inside the `Garbus.Resources` DLL
   (today's `Charts/test-chart.garbus`). Enumerated via the resource store's `GetAvailableResources()`
   filtered to `*.garbus`. These are playable but not editable.
2. **AppData library folder** — `%APPDATA%\Garbus\charts\`, obtained as
   `storage.GetStorageForDirectory("charts")`, scanned recursively for `.garbus` files. This is where
   the user's own charts live.

A chart on disk is a **folder containing its `.garbus` + audio + background** — exactly what `ChartFile`
and `ImportResource` already assume.

### Not user-configurable in v1

The songs directory is fixed at the `charts` subfolder of the game's storage. A configurable location can
come later (likely alongside the settings screen). No setting is added now.

## Grouping

Multiple charts (difficulties) of one song are grouped **by containing folder**:

- Disk: all `.garbus` in the same directory → one song group (they already share that folder's audio/bg).
- Resources: grouped by resource subfolder; a flat resource chart (e.g. `Charts/test-chart.garbus` with
  no subfolder) is its own single-chart group.

Grouping by folder (rather than by Title+Artist metadata) is robust even when metadata is inconsistent or
blank, and matches the on-disk layout directly.

## Components & files

New folder `Garbus.Game/Screens/SongSelect/`:

### `IChartSource`
Abstracts where charts come from. Responsibilities:
- Enumerate available charts (returns locators + decoded metadata).
- `LoadChart(locator)` → a full `GarbusChart` with `ApplyDefaults()` applied.
- `GetTrack(locator, audio)` → a **fresh** `Track` instance for that chart's audio.

Two implementations:
- **`ResourceChartSource`** — wraps the resource store / `ChartStore`. Enumerates `Charts/**.garbus`.
  Track lookups go through the existing resource-backed `ITrackStore` (as `PlayScreen` does today).
- **`DirectoryChartSource`** — scans a root `Storage`/directory recursively. Track lookups use a
  directory-rooted store: `audio.GetTrackStore(new StorageBackedResourceStore(new NativeStorage(dir)))`
  — identical to `ChartFile.GetTrackStore`.

### `ChartLibrary`
Builds the browsable model by unioning both sources at scan time. Scanning decodes each `.garbus` for
its `Metadata` + `PreviewTime` only (a full JSON decode under the hood, acceptable at v1 scale; the chart
is re-decoded fully on launch). Produces the grouped model below. Rescans on screen (re)entry.

### Data model
- **`ChartCard`** — one per `.garbus`. Fields: `Title`, `Artist`, `ChartName`, `Level`, `PreviewTime`,
  its owning `IChartSource` + locator. Delegates to the source for `LoadChart()` / `GetTrack()`.
- **`SongGroup`** — grouping key = containing folder. Holds display `Title`/`Artist` (from its first
  card) and its list of `ChartCard`, sorted by level within the group.

### `SongSelectScreen`
The framework `Screen`. Hosts:
- A scrollable list of the grouped/flat model.
- A **view toggle** (on-screen button + a hotkey) that drives **both layout and sort**:
  - **Grouped view** → song rows (`Title — Artist`) sorted by song title; each song's charts shown
    beneath it (sorted by level).
  - **Flat view** → one row per chart (`Title [ChartName] Lv.N`) sorted by level ascending.
- Navigation: keyboard ↑/↓ to move selection, Enter to launch, Esc to return to the main menu; mouse
  click to select/launch. Controller navigation is a nice-to-have, not required for v1.
- Audio preview (see below).

The last-used view is persisted in a new `GarbusSetting.SongSelectGrouped` (bool, default grouped).

## Audio preview

On every selection change:
- Load the selected chart's `Track` via its source, seek to `PreviewTime ?? 0`, and **play looping** at
  the game's normal volume, with a short fade-in.
- Stop (fade-out) and dispose the **previous** preview track.

The preview track is stopped and disposed on launch and on screen exit. Preview tracks are always
per-selection fresh instances (never shared with the gameplay track or a store cache), so the screen owns
and disposes them.

## Launch into gameplay

Selecting a chart:
1. `card.LoadChart()` → full `GarbusChart` (`ApplyDefaults` applied).
2. `card.GetTrack()` → a fresh gameplay `Track`.
3. `this.Push(new PlayScreen(chart, track))`.

This **reuses the existing** `PlayScreen(GarbusChart chart, Track track, double startTime = 0)`
constructor — the same path the editor's F5/Test mode already uses. Its XML doc is reframed from "editor
test mode" to "pre-loaded chart + track (editor test mode *or* song select)".

Wiring changes:
- Main menu **"Play"** button pushes `SongSelectScreen` instead of `PlayScreen` directly.
- The parameterless `PlayScreen()` (bundled `test-chart.garbus`) constructor stays for now — it is used
  by `TestScenePlayScreen` — but is no longer on the main-menu path.

## Configuration additions

- `GarbusSetting.SongSelectGrouped` (bool, default `true`) — persists the last-used view mode.

No other config changes. Master volume remains pinned in `GarbusGameBase` (its removal belongs to the
settings-screen sub-project, not this one).

## Testing

Headless (`Garbus.Game.Tests/`):
- `ChartLibrary` scans and unions a temp directory source + a fake/resource source.
- Group-by-folder correctness: two `.garbus` in one folder → one group with two cards; separate folders →
  separate groups; a flat resource chart → its own group.
- Sort behavior: grouped view sorts song groups by title; flat view sorts cards by level.
- Metadata decode: a card's `Title`/`Artist`/`ChartName`/`Level`/`PreviewTime` match the file.
- Launch: selecting a card yields a `PlayScreen` whose `Chart` equals the selected chart.

Visual (`Garbus.Game.Tests/`, framework `TestScene`):
- `TestSceneSongSelect` — list renders from a fixture library; the view toggle switches layout/sort;
  selection changes drive the preview lifecycle (previous preview stops, new one starts).

## Out of scope for v1 (deliberate, tracked as follow-ups)

- **Background image rendering** — `ChartMetadata.BackgroundFile` stays stored-but-unrendered. Song
  select does not display it in v1; it would be the first consumer when added.
- **Search / filter box** — no text filtering in v1.
- **Editor save-location integration** — the editor does **not** default new-chart saves into the songs
  library folder yet. The AppData library fills only from bundled charts + charts the user manually
  places there. Adding a default save location to the editor is a later task, so authored charts do not
  automatically appear in song select in v1.
- **Independent sort modes** — sort is coupled to the view toggle (title in grouped, level in flat); no
  separate osu-style sort menu.
- **Configurable songs directory** — fixed to the `charts` storage subfolder for now.
