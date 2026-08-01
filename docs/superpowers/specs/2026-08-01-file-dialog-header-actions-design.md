# File-select dialog — header actions (new folder, reveal in file manager)

## Problem

The file-select dialog can only navigate directories that already exist, and offers no route out to
the OS file manager. Two directory-level actions are missing:

- **Create a folder** in the directory being browsed.
- **Open that directory** in the platform's file manager.

There is also no `TooltipContainer` anywhere in the game. `IHasTooltip` is implemented in a handful of
editor components (`EditorRadioButton`, `SelectionBoxButton`, `HitObjectCompositionToolButton`), but
without a container in an ancestor position the framework never displays them — those tooltips are
dormant. Icon-only buttons are unreadable without a tooltip, so one has to exist for this work.

## Design

### `ModalOverlay` — tooltip host

`ModalOverlay` wraps its dim backdrop and centred panel in a framework `TooltipContainer`
(`RelativeSizeAxes = Axes.Both`, constructed with a null cursor container so the tooltip is offset
from the mouse position). Every modal dialog's content is therefore inside a tooltip host, and
`Panel` stays the protected container subclasses fill.

`TooltipContainer` derives from `CursorEffectContainer`, which resolves to the nearest enclosing
instance, so a modal nested inside another modal is fine.

Scope note: this makes tooltips work *inside modal dialogs only*. Lighting up the editor's dormant
tooltips means hosting a container at the game root, which is a separate change.

### `DialogIconButton` (new)

`Edit/Screens/Dialogs/DialogIconButton.cs`. A square `BasicButton` carrying a centred `SpriteIcon`
and implementing `IHasTooltip`. `Icon` and `TooltipText` are `init`-set; `IconScale` is the icon's
size as a fraction of the button, so the glyph tracks the button when a tuning scene resizes it.

`BasicButton` is a `Container`, so the icon is added as a normal child above the background and hover
layers. The inherited `Text` is left empty.

### `DialogHeader` (new)

`Edit/Screens/Dialogs/DialogHeader.cs`. The mirror of `DialogFooter`: a fixed-height strip anchored to
the top of a dialog panel, holding a right-anchored horizontal `FillFlowContainer` of action buttons.

- `HEIGHT` — height reserved inside the panel, consumed by the hosting dialog's padding.
- `AddAction(name, icon, tooltip, action)` — appends a `DialogIconButton` and returns it.
- `ActionSize` / `ActionSpacing` — settable so the tuning scene can drive them live.

Actions flow left-to-right in the order added, so the last one added is rightmost.

### `NewFolderDialog` (new)

`Edit/Screens/Dialogs/NewFolderDialog.cs`, a `ModalOverlay` over a small panel. Constructed with the
parent `DirectoryInfo` and an `Action<DirectoryInfo>` invoked with the created directory.

- Panel: a title, a name text box, an error line, and a `DialogFooter` whose confirm label is
  "Create". No settings column.
- The name box takes focus when the dialog is shown, and its `OnCommit` routes to `Confirm()` — Enter
  inside a `TextBox` never reaches the overlay's own key handling.
- `Confirm()` rejects, with a reason in the error line and the dialog left open, when the trimmed name
  is empty, contains characters invalid in a file name, or names something that already exists.
  Directory creation is wrapped in a try/catch and reports the exception message on failure.
- On success it creates the directory, invokes the callback and hides.

Accepted wrinkle, shared with `SaveAsDialog`: while the name box holds focus, the framework's
`TextBox` consumes `Escape` itself to unfocus, so cancelling by keyboard takes a second press.

### `FileSelectDialog`

Gains a `DialogHeader` with two actions, and pads the selector container down by `DialogHeader.HEIGHT`.

- **New folder** (`FontAwesome.Solid.FolderPlus`) — builds a `NewFolderDialog` against
  `fileSelector.CurrentPath.Value` and shows it in a local host container layered above the panel.
  The host is cleared when the dialog hides, on both create and cancel. On creation the selector
  navigates into the new directory. A no-op while `CurrentPath` is null (the drive-root listing).
- **Open in file manager** (`FontAwesome.Solid.ExternalLinkAlt`) — `GameHost.OpenFileExternally` on
  the current directory's full path. Also a no-op while `CurrentPath` is null.

Nesting the new-folder dialog inside the file dialog is what keeps the outer dialog's key handling out
of the way: `BlockNonPositionalInput` strips everything queued *before* the inner overlay, and
non-positional events dispatch deepest-first, so the inner overlay sees keys first and swallows them.

## Testing

`TestSceneFileSelectDialog` gains:

- clicking new folder, typing a name and confirming creates the directory on disk and moves the
  listing into it;
- cancelling the new-folder dialog creates nothing;
- a name that already exists is refused — no second directory, dialog stays open;
- header layout as relations: both action buttons sit in the panel's top-right quadrant and above the
  file listing, and the new-folder button is left of the reveal button;
- both header actions expose non-empty tooltip text, and a `TooltipContainer` encloses them.

The open-in-file-manager button is deliberately never clicked in a test: the headless test host
derives from `DesktopGameHost`, so `OpenFileExternally` would shell-execute a real file manager.

`Tuning/TestSceneFileSelectDialogTuning` gains action-button size, action spacing and icon-scale
sliders, plus a step that opens the new-folder dialog for eyeballing. Header height stays a constant:
the hosting dialog's padding reads it, and making it live would mean a production seam that only the
tuning scene uses.

`docs/agents/editor.md` records the header component, the tooltip host and the nested-modal rule.
