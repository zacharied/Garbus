# Charts & the song format

## Purpose & scope

The native data model and its on-disk `.garbus` JSON format: what a song and a chart contain, how
they serialize, and how the editor and the game load them. Timing lives here too (the control-point
stack). Judgement/hit-object *behavior* is in [gameplay.md](gameplay.md); the editor's disk workflow
is in [editor.md](editor.md). This model replaces osu's `Beatmap`/`WorkingBeatmap`/decoder pipeline
outright — charts load directly, no conversion step, no realm.

## The two-tier model: song → charts

A **`GarbusSong`** (`Charts/GarbusSong.cs`) is the top-level unit and the thing a `.garbus` file
holds:

- `SongMetadata` (title, artist, romanized variants, source) and `SongResources` (`Track`,
  `Background` — **relative paths within the song directory**; `ValidateStructure` rejects rooted or
  `..`-escaping paths on *either* OS, since a chart authored on one platform may play on another).
- An optional shared `ControlPointInfo` (timing).
- A list of one or more **`GarbusChart`**s.
- **Timing is either song-shared or chart-local, never both** — `ValidateStructure` enforces the
  mutual exclusion. `UsesSharedTiming` reports which. `GetEffectiveControlPointInfo(chart)` resolves
  the applicable timing.
- `CreatePlayableChart(chartId)` applies defaults and returns a **`PlayableChart`** record
  (`Song`, `Chart`, effective `ControlPointInfo`) — the bundle gameplay consumes.

A **`GarbusChart`** (`Charts/GarbusChart.cs`) is one playable chart of the song:

- `ChartId` (`Guid`), `ChartMetadata` (`Charts/ChartMetadata.cs`: `ChartName`, `Charter`, `Level`,
  `Difficulty` — the song holds title/artist; `GetDisplayedChartName()` falls back to the
  `Difficulty` name when `ChartName` is empty).
- Optional chart-local `ControlPointInfo`, a `DesignPointInfo` (see below), `PreviewTime`, and the
  `List<GarbusHitObject> HitObjects`.
- `ApplyDefaults()` calls `ApplyDefaults()` on every hit object (fixed hit windows + nested-object
  creation). Call it after loading and before play — `CreatePlayableChart` does this for you.

`Difficulty` (`Charts/Difficulty.cs`) is the per-chart gradation enum (defaults to `Novice`).

## Serialization

Two static serializers under `Charts/Format/`, both writing the **`.garbus`** extension:

- **`GarbusSongSerializer`** (`CURRENT_VERSION = 2`) — the current song-container codec. `Decode`
  reads the top-level integer `version` first: **v1 files are auto-upconverted** (`convertV1` wraps a
  legacy single-chart file into a one-chart song, minting deterministic SHA-256 GUIDs); v2 decodes
  the full `SongFileDto`. Both paths run `ValidateStructure`. Returns a `SongDecodeResult(song,
  wasLegacy)`. `Encode` validates then writes v2. Unknown versions throw.
- **`GarbusChartSerializer`** (`CURRENT_VERSION = 1`) — the single-chart codec (`ChartFileDto`). Still
  the workhorse for three jobs: the editor's disk handle (`ChartFile`, below), the undo/redo
  per-object identity strings (`EncodeHitObject`), and clipboard (`EncodeHitObjects` /
  `DecodeHitObjects`). The song serializer delegates hit-object encoding to it and bridges each chart
  DTO through it, so the hit-object JSON shape is defined in exactly one place. A chart file always
  carries timing inline, so encoding a chart that defers to song-shared timing throws
  `InvalidOperationException` — route those through `GarbusSongSerializer` instead.

The DTO layer (`Charts/Format/ChartFileDto.cs`, `SongFileDto.cs`) is deliberately separate from the
domain model so the file format can stay stable while the model evolves — all mapping lives in the
serializers.

**Format invariants (do not break):**

- Top-level integer `version`; the decoder rejects versions it does not recognise.
- Hit objects are polymorphic on a **`"type"` discriminator that MUST be the first property** of each
  object — System.Text.Json (net8) requires it. The encoder always writes it first; keep it first
  when hand-editing a chart. Discriminators: `cardinal`, `cardinal-hold`, `shoulder`,
  `shoulder-hold`, `slider`, `slamCentered`, `slamEdge`. Design points use the same scheme
  (`tutorial-message`).
- Timing points serialize as a typed list (`time`, `beatLength`, `timeSignature` numerator — the
  denominator is always 4 — `omitFirstBarLine`). Camel-case property naming, indented output.
- Slider control points carry a `shapeOnly` flag (default false): a shape-only point shapes the
  body's sweep but spawns no judged child. **A slider's last control point is never shape-only** —
  the decoder rejects violating files on every path (chart, song, clipboard).

## Design points

`Charts/Design/` is a parallel typed-list stack to timing, for **non-gameplay authored events**.
`DesignPointInfo` holds `DesignPoint`s; the only concrete type today is `TutorialMessage` (timed
text, `StartTime`/`EndTime`/`Text`), authored in the editor's Design tab and rendered by
`Screens/DesignOverlay.cs`. New design-point types add a class + a `[JsonDerivedType]` discriminator.

## Loading paths

- **Game / song select:** `SongStore` (`Charts/SongStore.cs`) decodes bundled `.garbus` resources
  from the `Charts` namespace via `GarbusSongSerializer`; `GetAvailableSongs()` lists them. (An older
  `ChartStore` reads single-chart resources via `GarbusChartSerializer`.) Both are cached in
  `GarbusGameBase`. Song select also scans an on-disk library — see [screens.md](screens.md).
- **Editor:** `ChartFile` (`Charts/ChartFile.cs`) is the editor's handle on a `.garbus` at an
  arbitrary path — the `WorkingBeatmap` replacement, no realm. `Load`/`Save(path)`/`Save()` go
  through `GarbusChartSerializer`; `ImportResource` copies audio/background into the chart directory
  (self-copy guarded); `GetTrackStore`/`GetAudioStream` resolve resources from that directory with a
  lazily-cached `ITrackStore` invalidated on save-to-new-directory. **Track lookups must include the
  full filename with extension** — `ITrackStore` only probes `.mp3` for extension-less lookups.

## osu-framework background

No framework-specific machinery beyond System.Text.Json and the vendored timing stack. `ControlPointInfo`,
`TimingControlPoint`, `ControlPointGroup`, `TimeSignature` under `Charts/Timing/` are vendored from
osu.Game, trimmed to timing only (no effect/sample/difficulty points — kiai, skin samples, and
variable scroll are dropped). Read the originals in `docs/code-reference/osu` before editing them and
deviate minimally.

## Gotchas

- **`GarbusTestChartGenerator` is the source of truth for the bundled chart.** The bundled
  `Garbus.Resources/Charts/test-chart.garbus` is generated from it; after changing the generator or
  the format, regenerate with the `[Explicit]` test `TestChartFormat.RegenerateBundledTestChart`
  (`dotnet test --filter Name~RegenerateBundledTestChart`). `TestChartFormat` also pins roundtrip
  equality, unknown-version rejection, new-field roundtrips, and bundled-file-vs-generator agreement.
- **The `"type"` discriminator must stay first** (see invariants) — a hand-edited chart with `type`
  in second position fails to deserialize.
