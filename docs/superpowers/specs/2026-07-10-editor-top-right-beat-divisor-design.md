# Editor top-right: vertical zoom stack + full BeatDivisorControl

## Goal

Port osu's editor top-right region into the Garbus Compose view: a vertically stacked pair of
zoom buttons and, to their right, a full beat-divisor control — a graphical tick display, a
`1/N` divisor selector with chevrons and custom-value entry, and a Common/Triplets/Custom type
selector with chevrons.

This supersedes the earlier narrow plan (a single Common↔Triplets toggle button) drafted on the
`vk/3d71-editor-compose-t` branch. That branch contains only a design doc, no code — nothing to
unwind.

## Current state

- `BindableBeatDivisor` is DI-cached in `GarbusEditor` (`dependencies.Cache(beatDivisor)`), created
  with default divisor 4. It exposes `ValidDivisors` (a `Bindable<BeatDivisorPresetCollection>`),
  `Value`, `SelectNext`/`SelectPrevious`, `SetArbitraryDivisor(divisor, preferKnownPresets)`, and the
  static `GetDivisorForBeatIndex`. `BeatDivisorPresetCollection.COMMON/.TRIPLETS/.Custom(n)` and the
  `BeatDivisorType` enum (`Common`, `Triplets`, `Custom`) are defined.
- `ComposeTab` lays out a full-width `TimelineStrip` (70px tall) with two `BasicButton` zoom
  controls (`+`/`–`) **laid horizontally and overlaying** the strip's top-right, and the composer
  filling the space below. Zoom actions set `timelineStrip.Zoom = CurrentZoom ± 1`.
- Divisor changes are currently only reachable via Up/Down keys in `GarbusEditor.OnKeyDown`
  (`SelectPrevious`/`SelectNext`). There is no on-screen beat-divisor UI.
- `TimelineTickDisplay` already contains a private divisor→colour palette (`getColourForDivisor`)
  and a divisor→height helper (`getHeightForDivisor`).

## Porting constraint

osu's `BeatDivisorControl` (`osu.Game/Screens/Edit/Compose/Components/BeatDivisorControl.cs`) depends
on osu.Game UI types that Garbus deliberately does not vendor: `OverlayColourProvider`, `OsuColour`,
`OsuAnimatedButton`, osu.Game's `IconButton`, `OsuPopover`, `OsuNumberBox`, `OsuTextFlowContainer`.
Garbus's convention (see `EditorRadioButtonCollection`, `EditorToolboxGroup`) is a faithful
re-implementation on **osu-framework** primitives (`BasicButton`, `SpriteText`, `Box`, `BasicPopover`,
`BasicTextBox`) with hardcoded colours. osu-framework provides `IHasPopover`, `PopoverContainer`,
`BasicPopover`, and `BasicTextBox`; Garbus has no `PopoverContainer` in its tree yet.

## Design

### Layout — reserve a column (osu-faithful)

Restructure `ComposeTab` so the top region is a horizontal `GridContainer` mirroring
`TimelineArea.cs`:

```
Row (TimelineStrip height tall):
  [ flex          | 35px          | ~120px                 ]
  [ TimelineStrip | zoom column   | GarbusBeatDivisorControl ]
Below:
  [ composer fills remaining height ]
```

The timeline strip sits in the flex cell (narrower than before) so it no longer runs under the
controls. The reserved cells span the strip's height.

`TimelineStrip.HEIGHT` may be increased from 70px to ~90px if three legible rows (tick display + two
chevron rows) do not fit at 70px. The composer's top padding (`Padding.Top = TimelineStrip.HEIGHT`)
already references the constant, so the composer follows automatically.

### Zoom column

Two buttons stacked **vertically** in the 35px column — `+` on top, `–` on the bottom, each the full
column width and half its height. Actions are unchanged from today
(`timelineStrip.Zoom = timelineStrip.CurrentZoom.Value ± 1f`). This replaces the current horizontal
overlay pair.

### `GarbusBeatDivisorControl` (new, `Garbus.Game/Edit/Compose/`)

A `CompositeDrawable` resolving the DI-cached `BindableBeatDivisor`. A vertical `GridContainer` with
three rows, tallest first:

1. **Tick display (display-only).** Renders one tick per entry of
   `BindableBeatDivisor.GetDivisorForBeatIndex(i, largestPreset, presets)` across
   `0..largestPreset`, plus a marker positioned at the current divisor. Rebuilds when `ValidDivisors`
   changes; the marker moves when `Value` changes. Tick colour and size come from the shared divisor
   palette (see below). Position mapping follows osu: `x = tickIndex / largestPreset` for ticks and
   `1 - 1/divisor` for the marker. **No `SliderBar`, no drag/click selection** — this row is purely a
   readout; the divisor is changed via the chevron row, the type row, keys, or the popover.

2. **Divisor row** — `[ ◄ | "1/N" | ► ]`.
   - `◄` / `►` chevron buttons call `beatDivisor.SelectPrevious()` / `SelectNext()`.
   - The centre `1/N` is a **button** that opens a custom-divisor popover. The control implements
     `IHasPopover`; the popover is a `BasicPopover` containing a digit-restricted `BasicTextBox`.
     On commit, parse the integer and call `beatDivisor.SetArbitraryDivisor(n)`; on parse failure or
     out-of-range, reset the text box to the current value (osu's behaviour). A valid commit hides the
     popover.

3. **Type row** — `[ ◄ | "common"/"triplets"/"custom" | ► ]`.
   - Centre text shows `ValidDivisors.Value.Type` lower-cased, bound to `ValidDivisors`.
   - `◄` / `►` run a faithful port of osu's `cycleDivisorType(direction)`: cycle the current type by
     `direction` through `Common → Triplets → Custom` (wrapping), skip `Custom` when no
     `lastCustomDivisor` has been recorded, then land the divisor —
     `Common → SetArbitraryDivisor(4, true)`, `Triplets → SetArbitraryDivisor(6, true)`,
     `Custom → SetArbitraryDivisor(lastCustomDivisor)`. `lastCustomDivisor` is tracked from
     `ValidDivisors` changes where the new type is `Custom` (its last preset), exactly as osu does.

**Keyboard (Shift+number).** The control's `OnKeyDown` handles Shift+1..9 →
`beatDivisor.SetArbitraryDivisor(key - Key.Number0)`, matching osu. The existing Up/Down cycling in
`GarbusEditor` is unchanged.

### Popover host

`ComposeTab`'s content is wrapped in an osu-framework `PopoverContainer` so the custom-divisor popover
has a render layer. Scope is the compose tab (the only popover consumer); the popover opens near the
`1/N` button.

### Shared divisor palette

Extract the divisor→colour and divisor→height/size logic currently private in `TimelineTickDisplay`
into a shared static helper (e.g. `Garbus.Game/Edit/BeatDivisorColours` with `ColourFor(int)` and
`SizeFor(int)`), backed by hardcoded colours (no `OsuColour`). Point both `TimelineTickDisplay` and
the new tick display at it so there is a single source for the palette. This is a small,
work-adjacent de-duplication, not a broader refactor.

## Scope boundaries

- Tick display is a readout only — no drag/click divisor selection (osu's `TickSliderBar`
  interactivity is intentionally dropped).
- No changes to how the divisor feeds clock snapping, `TimelineTickDisplay`, or hit-object snapping —
  those already consume `BindableBeatDivisor`.
- The `PopoverContainer` is scoped to `ComposeTab`, not the whole editor.
- No menu-bar entries and no new global key bindings beyond the control-local Shift+number.

## Testing

Headless tests in the compose test scene, following existing `Garbus.Game.Tests/Editor/TestScene*`
patterns:

- Control loads; default collection is `COMMON`; the tick display renders the expected number of ticks
  for the default presets.
- `►` chevron → `SelectNext` advances `Value` and the tick marker follows.
- Type `►` cycles `Common → Triplets` (landing `Value == 6`) and back to `Common` (landing
  `Value == 4`); the type text tracks `ValidDivisors.Value.Type`.
- Custom popover: open via the `1/N` button, enter `5`, commit → `ValidDivisors.Value.Type == Custom`,
  `Value == 5`, type text shows "custom"; a subsequent type `◄`/`►` cycle returns to a preset
  collection and can cycle back to the recorded custom divisor.
- Invalid popover entry (non-numeric / out of range) leaves the divisor unchanged.
- Shift+3 sets `Value == 3`.
- Zoom `+`/`–` buttons still change `TimelineStrip.CurrentZoom`.
- Layout: the timeline strip no longer occupies the full width (the reserved zoom/divisor columns are
  present).
