# Big issues
* ~~Dragging objects: positioning "bugs out" to the wrong x-position, then heavy lag / GC spikes and a
  10–20 s lockup.~~ FIXED — two stacked defects: (1) when the cursor sat over an object's ghost twin the
  drag delta computed as ±360°, firing a mutate + drawable-rebuild on every mouse-move event with no net
  change (`HandleMovement` now reduces deltas via `MinimalDiff`); (2) the composer's non-pooled drawables
  were detached but never disposed on update/remove, leaving each one subscribed to
  `HitObject.DefaultsApplied` and re-running `Apply()` on every later update — zombies accumulated
  quadratically (composer now disposes removed drawables). Pinned by the incremental-drag +
  drawable-disposal tests in `TestSceneComposeSelection`.
* ~~Dropdown menus in top left (file, edit, etc.) have hover state but do not show anything when clicked.~~ FIXED
* ~~Setup/Compose/Timing/Verify buttons do not respond to click or hover at all~~ FIXED

Both had one root cause: the top bar was added before the tab container in GarbusEditor's child
list, so the compose blueprint stack (which claims positional input over the whole screen, as in
osu) swallowed every click on the bars, and menu dropdowns drew behind the tab content. Fixed by
reordering to match osu's Editor (content first, bars after); guarded by
TestSceneEditorShell.TestTabSwitchingViaClick / TestFileMenuSaveViaClick.