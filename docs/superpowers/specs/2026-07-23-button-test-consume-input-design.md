# Button-test panel: consume bound button inputs

## Goal

While the button-test panel is showing, a press of any **bound game action**
should light its cell and stop there — the raw key/joystick event must not
propagate past the panel to whatever sits behind the open settings overlay.

Scope is deliberately narrow: the 10 `GarbusAction`s that already have cells
(NESW ×2 for D-Pad/Face, plus L/R shoulders). Unbound gamepad buttons, raw
keyboard keys, mouse, and analog-stick events are **not** trapped.

## Current behaviour

`ButtonTestPanel.ButtonCell` implements `IKeyBindingHandler<GarbusAction>` and
its `OnPressed` always `return false` ("observe only — this panel never consumes
input"). The cell lights, but the press keeps propagating. The panel's embedded
`GarbusInputManager` is a `KeyBindingContainer<GarbusAction>`; nothing consumes,
so the underlying raw event reaches drawables behind the settings overlay.

## Change

Make `ButtonCell.OnPressed` return `true` when `e.Action == Action`
(consume the action it owns), and `false` otherwise.

Because a `KeyBindingContainer` marks the originating raw input handled once a
binding handler returns `true`, consuming the binding consumes the raw
key/joystick event — it stops at the panel. Each of the 10 bound actions has
exactly one cell, so all bound actions are covered.

`OnReleased` is unchanged (`void`; the framework still delivers releases to
unlight the cell).

### Why nothing else is needed

Consumption is already time-scoped by visibility. The panel is `Alpha = 0`
until the Controls sub-view opens (`SettingsOverlay.showControls`); a
non-present subtree is not in the input queue, so cells receive — and therefore
consume — presses only while the panel is actually visible. No explicit
enable/disable gate is required.

## Rejected alternative

A panel-level catch-all `IKeyBindingHandler<GarbusAction>` returning `true` for
*every* action (cells left observe-only) would also consume actions that lack a
cell. Rejected: every current action has a cell, so it adds machinery for no
present benefit and splits "consume" from "light up." Revisit only if an action
is ever added without a corresponding cell.

## Testing

Add a consumption test to `TestSceneButtonTest`:

- Place a sentinel behind the panel in the `ManualInputManager` — a drawable
  that records raw `OnJoystickPress` (or a sibling `IKeyBindingHandler` that
  records the action). "Behind" = earlier in the child list so it sits later in
  the input queue.
- Press a bound button (e.g. `JoystickButton.Hat1Up` → `ButtonN1`).
- Assert the matching cell lights **and** the sentinel never fired — the press
  was consumed, not propagated.

Existing `TestBoundButtonLightsCell`, `TestBoundShoulderLightsCell`, and
`TestStickMovesDot` stay green (lighting/unlighting and stick behaviour are
unchanged).
