# Settings menu: scrollable sections with a floating header

## Goal

The settings overlay presents its rows as one flat, non-scrolling list. Rows clip off the bottom of
the panel once the list grows past the window height, and the rows read as an undifferentiated
stack.

Rework it into a scrollable menu whose rows are grouped into labelled sections, with the "Settings"
title and its dismiss button floating above the scrolling content in a distinct colour, casting a
drop shadow onto the rows passing beneath.

## Layout

`SettingsOverlay.panel` becomes three layers:

```
panel : Container (Width 350, RelativeSizeAxes.Y, Masking = true)
 ├ Box                          panel background (20, 20, 28, 240)
 ├ contentArea : Container      holds the settings scroll OR the controls sub-view
 │   └ BasicScrollContainer     fills the FULL panel height
 │       └ FillFlowContainer    Padding.Top = header_height + content padding
 │           ├ SettingsSection "Audio"
 │           ├ SettingsSection "Graphics"
 │           └ SettingsSection "Gameplay"
 └ header : SettingsPanelHeader  added last, so it draws over the content
```

The scroll container spans the whole panel rather than starting below the header, and the inner flow
carries top padding equal to the header height. Rows therefore scroll *underneath* the header
instead of stopping short of it, which is what gives the drop shadow something to fall on. The
header's Box is opaque so rows vanish cleanly beneath it.

`panel.Masking = true` clips the header shadow's sideways spill past the panel edge.

## Sections

| Section | Rows |
| --- | --- |
| Audio | Master volume, Music volume, Hitsound volume |
| Graphics | Frame limiter, Screen mode |
| Gameplay | Scroll speed, Controls… |

Section headers scroll with the content — they are ordinary rows in the flow, not sticky.

The screen-mode row stays conditional on the platform offering more than one window mode; it is now
a conditional entry within the Graphics section's row list rather than within one flat list.

## Components

### `Settings/SettingsPanelHeader.cs`

A floating bar: `RelativeSizeAxes = Axes.X`, fixed height, `Masking = true` with an
`EdgeEffectParameters { Type = Shadow }`. Children are an opaque Box in a lighter slate than the
panel body, an icon button on the left, and the title text.

The overlay owns a single instance and retargets it per view:

```csharp
header.ShowAs("Settings", FontAwesome.Solid.SignOutAlt, Hide);
header.ShowAs("Controls", FontAwesome.Solid.ChevronLeft, showSettings);
```

One header means one shadow and one style definition. `ControlsPanel` consequently drops its own
inline "‹ Back" link and "Controls" title and becomes purely the scrollable rebind list.

### `Settings/SettingsSection.cs`

Constructed as `(string title, params Drawable[] rows)`. Renders an uppercase muted label, a 1px
divider rule, then the rows. `Name = title`, matching the existing `SettingsSlider.Name = label`
convention so tests and debugging locate sections by title rather than by layout position.

### `SettingsOverlay`

`buildSettingsRows()` becomes `buildSections()`. `showControls()` / `showSettings()` swap the
contents of `contentArea` and call `header.ShowAs(...)` rather than relying on each view to supply
its own title and back affordance.

The `LeaveButton` nested type moves into `SettingsPanelHeader` as its icon button and is located in
tests by `Name`, not by type or by glyph.

## Interaction

- Neither `SliderBar` nor `Dropdown` handles `OnScroll` in osu-framework, so a wheel event over a
  volume slider scrolls the list rather than nudging the value. No guard is required.
- Escape, clicking outside the panel, and the header button all still dismiss the overlay.
- The overlay always opens on the settings view, never the controls sub-view.

## Known limitations

- An open dropdown menu near the bottom of the viewport is clipped by the scroll container's
  masking, as in osu.Game's settings panel. Generous bottom padding on the flow reduces how often
  this bites; it does not eliminate it.
- The scrollbar runs the full panel height, so its top few pixels sit behind the header. Insetting
  it would require padding the scroll container itself, which would destroy the scroll-under effect
  the shadow depends on.

## Testing

Per the repo's testing rules, bare styling values (colours, glyphs, alphas, offsets) are never
asserted — the tests assert relations instead:

- The header's Y position is unchanged after the scroll container's `Current` moves.
- Sections appear in the expected order, each containing its expected rows.
- The flow overflows the viewport, so `ScrollableExtent > 0`.
- Opening Controls retargets the header title, and the header button returns to the settings view.
- Existing coverage (volume taper, scroll-speed binding, leave-button dismissal) survives the
  restructure.

`TestSceneSettingsOverlay` currently locates the leave button by type, which required widening the
nested type's visibility for the test's benefit. It switches to a `Name` lookup, which needs no
visibility change and matches how `SettingsSlider` rows are already found.

A tuning scene `Garbus.Game.Tests/Tuning/TestSceneSettingsPanelTuning.cs` (following
`TestSceneSliderGlowTuning`: `[TestFixture]`, `[Explicit]`, slider steps in the constructor) exposes
header height, header colour, shadow radius / offset / alpha, section label colour, and divider
alpha as live controls.

## Documentation

`docs/agents/screens.md` — rewrite the settings-overlay section to describe the scroll container,
the shared floating header, and the section grouping.
