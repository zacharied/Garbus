# Agent documentation restructure — design

## Problem

Agent performance in this repo is degrading because knowledge is scattered: CLAUDE.md has accreted
into a 242-line mix of onboarding, per-phase state dumps, and ~20 gotchas; PLAN-port.md frames the
whole project as a port of BAC (framing that is no longer true or useful); ISSUES.md and
Phase4-Issues.md are mostly crossed-out FIXED entries whose write-ups hold real root-cause
knowledge; and there is no index that tells an agent where to look for a given domain.

## Goal

- Remove all BAC/"port" framing from agent-facing context. Garbus is described as what it is: a
  standalone rhythm game built directly on osu-framework.
- Centralize knowledge into self-sufficient per-domain docs under `docs/agents/`, each covering the
  local codebase, the osu-framework background that domain leans on, and its gotchas.
- Make `AGENTS.md` the canonical onboarding doc and index, with a Mermaid mind map of the repo.
- Docs-only change: no code changes.

## End-state file layout

```
AGENTS.md                       canonical onboarding + rules + index + Mermaid mind map (new)
CLAUDE.md                       one-line pointer stub to AGENTS.md
docs/agents/osu-framework.md    framework primer + cross-cutting traps (new)
docs/agents/charts.md           (new)
docs/agents/gameplay.md         (new)
docs/agents/editor.md           (new)
docs/agents/timing-audio.md     (new)
docs/agents/input.md            (new)
docs/agents/screens.md          (new)
docs/agents/testing.md          (new)
docs/rules-specs/               unchanged (game rules; linked from the index)
docs/charting-specs/            unchanged (linked from the index)
docs/presentation-specs/        unchanged (linked from the index)
docs/superpowers/               unchanged; historical; NOT linked from AGENTS.md
reference/osu-framework         git submodule, pinned to tag 2026.629.0 (reference only, never built)
reference/osu                   git submodule, pinned near that release (reference only, never built)
.gitmodules                     (new)
```

Deleted files: `PLAN-port.md`, `ISSUES.md`, `Phase4-Issues.md`, `docs/plan-timing-screen-port.md`.
Their still-true knowledge migrates per the map below; their open/todo items are dropped as
outdated (user decision, 2026-07-24).

## AGENTS.md content

Short and stable. In order:

1. **What Garbus is** — a standalone rhythm game built directly on osu-framework (no osu.Game
   dependency): hit objects spawn at the centre of a circular playfield and travel outward toward
   the ring, judged at the edge in time with the music. No mention of BAC or "port".
2. **The experimental rule** — the existing "backwards compatibility will NEVER matter until this
   line is removed" paragraph, kept verbatim. Integration branch is master.
3. **Build / run / test / logs** — the four commands plus log/config paths, carried over from
   CLAUDE.md, plus the one-time `git submodule update --init` note for populating `reference/`
   (framework/osu.Game source lookup; not required to build or run).
4. **Rules** — a dedicated section for hard behavioral rules, seeded with what is already
   rule-shaped today (no historical context in docs; update the relevant domain doc as work lands;
   vendored files keep their ppy MIT attribution header and deviate minimally). The deferred
   wishlist rules (`new-agents-md-wishlist.txt` in the main repo dir: tuning tests for visual
   features, no test warnings, run formatters, never run the app unasked, no ordering-dependent
   tests) slot in here later — out of scope for this restructure.
5. **Conventions** — nullability enabled solution-wide (`= null!` for DI/BDL fields); osu's
   "beatmap" is "chart" here; the deliberately-dropped list (no mods, difficulty calculation,
   replays, skinning, or realm — stated as present-tense design facts, not port decisions).
6. **Mind map** — a Mermaid `mindmap` block whose top-level branches match the eight domain docs,
   with key subtopics per branch, so the map doubles as the table of contents.
7. **Doc index** — a table linking the eight `docs/agents/` docs plus the rules-specs,
   charting-specs, and presentation-specs, each with a one-line "read this when…" hook.

CLAUDE.md becomes a stub: a single line directing the reader to AGENTS.md (kept because Claude
Code auto-loads CLAUDE.md; AGENTS.md is the tool-agnostic canonical home).

## Local osu-framework / osu source for agent lookup

Agents currently have to read framework and osu.Game source out of BAC's
`C:\Users\zachd\Code\BAC\LocalDependencies` — an unrelated repo that can move or drift. NuGet
offers no on-disk source tree (`ppy.osu.Framework` ships compiled DLLs plus a SourceLink symbols
package that only fetches individual files into an IDE debugger on demand — nothing grep-able). So
the reference source is vendored as two git submodules under a new `reference/` directory:

- `reference/osu-framework` pinned to tag **2026.629.0** (exactly the `ppy.osu.Framework`
  `PackageReference` version).
- `reference/osu` pinned to the release tag nearest that framework version (reference only — Garbus
  vendored specific files and never builds against osu.Game, so the exact pin is low-stakes; the
  chosen tag is recorded in osu-framework.md).

Constraints:

- **Never built.** The `.slnf` files enumerate projects explicitly, so the submodules are already
  outside every build; the design additionally confirms no MSBuild glob (`Directory.Build.props`,
  wildcards) reaches into `reference/`, and adds a guard if one does.
- **Opt-in checkout.** Submodules are not populated until `git submodule update --init`, so they
  never bloat a checkout involuntarily. AGENTS.md's build section documents this one-time command.
- `osu-framework.md` points agents at `reference/` for framework/osu.Game source; the BAC
  `LocalDependencies` path is removed from all agent-facing docs.

## Domain doc template

Every `docs/agents/*.md` follows the same skeleton:

1. **Purpose & scope** — one paragraph; what the domain covers and what it explicitly does not.
2. **File / class map** — where things live, key types with one-line descriptions.
3. **How it connects** — the other domains it touches and through what seams.
4. **osu-framework background** — the framework concepts this domain leans on, explained enough
   that an agent does not need to leave the doc (per-domain self-sufficiency requirement).
5. **Gotchas** — each entry: symptom, root cause, the rule to follow, and the pinning test.

## Per-doc scope

| Doc | Scope |
|---|---|
| `osu-framework.md` | Framework primer: DI/BDL, drawable lifetime + transforms, input hierarchy, clock plumbing, test scene basics. Cross-cutting traps stated generally (vertical fill-flow collapses relative-size children, ScrollContainer content anchoring, lambda subscription leaks, manual drawable disposal), cross-linked to the domain-specific instances. Points agents at the `reference/osu-framework` and `reference/osu` submodules for framework/osu.Game source lookup (with the `git submodule update --init` note), and records the pinned tags. No BAC `LocalDependencies` reference. |
| `charts.md` | `.garbus` JSON format ("type" discriminator first, version field), serializer/DTO layer, `GarbusChart`/`ChartMetadata`, `ChartFile` (editor disk handle), `ChartStore`, `Charts/Timing/` control points, `GarbusTestChartGenerator` + bundled-chart regeneration. |
| `gameplay.md` | Vendored HitObject/DrawableHitObject/pooling stack and what was trimmed; Garbus objects + drawables; `GarbusPlayfield`/`Ring`/`Lane` and the polar time→radius scrolling container; ordered hit policy; judgement implementation (families, windows, note-lock, duration/slider/slam rules) and the judgement feedback halo — linking `docs/rules-specs/Judgement.md` as the rules source of truth. |
| `editor.md` | Everything under `Edit/`: shell (tabs, menus, hotkeys, dirty tracking), EditorChart aliasing, undo/redo JSON diff, compose stack (angle mapping, blueprints, drawable lifecycle), timeline strip, bottom bar/test mode, Setup/Timing/Verify tabs, clipboard. Largest gotcha section (~15 entries). |
| `timing-audio.md` | The vendored clock stack (`FramedChartClock`, `OffsetCorrectionClock`, gameplay clock containers), platform offset constants, what latency tuning lives in osu-framework and must not be touched, hitsound playback (`HitSoundContainer`), the `Content.Clock = GameplayClock` deviation and the FrameStabilityContainer explanation (what it did in osu, why Garbus doesn't have it, when to vendor it). |
| `input.md` | `GarbusAction`, `GarbusInputManager` key binding container, keyboard/gamepad defaults, `AnalogInputManager` and stick catchers, radial deadzone handling, controller remapping. |
| `screens.md` | Screen flow: main menu, song select (chart library/sources/grouping/preview), `PlayScreen` game loop (score/combo/rewind, inline results), settings overlay, build-info overlay, updater/release workflow. Known gaps stated present-tense (verified against code at migration time, not copied from PLAN-port). |
| `testing.md` | Headless vs visual test patterns; the test browser; testing practices and conventions; manual-clock stepping gotcha (sub-window increments); harness clock wiring (composer subtree must use the EditorClock); placement auto-seek waits; the visual tuning-scene pattern (e.g. slider-glow tuning scene, profiling scenes) as the template wishlist item 1 will build on. |

## Content migration map

| Source | Destination |
|---|---|
| CLAUDE.md intro / port framing | Rewritten in AGENTS.md without BAC; `Bac*`→`Garbus*` rename note dropped |
| CLAUDE.md build/run/test, conventions | AGENTS.md |
| CLAUDE.md phase-state dumps (Phases 2–4 + Phase 3 section) | Present-tense file maps in charts.md / gameplay.md / editor.md / timing-audio.md / input.md / screens.md |
| CLAUDE.md gotcha list | editor.md (most), gameplay.md, testing.md; generalizable traps also stated in osu-framework.md |
| CLAUDE.md judgement + feedback summaries | gameplay.md |
| PLAN-port.md locked decisions | Present-tense facts in AGENTS.md (dropped-list) and charts.md (own-format rationale) |
| PLAN-port.md audio investigation + platform offsets | timing-audio.md |
| PLAN-port.md vendoring manifest / dependency split | Brief per-domain "vendored from osu.Game — deviate minimally" notes; the manifest itself is not preserved |
| BAC `LocalDependencies` reference path | Replaced by the `reference/` submodules; pointer lives in osu-framework.md |
| PLAN-port.md phase checklists, open questions | Dropped (verify any surviving claim against code first) |
| ISSUES.md FIXED write-ups | Gotcha entries in editor.md / gameplay.md / timing-audio.md / testing.md |
| ISSUES.md FrameStabilityContainer Q&A | timing-audio.md |
| ISSUES.md / Phase4-Issues.md open items | Dropped as outdated (user decision) |
| Phase4-Issues.md FIXED write-ups | editor.md gotchas (input-order root cause is already a CLAUDE.md gotcha; merge, don't duplicate) |
| docs/plan-timing-screen-port.md | Deleted (pointer to a completed plan) |
| docs/superpowers/ specs + plans | Left in place, unlinked; still-load-bearing knowledge folded into domain docs so agents never need them |

## Accuracy rule

CLAUDE.md and PLAN-port.md are known-stale in places (e.g. "Phase 4 complete" headers while song
select, settings, and judgement feedback have landed; PLAN-port lists a settings screen as
unfinished while `Garbus.Game/Settings/` exists). No claim is migrated blind: every factual
statement is verified against the current tree (class exists, test exists, behavior matches)
before it lands in a domain doc; dead or unverifiable claims are dropped. All docs are written
present-tense with no phase history or version numbers, per the repo's no-historical-context rule.

## Verification

- `rg "BigAssCircle|\bBAC\b|PLAN-port|ISSUES.md|Phase4-Issues" -i` over the repo's own files
  (excluding the `reference/` submodules) comes back clean of agent-facing references; the only
  sanctioned remaining mentions are ppy MIT attribution headers in vendored source files. The BAC
  `LocalDependencies` path is gone entirely, replaced by the `reference/` submodules.
- `reference/` is excluded from every build: `dotnet build Garbus.Desktop.slnf` does not compile
  any file under it, confirmed with the submodules checked out.
- Every relative link in AGENTS.md and the domain docs resolves to an existing file.
- `dotnet build Garbus.Desktop.slnf` and the headless test suite still pass (docs-only change;
  confirms nothing referenced the deleted files).

## Out of scope

- The five behavioral hard rules from `new-agents-md-wishlist.txt` (the Rules section reserves
  their home; writing them is a follow-up).
- Any change to game code, tests, or the rules-specs/charting-specs/presentation-specs content.
- Mining or deleting docs/superpowers history.
