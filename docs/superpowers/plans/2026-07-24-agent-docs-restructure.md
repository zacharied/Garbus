# Agent Documentation Restructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the scattered, BAC-port-framed agent docs (CLAUDE.md, PLAN-port.md, ISSUES.md, Phase4-Issues.md) with a canonical `AGENTS.md` index plus eight self-sufficient domain docs under `docs/agents/`, and add reference source submodules under `docs/code-reference/`.

**Architecture:** Docs-only change (no game code touched). Knowledge is mined from the four legacy files and **verified against the current source tree** before landing in domain docs; each domain doc follows a fixed template ending in a gotcha section. `AGENTS.md` becomes the hub (orientation + rules + Mermaid mind map + doc index); `CLAUDE.md` becomes a one-line pointer stub. Two never-built git submodules give agents a local, grep-able copy of osu-framework / osu.Game source.

**Tech Stack:** Markdown, Mermaid (`mindmap`), git submodules. Verification via `rg`, `dotnet build Garbus.Desktop.slnf`, and the headless test suite.

**Spec:** `docs/superpowers/specs/2026-07-24-agent-docs-restructure-design.md`

## Global Constraints

- **No historical context in docs.** Present-tense only; no phase history, no "port", no BAC/BigAssCircle mentions, no version numbers on anything. (Repo rule from CLAUDE.md, preserved.)
- **No BAC references in agent-facing files.** The only sanctioned remaining "BAC"/"ppy" mentions are the MIT attribution headers already inside vendored `.cs` source files — those are NOT touched by this plan.
- **Verify before you write.** Every factual claim migrated into a domain doc (a class exists, a test exists, a behavior holds) must be confirmed against the current tree first. Drop anything unverifiable. The legacy files are known-stale.
- **Integration branch is master.** Terminology: osu's "beatmap" is "chart"; `= null!` for DI/BDL fields; nullability is solution-wide.
- **Deliberately dropped (state as present-tense design facts):** no mods, difficulty calculation, replays, skinning, or realm.
- **Reference submodules are never built** and populated opt-in via `git submodule update --init`.
- **Domain doc template (every `docs/agents/*.md`):** (1) Purpose & scope, (2) File/class map, (3) How it connects, (4) osu-framework background, (5) Gotchas (each: symptom → root cause → rule → pinning test).
- **Deferred (out of scope, do not implement):** the five behavioral hard rules from `new-agents-md-wishlist.txt`. AGENTS.md's Rules section reserves their home only.

---

### Task 1: Add reference source submodules

**Files:**
- Create: `.gitmodules`
- Create (submodule checkouts): `docs/code-reference/osu-framework`, `docs/code-reference/osu`

**Interfaces:**
- Produces: the `docs/code-reference/osu-framework` and `docs/code-reference/osu` paths that `osu-framework.md` (Task 2) and `AGENTS.md` (Task 10) reference. Records the pinned tags for those docs to cite.

- [ ] **Step 1: Confirm the framework version to pin**

Run: `rg "ppy.osu.Framework" Directory.Build.props Garbus.Game/Garbus.Game.csproj`
Expected: `<PackageReference Include="ppy.osu.Framework" Version="2026.629.0" />` — pin `osu-framework` to tag `2026.629.0`.

- [ ] **Step 2: Add the osu-framework submodule pinned to the exact tag**

```bash
git submodule add https://github.com/ppy/osu-framework.git docs/code-reference/osu-framework
git -C docs/code-reference/osu-framework fetch --tags --depth 1 origin refs/tags/2026.629.0
git -C docs/code-reference/osu-framework checkout tags/2026.629.0
```
Expected: `docs/code-reference/osu-framework` populated; detached HEAD at tag `2026.629.0`.

- [ ] **Step 3: Add the osu submodule pinned to the nearest release tag**

```bash
git submodule add https://github.com/ppy/osu.git docs/code-reference/osu
git -C docs/code-reference/osu tag --list "2026.*" | sort | tail -5
```
Then check out the newest release tag dated on or before 2026-06-29 (nearest the framework release). Record the chosen tag — it goes in `osu-framework.md`.
```bash
git -C docs/code-reference/osu checkout tags/<chosen-tag>
```
Expected: `docs/code-reference/osu` populated at a release tag near the framework version. (Reference-only; exact pin is low-stakes per the spec.)

- [ ] **Step 4: Verify the submodules are outside the build**

Run: `dotnet build Garbus.Desktop.slnf 2>&1 | rg -i "code-reference" ; echo "exit: $?"`
Expected: no line mentions `code-reference` (project compile globs are project-relative, so repo-root `docs/` is never globbed). Build succeeds. If any file under `docs/code-reference/` is compiled, add `<Compile Remove="..\docs\code-reference\**\*.cs" />` to the offending project and re-verify.

- [ ] **Step 5: Verify opt-in checkout wording holds**

Run: `git config -f .gitmodules --get-regexp path`
Expected: two entries under `docs/code-reference/`. (These stay un-populated on a fresh clone until `git submodule update --init` — the command AGENTS.md documents.)

- [ ] **Step 6: Commit**

```bash
git add .gitmodules docs/code-reference
git commit -m "docs: add osu-framework/osu reference submodules"
```

---

### Task 2: Write `docs/agents/osu-framework.md`

**Files:**
- Create: `docs/agents/osu-framework.md`
- Mine from: `CLAUDE.md` (gotcha list — the generalizable traps), `PLAN-port.md:34-45` (framework-vs-osu.Game split, for background only)

**Interfaces:**
- Produces: the framework-primer doc that every other domain doc cross-links for cross-cutting traps. Owns the general statements of: vertical-fill-flow collapse, ScrollContainer content anchoring, lambda-subscription leaks, manual drawable disposal. Records the two submodule paths + pinned tags from Task 1.

- [ ] **Step 1: Confirm the cross-cutting traps are real and locate their pinning tests**

Run: `rg -l "FillFlowContainer|ScrollContent|DefaultsApplied|Dispose" Garbus.Game.Tests --include=*.cs | head`
Also: `rg "TestTabContentHasHeight|TestCentreMarkerPinnedToViewportCentre|TestRemovedObjectDrawableIsDisposed" Garbus.Game.Tests -l`
Expected: the tests named in CLAUDE.md's gotchas exist. Note any that do not — drop the claim if its pin is gone.

- [ ] **Step 2: Write the doc using the template**

Sections, in order:
1. **Purpose & scope** — this is the shared framework primer; per-domain docs assume it.
2. **File/class map** — n/a for framework itself; instead, "where framework code lives": the `ppy.osu.Framework` NuGet package (compiled) + the `docs/code-reference/osu-framework` submodule (source, tag `2026.629.0`) and `docs/code-reference/osu` (osu.Game source, tag `<chosen>`), populated via `git submodule update --init`.
3. **How it connects** — DI/BDL (`[BackgroundDependencyLoader]`, `[Resolved]`, `[Cached]`), drawable lifetime + transforms, input hierarchy (`ReceivePositionalInputAt`, `IKeyBindingHandler`), clock plumbing (`IFrameBasedClock`, `Content.Clock`), `TestScene` basics.
4. **osu-framework background** — folded into (3).
5. **Gotchas (general — cross-linked to domain instances):**
   - Vertical `FillFlowContainer` collapses a `RelativeSizeAxes.Both` child to zero height → use a padded plain `Container`. (Instance: editor tab area — see editor.md. Pin: `TestTabContentHasHeight`.)
   - `ScrollContainer`'s `base.Content` scrolls and auto-sizes to full track width; fixed overlays must use `AddInternal`, not `base.Content.Add`. (Instance: timeline — see editor.md. Pin: `TestCentreMarkerPinnedToViewportCentre`.)
   - Lambda event subscriptions leak; keep a field reference and unsubscribe in `Dispose`. (Instances across timeline/metronome — see editor.md.)
   - Non-pooled drawables must be explicitly `Dispose()`d; an undisposed drawable stays subscribed to `HitObject.DefaultsApplied`. (Instance: composer — see editor.md/gameplay.md. Pin: `TestRemovedObjectDrawableIsDisposed`.)

- [ ] **Step 3: Verify no BAC leakage and links resolve**

Run: `rg -i "bigasscircle|\bBAC\b|port" docs/agents/osu-framework.md`
Expected: no matches.

- [ ] **Step 4: Commit**

```bash
git add docs/agents/osu-framework.md
git commit -m "docs: add osu-framework agent primer"
```

---

### Task 3: Write `docs/agents/charts.md`

**Files:**
- Create: `docs/agents/charts.md`
- Mine from: `CLAUDE.md` "Phase 3 complete" + "Phase 4 Disk I/O" sections, `PLAN-port.md:152-173` (format decisions — rationale only, present tense)
- Verify against: `Garbus.Game/Charts/` (`GarbusChart`, `ChartMetadata`, `ChartStore`, `ChartFile`, `Format/`, `Timing/`, `Design/`)

**Interfaces:**
- Produces: the charts domain doc. Cross-references gameplay.md (consumers of `GarbusChart`) and editor.md (`ChartFile` disk handle).

- [ ] **Step 1: Verify the class/file map**

Run: `ls Garbus.Game/Charts Garbus.Game/Charts/Format Garbus.Game/Charts/Timing Garbus.Game/Charts/Design`
Run: `rg "class GarbusChart|class ChartMetadata|class ChartStore|class ChartFile|class GarbusChartSerializer|class GarbusTestChartGenerator" Garbus.Game -n`
Expected: each type resolves to a file. Record actual paths for the file/class map.

- [ ] **Step 2: Verify the format invariants still hold**

Run: `rg -n "\"type\"|version|discriminator" Garbus.Game/Charts/Format`
Run: `rg -n "RegenerateBundledTestChart|test-chart.garbus" Garbus.Game Garbus.Game.Tests`
Expected: confirms the `"type"`-first discriminator rule, integer `version` field, and the `[Explicit]` regeneration test. Drop any claim that no longer matches.

- [ ] **Step 3: Write the doc using the template**

Cover: `.garbus` JSON format (integer `version`, decoder rejects unknown versions, `"type"` must be first property of each hit object); `GarbusChart` (metadata + timing + hit objects; hit windows fixed via `DefaultHitWindows`, no per-chart difficulty); `ChartMetadata`; `Format/` DTO layer + `GarbusChartSerializer` (System.Text.Json); `ChartStore` (decodes `.garbus` resources, cached in `GarbusGameBase`); `ChartFile` (editor's disk handle — load/`Save(path)`/`Save()`, `ImportResource`, lazily-cached directory `ITrackStore`); `Charts/Timing/` (timing-only `ControlPointInfo` stack); `Charts/Design/` (design points — verify contents in Step 1); `GarbusTestChartGenerator` as the bundled file's source of truth + regeneration via the `[Explicit]` test. Own-format rationale stated present-tense (readable/diffable/editor-friendly; deletes the need for a legacy encoder roundtrip). **osu-framework background:** none specific — note System.Text.Json + the vendored-from-osu.Game `ControlPointInfo` (deviate minimally).

- [ ] **Step 4: Verify + commit**

Run: `rg -i "bigasscircle|\bBAC\b|phase [0-9]|port" docs/agents/charts.md`
Expected: no matches.
```bash
git add docs/agents/charts.md
git commit -m "docs: add charts domain doc"
```

---

### Task 4: Write `docs/agents/gameplay.md`

**Files:**
- Create: `docs/agents/gameplay.md`
- Mine from: `CLAUDE.md` "Phase 2 state" + the two Judgement paragraphs + the Judgement-feedback paragraph + relevant gotchas
- Verify against: `Garbus.Game/Gameplay/`, `Garbus.Game/Objects/`, `Garbus.Game/UI/`, `docs/rules-specs/Judgement.md`

**Interfaces:**
- Produces: the gameplay domain doc. Links `docs/rules-specs/Judgement.md`, `docs/rules-specs/Inputs.md`, `docs/presentation-specs/Playfield.md` as rules sources of truth; cross-links timing-audio.md (clock) and input.md (actions).

- [ ] **Step 1: Verify the class/file map**

Run: `ls Garbus.Game/Gameplay Garbus.Game/Gameplay/Objects Garbus.Game/Gameplay/UI Garbus.Game/UI Garbus.Game/Objects`
Run: `rg "class GarbusPlayfield|class Ring|class Lane|class GarbusScrollingHitObjectContainer|class HitResult|GarbusOrderedHitPolicy|class JudgementFeedbackDisplay" Garbus.Game -n`
Expected: types resolve. Record paths.

- [ ] **Step 2: Verify judgement + feedback claims against spec and code**

Run: `rg -n "CriticalPerfect|HitWindowRange|LateEligibilityEdge|DurationJudgement|SlamHitWindows" Garbus.Game docs/rules-specs/Judgement.md`
Run: `rg -n "DisplayResult|DisplayTimingOffset|JudgementFeedbackDisplay" Garbus.Game`
Expected: the ladder, asymmetric windows, note-lock policy, duration/slam rules, and feedback-halo behaviors described in CLAUDE.md are present in code. Keep the doc's judgement section a *summary that points to `docs/rules-specs/Judgement.md`* as the authority — do not duplicate the full spec.

- [ ] **Step 3: Write the doc using the template**

Cover: the vendored HitObject/DrawableHitObject/pooling stack and what was trimmed (skinning/combo-colour/mods stripped; `HitSoundContainer`/`DrawableSample` replaces `SkinnableSound`); Garbus objects + drawables (`Objects/`, `Objects/Drawables/`, polar drawables vs editor drawables); `GarbusPlayfield` → `Ring` → `Lane` and `GarbusScrollingHitObjectContainer` (polar time→radius mapping, `GarbusScrollingInfo`, constant scroll algorithm); ordered hit policy; judgement implementation (summary + link to Judgement.md); the judgement feedback halo (`JudgementFeedbackDisplay` owned by `Ring`, `DisplayResult`/`DisplayTimingOffset` gates, rewind-by-reference). **osu-framework background:** drawable lifetime/pooling, `IScrollAlgorithm`. **Gotchas:** the gameplay-clock wiring (`Content.Clock = GameplayClock`) — cross-link timing-audio.md; the manual-clock-jump test caveat — cross-link testing.md.

- [ ] **Step 4: Verify + commit**

Run: `rg -i "bigasscircle|\bBAC\b|phase [0-9]|vendor.*port" docs/agents/gameplay.md`
Expected: no matches (a plain "vendored from osu.Game" note is allowed; "port" is not).
```bash
git add docs/agents/gameplay.md
git commit -m "docs: add gameplay domain doc"
```

---

### Task 5: Write `docs/agents/editor.md`

**Files:**
- Create: `docs/agents/editor.md`
- Mine from: `CLAUDE.md` "Current state (Phase 4 complete)" block + the entire gotcha list (most are editor gotchas), `ISSUES.md` FIXED write-ups, `Phase4-Issues.md` FIXED write-ups
- Verify against: `Garbus.Game/Edit/` (all subdirs)

**Interfaces:**
- Produces: the editor domain doc — the largest gotcha section. Cross-links osu-framework.md (general traps), charts.md (`ChartFile`, `EditorChart` aliasing), gameplay.md (editor vs gameplay drawables), testing.md (harness clock wiring).

- [ ] **Step 1: Verify the class/file map**

Run: `ls Garbus.Game/Edit Garbus.Game/Edit/Compose Garbus.Game/Edit/Blueprints Garbus.Game/Edit/Drawables Garbus.Game/Edit/Screens Garbus.Game/Edit/Tools`
Run: `rg "class GarbusEditor|class EditorChart|class GarbusChartChangeHandler|class EditorAngleMapping|class GarbusEditorPlayfield|class GarbusSelectionHandler|class EditorClipboard" Garbus.Game/Edit -n`
Expected: types resolve. Record paths.

- [ ] **Step 2: Confirm each migrated gotcha's pinning test exists**

Run this and cross-check every test named in CLAUDE.md's gotcha list + the ISSUES.md FIXED entries:
```bash
rg -o "Test[A-Za-z0-9_]+" CLAUDE.md ISSUES.md Phase4-Issues.md | sort -u > /tmp/claimed_tests.txt
rg -o "public void (Test[A-Za-z0-9_]+)" Garbus.Game.Tests -r '$1' --no-filename | sort -u > /tmp/real_tests.txt
comm -23 /tmp/claimed_tests.txt /tmp/real_tests.txt
```
Expected: the `comm` output lists claimed tests that no longer exist. **For each one, drop or rewrite the gotcha** rather than citing a dead test. Keep only gotchas whose pin still exists (or whose behavior you can re-confirm in code).

- [ ] **Step 3: Write the doc using the template**

Cover, in the file/class map: shell (`GarbusEditor` — four `EditorTab`s, menu bar, dialog overlay, hotkeys, dirty tracking, DI caches); core (`EditorClock`, `BindableBeatDivisor`, `EditorChart` aliasing `Chart.HitObjects`, undo/redo via `EditorChangeHandler` + `GarbusChartChangeHandler` JSON-identity diff); compose (`EditorAngleMapping` as sole angle↔timeline-x authority, `GarbusEditorPlayfield`, `Edit/Drawables/`, tools, placement/selection blueprints, `GarbusSelectionHandler`); timeline (`Edit/Screens/Timeline/`) + bottom bar/transport + F5 test mode; Setup/Timing/Verify tabs + `EditorClipboard`. **Gotchas section:** migrate every surviving gotcha from CLAUDE.md (fill-flow tab area, EditorChart aliasing, non-pooled drawable disposal, in-place `Update` refresh + `LifetimeEnd` swallow, drag delta `MinimalDiff`/`Direction` mapping, lambda leaks, bar-ordering for input, wheel-seek convention, `AddInternal` for fixed overlays, `TransferBlueprintFor`, placement auto-seek, node selection locality, head-only slider head-node ineligibility, drag-handle-disposed-mid-drag transaction stranding, judgement-line offset shared inset) + the mined ISSUES.md root causes not already covered. Each gotcha: symptom → root cause → rule → pinning test.

- [ ] **Step 4: Verify + commit**

Run: `rg -i "bigasscircle|\bBAC\b|phase [0-9]|port" docs/agents/editor.md`
Expected: no matches.
```bash
git add docs/agents/editor.md
git commit -m "docs: add editor domain doc"
```

---

### Task 6: Write `docs/agents/timing-audio.md`

**Files:**
- Create: `docs/agents/timing-audio.md`
- Mine from: `CLAUDE.md` "Phase 2" clock-stack paragraph, `PLAN-port.md:35-56` (audio investigation + platform offsets — present tense), `ISSUES.md` FrameStabilityContainer Q&A (lines 22-30)
- Verify against: `Garbus.Game/Timing/`, `Garbus.Game/Gameplay/Audio/`, `Garbus.Game/Configuration/`

**Interfaces:**
- Produces: the timing/audio domain doc. Cross-links gameplay.md (the `Content.Clock` gotcha lives on the boundary — state it here, reference from gameplay.md).

- [ ] **Step 1: Verify the class/file map + constants**

Run: `ls Garbus.Game/Timing Garbus.Game/Gameplay/Audio`
Run: `rg -n "FramedChartClock|OffsetCorrectionClock|GameplayClockContainer|MasterGameplayClockContainer|ChartOffset|15|WASAPI" Garbus.Game/Timing`
Expected: the clock classes and platform-offset constants (Windows +15ms base; experimental WASAPI −25ms) resolve. Confirm the numbers before quoting them.

- [ ] **Step 2: Write the doc using the template**

Cover: the vendored clock stack (`FramedChartClock` with plain `ChartOffset`, `OffsetCorrectionClock` rate-scaled user offset, `GameplayClockContainer`/`MasterGameplayClockContainer` taking a `Track` directly, `IGameplayClock`); the **`Content.Clock = GameplayClock` deviation** — why it exists (Garbus dropped DrawableRuleset/FrameStabilityContainer, so nothing else applies the gameplay clock to the playfield subtree) and the symptom if removed (playfield runs on wall-time; objects vanish once the app outlives the chart); **background:** almost all latency-critical audio (BASS stack, `AudioManager.InitBass` buffer/update values, interpolating/decoupling clocks) lives in osu-framework and must not be touched without hardware latency data; hitsounds (`HitSoundContainer` + `DrawableSample`, fixed per-object bank); the FrameStabilityContainer note (what it did in osu — clock application + fixed-substep frame stability for replay determinism; Garbus has no replays so substepping was skipped; vendorable standalone if low-FPS judgement precision ever matters). `GarbusConfigManager.AudioOffset` global setting.

- [ ] **Step 3: Verify + commit**

Run: `rg -i "bigasscircle|\bBAC\b|phase [0-9]|\bport\b" docs/agents/timing-audio.md`
Expected: no matches.
```bash
git add docs/agents/timing-audio.md
git commit -m "docs: add timing/audio domain doc"
```

---

### Task 7: Write `docs/agents/input.md`

**Files:**
- Create: `docs/agents/input.md`
- Mine from: `CLAUDE.md` "Phase 2" input paragraph, `docs/rules-specs/Inputs.md`
- Verify against: `Garbus.Game/Input/`

**Interfaces:**
- Produces: the input domain doc. Cross-links gameplay.md (actions drive hit judgement) and `docs/rules-specs/Inputs.md` (rules authority).

- [ ] **Step 1: Verify the class/file map**

Run: `ls Garbus.Game/Input`
Run: `rg "enum GarbusAction|class GarbusInputManager|class AnalogInputManager|class RadialJoystickHandler|class StickGestureTracker|class KeyBindingStore" Garbus.Game/Input -n`
Expected: types resolve (`GarbusAction`, `GarbusInputManager`, `AnalogInputManager`, `RadialJoystickHandler`, `StickGestureTracker`, `KeyBindingStore`, gamepad button types).

- [ ] **Step 2: Write the doc using the template**

Cover: `GarbusAction` enum; `GarbusInputManager` (framework `KeyBindingContainer<GarbusAction>`, `SimultaneousBindingMode.All`, keyboard + gamepad defaults); `KeyBindingStore` (config-backed rebinding — verify it is now config-backed, not hardcoded, since CLAUDE.md's "hardcoded defaults" note predates the controller-remapping work); `AnalogInputManager` + `RadialJoystickHandler` + `StickGestureTracker` (stick catchers, radial deadzone, slam gestures); gamepad button icons/types. **osu-framework background:** `KeyBindingContainer`, `IKeyBindingHandler`, action-vs-key mapping. Point to `docs/rules-specs/Inputs.md` for the input *rules*.

- [ ] **Step 3: Verify + commit**

Run: `rg -i "bigasscircle|\bBAC\b|phase [0-9]|hardcoded" docs/agents/input.md`
Expected: no stale claims (confirm the rebinding status in Step 2 before writing).
```bash
git add docs/agents/input.md
git commit -m "docs: add input domain doc"
```

---

### Task 8: Write `docs/agents/screens.md`

**Files:**
- Create: `docs/agents/screens.md`
- Mine from: `CLAUDE.md` Phase 5 song-select/settings notes, `PLAN-port.md:212-235` (Phase 5 — present tense, VERIFY each item)
- Verify against: `Garbus.Game/Screens/`, `Garbus.Game/Settings/`, `Garbus.Game/BuildInfo*.cs`

**Interfaces:**
- Produces: the screens domain doc. Cross-links gameplay.md (PlayScreen hosts the playfield) and charts.md (song select reads the chart library).

- [ ] **Step 1: Verify what actually exists (legacy notes are stale here)**

Run: `ls Garbus.Game/Screens Garbus.Game/Screens/SongSelect Garbus.Game/Settings`
Run: `rg "class MainMenuScreen|class PlayScreen|class SongSelectScreen|class SettingsOverlay|class ChartLibrary|class BuildInfoOverlay" Garbus.Game -n`
Run: `rg -li "class .*Results" Garbus.Game --include=*.cs`
Expected: main menu, PlayScreen, song select stack, `SettingsOverlay` (settings EXISTS — do not repeat PLAN-port's "settings unfinished" claim), build-info overlay all resolve; **no results screen exists** — state that as a present-tense gap. There is **no in-app updater screen** — the platform-updater/release-workflow is CI/desktop tooling, not a game screen; do not describe it as a screen.

- [ ] **Step 2: Write the doc using the template**

Cover: screen flow (main menu → song select → PlayScreen; settings via overlay); song select (`SongSelectScreen`, `ChartLibrary`, `IChartSource`/`ResourceChartSource`/`DirectoryChartSource`, `SongGroup` grouping, `ChartCard`/`ChartRow`/`ChartDetailPanel`, looping audio preview, grouped↔flat toggle persisted in config); `PlayScreen` game loop (clock stack → input → playfield, score/combo/accuracy with rewind-revert, inline results summary, space=pause, R=restart, Escape exits); `SettingsOverlay` + panels (`ControlsPanel`, `ButtonTestPanel`, volume/offset sliders, `KeyBindingRow`); `BuildInfoOverlay`. **Present-tense gaps:** no dedicated results screen; song-select deferred items (background rendering, search box). **osu-framework background:** `ScreenStack`/`IScreen`, overlays.

- [ ] **Step 3: Verify + commit**

Run: `rg -i "bigasscircle|\bBAC\b|phase [0-9]|deferred to phase|unfinished" docs/agents/screens.md`
Expected: no matches (gaps stated present-tense, not as "phase" items).
```bash
git add docs/agents/screens.md
git commit -m "docs: add screens domain doc"
```

---

### Task 9: Write `docs/agents/testing.md`

**Files:**
- Create: `docs/agents/testing.md`
- Mine from: `CLAUDE.md` test gotchas + the manual-clock caveat + harness-clock-wiring gotcha, `PLAN-port.md` test notes
- Verify against: `Garbus.Game.Tests/` (esp. `Tuning/`, `Profiling/`, `Editor/`, `Visual/`)

**Interfaces:**
- Produces: the testing domain doc. Referenced by AGENTS.md's Rules section (deferred wishlist items 1/2/6 will link here).

- [ ] **Step 1: Verify the test layout + tuning scenes**

Run: `ls Garbus.Game.Tests Garbus.Game.Tests/Tuning Garbus.Game.Tests/Profiling Garbus.Game.Tests/Editor Garbus.Game.Tests/Visual`
Expected: confirms `Tuning/TestSceneSliderGlowTuning.cs`, `Profiling/TestSceneHeadOnlySliderStream.cs`, the visual and editor test dirs. Record real paths.

- [ ] **Step 2: Write the doc using the template**

Cover: the two test modes — headless NUnit (`dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`) vs the visual test browser (run `Garbus.Game.Tests` directly); base `GarbusTestScene`; testing practices (deterministic manual clock; **the manual-clock-jump gotcha** — a single jump larger than an object's alive window skips judgement entirely, step in sub-window increments); **harness clock wiring** — placement/selection harnesses must wire the composer subtree's `Clock` to the `EditorClock` (as `ComposeTab` does) or editor-time-relative behavior can't be reproduced; placement auto-seek waits before asserting positions; the **visual tuning-scene pattern** (`Tuning/`, `Profiling/`) as the template for tuning tests on new visual features. **osu-framework background:** `TestScene`, `ManualInputManager`, `AddStep`/`AddAssert`, manual clocks. Add a short "Testing practices" subsection noting AGENTS.md's Rules section is where the enforced rules (no ordering-dependent tests, no warnings, tuning tests for visual features) will live.

- [ ] **Step 3: Verify + commit**

Run: `rg -i "bigasscircle|\bBAC\b|phase [0-9]|port" docs/agents/testing.md`
Expected: no matches.
```bash
git add docs/agents/testing.md
git commit -m "docs: add testing domain doc"
```

---

### Task 10: Write `AGENTS.md` (hub + mind map + index)

**Files:**
- Create: `AGENTS.md`
- Mine from: `CLAUDE.md` header/build/conventions, `PLAN-port.md` locked decisions (dropped-list), `new-agents-md-wishlist.txt` (Rules section seed only)

**Interfaces:**
- Consumes: all eight `docs/agents/*.md` paths (Tasks 2-9) — every link in the index and mind map must resolve. Consumes the submodule paths + `git submodule update --init` command (Task 1).
- Produces: the canonical hub referenced by CLAUDE.md (Task 11).

- [ ] **Step 1: Write AGENTS.md**

Sections in order (per spec §"AGENTS.md content"):
1. **What Garbus is** — standalone rhythm game built directly on osu-framework (no osu.Game dependency); circular playfield, objects spawn centre → travel to ring, judged at the edge to the music. No BAC/"port".
2. **Experimental rule** — the "backwards compatibility will NEVER matter…" paragraph verbatim from CLAUDE.md; integration branch is master.
3. **Build / run / test / logs** — the four commands + log/config paths from CLAUDE.md, plus: "Reference source: `git submodule update --init` populates `docs/code-reference/` (osu-framework `2026.629.0` + osu.Game source) for lookup — not needed to build or run."
4. **Rules** — seed with today's rule-shaped items (no historical context in docs; update the relevant domain doc as work lands; vendored files keep their ppy MIT header and deviate minimally). Add a note: "Reserved for enforced behavioral rules (see `new-agents-md-wishlist.txt`)."
5. **Conventions** — nullability + `= null!`; chart-not-beatmap; deliberately-dropped list (no mods/difficulty calc/replays/skinning/realm) as design facts.
6. **Mind map** — a Mermaid ` ```mermaid / mindmap ` block, root `Garbus`, one branch per domain doc (osu-framework, charts, gameplay, editor, timing-audio, input, screens, testing) with 2-4 key subtopics each.
7. **Doc index** — a table: each `docs/agents/*.md` + `docs/rules-specs/*`, `docs/charting-specs/*`, `docs/presentation-specs/*` with a "read when…" hook.

- [ ] **Step 2: Verify every link resolves and the mind map renders**

Run: `rg -o "\]\(([^)]+\.md)\)" AGENTS.md -r '$1' | while read f; do test -e "$f" || echo "BROKEN: $f"; done`
Expected: no `BROKEN:` lines.
Run: `rg -n "^\s*mindmap" AGENTS.md`
Expected: one match inside a ```mermaid fence.

- [ ] **Step 3: Verify no BAC leakage**

Run: `rg -i "bigasscircle|\bBAC\b|PLAN-port|\bport\b" AGENTS.md`
Expected: no matches.

- [ ] **Step 4: Commit**

```bash
git add AGENTS.md
git commit -m "docs: add AGENTS.md canonical index and mind map"
```

---

### Task 11: Reduce CLAUDE.md to a pointer stub

**Files:**
- Modify: `CLAUDE.md` (replace entire contents)

**Interfaces:**
- Consumes: `AGENTS.md` (Task 10) must exist first.

- [ ] **Step 1: Replace CLAUDE.md with the stub**

Full new contents:
```markdown
# Garbus

Agent onboarding, build/run/test, conventions, the repo mind map, and per-domain knowledge docs
live in [AGENTS.md](AGENTS.md). Start there.
```

- [ ] **Step 2: Verify**

Run: `wc -l CLAUDE.md && rg -i "bigasscircle|\bBAC\b|phase [0-9]" CLAUDE.md`
Expected: a handful of lines; no matches.

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: reduce CLAUDE.md to AGENTS.md pointer"
```

---

### Task 12: Delete the legacy files

**Files:**
- Delete: `PLAN-port.md`, `ISSUES.md`, `Phase4-Issues.md`, `docs/plan-timing-screen-port.md`

**Interfaces:**
- Consumes: Tasks 2-11 complete (all still-true knowledge migrated + verified).

- [ ] **Step 1: Confirm nothing references the files being deleted**

Run: `rg -n "PLAN-port|ISSUES\.md|Phase4-Issues|plan-timing-screen-port" --glob '!docs/superpowers/**' .`
Expected: only matches inside the files themselves (and this plan). If any live doc links them, fix it first.

- [ ] **Step 2: Delete**

```bash
git rm PLAN-port.md ISSUES.md Phase4-Issues.md docs/plan-timing-screen-port.md
```

- [ ] **Step 3: Commit**

```bash
git commit -m "docs: remove port plan and stale issue trackers"
```

---

### Task 13: Final whole-repo verification

**Files:** none (verification only).

- [ ] **Step 1: BAC/port sweep is clean across agent-facing files**

Run: `rg "BigAssCircle|\bBAC\b|PLAN-port|ISSUES.md|Phase4-Issues|\bport plan\b" -i --glob '!docs/code-reference/**' --glob '!docs/superpowers/**' .`
Expected: the only remaining hits are ppy MIT attribution headers inside vendored `.cs` files (leave those) and the sanctioned `docs/code-reference` submodule pointer in `osu-framework.md`/`AGENTS.md`. No prose references to BAC or a port.

- [ ] **Step 2: All AGENTS + domain-doc links resolve**

Run:
```bash
rg -o "\]\(([^)]+\.md)\)" AGENTS.md docs/agents/*.md -r '$1' --no-filename | sort -u | while read f; do test -e "$f" || test -e "$(dirname AGENTS.md)/$f" || echo "CHECK: $f"; done
```
Expected: no `CHECK:` lines for repo-relative doc links (resolve any that are written relative to a different base).

- [ ] **Step 2b: Every domain doc follows the 5-section template**

Run: `for f in docs/agents/*.md; do echo "== $f =="; rg -c "^#{1,3} " "$f"; done`
Expected: each doc has the Purpose/File map/Connects/Framework/Gotchas headings. Spot-check one to confirm the gotcha entries carry a pinning-test reference.

- [ ] **Step 3: Build + tests still pass (nothing referenced deleted files)**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: build succeeds.
Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all tests green (docs-only change; this confirms no code depended on the deleted files).

- [ ] **Step 4: Final commit (if any verification fixes were made)**

```bash
git add -A && git commit -m "docs: final restructure verification fixes" || echo "nothing to commit"
```

---

## Self-Review

**Spec coverage:**
- End-state layout (8 domain docs, AGENTS.md, CLAUDE.md stub, deletions, submodules) → Tasks 1-12. ✓
- AGENTS.md content (7 sub-sections incl. Rules + mind map + index) → Task 10. ✓
- Domain doc template → enforced per doc + Task 13 Step 2b. ✓
- Content migration map → each domain task's "Mine from" + the accuracy `comm` check (Task 5 Step 2). ✓
- Reference submodules under `docs/code-reference/` (pinned, never built, opt-in) → Task 1. ✓
- Accuracy rule (verify before write) → every task Step 1 + Global Constraints. ✓
- Verification (BAC-clean grep, links resolve, build/tests pass) → Task 13. ✓
- Out-of-scope wishlist rules → reserved in Task 10 Rules section, not implemented. ✓

**Placeholder scan:** No TBD/TODO. The domain-doc content steps intentionally specify *what to mine + what to verify + the required skeleton* rather than pre-writing final prose, because the prose must be generated from verified source reads at execution time (pre-writing it in the plan would risk fabricating unverified claims — the exact failure mode the accuracy rule guards against). Every such step names exact source sections, exact verification commands, and the exact section list to produce.

**Type consistency:** Doc filenames, submodule paths (`docs/code-reference/...`), and the pinned tag (`2026.629.0`) are used identically across Tasks 1, 2, 10. Cross-links between domain docs are declared in each task's Interfaces block.
