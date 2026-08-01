# Jacket background in gameplay — design

Show the song's jacket artwork during gameplay: the jacket itself circle-clipped under the
playfield at heavy dim, and a blurred "color wash" of the jacket filling the background outside
the judgement ring.

## Goals

- The playfield disc shows the jacket at ~20% brightness (80% dim), clipped to the ring's circle.
- The area outside the ring shows the jacket dissolved into a smooth color wash — an extreme
  downscale+blur, not a bokeh/disc-highlight effect.
- The wash is static: computed once per song load, zero recurring per-frame cost.
- Paths without a jacket (bundled test chart, songs with no jacket set, missing file) degrade to
  the current flat dark background with no error.

## Component: `JacketBackground`

New drawable `Garbus.Game/UI/JacketBackground.cs`. Receives a `Texture?` at construction — it
performs no store lookups itself. Hosted by `PlayScreen` between the existing dark base `Box` and
the gameplay-clock subtree. It is static presentation, so it lives **outside** the gameplay-clock
subtree.

Two layers, drawn bottom→top (full order: flat dark box → wash → jacket disc → gameplay):

1. **Wash layer (outside the ring).** A screen-filling jacket `Sprite`
   (`RelativeSizeAxes = Both`, `FillMode.Fill` — the square jacket covers the whole screen)
   inside a `BufferedContainer(cachedFrameBuffer: true)` with a small `FrameBufferScale`
   (default 0.05) and a tuned `BlurSigma`, dimmed with a multiplicative colour tint. The cached
   framebuffer renders once and is then reused every frame — the downscale+blur dissolves the
   jacket into its component colors with built-in framework pieces only (no custom shader, no
   CPU pixel work).
2. **Jacket disc (under the playfield).** A `CircularContainer` with `Masking = true`, square via
   `FillMode.Fit`, centred, containing the un-blurred jacket sprite at ~20% brightness. It mirrors
   the playfield's geometry so it aligns with the judgement ring: the same screen padding, with
   the circle diameter being `min(width, height)` of the padded area (the same rule `Arc` uses).

The disc draws on top of the wash, so the wash only reads outside the ring — no inverse masking.

### Shared geometry constant

The playfield's screen padding (currently a literal `30` in `GarbusPlayfield`'s constructor) is
extracted to a shared constant (`GarbusPlayfield.SCREEN_PADDING`) consumed by both
`GarbusPlayfield` and `JacketBackground`, so ring/disc alignment cannot drift.

## Plumbing

`PlayScreen` gains an optional `Texture? jacket` parameter:

- **Song select:** `SongSelectScreen.launchPlay` passes
  `SelectedChart.Source.GetBackground(SelectedChart)` (already used for the detail panel) into
  `new PlayScreen(chart, track, jacket)`. Both `ResourceChartSource` (bundled `Jackets/`
  namespace) and `DirectoryChartSource` (per-directory `LargeTextureStore`) already implement
  `GetBackground`.
- **Editor test mode (F5):** `GarbusEditor.testPlay` resolves the jacket from the song's
  directory using `SongFile.Song.Resources.Background`, the same way it resolves its own track
  file, and passes it along. No jacket set → null.
- **Standalone path** (main-menu Play, bundled test chart): null — flat box remains.

The screen owns the texture for its lifetime; `JacketBackground` just receives it.

## Tunables and dim semantics

Dim is a multiplicative colour tint per layer. Tunables exposed on `JacketBackground`, with
defaults as constants in the component (nothing in config/settings):

| Tunable | Default |
|---|---|
| Disc dim | 20% brightness (80% dim) |
| Wash dim | 55% brightness (starting point; tuned by eye in the Tuning scene) |
| `BlurSigma` | (5, 5) (starting point; tuned by eye in the Tuning scene) |
| `FrameBufferScale` | 0.05 |

## Fallback / error handling

Null texture → neither layer is added; the flat dark box shows. No placeholder art, no logging
beyond what texture stores already do.

## Testing

- **Tuning scene** (repo rule for new visuals): `Garbus.Game.Tests/Tuning/`
  `TestSceneJacketBackgroundTuning.cs` — sliders for disc dim, wash dim, blur sigma, framebuffer
  scale, plus a "no jacket" toggle. Uses a programmatically generated gradient/blob texture,
  never real song content (ephemeral-content rule).
- **Headless pins:**
  1. A null-jacket `PlayScreen` constructs and runs without the background layers present.
  2. Alignment relation: the jacket disc's drawn diameter equals `min(width, height)` of the
     padded area the ring uses (relation assertion, not a bare styling pin).

## Out of scope

- Song select / editor-shell backgrounds (possible follow-up).
- Live/animated blur (music-reactive pulsing). The static design keeps the door open: swapping
  the cached `BufferedContainer` for a live one is a local change inside `JacketBackground`.
- Bokeh/disc-kernel shader effects.
