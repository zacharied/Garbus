# Mini Preview Workspace Drag Design

## Goal

Keep the Mini preview exactly as it appears and behaves now, while allowing it to be dragged across
the complete Compose workspace instead of only inside the playfield.

## Behavior

- Mini remains a fixed 190x190 preview with the existing rendering, border, rounded corners, and
  visibility behavior.
- Mini may be positioned over the Compose timeline, playfield, left toolbox, or inspector.
- Mini remains fully visible inside the Compose content area. The top menu and bottom transport bar
  remain outside its draggable bounds.
- Mini continues to claim left-button press and drag and to consume normal and modified wheel input.
- Editor controls outside Mini's rectangle continue receiving input normally.
- Position remains stored as positive bottom-right-relative offsets and is written only at drag end.
- Existing saved offsets are interpreted relative to the expanded workspace and clamped after layout
  and whenever workspace bounds change.

## Structure

`ComposeTab` owns a transparent, full-size overlay above its timeline, playfield, and both toolbox
hosts. The tab's existing bounds exclude the editor's top and bottom bars. `ComposeTab` mounts
`InlineChartPreviewPanel` into this overlay instead of passing the panel into
`GarbusHitObjectComposer` for mounting in the playfield-only overlay.

The overlay itself draws nothing and does not claim input. Only Mini claims input inside its own
rectangle. Drag calculations continue using parent-local coordinates, so the larger parent changes
the allowed range without changing display-scaling behavior.

## Testing

Automated coverage verifies that Mini:

- can overlap the Compose timeline, playfield, left toolbox, and inspector;
- clamps at all four edges of the full Compose workspace;
- cannot enter the top menu or bottom transport bar regions;
- restores and reclamps its persisted position after recreation and resize;
- still owns drag and wheel input while uncovered editor controls retain their existing input;
- retains its current size, rendering canvas, and preview controller lifecycle.

Headed verification on Pebble confirms free dragging across the workspace and checks that no visual
or input behavior changed beyond the expanded drag range.
