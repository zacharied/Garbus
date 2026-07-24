# Mini Preview Checkbox Design

## Goal

Replace `View > Preview > Hide | Mini` with one direct `View > Mini Preview` checkbox without changing
Mini behavior.

## Design

The checkbox is checked by default and binds directly to a boolean owned by `GarbusEditor`. Remove the
two-value `EditorPreviewMode` enum and radio-menu implementation because preview selection is now a
binary visibility choice.

Mini remains visible only when the checkbox is checked and Compose is active. Suspension remembers the
boolean, temporarily disables Mini, and restores it on return. A Mini failure unchecks the checkbox.
Hide/reopen still closes subscriptions and sends an authoritative full state.

Mini rendering, size, drag bounds, persisted offsets, input ownership, live edits, clock, results,
rewind, Test return behavior, and disposal remain unchanged.

## Testing

Drive the real View menu checkbox. Verify it is checked by default, directly under View, hides Mini,
reopens Mini with current chart state, and remains synchronized through suspension and failure. Run
the editor/Mini focused suite, full unfiltered suite, build, review, PR update, and Pebble deployment.
