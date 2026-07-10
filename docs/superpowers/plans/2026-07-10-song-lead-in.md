# Song Lead-In Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every chart a fixed silent count-in — the gameplay clock starts `LEAD_IN_TIME` (3000 ms) before the intended gameplay start, runs through negative time with the screen empty, and the music begins exactly at the intended start.

**Architecture:** Reuse the already-vendored decoupled clock (`requireDecoupling: true`), which is built to run negative time on a realtime reference and couple to the audio track at t≥0. "Lead-in" is just seeding the clock's start `LEAD_IN_TIME` earlier than `GameplayStartTime`. Also unify the editor Test-mode pre-roll onto this single mechanism (removing an ad-hoc `-1500`) and, as a side effect, fix a latent clamp bug that pinned mid-song editor starts to 0.

**Tech Stack:** C# / .NET 8, osu-framework, NUnit headless visual test scenes.

## Global Constraints

- Nullability enabled solution-wide; DI/BDL fields use `= null!`.
- Vendored osu files keep the ppy MIT header plus the "Adapted for Garbus:" line.
- No backwards-compatibility layers or version bumps (experimental project).
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`. Baseline is 197/197 green (commit `90f7326`).
- `LEAD_IN_TIME = 3000` (ms), a `public const double` on `MasterGameplayClockContainer`.
- Two distinct "StartTime"s — do not conflate:
  - `PlayScreen.StartTime` / `GameplayClockContainer.GameplayStartTime` = the **intended** gameplay start (0 for normal play, the editor playhead for Test mode). Tests assert on this.
  - `GameplayClockContainer.StartTime` = the clock's seek target = `GameplayStartTime - LEAD_IN_TIME` (negative for normal play).

---

### Task 1: Apply the lead-in in the gameplay clock (core)

**Files:**
- Modify: `Garbus.Game/Timing/MasterGameplayClockContainer.cs` (constant block near line 27; constructor body lines 62–65)
- Test: `Garbus.Game.Tests/Visual/TestScenePlayScreen.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `public const double MasterGameplayClockContainer.LEAD_IN_TIME` (value `3000`).
  - After construction, for a `MasterGameplayClockContainer(track, gameplayStartTime)`:
    `GameplayStartTime == gameplayStartTime` and `StartTime == gameplayStartTime - LEAD_IN_TIME`.
  - Both `StartTime` and `GameplayStartTime` are already `public` getters inherited from `GameplayClockContainer`.

- [ ] **Step 1: Write the failing test**

Add this test to `Garbus.Game.Tests/Visual/TestScenePlayScreen.cs` (inside the class, after `TestObjectsBecomeVisible`). Also add `using Garbus.Game.Timing;` to the existing `using` block at the top of the file.

```csharp
        [Test]
        public void TestLeadInBeginsBeforeGameplayStart()
        {
            AddUntilStep("clock created", () => this.ChildrenOfType<MasterGameplayClockContainer>().Any());

            AddAssert("gameplay start time is zero (normal play)", () =>
                this.ChildrenOfType<MasterGameplayClockContainer>().Single().GameplayStartTime == 0);

            AddAssert("clock starts one lead-in before gameplay", () =>
                this.ChildrenOfType<MasterGameplayClockContainer>().Single().StartTime
                    == -MasterGameplayClockContainer.LEAD_IN_TIME);
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestScenePlayScreen.TestLeadInBeginsBeforeGameplayStart`
Expected: FAIL. The first assert passes (`GameplayStartTime` is 0), the second fails — with the current `StartTime = Math.Min(0, gameplayStartTime)` the clock start is `0`, not `-3000`. (If `LEAD_IN_TIME` doesn't compile yet, that's also an expected red — add it in Step 3.)

- [ ] **Step 3: Implement the lead-in**

In `Garbus.Game/Timing/MasterGameplayClockContainer.cs`, add the constant directly below the existing `MINIMUM_SKIP_TIME` block (around line 27):

```csharp
        /// <summary>
        /// Duration before gameplay start time required before skip button displays.
        /// </summary>
        public const double MINIMUM_SKIP_TIME = 1000;

        /// <summary>
        /// Silent count-in before gameplay begins. The clock starts this far before
        /// <see cref="GameplayClockContainer.GameplayStartTime"/> and runs through the
        /// negative time on the decoupled clock's realtime reference, coupling to the
        /// audio track exactly at the gameplay start.
        /// </summary>
        public const double LEAD_IN_TIME = 3000;
```

Then replace the constructor's start-time lines (currently lines 62–65):

```csharp
            GameplayStartTime = gameplayStartTime;

            // Unlike osu, no storyboard / audio lead-in inference (yet) — just start at or before zero.
            StartTime = Math.Min(0, gameplayStartTime);
```

with:

```csharp
            GameplayStartTime = gameplayStartTime;

            // Begin a fixed lead-in before the intended gameplay start. The decoupled clock runs
            // through the negative time (screen empty, audio silent) and couples to the track exactly
            // at GameplayStartTime. Deliberately unconditional (no Math.Min(0, …)) so a positive
            // mid-song start — the editor Test path — is honoured rather than clamped to 0.
            StartTime = gameplayStartTime - LEAD_IN_TIME;
```

Also update the "Adapted for Garbus:" header line (lines 4–5) to reflect the new behavior. Change:

```csharp
// Adapted for Garbus: takes a Track directly instead of a WorkingBeatmap (no storyboard/lead-in
// start-time inference yet); MusicController, mod adjustments and IBeatSyncProvider removed.
```

to:

```csharp
// Adapted for Garbus: takes a Track directly instead of a WorkingBeatmap; a fixed LEAD_IN_TIME
// count-in replaces osu's storyboard/AudioLeadIn start-time inference (Garbus has neither);
// MusicController, mod adjustments and IBeatSyncProvider removed.
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestScenePlayScreen.TestLeadInBeginsBeforeGameplayStart`
Expected: PASS (both asserts).

- [ ] **Step 5: Run the neighbouring play/gameplay tests to confirm no regressions**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestScenePlayScreen`
Expected: PASS. `TestGameplayStarts` (waits `Time.Current > 0`) and `TestObjectsBecomeVisible` (waits an object alive) still pass — the headless `FastClock` advances the lead-in quickly, so the clock still crosses 0 and objects still become alive.

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Timing/MasterGameplayClockContainer.cs Garbus.Game.Tests/Visual/TestScenePlayScreen.cs
git commit -m "Add fixed lead-in count-in to the gameplay clock"
```

---

### Task 2: Unify the editor Test-mode pre-roll onto the lead-in

**Files:**
- Modify: `Garbus.Game/Edit/Screens/GarbusEditor.cs` (lines 294–295)
- Test: `Garbus.Game.Tests/Editor/TestSceneTestMode.cs` (`TestStartTimeOffsetFromEditorClock` assertion; the file header docstring item 2; the section comment above the start-time tests)

**Interfaces:**
- Consumes: `MasterGameplayClockContainer.LEAD_IN_TIME` and the new start semantics from Task 1.
- Produces: the editor passes `PlayScreen`'s intended start as `Math.Max(0, editorClock.CurrentTime)` (no `-1500`). `PlayScreen.StartTime` therefore equals the clamped editor playhead; the count-in is applied inside the clock.

- [ ] **Step 1: Update the test to expect the new behavior (make it fail against current code)**

In `Garbus.Game.Tests/Editor/TestSceneTestMode.cs`, find `TestStartTimeOffsetFromEditorClock`. Replace its `AddAssert` block:

```csharp
            AddAssert("gameplay start time ≈ editorTime − 1500 (within 50ms)", () =>
            {
                var ps = (PlayScreen)stack.CurrentScreen;
                double expected = System.Math.Max(0, capturedEditorTime - 1500);
                return System.Math.Abs(ps.StartTime - expected) < 50;
            });
```

with:

```csharp
            AddAssert("gameplay start time ≈ editor playhead (within 50ms)", () =>
            {
                var ps = (PlayScreen)stack.CurrentScreen;
                double expected = System.Math.Max(0, capturedEditorTime);
                return System.Math.Abs(ps.StartTime - expected) < 50;
            });
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneTestMode.TestStartTimeOffsetFromEditorClock`
Expected: FAIL. The editor is seeked to 5000; current production code still passes `Max(0, 5000 - 1500) = 3500`, so `ps.StartTime` is 3500 while the test now expects 5000 (`|3500 - 5000| = 1500 > 50`).

- [ ] **Step 3: Remove the `-1500` in the editor Test launch**

In `Garbus.Game/Edit/Screens/GarbusEditor.cs`, replace lines 294–295:

```csharp
            // Start 1500 ms before the editor's current position, clamped to ≥ 0.
            double startTime = Math.Max(0, editorClock.CurrentTime - 1500);
```

with:

```csharp
            // Start at the editor's current position, clamped to ≥ 0. The lead-in count-in is applied
            // inside MasterGameplayClockContainer (StartTime = GameplayStartTime − LEAD_IN_TIME), so the
            // editor no longer needs its own ad-hoc pre-roll offset.
            double startTime = Math.Max(0, editorClock.CurrentTime);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneTestMode.TestStartTimeOffsetFromEditorClock`
Expected: PASS (`ps.StartTime == 5000`, expected `5000`).

- [ ] **Step 5: Update the stale docstring/comments in the test file**

In `Garbus.Game.Tests/Editor/TestSceneTestMode.cs`, update the file-header contract line:

```
//   2. Start time ≈ editorClock.CurrentTime − 1500 (clamped ≥ 0).
```

to:

```
//   2. Start time ≈ editorClock.CurrentTime (clamped ≥ 0); the lead-in count-in is inside the clock.
```

And update the section comment above the start-time tests:

```csharp
        // ------------------------------------------------------------------
        // 2. Start time ≈ editorClock.CurrentTime − 1500 (clamped ≥ 0)
        // ------------------------------------------------------------------
```

to:

```csharp
        // ------------------------------------------------------------------
        // 2. Start time ≈ editorClock.CurrentTime (clamped ≥ 0)
        // ------------------------------------------------------------------
```

- [ ] **Step 6: Run the full editor Test-mode fixture to confirm the rest still passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneTestMode`
Expected: PASS for all tests. Notably:
- `TestStartTimeClampsToZero` still passes (`Max(0, 0) == 0`, unchanged).
- `TestExitingPlayScreenSeeksEditorClock` still passes — it asserts the editor clock ≈ `PlayScreen.ExitTime` (internal consistency), not an absolute value; the landing point during lead-in simply shifts earlier.
- `TestPushedPlayScreenShowsObjects` still passes (editor at 0 → intended start 0 → objects become alive after the count-in).

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Screens/GarbusEditor.cs Garbus.Game.Tests/Editor/TestSceneTestMode.cs
git commit -m "Unify editor Test-mode pre-roll onto the gameplay lead-in"
```

---

### Task 3: Full-suite regression and manual smoke

**Files:** none (verification only).

**Interfaces:**
- Consumes: the complete change set from Tasks 1–2.
- Produces: confirmation the whole suite is green and the count-in is visible/audible in the real app.

- [ ] **Step 1: Run the entire headless suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — the full suite green (was 197/197 at baseline; now 198 with the new `TestLeadInBeginsBeforeGameplayStart`).

- [ ] **Step 2: Manual smoke of the standalone Play path**

Run: `dotnet run --project Garbus.Desktop`, choose Play from the main menu.
Expected: on entering the playfield the screen is empty and silent for ~3 seconds, then the music starts and the first objects begin their approach — no object is visible at the instant the screen appears.

- [ ] **Step 3: Manual smoke of the editor Test path**

Test two playhead positions, which exercise the two branches of the clock's seek path:
- **Playhead < `LEAD_IN_TIME` (e.g. near 0):** the clock start is negative, so a truly silent ~3 s
  count-in precedes playback (decoupled negative-time path, `lastSeekFailed` true).
- **Playhead ≥ `LEAD_IN_TIME` (e.g. 5000 ms):** the clock start is a positive mid-song point
  (`playhead − LEAD_IN_TIME`), so audio plays *immediately* from ~3 s before the playhead — the
  pre-roll exists but is NOT silent (coupled path, `lastSeekFailed` false).

In both cases pressing Escape returns to the editor at (approximately) where playback reached.

- [ ] **Step 4: Confirmation note**

No commit needed unless a manual step surfaced a defect (in which case, stop and debug via superpowers:systematic-debugging before proceeding).

---

## Self-Review

**Spec coverage:**
- Constant `LEAD_IN_TIME = 3000` on `MasterGameplayClockContainer` → Task 1, Step 3. ✓
- Clock start semantics `StartTime = GameplayStartTime - LEAD_IN_TIME` (drop `Math.Min(0, …)`, fix mid-song clamp) → Task 1, Step 3. ✓
- Editor Test-mode unify (drop `-1500`) → Task 2, Step 3. ✓
- osu-comparison / deliberate-deviation rationale → captured in code comments (Task 1 header + ctor) and the spec doc. ✓
- Gotcha: first negative-time run — covered by the deterministic `StartTime`/`GameplayStartTime` assertions (Task 1) rather than a racy live-negative check. ✓
- Gotcha: lead-in advances on realtime reference, not manual clock — plan uses running-clock `AddUntilStep`/deterministic property asserts, never manual-clock jumps. ✓
- Known test breakage `TestStartTimeOffsetFromEditorClock` + docstring → Task 2, Steps 1 & 5. ✓
- Full suite green + manual behavioral confirmation → Task 3. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases"; every code step shows exact code. ✓

**Type consistency:** `LEAD_IN_TIME`, `StartTime`, `GameplayStartTime`, `PlayScreen.StartTime` used consistently across tasks and match the existing public surface. ✓
