# Song lead-in — design

## Goal

When gameplay begins, give the player a fixed count-in before anything happens: the
clock starts a configurable duration (default **3000 ms**) *before* the intended gameplay
start, runs through that negative time with the screen empty and the audio silent, and the
music begins exactly at the intended start (t = 0 for normal play).

## Behavior model (decided)

The clock **runs** through the lead-in — it is not frozen. It seeks to `-LEAD_IN_TIME` and
immediately starts; the vendored `DecouplingFramedClock` advances negative time on a realtime
reference clock and couples to the audio track exactly when time crosses 0. This is the
standard osu / rhythm-game model and is what `requireDecoupling: true` (already set) exists for.

## Approach

Reuse the existing vendored clock machinery — add no new timing mechanism. "Lead-in" is simply:
start the clock `LEAD_IN_TIME` before `GameplayStartTime`.

### 1. The constant

In `MasterGameplayClockContainer`, alongside the existing `MINIMUM_SKIP_TIME`:

```csharp
/// <summary>
/// Silent count-in before gameplay begins. The clock starts this far before GameplayStartTime.
/// </summary>
public const double LEAD_IN_TIME = 3000;
```

### 2. Clock start semantics (core change)

Redefine `GameplayStartTime` as the *intended* moment gameplay begins, and always start the
clock `LEAD_IN_TIME` earlier. In the `MasterGameplayClockContainer` constructor:

```csharp
GameplayStartTime = gameplayStartTime;
StartTime = gameplayStartTime - LEAD_IN_TIME;   // replaces Math.Min(0, gameplayStartTime)
```

- **Normal play:** `gameplayStartTime = 0` → `StartTime = -3000`. Clock runs -3000 → 0, screen
  empty during the count-in, audio begins at 0.
- Dropping `Math.Min(0, …)` also fixes a latent bug: a positive mid-song start (the editor path)
  was previously clamped to 0. Now `StartTime` is simply 3 s before the requested point, so
  mid-song starts work.

`LoadComplete`'s existing `gameplayClock.Reset(startClock: true)` already seeks to `StartTime`
and starts — no change needed there.

### 3. Editor Test mode (unify)

Lead-in becomes the single pre-roll mechanism. In `GarbusEditor`, drop the ad-hoc `- 1500` and
pass the playhead directly as the intended start:

```csharp
double startTime = Math.Max(0, editorClock.CurrentTime);   // was CurrentTime - 1500
this.Push(new PlayScreen(clonedChart, freshTrack, startTime));
```

Test mode now gives a uniform 3 s count-in before the playhead. The `ExitTime` / seek-back-on-
resume path is unaffected (it records `CurrentTime`, which may now be slightly negative if you
exit during the count-in — a harmless editor seek that clamps to 0).

## osu comparison

osu's `MasterGameplayClockContainer.findEarliestStartTime` computes:

```csharp
double time = Math.Min(0, gameplayStartTime);        // never later than 0
if (firstStoryboardEvent != null) time = Math.Min(time, firstStoryboardEvent.Value);
if (beatmap.AudioLeadIn > 0)      time = Math.Min(time, firstHitObjectTime - beatmap.AudioLeadIn);
return time;
```

osu does **not** force a default lead-in — its clock starts at 0 unless a storyboard has
negative-time events or the beatmap sets an explicit `AudioLeadIn`. The empty run-up seen in
osu is just the first object's approach window. Garbus forcing a fixed `LEAD_IN_TIME` is a
**deliberate deviation** (Garbus has no storyboards and no per-chart `AudioLeadIn`), not a port
regression — recorded here so it is not "corrected" back toward osu later.

The negative-time mechanism itself is faithfully reused and osu-proven:
`FramedChartClock` builds `new DecouplingFramedClock(source) { AllowDecoupling = requireDecoupling }`
and MGCC passes `requireDecoupling: true`; `Reset()` does `Stop → Seek(StartTime) → Start`, which
is exactly what the decoupling clock expects for negative seeks.

## Gotchas (recorded)

1. **First time Garbus runs the clock at negative time.** The machinery is intact and osu-proven,
   but `StartTime` was always clamped to ≥ 0 before, so this path has never actually executed.
   Test coverage is load-bearing.
2. **Lead-in advances on a realtime reference clock, not the gameplay/manual clock.** In headless
   non-realtime tests the framework sets `DebugUtils.RealtimeClock = FastClock` (per-frame,
   deterministic), so `AddUntilStep` / `AddWaitStep` tests work fine and fast. A test that jumps a
   **ManualClock** to a negative time will *not* drive the lead-in (the source clock is bypassed
   while decoupled). → Lead-in tests use the real running clock stack plus deterministic
   assertions on `StartTime` / `GameplayStartTime`, never manual-clock jumps.
3. **"Nothing visible" holds only while `timeRange < LEAD_IN_TIME`.** Object lifetimes are relative
   to start time and the scroll `timeRange`; at t = -3000 with chart objects at t ≥ 0, nothing is
   alive as long as `timeRange < 3000`. If a chart's approach window ever exceeded 3000 ms the
   earliest object would be mid-approach at the start — correct behavior, just not literally empty.
   Not a concern for current fixed scroll settings.
4. **`Skip()` / `MINIMUM_SKIP_TIME`** would target `GameplayStartTime - 1000` during lead-in, but
   no skip UI is wired, so it is inert. No change needed.
5. **Editor exit during lead-in** yields a negative `ExitTime`; the editor seek-back clamps to 0.
   Harmless.

## Testing

- **Deterministic assertions** (no clock advancement needed): after load, normal-play
  `gameplayClock.StartTime == -LEAD_IN_TIME` and `GameplayStartTime == 0`.
- **Running-clock assertion** (`AddUntilStep`): from load, `CurrentTime` begins negative, no hit
  object has been judged / is alive while `CurrentTime < 0` (given current `timeRange`), and once
  `CurrentTime` crosses 0 the track is running and objects judge normally.
- **Editor Test launch:** confirm the removed `-1500` doesn't break `TestSceneEditorIntegration` /
  the Test-launch coverage; adjust any assertion that hard-codes the 1500 offset.
- Full `dotnet test` green.

## Out of scope (YAGNI)

Skip-the-intro UI, runtime-configurable lead-in (garbus.ini), per-chart audio-lead-in inference,
countdown visuals / ticks.