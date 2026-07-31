# osu-framework primer

Shared background for every domain. The other docs in `docs/agents/` assume you have read this one;
they cross-link back here for cross-cutting traps rather than repeating them.

## Purpose & scope

Garbus is built directly on **osu-framework** (`ppy.osu.Framework`) — the rendering, input, audio,
and dependency-injection engine. It does *not* depend on osu.Game; the osu.Game pieces it needs
(the clock stack, hit-object/pooling infrastructure, control points, scroll algorithm, judgement
primitives) are vendored as source files, each keeping its ppy MIT header. This doc covers the
framework concepts those vendored files and the Garbus code lean on. It is not an API reference —
when you need the real signature, read the source (see below).

## Where the framework source lives

- **Compiled package:** `ppy.osu.Framework` version `2026.629.0` (`Garbus.Game/Garbus.Game.csproj`).
- **Grep-able source:** two git submodules under `docs/code-reference/` (reference only — never
  built, not needed to build or run Garbus):
  - `docs/code-reference/osu-framework` pinned to tag **2026.629.0** (exactly the package version).
  - `docs/code-reference/osu` pinned to tag **2026.621.0** (nearest osu.Game release — used to read
    the *original* of any vendored osu.Game file).
- **Populate them once** with `git submodule update --init`. They stay empty on a fresh clone until
  you run that.

When editing a vendored file, read its original in `docs/code-reference/osu` first and deviate
minimally.

## Core framework concepts

### Dependency injection (DI / BDL)

Framework wires dependencies through a `DependencyContainer`, not constructors:

- `[BackgroundDependencyLoader] private void load(SomeDep dep) { ... }` — the BDL method; runs once
  off-thread during load. DI-injected and BDL-initialised fields are declared `= null!` (nullability
  is on solution-wide).
- `[Resolved] private SomeDep Dep { get; set; } = null!;` — pull a cached dependency as a property.
- `[Cached]` on a class/field, or `dependencies.Cache(obj)` in a `CreateChildDependencies` override —
  publish something for descendants to resolve. `GarbusGameBase` caches the config manager, chart
  store, etc.; `GarbusEditor` caches the editor clock, editor chart, change handler, and more.

### Drawables, lifetime, and transforms

- Everything visual is a `Drawable` in a `Container`/`CompositeDrawable` tree. Layout is driven by
  `RelativeSizeAxes`, `Anchor`/`Origin`, `Padding`, and fill/grid containers.
- **Transforms** (`this.MoveTo`, `FadeTo`, `TransformTo(...)`) animate properties against the
  drawable's clock. A drawable's `LifetimeStart`/`LifetimeEnd` bound when it is alive; scrolling
  containers only lay out entries that are currently alive.
- Framework can **pool** drawables (reuse instances across many objects). Garbus's *gameplay*
  playfield uses pooling; the *editor* composer deliberately does **not** — its drawables are
  managed manually, which is the source of several editor gotchas below.

### Input

- Positional input is hit-tested down the tree; a drawable overrides
  `ReceivePositionalInputAt(Vector2 screenSpacePos)` to claim (or decline) clicks over its area.
  Returning `true` unconditionally claims the whole screen — the editor's blueprint stack does this,
  which is why child-order matters (see the editor doc).
- Action input goes through a `KeyBindingContainer<T>` that maps keys/buttons to an action enum and
  dispatches to `IKeyBindingHandler<T>`. `PlatformAction` (copy/paste/delete/save) is handled the
  same way and is seen *before* a `SelectionHandler`'s own key handling.

### Clocks

- Drawables run against an `IFrameBasedClock` inherited from their parent unless overridden.
  Setting `Content.Clock = someClock` reparents a subtree's time source — Garbus uses this to put
  the playfield on the gameplay clock (see the timing/audio doc; it is a deliberate deviation from
  osu because Garbus dropped osu's `DrawableRuleset`/`FrameStabilityContainer`).
- Latency-critical audio and clock smoothing (BASS, `AudioManager.InitBass`, interpolating and
  decoupling clocks) live entirely in the framework. Do not retune them without hardware latency
  data.

### Test scenes

- Visual/headless tests derive from framework `TestScene` (Garbus's base is `GarbusTestScene`).
  Steps are declared with `AddStep`/`AddAssert`/`AddUntilStep`; input is driven with
  `ManualInputManager`; time is driven with a manual clock. See the testing doc for the Garbus
  conventions and traps.

## Cross-cutting gotchas

These are framework-shaped traps that bite in more than one domain. The domain-specific instance and
its pinning test are noted; the domain doc has the full story.

- **A vertical `FillFlowContainer` collapses a `RelativeSizeAxes.Both` child to zero height.** A
  relative-size child inside a vertical fill flow has no definite height to be relative to, so it
  measures as zero. Use a padded plain `Container` (reserve sibling space via `Padding`) when a child
  must fill remaining space. *Instance:* the editor tab-content area — see [editor.md](editor.md).
  *Pin:* `TestSceneEditorShell.TestTabContentHasHeight`.
- **A `ScrollContainer`'s `base.Content` scrolls and auto-sizes to the full scrollable extent**, so
  anchoring an overlay there pins it to the content midpoint, not the viewport. Fixed overlays
  (playheads, centre markers) must be added via `AddInternal`, outside the scrolling content.
  *Instance:* the timeline centre marker — see [editor.md](editor.md). *Pin:*
  `TestSceneTimeline.TestCentreMarkerPinnedToViewportCentre`.
- **Lambda event subscriptions leak.** Subscribing to an event (`clock`, `ControlPointInfo`
  changes, selection, `HitObjectUpdated`) with an inline lambda and never unsubscribing keeps the
  subscriber alive. Keep a field reference to the handler and unsubscribe in `Dispose`. *Instances:*
  timeline/metronome components — see [editor.md](editor.md).
- **Non-pooled drawables must be explicitly `Dispose()`d when removed.** `HitObjectContainer`
  detaches with `RemoveInternal(..., false)` (correct for osu's pooled path). An undisposed drawable
  stays subscribed to `HitObject.DefaultsApplied` and re-runs `Apply()` on every later update of that
  object — zombies pile up quadratically into a GC storm. *Instance:* the editor composer's manual
  drawable map — see [editor.md](editor.md) and [gameplay.md](gameplay.md). *Pin:*
  `TestSceneComposeSelection.TestRemovedObjectDrawableIsDisposed`.
- **An uncached `BufferedContainer` re-renders its framebuffer every frame.** The constructor default
  (`cachedFrameBuffer: false`) is equivalent to calling `ForceRedraw()` each frame — with `BlurSigma`
  set, that re-runs both Gaussian passes (kernel radius ≈ 2.15·sigma, two passes over the whole
  buffer) every frame, which cripples integrated GPUs. For content that only changes at discrete
  edges, pass `cachedFrameBuffer: true` and call `ForceRedraw()` at the change site — and note that
  invalidations propagate only **one level** up the tree, so a nested child's new geometry never
  reaches the buffer on its own (fading the buffer itself is fine: colour invalidations don't touch
  the redraw version, alpha applies at blit time). *Instance:* the warning-indicator glow — see
  [gameplay.md](gameplay.md). *Pins:* `TestSceneWarningIndicator.TestGlowBuffersAreCached`,
  `TestSceneWarningIndicator.TestAngleChangeForcesGlowRedraw`.
- **Assign `Sprite.Texture` after `RelativeSizeAxes` in object initializers.** The `Texture` setter
  fills a zero `Size` with the texture's pixel size; a later `RelativeSizeAxes.Both` assignment keeps
  that Size and reinterprets it as a relative factor, scaling the sprite to texture-pixel-size times
  its parent. A 1×1 texture (`renderer.WhitePixel`) accidentally works, so tests using it can't catch
  the trap — pin with a larger texture. *Instance:* the gameplay jacket background — see
  [gameplay.md](gameplay.md). *Pin:* `TestSceneJacketBackground.TestSpritesSizeToLayersNotTexture`.
