# Controller Button Remapping — Design

**Status:** Approved design, ready for implementation planning.
**Scope:** Phase 5 (port plan). Basic controller button remapping — a persistence store plus a
minimal in-overlay rebind panel. This is the "Key rebinding UI" the Settings Overlay design
(`2026-07-18-settings-overlay-design.md`) deferred as separate Phase 5 work.

## Goal

Let the player remap their controller buttons. Each of the 10 `GarbusAction` values maps to
exactly one physical button; the player can reassign any of them, and the choices persist across
sessions. Deliberately **not** a faithful port of osu's realm-backed `KeyBindingStore` /
`KeyBindingsSubsection` — a purpose-built store + panel scoped to gamepad buttons.

### In scope

- Persist custom bindings and feed them into gameplay input.
- Expandable "Controls" sub-panel inside the existing `SettingsOverlay`.
- One button per action, **replace on rebind**.
- **Reset-to-defaults** button.

### Out of scope

- Conflict detection (two actions may silently share a button).
- Keyboard bindings (gamepad only).
- Multiple bindings per action.
- Live reload mid-gameplay (not needed — see below).

## Background: current input

- `GarbusInputManager` is a plain `KeyBindingContainer<GarbusAction>` with **hardcoded**
  `DefaultKeyBindings` (N/E/S/W each split into a d-pad "…1" and face "…2" action, plus L/R
  shoulders = 10 actions). It is constructed fresh inside `PlayScreen` — the only instantiation.
- osu's realm-backed `KeyBindingStore` was deliberately dropped, so today there is no persistence
  and no rebind UI.
- A `KeyBindingContainer` uses `DefaultKeyBindings` **unless** its `KeyBindings` property is
  assigned — that assignment is the hook this design uses.

## Architecture

### `KeyBindingStore`

A new store cached at game level in `GarbusGameBase.load`, alongside `LocalConfig` and
`scrollingInfo`, following the same pattern those use.

- **Construction:** reads `keybindings.json` from the framework `Storage` (`%APPDATA%\Garbus`).
- **Effective bindings:** starts from `GarbusInputManager`'s hardcoded defaults, then applies
  per-action overrides from the file. An action absent from the file keeps its default — a partial
  or stale file can never leave an action unbound.
- **Public surface:**
  - Read the current `InputKey` for a given `GarbusAction`.
  - Produce the full effective `IEnumerable<IKeyBinding>` for the input manager.
  - `Rebind(GarbusAction action, InputKey key)` — replace that action's binding and persist.
  - `ResetToDefaults()` — clear all overrides and persist.
  - A change event so an open rebind panel refreshes its rows.

### Wiring into input

`GarbusInputManager` resolves the store as `[Resolved(canBeNull: true)]`:

- If a store is present, it sets `KeyBindings` from the store's effective bindings.
- If not (bare construction in existing tests), it falls back to `DefaultKeyBindings`.

This mirrors the `GarbusScrollingInfo` fallback pattern, so every existing test that constructs a
bare `GarbusInputManager` keeps working unchanged.

**No live reload:** the rebind panel is menu-only (`SettingsOverlay` shows only on screens
implementing `IAllowSettings` — main menu + song select), and `GarbusInputManager` is rebuilt for
each `PlayScreen`. The next play session simply reads the current bindings.

## Serialization format

`keybindings.json` — a flat object holding **only overridden** actions:

```json
{ "ButtonE1": "Joystick3", "ButtonN2": "JoystickHat1Up" }
```

- Keys are `GarbusAction` enum names; values are `InputKey` enum names. Both serialized by name
  (resilient to enum reordering, human-readable).
- Actions absent from the file use their default.
- One button per action → a single value, not a list.
- `ResetToDefaults` writes `{}` (or deletes the file).

## Rebind UI

Extends the existing `SettingsOverlay` (350px left-anchored slide-in panel).

### Entry point

A **"Controls…"** button row below the scroll-speed slider. Clicking it slides the panel content
over to the rebind view; a **back** (‹) button returns to the main settings view. The main overlay
stays visually clean.

### Rebind view

- A vertical `FillFlowContainer` of **10 rows**, one per `GarbusAction`, ordered as declared.
- Each row: action label (from the existing `[Description]` attributes, e.g. "Button East
  (D-Pad)") + the current button's display name + a click target.
- A **Reset to defaults** button at the bottom.

### Capture flow (per row)

1. Click a row → it enters "listening" state (highlighted, "Press a button…").
2. The next gamepad button press is captured, translated to an `InputKey`, written via
   `store.Rebind`, and listening ends.
3. Escape or a click elsewhere cancels listening without changing the binding.

### Capture mechanics

The listening row overrides `OnJoystickPress` and translates the raw event into the same `InputKey`
vocabulary the defaults use (`Joystick1..6`, `JoystickHat1Up/Down/Left/Right`). The framework
exposes this mapping (osu's `KeyBindingRow` does the equivalent via the event's input key /
`KeyCombination.FromInputState`); the exact call is confirmed during implementation and pinned by a
test. Only joystick button and hat events are handled — gamepad-only.

## Testing

Headless NUnit scenes, matching the repo's existing editor/gameplay style:

- **Store:**
  - No file → all defaults.
  - Partial file → listed actions overridden, the rest default.
  - `Rebind` then re-read from disk round-trips the override.
  - `ResetToDefaults` clears the file and restores defaults.
- **Input manager:**
  - With a cached store carrying an override, the effective bindings reflect it.
  - With no store, defaults are still used (guards existing tests).
- **Panel:**
  - Clicking a row and simulating a `JoystickPress` via `ManualInputManager` rebinds that action.
  - Reset restores all rows to defaults.
  - The raw joystick event → `InputKey` translation matches the default vocabulary.
