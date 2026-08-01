# File selection dialog — modal input capture and footer layout

## Problem

The editor's file pickers are modal in appearance only.

- **Keyboard leaks to the editor.** `GarbusEditor.OnKeyDown`/`OnScroll` sit on an ancestor of the
  dialog overlay, so while a picker is open Space toggles playback, arrows seek and change the beat
  divisor, Z/X/C/V drive the transport, and the wheel seeks. The user is picking a file and the chart
  moves underneath.
- **Three hand-rolled pickers.** `OpenChartDialog` and the inline panel `FileChooserRow` builds are
  near-identical copies; `SaveAsDialog` is a third variant with a filename box.
- **Scattered chrome.** Confirm sits bottom-left and Cancel bottom-right (inverted from the
  conventional order), and the show-hidden-items control is a wide text button crammed into the
  selector's breadcrumb row.

## Design

### `ModalOverlay` (new, abstract)

`Edit/Screens/Dialogs/ModalOverlay.cs`. Base for the picker dialogs. Derives from the framework's
`OverlayContainer` — itself a `VisibilityContainer`, so `Show`/`Hide`/`PopIn`/`PopOut` semantics are
unchanged for callers.

- `BlockNonPositionalInput => true`. `OverlayContainer.BuildNonPositionalInputQueue` strips every
  drawable queued before the overlay from the keyboard queue, keeping only `IHandleGlobalKeyboardInput`
  implementers. The queue is built root-first, so ancestors — `GarbusEditor` included — are queued
  before the overlay and are stripped. Global handlers (fullscreen, frame statistics) survive by
  design.
- `BlockPositionalInput` is `true` by default on `OverlayContainer`, which also blocks the editor's
  wheel-seek (`BlockScrollInput` follows it).
- `OnKeyDown` dispatches `Escape` to `Cancel()` and `Enter`/`KeypadEnter` to `Confirm()`, then returns
  `true` **unconditionally** — every other key is swallowed. Both handlers are `protected virtual`;
  the base `Cancel()` hides the dialog and the base `Confirm()` does nothing.
- Owns the dim backdrop and the centred panel `Container`; subclasses fill the panel.

`ConfirmDialog` is deliberately left on plain `VisibilityContainer` for now.

### `DialogFooter` (new)

`Edit/Screens/Dialogs/DialogFooter.cs`. The shared bottom strip of a picker panel, fixed height.

- **Bottom-left:** a vertical `FillFlowContainer` of setting checkboxes, anchored `BottomLeft` so it
  grows upward as settings are added. Today it holds one entry, "Show hidden items".
- **Bottom-right:** a horizontal `FillFlowContainer` anchored `BottomRight` holding **Cancel first,
  then Confirm**, so confirm is the rightmost control.
- Exposes `public const string` names for the cancel and confirm buttons; tests locate them through
  those consts (repo rule: locate drawables by `Name`).

### Selector plumbing

`DirectorySelector.ShowHiddenItems` is `protected`, so a checkbox outside the selector cannot bind to
it.

- `GarbusFileSelector` gains `public BindableBool ShowHiddenFiles => ShowHiddenItems;` and drops its
  `CreateHiddenToggleButton` override, removing the in-selector toggle button from the breadcrumb row.
- `GarbusDirectorySelector` (new, thin `BasicDirectorySelector` subclass) does the same for the save
  dialog.

These are production API, not test seams — the checkbox is a real consumer.

### `FileSelectDialog` (new) — replaces `OpenChartDialog`

`Edit/Screens/Dialogs/FileSelectDialog.cs`, a `ModalOverlay`. Parameters: valid file extensions, the
confirm-button label, and an `Action<string>` invoked with the chosen absolute path. It owns the
`LastFileDirectory` config read on construction and write on confirm.

- Panel: `GarbusFileSelector` filling the area above a `DialogFooter`.
- Confirm is a no-op when no file is selected, so Enter with an empty selection does nothing.
- `OpenChartDialog` is **deleted**. `GarbusEditor` (File › Open) and `MainMenuScreen` construct
  `FileSelectDialog` with `".garbus"` and the label "Open"; `FileChooserRow` constructs it with its
  own extensions and the label "Select", replacing its hand-built panel.

### `SaveAsDialog`

Rebased onto `ModalOverlay` + `DialogFooter`, keeping its title, `GarbusDirectorySelector` and
filename box. Cancel/Save move to the bottom-right; the hidden-items checkbox appears bottom-left.
Enter is wired through `filenameBox.OnCommit` as well as the base `Confirm()`.

Accepted wrinkle: while the filename box holds focus, the framework's `TextBox` consumes `Escape`
itself (unfocusing), so Escape-to-cancel needs a second press there.

## Known limitation

`FileChooserRow` presents its dialog into a local overlay container inside the Setup tab rather than
the editor's top-level `dialogOverlay`. Blocking strips only what is queued *before* the overlay,
which covers `GarbusEditor` — the reported problem — but not the top/bottom bars, queued after
`tabContainer`. Implementation verifies whether those bars handle key input; if they do, that picker
moves to the shared overlay, otherwise it stays local.

## Testing

- `TestSceneGarbusFileSelector` asserts on the removed toggle button's text today — rewritten to drive
  the checkbox.
- New `TestSceneFileSelectDialog`:
  - a host drawable counting key presses receives none while the dialog is open;
  - Escape cancels without invoking the callback;
  - Enter confirms when a file is selected, and does nothing when none is;
  - the checkbox toggles hidden-item visibility in the listing;
  - layout is asserted as **relations** — cancel's right edge left of confirm's left edge, both inside
    the panel's bottom-right quadrant, the settings column inside the bottom-left quadrant. No pixel
    offsets, per the test-value rules.
- `Tuning/TestSceneFileSelectDialogTuning`, `[Explicit]`, exposing panel size, footer height, button
  size and flow spacing as live controls.
- `docs/agents/editor.md` records the modal input-blocking rule and the new dialog components.
