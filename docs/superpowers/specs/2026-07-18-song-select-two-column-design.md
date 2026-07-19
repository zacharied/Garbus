# Two-column song select — design

## Goal

Split `SongSelectScreen` into two columns. The **left** column is the existing chart list
(unchanged behaviour). The **right** column is a new detail panel for the currently selected chart,
showing:

- A square with the song's background image (placeholder square when the chart has no background).
- The song **title**.
- The song **artist**.
- The **chart name and level**.
- A square button reading exactly **"Press X to play!"** that launches the selected chart when clicked.

## Decisions (locked)

- The play button text is the literal string `Press X to play!`. **No new `X` key binding** is added —
  clicking the button launches, and the existing `Enter` key still launches.
- When a chart has no background file (or the image fails to load), the image square shows a neutral
  **placeholder** (a solid coloured square), not a collapsed/empty area.

## Layout

Today `SongSelectScreen` is a full-screen background `Box` plus a full-width `BasicScrollContainer`
(holding the `FillFlowContainer` list), a "Select a chart" title, and a top-right "View: …" toggle
button.

New structure:

- The background `Box` stays full-screen behind everything.
- A two-column `GridContainer` (`RelativeSizeAxes = Both`) sits above it:
  - **Left column** — flexible width (`GridSizeMode.Distributed`). Contains the existing scroll
    container + list + "Select a chart" title + View toggle button, moved in unchanged.
  - **Right column** — fixed width (`GridSizeMode.Absolute`, ~380px). Contains the new
    `ChartDetailPanel`.

The existing left-column contents keep their current padding/positioning relative to the left region.

## New component: `ChartDetailPanel`

New file `Garbus.Game/Screens/SongSelect/ChartDetailPanel.cs`. A `CompositeDrawable` laying out, top to
bottom (vertical `FillFlowContainer`, with the panel padded):

1. **Image square** — a fixed-size square (e.g. 300×300). Holds a `Sprite` when a background texture is
   available; otherwise a neutral placeholder `Box`. (Implementation: always a container with a
   placeholder `Box` background and an optional `Sprite` on top; the sprite is only added/shown when a
   texture resolves.)
2. **Title** — large `SpriteText`.
3. **Artist** — smaller, muted `SpriteText`.
4. **Chart name + level** — one `SpriteText`, e.g. `"{ChartName} · Lv.{Level}"`, falling back
   gracefully when either is empty (reuse `ChartCard.ChartName` / `ChartCard.Level`; blank pieces are
   omitted).
5. **Play button** — a square `BasicButton` with `Text = "Press X to play!"` whose `Action` invokes the
   screen's `Launch()`.

Public API:

- `void Show(ChartCard? card, Texture? background)` — repopulates all fields. When `card` is null,
  shows an empty state (e.g. muted "Select a chart" placeholder text, no button action / disabled
  button, placeholder image).

The panel does **not** own selection or launching logic — it calls back into the screen. The launch
callback is supplied by `SongSelectScreen` (constructor arg or settable `Action`).

## Background image loading (the one new capability)

`ChartMetadata.BackgroundFile` already exists but is not exposed on `ChartCard` nor rendered. Changes:

1. **`ChartCard`** — add `public string BackgroundFile { get; init; } = string.Empty;`.
2. **`ResourceChartSource.Enumerate` / `DirectoryChartSource.Enumerate`** — populate
   `BackgroundFile = chart.Metadata.BackgroundFile`.
3. **`IChartSource`** — add `Texture? GetBackground(ChartCard card)`, mirroring the existing
   `Track GetTrack(...)`.
   - **`DirectoryChartSource`** — a per-directory cached `TextureStore`, built exactly parallel to its
     existing `trackStores` dictionary: `new TextureStore(renderer, host.CreateTextureLoaderStore(new
     StorageBackedResourceStore(new NativeStorage(dir))))`, keyed by the chart's directory, disposed in
     `Dispose()`. Returns `store.Get(card.BackgroundFile)` (null when `BackgroundFile` is empty or the
     file is absent). This requires the source to hold an `IRenderer` and `GameHost`, passed to its
     constructor.
   - **`ResourceChartSource`** — uses the DI `TextureStore` (passed to its constructor).
     Returns `textures.Get(card.BackgroundFile)`; returns null when not found. Bundled charts currently
     have no background, so this is the placeholder path.
4. **`SongSelectScreen.load`** — resolve `IRenderer` and `GameHost` from DI and pass them to
   `DirectoryChartSource`; pass the DI `TextureStore` to `ResourceChartSource`.

`Texture.Get` returning null for a missing name is the trigger for the placeholder.

## Wiring in `SongSelectScreen`

- Construct one `ChartDetailPanel` in `load`, placed in the right grid column, with its launch callback
  wired to `Launch()`.
- `Select(ChartCard card)` — after updating row highlight state, call
  `detailPanel.Show(card, card.Source.GetBackground(card))`.
- Initial / no-selection state — panel starts in its empty state until the first `Select`.
- `OnResuming` re-resolves `SelectedChart` after a rescan; refresh the panel there too (call `Show`
  with the re-resolved card, or null).
- Arrow-key navigation already funnels through `Select`, so it refreshes automatically.

## Testing

Extend `TestSceneSongSelect` (and add a focused panel test if cleaner):

- Selecting a chart makes the detail panel display that chart's title/artist and its level text.
- A chart with no background file shows the placeholder (the panel reports no sprite texture / an
  `IsPlaceholder`-style flag, since asserting on a rendered `Box` colour is brittle — expose a small
  test hook on `ChartDetailPanel`, e.g. `bool HasBackground`).
- Clicking the "Press X to play!" button pushes a `PlayScreen` (parallel to the existing
  `TestSelectThenLaunchPushesPlayScreen`, but driven through the button rather than `Launch()`).

No new key binding is tested (none added).

## Out of scope

- No new `X` key binding.
- No changes to grouping/flat view, preview audio, or launch semantics beyond refreshing the panel.
- No bundled background asset is added; the placeholder path is what the bundled chart exercises.
