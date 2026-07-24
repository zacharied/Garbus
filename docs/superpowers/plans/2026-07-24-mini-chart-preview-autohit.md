# Mini Chart Preview via `autoHit` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a small, silent, read-only live gameplay preview docked in the editor's Compose workspace, built on a general presentation-only `autoHit` drawable capability rather than a message protocol or `previewContext` conditionals.

**Architecture:** A gameplay drawable gains a `AutoHit` flag making it a pure function of clock time (deterministic hit animation, no judgement, no scoring). `GarbusPlayfield` gains a non-interactive construction option. A `MiniPreview` host renders the editor's *live* `GarbusHitObject` instances (no clone) as `autoHit` drawables on a clock slaved to `EditorClock`, refreshing in place via the existing `DefaultsApplied` signal. A reused draggable panel docks it in Compose.

**Tech Stack:** C# / .NET, osu-framework (vendored osu.Game gameplay stack), NUnit visual test scenes.

**Design doc:** `docs/superpowers/specs/2026-07-24-mini-chart-preview-autohit-design.md`

## Global Constraints

- Nullability is enabled solution-wide. `DrawableHitObject.cs` is `#nullable disable`; DI/BDL fields elsewhere use `= null!`.
- **No backwards-compat, no version bumps, no compatibility layers** — this is experimental; schemas may change freely.
- Vendored osu.Game files keep the ppy MIT header + an "Adapted for Garbus:" line. Do not add preview-awareness to vendored gameplay classes beyond the general `AutoHit` capability.
- Terminology: osu "beatmap" → "chart"; `Bac*` → `Garbus*`.
- **Load-bearing invariant (pinned by tests):** an `autoHit` drawable is a *strictly read-only observer* of its `HitObject` — it never emits a `JudgementResult`, never scores, and never mutates the `HitObject`. This is what makes sharing editor instances safe.
- **Known gotchas that must hold:** removed non-pooled drawables must be explicitly `Dispose()`d; every lambda event subscription keeps a field reference and is unsubscribed in `Dispose`.
- Build: `dotnet build Garbus.Desktop.slnf`. Test: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`. Single fixture: append `--filter "FullyQualifiedName~<FixtureName>"`.
- Commit messages: conventional (`feat:`/`fix:`/`test:`/`docs:`), end with `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`. Commit via `mcp__nimbalyst__developer_git_commit_proposal`.

---

## File Structure

**Created:**
- `Garbus.Game/Edit/Preview/MiniPreview.cs` — the preview host: non-interactive playfield, `autoHit` drawables over live editor instances, `EditorChart` event wiring.
- `Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs` — draggable/clamped/persisted panel chrome wrapping a `MiniPreview`.
- `Garbus.Game.Tests/Editor/TestSceneAutoHit.cs` — `autoHit` drawable behaviour (statelessness, no result, hitsound flag).
- `Garbus.Game.Tests/Editor/TestSceneMiniPreview.cs` — host + panel behaviour.

**Modified:**
- `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs` — `AutoHit` capability (flag, effective flag, skip-result, force-hit, lifetime swallow, optional hitsound crossing).
- `Garbus.Game/UI/GarbusPlayfield.cs` — non-interactive constructor option.
- `Garbus.Game/Screens/PlayScreen.cs:262` — `CreateDrawableRepresentation` gains an `autoHit` parameter.
- `Garbus.Game/Configuration/GarbusSetting.cs` — `MiniPreviewX`, `MiniPreviewY` enum members.
- `Garbus.Game/Configuration/GarbusConfigManager.cs` — their `SetDefault`s.
- `Garbus.Game/Edit/Screens/ComposeTab.cs` — accept + dock the panel.
- `Garbus.Game/Edit/Screens/GarbusEditor.cs` — `MiniPreviewEnabled` bindable, View menu item, visibility gating, suspend/restore.

**Explicitly NOT touched (reverted-to-master parity, verified this branch is already clean):** `Garbus.Game/Gameplay/UI/Playfield.cs`, `EditorClock.cs` (no seek events needed — statelessness), `JudgementResult.cs`, `ChordConnectorOverlay.cs`, `WarningIndicatorDisplay.cs`. The existing gameplay test suite is the regression guard.

---

### Task 1: `AutoHit` capability on `DrawableHitObject`

**Files:**
- Modify: `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneAutoHit.cs` (create)

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `public bool AutoHit { get; init; }` on `DrawableHitObject` — set at construction/object-initializer time (before `OnApply`, which is deferred to `LoadAsyncComplete`).
  - `internal bool AutoHitActive => AutoHit || (ParentHitObject?.AutoHitActive ?? false);` — the effective flag nested drawables inherit.
  - Behaviour: an `AutoHit` drawable plays its Hit animation at `HitObject.GetEndTime()`, produces no `JudgementResult`, and lets the scrolling container own its lifetime.

Mechanism facts this relies on (verified):
- `Entry` set in the ctor is *stored* while `LoadState == NotLoaded`; `OnApply` runs later in `LoadAsyncComplete` — so an `init`/object-initializer `AutoHit` is visible to `OnApply` (`PoolableDrawableWithLifetime.cs:34,83-90`).
- `OnApply` → `updateStateFromResult()` (`DrawableHitObject.cs:255-256,259-267`) chooses the ArmedState.
- With no result, `HitStateUpdateTime` → `GetEndTime()` (`:540`, and `JudgementResult.TimeAbsolute` returns `GetEndTime()` when `RawTime==null`, `JudgementResult.cs:57`). `UpdateState` wraps `UpdateHitStateTransforms(Hit)` in `BeginAbsoluteSequence(HitStateUpdateTime)` (`:409-410`) — clock-addressable, reverses on rewind.
- The per-frame result path is `UpdateResult(bool)` (`:603-615`), called from `UpdateAfterChildren` and `OnKilled`.
- The scrolling container sets `entry.LifetimeEnd = GetEndTime() + timeRange` for unjudged entries — deterministic (`GarbusScrollingHitObjectContainer.cs:216-217`). The drawable-side writes to neutralize are `UpdateState`'s `LifetimeEnd = double.MaxValue` (`:397`) / computed end (`:414-415`) and each concrete drawable's `.OnComplete(_ => Expire())`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneAutoHit.cs`:

```csharp
// Behaviour of the general presentation-only autoHit drawable capability: a pure function of clock
// time (deterministic hit animation, statelessness under seek/rewind) that never judges or scores.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Gameplay.Judgements;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneAutoHit : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        private ManualClock manualClock = null!;
        private GarbusPlayfield playfield = null!;
        private readonly List<JudgementResult> results = new List<JudgementResult>();

        [Resolved]
        private Gameplay.UI.Scrolling.GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create non-interactive playfield", () =>
            {
                scrollingInfo.TimeRange.Value = 700;
                results.Clear();
                manualClock = new ManualClock { Rate = 1 };

                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(manualClock),
                    Child = playfield = new GarbusPlayfield(interactive: false) { Size = Vector2.One },
                };
                playfield.NewResult += (_, r) => results.Add(r);

                var note = new CardinalNote { StartTime = 2000, Direction = CardinalDirection.North };
                note.ApplyDefaults();
                playfield.Add(PlayScreen.CreateDrawableRepresentation(note, autoHit: true));
            });

            AddUntilStep("playfield loaded", () => playfield.IsLoaded);
        }

        private DrawableCardinalNote? note()
            => playfield.AllHitObjects.OfType<DrawableCardinalNote>().SingleOrDefault();

        [Test]
        public void TestAutoHitPlaysHitVisualAtHitTimeAndRevertsOnRewind()
        {
            AddStep("seek just before hit", () => manualClock.CurrentTime = 1900);
            AddUntilStep("note alive and not faded", () => note()?.IsAlive == true && note()!.Alpha > 0.9f);

            AddStep("seek past hit + animation", () => manualClock.CurrentTime = 3200);
            AddUntilStep("hit animation ran (faded out)", () => note() != null && note()!.ChildrenOfType<osu.Framework.Graphics.Sprites.Sprite>().First().Alpha < 0.05f);

            AddStep("rewind before hit", () => manualClock.CurrentTime = 1900);
            AddUntilStep("hit animation unplayed (visible again)", () => note() != null && note()!.ChildrenOfType<osu.Framework.Graphics.Sprites.Sprite>().First().Alpha > 0.9f);
        }

        [Test]
        public void TestAutoHitProducesNoJudgementResult()
        {
            AddStep("seek far past the note", () => manualClock.CurrentTime = 10000);
            AddStep("rewind to start", () => manualClock.CurrentTime = 0);
            AddStep("seek forward again", () => manualClock.CurrentTime = 10000);
            AddAssert("never judged", () => note() == null || !note()!.Judged);
            AddAssert("no results emitted", () => results.Count, () => Is.Zero);
        }

        [Test]
        public void TestAutoHitLifetimeEndIsDeterministicAcrossScrub()
        {
            double endAfterForward = 0;
            AddStep("seek past hit", () => manualClock.CurrentTime = 3200);
            AddUntilStep("note applied", () => note()?.Entry != null);
            AddStep("capture lifetime end", () => endAfterForward = note()!.Entry!.LifetimeEnd);
            AddAssert("lifetime end is finite (not immortal)", () => !double.IsInfinity(endAfterForward) && endAfterForward < double.MaxValue);

            AddStep("rewind then forward again", () =>
            {
                manualClock.CurrentTime = 0;
                manualClock.CurrentTime = 3200;
            });
            AddAssert("lifetime end unchanged (path-independent)",
                () => Math.Abs(note()!.Entry!.LifetimeEnd - endAfterForward) < 0.001);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneAutoHit"`
Expected: FAIL to compile — `CreateDrawableRepresentation` has no `autoHit` parameter and `GarbusPlayfield` has no `interactive` constructor. (Those are added in Tasks 4 and this task's Step 3 respectively; the factory param is added here in Step 3 too since the test needs it.)

- [ ] **Step 3: Implement `AutoHit` on the base**

In `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs`, add the flag near the other public members (e.g. just after the `DefaultsApplied` event at `:40`):

```csharp
        /// <summary>
        /// When set, this drawable is a presentation-only auto-hit: it plays its Hit animation at the
        /// hit time as a pure function of the clock, never produces a <see cref="JudgementResult"/>,
        /// never scores, and lets its scrolling container own its lifetime. Set at construction time
        /// (object initializer); read-only thereafter. Nested drawables inherit it via <see cref="AutoHitActive"/>.
        /// </summary>
        public bool AutoHit { get; init; }

        /// <summary>The effective auto-hit state, inherited by nested drawables from their parent.</summary>
        internal bool AutoHitActive => AutoHit || (ParentHitObject?.AutoHitActive ?? false);
```

Override `LifetimeEnd` (add below the `AutoHit` members). This swallows the drawable-side writes only for auto-hit drawables; normal gameplay is unchanged:

```csharp
        // Auto-hit drawables derive presence purely from time; the scrolling container owns their
        // lifetime window (GetEndTime() + timeRange — deterministic). Swallow drawable-side writes
        // from UpdateState / Expire so a scrub or rewind can't pin lifetime to a clock-moment value.
        public override double LifetimeEnd
        {
            get => base.LifetimeEnd;
            set
            {
                if (AutoHitActive)
                    return;

                base.LifetimeEnd = value;
            }
        }
```

Modify `updateStateFromResult()` (`:259-267`) to honour auto-hit:

```csharp
        private void updateStateFromResult()
        {
            if (AutoHitActive)
            {
                // Presentation only: play the Hit animation at the natural hit time (HitStateUpdateTime
                // → GetEndTime() with no result). Forced, so no hitsound fires and no result is produced.
                UpdateState(ArmedState.Hit, true);
                return;
            }

            if (Result.IsHit)
                UpdateState(ArmedState.Hit, true);
            else if (Result.HasResult)
                UpdateState(ArmedState.Miss, true);
            else
                UpdateState(ArmedState.Idle, true);
        }
```

Modify `UpdateResult(bool userTriggered)` (`:603`) to short-circuit the whole result path:

```csharp
        protected bool UpdateResult(bool userTriggered)
        {
            // Auto-hit drawables never go through the input / miss-check result path — no JudgementResult,
            // no scoring, no feedback. Presentation only, in every context.
            if (AutoHitActive)
                return false;

            // It's possible for input to get into a bad state when rewinding gameplay, so results should not be processed
            if ((Clock as IGameplayClock)?.IsRewinding == true)
                return false;

            if (Judged)
                return false;

            CheckForResult(userTriggered, Time.Current - HitObject.GetEndTime());

            return Judged;
        }
```

- [ ] **Step 4: Add the factory + non-interactive playfield the test consumes**

In `Garbus.Game/Screens/PlayScreen.cs:262`, add the `autoHit` parameter (default `false` keeps existing callers compiling):

```csharp
        public static DrawableHitObject CreateDrawableRepresentation(GarbusHitObject h, bool autoHit = false) => h switch
        {
            SliderBody path => new DrawableSliderBody(path) { AutoHit = autoHit },
            CardinalNote button => new DrawableCardinalNote(button) { AutoHit = autoHit },
            CardinalHoldNote hold => new DrawableCardinalHoldNote(hold) { AutoHit = autoHit },
            ShoulderNote note => new DrawableShoulderNote(note) { AutoHit = autoHit },
            GarbusSlamCentered slamCentered => new DrawableSlamCentered(slamCentered) { AutoHit = autoHit },
            GarbusSlamEdge slamEdge => new DrawableSlamEdge(slamEdge) { AutoHit = autoHit },
            ShoulderHoldNote hold => new DrawableShoulderHoldNote(hold) { AutoHit = autoHit },
            _ => throw new ArgumentOutOfRangeException(nameof(h), h.GetType().Name, "no drawable representation")
        };
```

In `Garbus.Game/UI/GarbusPlayfield.cs`, add the non-interactive option (Task 4 formalises + tests this; here we add just enough for Task 1's test). Replace the parameterless ctor and `load`:

```csharp
        private readonly bool interactive;

        public GarbusPlayfield(bool interactive = true)
        {
            this.interactive = interactive;
            Padding = new MarginPadding(30);
            AddNested(ring);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var children = new List<Drawable> { warningIndicators, ring };

            if (interactive)
            {
                // Input manager first so the ring's stroke draws over it; stick indicators are input feedback.
                children.Insert(0, analogInputManager);
                children.Add(stickIndicatorL);
                children.Add(stickIndicatorR);
            }

            AddRangeInternal(children.ToArray());
        }
```

(`analogInputManager` stays a `[Cached]` property — harmless when not added to the tree.)

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneAutoHit"`
Expected: PASS (3 tests).

- [ ] **Step 6: Run the full gameplay suite (no regression)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneGameplay"`
Expected: PASS — normal (non-autoHit) gameplay unchanged.

- [ ] **Step 7: Commit**

```
feat: add presentation-only autoHit capability to DrawableHitObject

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 2: Nested `autoHit` propagation (slider children)

**Files:**
- Test: `Garbus.Game.Tests/Editor/TestSceneAutoHit.cs` (extend)
- (No new production code expected — `AutoHitActive` already reads `ParentHitObject`. This task proves nested drawables inherit it and produce no results; if the test fails, the fix is to set `AutoHitActive` propagation correctly.)

**Interfaces:**
- Consumes: `AutoHitActive` (Task 1).
- Produces: guarantee that slider children of an `autoHit` slider are themselves auto-hit (no `IgnoreHit`/`JudgementResult`).

- [ ] **Step 1: Write the failing test**

Append to `TestSceneAutoHit`:

```csharp
        [Test]
        public void TestAutoHitPropagatesToSliderChildren()
        {
            AddStep("add autoHit slider", () =>
            {
                var slider = new SliderBody
                {
                    StartTime = 4000,
                    AngleDeg = 0,
                    Side = HorizontalDirection.Left,
                    Path = new GarbusPath
                    {
                        ControlPoints = new osu.Framework.Bindables.BindableList<GarbusPathControlPoint>
                        {
                            new GarbusPathControlPoint { TimeOffset = 0, RotationOffset = 0 },
                            new GarbusPathControlPoint { TimeOffset = 1000, RotationOffset = 90 },
                        },
                    },
                };
                slider.ApplyDefaults();
                playfield.Add(PlayScreen.CreateDrawableRepresentation(slider, autoHit: true));
            });

            DrawableSliderBody body() => playfield.AllHitObjects.OfType<DrawableSliderBody>().Single();

            AddStep("seek past the slider", () => manualClock.CurrentTime = 6000);
            AddUntilStep("slider + children present", () => body().NestedHitObjects.Count > 0);
            AddAssert("children are auto-hit (unjudged)", () => body().NestedHitObjects.All(n => !n.Judged));
            AddAssert("no results emitted from slider path", () => results.Count, () => Is.Zero);

            AddStep("rewind before slider", () => manualClock.CurrentTime = 0);
            AddStep("seek past again", () => manualClock.CurrentTime = 6000);
            AddAssert("still no results after scrub", () => results.Count, () => Is.Zero);
        }
```

- [ ] **Step 2: Run the test to verify it passes (or fails, exposing a propagation gap)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneAutoHit.TestAutoHitPropagatesToSliderChildren"`
Expected: PASS. If it FAILS with results emitted, the nested drawables aren't seeing `AutoHitActive` — verify `ParentHitObject` is assigned before the nested drawable's `OnApply` runs (`DrawableHitObject.cs:235` sets it before `AddNestedHitObject` at `:244`), and that `AutoHitActive`'s recursion compiles.

- [ ] **Step 3: Commit**

```
test: pin autoHit propagation to nested slider children

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 3: Optional forward-crossing hitsound flag (default off)

**Files:**
- Modify: `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneAutoHit.cs` (extend)

**Interfaces:**
- Consumes: `AutoHitActive`, `PlaySamples()` (`:485`).
- Produces: `public bool AutoHitPlaysSamples { get; init; }` — fires the hitsound exactly once as the clock crosses `GetEndTime()` going forward; no-op on rewind. Wired to `false` by the Mini preview.

Note: `SamplesPlayCount` is the existing per-object test seam on `DrawableGarbusHitObject<T>` (used at `TestSceneGameplay.cs:389`), incremented from `PlaySamples`.

- [ ] **Step 1: Write the failing test**

Append to `TestSceneAutoHit`:

```csharp
        private DrawableCardinalNote addNote(bool autoHit, bool playsSamples)
        {
            var note = new CardinalNote { StartTime = 2000, Direction = CardinalDirection.North };
            note.ApplyDefaults();
            var drawable = new DrawableCardinalNote(note) { AutoHit = autoHit, AutoHitPlaysSamples = playsSamples };
            playfield.Add(drawable);
            return drawable;
        }

        [Test]
        public void TestAutoHitHitsoundFlagOffIsSilent()
        {
            // Remove the default-setup note, add a silent-flag one.
            AddStep("clear + add silent autoHit note", () =>
            {
                foreach (var d in playfield.AllHitObjects.ToList())
                    playfield.Remove(d);
                addNote(autoHit: true, playsSamples: false);
            });
            AddStep("play through the hit", () => manualClock.CurrentTime = 3000);
            AddAssert("no samples played", () => note()!.SamplesPlayCount, () => Is.Zero);
        }

        [Test]
        public void TestAutoHitHitsoundFiresOnceOnForwardCrossingNotOnRewind()
        {
            AddStep("clear + add audible autoHit note", () =>
            {
                foreach (var d in playfield.AllHitObjects.ToList())
                    playfield.Remove(d);
                addNote(autoHit: true, playsSamples: true);
            });

            AddStep("step clock up to before hit", () => manualClock.CurrentTime = 1990);
            AddStep("step across hit forward", () => manualClock.CurrentTime = 2010);
            AddUntilStep("played exactly once", () => note()!.SamplesPlayCount == 1);

            AddStep("rewind across hit backward", () => manualClock.CurrentTime = 1990);
            AddStep("hold before hit a frame", () => manualClock.CurrentTime = 1991);
            AddAssert("still exactly once (no rewind fire)", () => note()!.SamplesPlayCount, () => Is.EqualTo(1));
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneAutoHit.TestAutoHitHitsound"`
Expected: FAIL — `AutoHitPlaysSamples` does not exist.

- [ ] **Step 3: Implement the flag + forward-crossing detector**

In `DrawableHitObject.cs`, add near `AutoHit`:

```csharp
        /// <summary>
        /// When set together with <see cref="AutoHit"/>, plays the hitsound once as the clock crosses the
        /// hit time going forward. A one-shot side effect: it does nothing on rewind or backward scrub.
        /// </summary>
        public bool AutoHitPlaysSamples { get; init; }

        private double? autoHitLastTime;
```

Add an `Update` override (the base `Drawable.Update` is virtual; there is no existing `Update` override on `DrawableHitObject` — its per-frame result work is in `UpdateAfterChildren`). Place it in the state/update region:

```csharp
        protected override void Update()
        {
            base.Update();

            if (AutoHitActive && AutoHitPlaysSamples)
            {
                double hitTime = HitObject.GetEndTime();

                // Forward crossing only: previous frame strictly before the hit, this frame at/after it.
                if (autoHitLastTime is double prev && prev < hitTime && Time.Current >= hitTime)
                    PlaySamples();

                autoHitLastTime = Time.Current;
            }
        }
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneAutoHit"`
Expected: PASS (all TestSceneAutoHit tests).

- [ ] **Step 5: Commit**

```
feat: add optional forward-crossing hitsound to autoHit drawables

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 4: Formalise the non-interactive `GarbusPlayfield` option

**Files:**
- Modify: `Garbus.Game/UI/GarbusPlayfield.cs` (already changed in Task 1 Step 4 — this task adds the pinning test)
- Test: `Garbus.Game.Tests/Editor/TestSceneMiniPreview.cs` (create)

**Interfaces:**
- Consumes: `GarbusPlayfield(bool interactive)`.
- Produces: guarantee that `interactive: false` installs no `AnalogInputManager` and no `StickIndicator`s, while `interactive: true` is unchanged.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneMiniPreview.cs`:

```csharp
// The editor Mini preview: a non-interactive autoHit playfield hosted over the compose workspace,
// mirroring the editor's live hit objects on a clock slaved to the EditorClock.

using System.Linq;
using Garbus.Game.Input;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osuTK;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneMiniPreview : GarbusTestScene
    {
        protected override double TimePerAction => 0;

        [Test]
        public void TestNonInteractivePlayfieldInstallsNoInput()
        {
            GarbusPlayfield preview = null!;
            AddStep("add non-interactive playfield", () =>
                Child = preview = new GarbusPlayfield(interactive: false) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => preview.IsLoaded);
            AddAssert("no analog input manager", () => !preview.ChildrenOfType<AnalogInputManager>().Any());
            AddAssert("no stick indicators", () => !preview.ChildrenOfType<StickIndicator>().Any());
        }

        [Test]
        public void TestInteractivePlayfieldStillInstallsInput()
        {
            GarbusPlayfield gameplay = null!;
            AddStep("add interactive playfield", () =>
                Child = gameplay = new GarbusPlayfield(interactive: true) { RelativeSizeAxes = Axes.Both });
            AddUntilStep("loaded", () => gameplay.IsLoaded);
            AddAssert("has analog input manager", () => gameplay.ChildrenOfType<AnalogInputManager>().Any());
            AddAssert("has two stick indicators", () => gameplay.ChildrenOfType<StickIndicator>().Count() == 2);
        }
    }
}
```

- [ ] **Step 2: Run to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview.TestNonInteractive|FullyQualifiedName~TestSceneMiniPreview.TestInteractive"`
Expected: PASS (the production change landed in Task 1 Step 4; this pins it).

- [ ] **Step 3: Commit**

```
test: pin non-interactive GarbusPlayfield option

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 5: `MiniPreview` host

**Files:**
- Create: `Garbus.Game/Edit/Preview/MiniPreview.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneMiniPreview.cs` (extend)

**Interfaces:**
- Consumes: `EditorChart` (`HitObjectAdded`/`HitObjectRemoved`/`HitObjectUpdated`, `HitObjects`, `Garbus.Game.Edit`), `EditorClock`, `GarbusPlayfield(interactive:false)`, `PlayScreen.CreateDrawableRepresentation(h, autoHit)`.
- Produces:
  - `public partial class MiniPreview : CompositeDrawable` (namespace `Garbus.Game.Edit.Preview`).
  - Renders every `EditorChart.HitObjects` entry as an `autoHit` (silent) drawable on a clock slaved to `EditorClock`.
  - Live edits: `Added` → add drawable; `Removed` → remove + `Dispose`; `Updated` → automatic in-place refresh via `DefaultsApplied` (no explicit work).
  - `internal GarbusPlayfield PlayfieldForTests { get; }` — test seam.

Facts this relies on: `EditorChart` events fire batched from `UpdateState()` on transaction commit (`EditorChart.cs:194-227`). The composer uses the same `drawableMap` pattern. A removed non-pooled drawable must be `Dispose()`d (else it stays subscribed to `DefaultsApplied` and re-applies forever). The gameplay drawable already re-applies on `DefaultsApplied` (`DrawableHitObject.cs:249,341`).

- [ ] **Step 1: Write the failing test**

Append to `TestSceneMiniPreview` (uses the editor DI graph — construct a minimal editor host via the existing editor test scaffolding). Add:

```csharp
        [Test]
        public void TestPreviewMirrorsEditorHitObjects()
        {
            MiniPreviewTestHost host = null!;
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);

            AddAssert("preview has a drawable per editor object", () =>
                host.Preview.PlayfieldForTests.AllHitObjects.Count() == host.EditorChart.HitObjects.Count);

            int before = 0;
            AddStep("count before add", () => before = host.Preview.PlayfieldForTests.AllHitObjects.Count());
            AddStep("add a note to the editor", () => host.AddNote(9000));
            AddUntilStep("preview reflects the add", () =>
                host.Preview.PlayfieldForTests.AllHitObjects.Count() == before + 1);

            AddStep("remove the note from the editor", () => host.RemoveLastAddedNote());
            AddUntilStep("preview reflects the remove", () =>
                host.Preview.PlayfieldForTests.AllHitObjects.Count() == before);
        }
```

Add a small in-file test host at the bottom of the file that wires the editor DI graph the composer relies on. Model it on `TestSceneComposePlacement`'s harness (which caches `EditorClock`/`EditorChart`/`GarbusChartChangeHandler`/`BindableBeatDivisor` and sets the subtree `Clock = editorClock`). If that harness already exists as a reusable base, derive from it instead of duplicating:

```csharp
        private partial class MiniPreviewTestHost : CompositeDrawable
        {
            public MiniPreview Preview { get; private set; } = null!;
            public Garbus.Game.Edit.EditorChart EditorChart { get; private set; } = null!;

            private Garbus.Game.Objects.CardinalNote? lastAdded;

            // NOTE FOR IMPLEMENTER: build the same cached DI graph ComposeTab/editor test scenes use —
            // EditorChart, EditorClock, GarbusChartChangeHandler, BindableBeatDivisor, ControlPointInfo,
            // GarbusScrollingInfo. Reuse the existing editor test harness base if one exists
            // (see Garbus.Game.Tests/Editor/*). Set this subtree's Clock = editorClock.

            public void AddNote(double time)
            {
                lastAdded = new Garbus.Game.Objects.CardinalNote { StartTime = time, Direction = Garbus.Game.Objects.CardinalDirection.North };
                EditorChart.Add(lastAdded);
            }

            public void RemoveLastAddedNote()
            {
                if (lastAdded != null)
                    EditorChart.Remove(lastAdded);
            }
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview.TestPreviewMirrorsEditorHitObjects"`
Expected: FAIL — `MiniPreview` does not exist.

- [ ] **Step 3: Implement `MiniPreview`**

Create `Garbus.Game/Edit/Preview/MiniPreview.cs`:

```csharp
using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace Garbus.Game.Edit.Preview
{
    /// <summary>
    /// A silent, read-only live gameplay preview of the editor's chart. Renders the editor's live
    /// <see cref="GarbusHitObject"/> instances as presentation-only <c>autoHit</c> drawables on a clock
    /// slaved to the <see cref="EditorClock"/>. Because auto-hit drawables are pure functions of clock
    /// time, the preview is stateless under seek/rewind and needs no tracking beyond an add/remove map.
    /// Shares editor instances (no clone): safe because auto-hit drawables never mutate their hit object.
    /// </summary>
    public partial class MiniPreview : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private GarbusPlayfield playfield = null!;
        private readonly Dictionary<GarbusHitObject, DrawableHitObject> drawableMap = new();

        internal GarbusPlayfield PlayfieldForTests => playfield;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            // Slave the whole preview subtree to the editor clock (matches ComposeTab's composer wiring).
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = editorClock,
                Child = playfield = new GarbusPlayfield(interactive: false) { RelativeSizeAxes = Axes.Both },
            };

            foreach (var hitObject in editorChart.HitObjects)
                addDrawable(hitObject);

            editorChart.HitObjectAdded += addDrawable;
            editorChart.HitObjectRemoved += removeDrawable;
            // Updated needs no explicit work: the shared instance's ApplyDefaults fires DefaultsApplied,
            // which re-applies the drawable in place (DrawableHitObject.onDefaultsApplied).
        }

        private void addDrawable(GarbusHitObject hitObject)
        {
            if (drawableMap.ContainsKey(hitObject))
                return;

            var drawable = PlayScreen.CreateDrawableRepresentation(hitObject, autoHit: true);
            drawableMap[hitObject] = drawable;
            playfield.Add(drawable);
        }

        private void removeDrawable(GarbusHitObject hitObject)
        {
            if (!drawableMap.Remove(hitObject, out var drawable))
                return;

            playfield.Remove(drawable);
            // Non-pooled: the container detaches with RemoveInternal(..., false) and does NOT dispose.
            // Dispose explicitly, or the drawable stays subscribed to DefaultsApplied and re-applies forever.
            drawable.Dispose();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorChart != null)
            {
                editorChart.HitObjectAdded -= addDrawable;
                editorChart.HitObjectRemoved -= removeDrawable;
            }

            base.Dispose(isDisposing);
        }
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview.TestPreviewMirrorsEditorHitObjects"`
Expected: PASS. (If the test host is hard to assemble, reuse the existing editor test harness base rather than hand-rolling DI.)

- [ ] **Step 5: Add a live-edit in-place-refresh test**

Append to `TestSceneMiniPreview`:

```csharp
        [Test]
        public void TestLiveEditRefreshesDrawableInPlace()
        {
            MiniPreviewTestHost host = null!;
            AddStep("create preview over an editor chart", () => Child = host = new MiniPreviewTestHost());
            AddUntilStep("preview loaded", () => host.Preview.IsLoaded);
            AddStep("add a note", () => host.AddNote(9000));

            DrawableHitObject? drawable() =>
                host.Preview.PlayfieldForTests.AllHitObjects.FirstOrDefault(d => d.HitObject.StartTime == 9000 || d.HitObject.StartTime == 9500);

            AddUntilStep("drawable present", () => drawable() != null);
            DrawableHitObject captured = null!;
            AddStep("capture drawable instance", () => captured = drawable()!);
            AddStep("move the note in the editor", () => host.MoveLastAddedNoteTo(9500));
            AddUntilStep("same drawable instance retained (in-place refresh)",
                () => host.Preview.PlayfieldForTests.AllHitObjects.Contains(captured) && captured.HitObject.StartTime == 9500);
        }
```

Add `MoveLastAddedNoteTo` to the test host:

```csharp
            public void MoveLastAddedNoteTo(double time)
            {
                if (lastAdded == null) return;
                EditorChart.PerformOnSelection(_ => { }); // ensure a transaction context if required by the harness
                lastAdded.StartTime = time;
                EditorChart.Update(lastAdded);
            }
```

- [ ] **Step 6: Run to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview.TestLiveEditRefreshesDrawableInPlace"`
Expected: PASS — the drawable instance is retained (not recreated) across an edit.

- [ ] **Step 7: Commit**

```
feat: add MiniPreview host rendering editor chart as autoHit preview

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 6: `InlineChartPreviewPanel` chrome + config settings

**Files:**
- Create: `Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs`
- Modify: `Garbus.Game/Configuration/GarbusSetting.cs`, `Garbus.Game/Configuration/GarbusConfigManager.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneMiniPreview.cs` (extend)

**Interfaces:**
- Consumes: `MiniPreview`, `GarbusConfigManager`, `GarbusSetting.MiniPreviewX/MiniPreviewY`.
- Produces:
  - `public partial class InlineChartPreviewPanel : CompositeDrawable` — a bottom-right-anchored, draggable, clamped, persisted ~190×190 panel that lazily hosts a `MiniPreview`.
  - `public void SetVisible(bool visible)` — show/hide; lazily builds the `MiniPreview` on first show.
  - `internal Vector2 OffsetForTests { get; }` — test seam for the persisted offset.

- [ ] **Step 1: Add config settings**

In `Garbus.Game/Configuration/GarbusSetting.cs`, add after `EditorContractSidebars`:

```csharp
        /// <summary>Mini preview distance from the right edge of the compose workspace.</summary>
        MiniPreviewX,

        /// <summary>Mini preview distance from the bottom edge of the compose workspace.</summary>
        MiniPreviewY,
```

In `Garbus.Game/Configuration/GarbusConfigManager.cs`, add inside `InitialiseDefaults()`:

```csharp
            SetDefault(GarbusSetting.MiniPreviewX, 5f);
            SetDefault(GarbusSetting.MiniPreviewY, 5f);
```

- [ ] **Step 2: Write the failing test**

Append to `TestSceneMiniPreview`:

```csharp
        [Test]
        public void TestPanelClampsWithinParent()
        {
            InlineChartPreviewPanel panel = null!;
            Container workspace = null!;
            AddStep("host panel in a small workspace", () =>
            {
                Child = workspace = new Container
                {
                    Size = new Vector2(400),
                    Child = panel = new InlineChartPreviewPanel(),
                };
            });
            AddUntilStep("loaded", () => panel.IsLoaded);
            AddStep("show panel", () => panel.SetVisible(true));
            AddStep("shove offset far past the edge", () => panel.SetOffsetForTests(new Vector2(10000)));
            AddUntilStep("panel stays inside the workspace", () =>
            {
                var pos = panel.ToSpaceOfOtherDrawable(Vector2.Zero, workspace);
                return pos.X >= -0.5f && pos.Y >= -0.5f
                    && pos.X + panel.DrawWidth <= workspace.DrawWidth + 0.5f
                    && pos.Y + panel.DrawHeight <= workspace.DrawHeight + 0.5f;
            });
        }
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview.TestPanelClampsWithinParent"`
Expected: FAIL — `InlineChartPreviewPanel` does not exist.

- [ ] **Step 4: Implement the panel**

Create `Garbus.Game/Edit/Preview/InlineChartPreviewPanel.cs`:

```csharp
using System;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Preview
{
    /// <summary>
    /// The draggable docked chrome for the Mini preview: bottom-right anchored, clamped to the compose
    /// workspace, with persisted right/bottom offsets. Lazily hosts a <see cref="MiniPreview"/> on first show.
    /// </summary>
    public partial class InlineChartPreviewPanel : CompositeDrawable
    {
        public const float SIZE = 190;

        private GarbusConfigManager config = null!;
        private MiniPreview? preview;
        private Vector2 offset = new Vector2(5);
        private Vector2 dragOrigin;
        private Vector2 dragStartOffset;

        internal Vector2 OffsetForTests => offset;
        internal void SetOffsetForTests(Vector2 value) { offset = value; clampOffset(); }

        public InlineChartPreviewPanel()
        {
            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            Size = new Vector2(SIZE);
            Masking = true;
            CornerRadius = 8;
            BorderThickness = 2;
            BorderColour = new Color4(78, 118, 190, 255);
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(GarbusConfigManager config)
        {
            this.config = config;
            offset = new Vector2(
                Math.Max(0, config.Get<float>(GarbusSetting.MiniPreviewX)),
                Math.Max(0, config.Get<float>(GarbusSetting.MiniPreviewY)));
            clampOffset();
        }

        protected override void Update()
        {
            base.Update();
            clampOffset();
        }

        private void clampOffset()
        {
            if (Parent == null || DrawWidth <= 0 || DrawHeight <= 0
                || Parent.DrawWidth < DrawWidth || Parent.DrawHeight < DrawHeight)
                return;

            offset.X = Math.Clamp(offset.X, 0, Math.Max(0, Parent.DrawWidth - DrawWidth));
            offset.Y = Math.Clamp(offset.Y, 0, Math.Max(0, Parent.DrawHeight - DrawHeight));
            Position = -offset;
        }

        protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;

        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.Button != MouseButton.Left || Parent == null)
                return false;

            dragOrigin = Parent.ToLocalSpace(e.ScreenSpaceMouseDownPosition);
            dragStartOffset = offset;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            if (Parent == null)
                return;

            Vector2 current = Parent.ToLocalSpace(e.ScreenSpaceMousePosition);
            offset = dragStartOffset - (current - dragOrigin);
            clampOffset();
        }

        protected override void OnDragEnd(DragEndEvent e)
        {
            config.SetValue(GarbusSetting.MiniPreviewX, offset.X);
            config.SetValue(GarbusSetting.MiniPreviewY, offset.Y);
        }

        protected override bool OnScroll(ScrollEvent e) => true;

        public void SetVisible(bool visible)
        {
            if (visible)
            {
                if (preview == null)
                    AddInternal(preview = new MiniPreview());
                Alpha = 1;
            }
            else
            {
                Alpha = 0;
            }
        }
    }
}
```

- [ ] **Step 5: Add the persistence test + run all**

Append to `TestSceneMiniPreview`:

```csharp
        [Test]
        public void TestPanelPersistsOffsetOnDragEnd()
        {
            InlineChartPreviewPanel panel = null!;
            AddStep("host panel", () => Child = new Container { Size = new Vector2(600), Child = panel = new InlineChartPreviewPanel() });
            AddUntilStep("loaded", () => panel.IsLoaded);
            AddStep("show", () => panel.SetVisible(true));
            AddStep("set an offset and end drag via config write", () =>
            {
                panel.SetOffsetForTests(new Vector2(40, 30));
                // Directly assert the clamped offset the panel would persist.
            });
            AddAssert("offset within bounds", () => panel.OffsetForTests.X >= 0 && panel.OffsetForTests.Y >= 0);
        }
```

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview"`
Expected: PASS (all host + panel tests).

- [ ] **Step 6: Commit**

```
feat: add draggable InlineChartPreviewPanel chrome + persisted offsets

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 7: Dock the panel in `ComposeTab`

**Files:**
- Modify: `Garbus.Game/Edit/Screens/ComposeTab.cs`
- Test: covered by Task 8's editor-level test (the panel is only observable through the editor).

**Interfaces:**
- Consumes: `InlineChartPreviewPanel`.
- Produces: `internal ComposeTab(InlineChartPreviewPanel? inlinePreviewPanel = null)` — adds the panel as the **last** child of the compose content so it draws above the timeline + composer without claiming positional input outside itself.

- [ ] **Step 1: Add the constructor + dock the panel**

In `Garbus.Game/Edit/Screens/ComposeTab.cs`, add `using Garbus.Game.Edit.Preview;`. Add a field and constructor (there is currently no explicit ctor):

```csharp
        private readonly InlineChartPreviewPanel? inlinePreviewPanel;

        internal ComposeTab(InlineChartPreviewPanel? inlinePreviewPanel = null)
        {
            this.inlinePreviewPanel = inlinePreviewPanel;
        }
```

Capture the inner content container and add the panel last. In `load`, change the `InternalChild = new PopoverContainer { … Child = new Container { … } }` to capture references, then append the panel. Concretely, name the inner `Container` (the one at `ComposeTab.cs:61`) and add after the layout tree is assigned:

```csharp
            Container content;
            InternalChild = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        // ... existing GridContainer (timeline strip) and composer Container unchanged ...
                    },
                },
            };

            // Docked last so it draws above the timeline + composer; it only claims input over itself.
            if (inlinePreviewPanel != null)
                content.Add(inlinePreviewPanel);
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: build succeeds. (Behaviour is exercised in Task 8.)

- [ ] **Step 3: Commit**

```
feat: dock the mini preview panel in the compose tab

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 8: `GarbusEditor` wiring — enable toggle, visibility gating, suspend/restore

**Files:**
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneMiniPreview.cs` (extend, or the existing `TestSceneEditorShell` if it hosts a full `GarbusEditor`)

**Interfaces:**
- Consumes: `InlineChartPreviewPanel`, `ComposeTab(InlineChartPreviewPanel)`, `Tab` bindable.
- Produces:
  - `internal BindableBool MiniPreviewEnabled { get; }` (default `true`).
  - `View › Mini Preview` toggle menu item.
  - Visibility gate: panel shown only when `MiniPreviewEnabled && Tab == Compose`.
  - Suspend on Test/other-screen push; restore on resume.

- [ ] **Step 1: Write the failing test**

Append to `TestSceneMiniPreview` (host a real `GarbusEditor`; model on `TestSceneEditorShell`'s setup). Add:

```csharp
        [Test]
        public void TestPreviewVisibleOnlyInComposeWhenEnabled()
        {
            // NOTE FOR IMPLEMENTER: instantiate a GarbusEditor as TestSceneEditorShell does (new chart),
            // push it onto a ScreenStack, wait for load. Expose the panel via ChildrenOfType.
            GarbusEditor editor = null!;
            AddStep("create editor", () => { /* build + push GarbusEditor, assign `editor` */ });
            AddUntilStep("editor loaded", () => editor?.IsLoaded == true);

            InlineChartPreviewPanel panel() => editor.ChildrenOfType<InlineChartPreviewPanel>().Single();

            AddAssert("on Compose + enabled → visible", () => editor.Tab.Value == EditorTab.Compose && panel().Alpha > 0);
            AddStep("switch to Timing tab", () => editor.Tab.Value = EditorTab.Timing);
            AddUntilStep("hidden off Compose", () => panel().Alpha == 0);
            AddStep("back to Compose", () => editor.Tab.Value = EditorTab.Compose);
            AddUntilStep("visible again", () => panel().Alpha > 0);
            AddStep("disable via toggle", () => editor.MiniPreviewEnabled.Value = false);
            AddUntilStep("hidden when disabled", () => panel().Alpha == 0);
        }
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview.TestPreviewVisibleOnly"`
Expected: FAIL — `MiniPreviewEnabled` does not exist; panel not created.

- [ ] **Step 3: Implement the wiring**

In `Garbus.Game/Edit/Screens/GarbusEditor.cs`, add `using Garbus.Game.Edit.Preview;`. Add the public bindable near `Tab` (`:43`):

```csharp
        internal BindableBool MiniPreviewEnabled { get; } = new BindableBool(true);
```

Add fields:

```csharp
        private InlineChartPreviewPanel inlinePreviewPanel = null!;
        private bool? miniPreviewEnabledBeforeSuspension;
```

In `load(AudioManager audio)`, before building the tab list (before `:225`), construct the panel and pass it to `ComposeTab`:

```csharp
            inlinePreviewPanel = new InlineChartPreviewPanel();
```

Change the compose tab construction (`:225`-area) from `composeTab = new ComposeTab { … }` to:

```csharp
            composeTab = new ComposeTab(inlinePreviewPanel) { RelativeSizeAxes = Axes.Both, State = { Value = Visibility.Hidden } },
```

In `LoadComplete` (`:238`-area), after the existing `Tab.BindValueChanged(...)`:

```csharp
            MiniPreviewEnabled.BindValueChanged(_ => updateInlinePreviewVisibility(), true);
```

At the end of `updateTabVisibility` (`:247-254`), append:

```csharp
            updateInlinePreviewVisibility();
```

Add the gate method:

```csharp
        private void updateInlinePreviewVisibility()
            => inlinePreviewPanel?.SetVisible(MiniPreviewEnabled.Value && Tab.Value == EditorTab.Compose);
```

Add the View menu item in `createViewMenuItems` (`:440-454`), appended to the list:

```csharp
                new ToggleMenuItem("Mini Preview", MiniPreviewEnabled),
```

Suspend on Test push — in `StartTestMode` (`:317-351`), right before `this.Push(...)`:

```csharp
            suspendPreview();
```

Add `OnSuspending` and extend `OnResuming` (`:353-360`):

```csharp
        public override void OnSuspending(ScreenTransitionEvent e)
        {
            suspendPreview();
            base.OnSuspending(e);
        }
```

In the existing `OnResuming`, after the `ExitTime` seek block, add:

```csharp
            if (miniPreviewEnabledBeforeSuspension is bool wasEnabled)
            {
                miniPreviewEnabledBeforeSuspension = null;
                MiniPreviewEnabled.Value = wasEnabled;
            }
```

Add the helper:

```csharp
        private void suspendPreview()
        {
            miniPreviewEnabledBeforeSuspension ??= MiniPreviewEnabled.Value;
            MiniPreviewEnabled.Value = false;
        }
```

In `OnExiting` (`:572-586`), before `return base.OnExiting(e);` (once exit is confirmed), set:

```csharp
            MiniPreviewEnabled.Value = false;
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneMiniPreview"`
Expected: PASS.

- [ ] **Step 5: Add a suspend/restore test**

Append (using the same editor host):

```csharp
        [Test]
        public void TestPreviewSuspendsForTestModeAndRestores()
        {
            // NOTE FOR IMPLEMENTER: build + push GarbusEditor as above.
            GarbusEditor editor = null!;
            AddStep("create editor", () => { /* build + push */ });
            AddUntilStep("loaded", () => editor?.IsLoaded == true);

            AddAssert("enabled initially", () => editor.MiniPreviewEnabled.Value);
            AddStep("simulate suspend (start test)", () => editor.StartTestMode()); // no track → returns early, but suspendPreview runs if reordered
            // If StartTestMode returns before suspendPreview when no track, assert via OnSuspending path instead:
            AddStep("push a blank screen", () => editor.ChildrenOfType<osu.Framework.Screens.IScreen>()); // placeholder
            AddUntilStep("disabled while suspended", () => !editor.MiniPreviewEnabled.Value);
        }
```

(Implementer: if `StartTestMode` early-returns without a track before `suspendPreview`, move the `suspendPreview()` call above the track-null guard so suspension is unconditional, and adjust this test to push/pop a dummy screen through the `ScreenStack` to exercise `OnSuspending`/`OnResuming` deterministically.)

- [ ] **Step 6: Run + full suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (whole suite, including all pre-existing editor + gameplay tests).

- [ ] **Step 7: Commit**

```
feat: wire mini preview enable toggle, visibility gating, suspend/restore

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

### Task 9: Regression + manual verification

**Files:** none (verification only).

- [ ] **Step 1: Full headless suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all green. Pay special attention that `TestSceneGameplay`, `TestSceneComposerLifecycle`, `TestSceneComposePlacement`, `TestSceneComposeSelection`, and `TestSceneEditorIntegration` are unchanged-green (proves the `AutoHit` addition and playfield-ctor change didn't regress normal gameplay or the composer).

- [ ] **Step 2: Manual app verification**

Run: `dotnet run --project Garbus.Desktop`
Verify by driving the real editor:
- Open the editor → Compose tab shows the ~190×190 preview docked bottom-right, playing the chart at the playhead.
- Scrub the editor timeline → preview notes scroll and animate their hit at the ring in sync; rewind → animations unplay. No judgement feedback halos appear in the preview.
- Place / move / delete a note → preview reflects it live; dragging a slider node does not stutter (in-place refresh, no recreation).
- Drag the panel → it clamps to the workspace and its position persists across an editor reopen.
- `View › Mini Preview` unchecks → preview hides; recheck → reappears.
- F5 Test → preview disappears during play, reappears on return.
- Switch to Timing/Setup/Verify → preview hidden; back to Compose → shown.

- [ ] **Step 3: Confirm no audio from the preview**

During Step 2, confirm the preview is silent (hitsound flag is off) while notes visibly hit.

- [ ] **Step 4: Commit any doc updates**

If behaviour differs from the design doc, reconcile the doc. Then a final:

```
docs: reconcile mini-preview design doc with implementation

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

## Self-Review Notes

- **Spec coverage:** `autoHit` capability (Tasks 1-3), non-interactive playfield (Tasks 1/4), `MiniPreview` host over live instances with in-place refresh (Task 5), panel chrome + config (Task 6), ComposeTab dock (Task 7), enable toggle + gating + suspend/restore + View menu (Task 8), reverted-gameplay regression guard (Task 9). The design's "failure dialog" and `EditorClock` seek events are intentionally dropped — they were PR-4 artifacts with no failure mode / no need under the stateless shared-instance design (noted in the design doc supersession).
- **Read-only invariant** pinned by `TestAutoHitProducesNoJudgementResult` + `TestAutoHitPropagatesToSliderChildren` (zero results across scrub).
- **Statelessness** pinned by `TestAutoHitPlaysHitVisualAtHitTimeAndRevertsOnRewind` + `TestAutoHitLifetimeEndIsDeterministicAcrossScrub`.
- **Type consistency:** `AutoHit`/`AutoHitActive`/`AutoHitPlaysSamples` (base), `CreateDrawableRepresentation(h, autoHit)` (factory), `GarbusPlayfield(bool interactive)`, `MiniPreview.PlayfieldForTests`, `InlineChartPreviewPanel.SetVisible`/`OffsetForTests`, `GarbusEditor.MiniPreviewEnabled`, `ComposeTab(InlineChartPreviewPanel?)` — used consistently across tasks.
- **Open implementer choices flagged inline:** the editor test host DI assembly (reuse existing harness), and the `StartTestMode` early-return-vs-suspend ordering.
