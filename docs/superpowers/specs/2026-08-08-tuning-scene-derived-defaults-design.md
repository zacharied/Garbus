# Tuning scenes derive slider defaults from in-class defaults

## Problem

Each tuning scene needs its sliders' initial values at construction time (`AddSliderStep`
runs in the scene constructor; the tuned drawables don't exist until `LoadComplete`), so
scenes mirror the tuned classes' defaults as local literals. The mirrors drift:

- **TestSceneSliderGlowTuning** was born mismatched: the scene's `7.5 / 3.6 / 27.15 / 0.3`
  (thickness / sigma / strength / falloff) never matched `DrawableSliderBody`'s shipped
  `16 / 1.64 / 10.6 / 0.45`, and the scene force-applies its values to every drawable it
  builds — so the scene shows a look the game doesn't ship. Its `showLine` also starts
  `false` while the class ships `true`.
- **TestSceneSettingsPanelTuning** matches only modulo rounding through its brightness →
  tint formula (e.g. computed blue 47 vs the class's 48).
- **TestSceneSpawnHaloTuning** is in sync today but duplicates `SpawnHaloRing`'s literals
  (`2f`, `0.35f`).

Changing an in-class default silently does nothing to (or is overridden by) the tuning
scene.

## Decision

Extend the repo's existing `GarbusScrollingInfo.DEFAULT_*` pattern to every tuned class:
each tuned default becomes a public constant on the owning class, the class's own
initializer uses it, and the tuning scene references it. Scenes end up with **no mirrored
literals**, so drift is structurally impossible. Where the two sides disagree today, the
class default wins (per decision, the glow scene's never-shipped combo is dropped).

Scene-local values that are not class defaults — stream layout timings, playback rate,
`panel_height`, slider min/max ranges — stay scene-local.

## Changes by class

### SpawnHaloRing (`Garbus.Game/UI/SpawnHaloRing.cs`)

- `private const float default_thickness = 2` → `public const float DEFAULT_THICKNESS = 2`
- `private const float default_alpha = 0.35f` → `public const float DEFAULT_ALPHA = 0.35f`
- Constructor and bindable initializer keep using them (rename only).
- Scene: `2f` → `SpawnHaloRing.DEFAULT_THICKNESS`, `0.35f` → `SpawnHaloRing.DEFAULT_ALPHA`.

### DrawableSliderBody (`Garbus.Game/Objects/Drawables/DrawableSliderBody.cs`)

Init-property literals move into public constants, initializers reference them:

- `DEFAULT_THICKNESS = 16f`
- `DEFAULT_GLOW_BLUR_SIGMA = 1.64f`
- `DEFAULT_GLOW_STRENGTH = 10.6f`
- `DEFAULT_GLOW_FALLOFF = 0.45f`
- `DEFAULT_SHOW_LINE = true`
- `DEFAULT_ESCAPE_FADE_SCALE = 1.3f`
- `DEFAULT_UNCAUGHT_DIM_ALPHA = 0.4f`

`GlowColour` is not tuned by any scene and keeps its plain initializer.

Scene (`TestSceneSliderGlowTuning`): field initializers become
`DrawableSliderBody.DEFAULT_*`; the "Defaults mirror … tweak there" comment goes away.
`timeRange` and the scroll-time-range slider's `700` literals become
`GarbusScrollingInfo.DEFAULT_TIME_RANGE` (constant already exists). `showLine` initializes
to `DEFAULT_SHOW_LINE`; `AddToggleStep` has no initial-value parameter, so the button
renders unchecked while the state is `true` — first click is a no-op. Comment this at the
toggle.

### SettingsPanelHeader (`Garbus.Game/Settings/SettingsPanelHeader.cs`)

- `public const float DEFAULT_HEIGHT = 56` (ctor's `Height = 56`;
  `SettingsOverlay.HeaderHeight` forwards to `header.Height`, so the constant lives here).
- `default_background_colour` → `public static readonly Color4 DEFAULT_BACKGROUND_COLOUR`
  (value unchanged: 34, 34, 48, 255).
- Ctor's edge-effect literals → `public static readonly Color4 DEFAULT_SHADOW_COLOUR`
  (0, 0, 0, 140), `public const float DEFAULT_SHADOW_RADIUS = 12f`,
  `public const float DEFAULT_SHADOW_OFFSET_Y = 1.15f`.

### SettingsSection (`Garbus.Game/Settings/SettingsSection.cs`)

- `default_label_colour` → `public static readonly Color4 DEFAULT_LABEL_COLOUR`
  (150, 150, 175, 255).
- `default_divider_colour` → `public static readonly Color4 DEFAULT_DIVIDER_COLOUR`
  (90, 90, 115, 120).

### TestSceneSettingsPanelTuning — deriving scalars from colours

The scene tunes scalars (brightness, alpha) but the classes store finished `Color4`s
(float channels, 0–1). Slider defaults derive from channels; the tint formula's blue
scales derive from the same colours so the round trip is exact at the default position:

- `headerHeight = SettingsPanelHeader.DEFAULT_HEIGHT`
- `headerBrightness = DEFAULT_BACKGROUND_COLOUR.R * 255` (= 34); header blue scale =
  `DEFAULT_BACKGROUND_COLOUR.B / DEFAULT_BACKGROUND_COLOUR.R` (replaces literal `1.4f`)
- `shadowAlpha = DEFAULT_SHADOW_COLOUR.A` (≈ 0.549, replaces `0.55f`);
  `shadowRadius`, `shadowOffsetY` from their constants
- `labelBrightness = DEFAULT_LABEL_COLOUR.R * 255` (= 150); label blue scale =
  `DEFAULT_LABEL_COLOUR.B / DEFAULT_LABEL_COLOUR.R` (replaces `1.17f`)
- `dividerAlpha = DEFAULT_DIVIDER_COLOUR.A` (≈ 0.471, replaces `0.47f`); `apply()`'s
  hardcoded divider RGB (90, 90, 115) also reads from `DEFAULT_DIVIDER_COLOUR`
- The "Defaults mirror … tweak there" comment goes away.

## Error handling

None new — compile-time constants; no runtime failure modes. `static readonly` colours
(not `const`, which C# forbids for structs) are safe: scenes read them in constructors,
after the owning types' static initializers have run.

## Testing

The tuning scenes are `[Explicit]` visual scenes with no assertions; the guarantee here is
structural (one shared constant, no second literal to drift). Verification is: solution
builds, existing test suite passes, and a spot-check that each scene compiles against the
new constant names.
