# Big issues
* ~~Dropdown menus in top left (file, edit, etc.) have hover state but do not show anything when clicked.~~ FIXED
* ~~Setup/Compose/Timing/Verify buttons do not respond to click or hover at all~~ FIXED

Both had one root cause: the top bar was added before the tab container in GarbusEditor's child
list, so the compose blueprint stack (which claims positional input over the whole screen, as in
osu) swallowed every click on the bars, and menu dropdowns drew behind the tab content. Fixed by
reordering to match osu's Editor (content first, bars after); guarded by
TestSceneEditorShell.TestTabSwitchingViaClick / TestFileMenuSaveViaClick.