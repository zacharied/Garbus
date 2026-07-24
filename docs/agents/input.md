# Input

## Purpose & scope

How physical controller/keyboard input becomes game actions and analog-stick gestures. The
**input-to-judgement rules** (what a press does, slam gestures, note-lock timing) are specified in
[`docs/rules-specs/Inputs.md`](../rules-specs/Inputs.md) — this doc is the implementation. Consumed by
[gameplay.md](gameplay.md); rebinding UI is in [screens.md](screens.md).

## Actions

`Input/GarbusAction.cs` — one action per physical button. Each cardinal direction has **two** actions:
the `…1` action is driven by the d-pad and the `…2` action by the matching face button (e.g.
`ButtonE1` / `ButtonE2`). `ButtonL` / `ButtonR` are the shoulders. `GarbusButtonInput` is the
collapsed logical view where a direction is a single input; `GarbusActionExtensions` maps an action to
its `CardinalDirection` and to its `GarbusButtonInput`.

## Button input: `GarbusInputManager`

`Input/GarbusInputManager.cs` is a plain framework `KeyBindingContainer<GarbusAction>`
(`SimultaneousBindingMode.All`, `KeyCombinationMatchingMode.Any`) — it replaces osu's
`RulesetInputManager` with no replay plumbing.

- **Defaults** live in the static `DefaultBindings` map (gamepad: d-pad → `…1`, SDL face buttons
  Joystick1–4 → `…2`, Joystick5/6 → shoulders). Keyboard defaults come through the same
  `DefaultKeyBindings` projection.
- **Rebinding is config-backed** (bindings are persisted, not fixed in code). `Input/KeyBindingStore.cs` holds
  the effective bindings — `DefaultBindings` overlaid with per-action overrides persisted to
  `keybindings.json` in Garbus storage. It **persists only the overrides that differ from defaults**,
  so a stale or partial file can never leave an action unbound. `Rebind`/`ResetToDefaults` raise a
  `Changed` event.
- The store is cached at game level (`GarbusGameBase`) and resolved by `GarbusInputManager`
  (`[Resolved(canBeNull: true)]`, so bare-constructed test instances fall back to defaults).
  `ReloadMappings` pulls from the store on `LoadComplete` and after a rebind; `ReloadBindings()` lets a
  long-lived manager (e.g. the button-test panel beside the rebind view) reflect a change without being
  recreated. `ControlsPanel` (see [screens.md](screens.md)) is the editing UI.

## Analog input & slam gestures

- `Input/AnalogInputManager.cs` — routes analog-stick catchers.
- `Input/RadialJoystickHandler.cs` — a drop-in replacement for the framework's `JoystickHandler` that
  deadzones on the stick **vector** (radially) instead of per-axis. The stock per-axis deadzone
  zeroes whichever of X/Y is individually below threshold, flattening a wedge around each cardinal so
  the sticks "snap" to N/E/S/W; radial deadzoning avoids that.
- `Input/StickGestureTracker.cs` — the slam gesture machine: a per-side rolling buffer of recent
  analog samples answering motion queries for slam judgement. Scanning a recency buffer (not an
  edge-triggered per-frame flag) lets a gesture that completed slightly *before* the poll — including
  before the object's `StartTime`, as early-permissive slams require — still register. Pinned by
  `StickGestureTrackerTest`.

## Gamepad artwork

`GamepadButton` names buttons by position (e.g. `FaceSouth`) so one value resolves to different
artwork per `GamepadType` (Cross on a DualSense, A on an Xbox pad). `GamepadButtonIcons` /
`GamepadButtonSprite` resolve and draw them — used by the controls/rebind UI.

## osu-framework background

`KeyBindingContainer<T>`, `IKeyBinding`/`KeyBinding`, `InputKey`, and `JoystickHandler` from
osu-framework (`docs/code-reference/osu-framework`). Action dispatch reaches `IKeyBindingHandler<T>`
implementers; `PlatformAction` (copy/paste/save/delete) flows through the same mechanism and is seen
before a `SelectionHandler`'s own handling (relevant in the editor — see [editor.md](editor.md)).

## Gotchas

- **Do not reintroduce a per-axis stick deadzone** — use `RadialJoystickHandler`; per-axis deadzoning
  snaps the sticks to cardinals.
- **Slam detection must scan the recency buffer, not a per-frame edge flag** — early-permissive slams
  can complete before the object's `StartTime`. Pinned by `StickGestureTrackerTest`.
- **Persist only non-default key overrides** — writing the full map lets a partial/stale file unbind
  an action. Pinned by `TestKeyBindingStore`.
