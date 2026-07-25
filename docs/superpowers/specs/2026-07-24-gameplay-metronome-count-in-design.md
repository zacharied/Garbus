# Gameplay metronome count-in — design

## Goal

Before the first timing point of a song, play an audible **one-bar metronome count-in** so the
player hears the tempo before the first beat arrives. The count-in ticks use the **tempo and time
signature of the first timing point**, land exactly on that timing point's beat grid, and the last
tick sits one beat before the first beat — the classic "1-2-3-4, [play]".

This supersedes the "countdown visuals / ticks" out-of-scope line in
[2026-07-10-song-lead-in-design.md](2026-07-10-song-lead-in-design.md): the silent lead-in that
spec built is now filled with metronome ticks.

## Decisions

- **Length:** exactly **one bar** (`BeatLength × TimeSignature.Numerator` of the first timing
  point). Not configurable, not per-chart.
- **When it applies:** whenever the gameplay start time is at or before the first timing point
  (`StartTime ≤ T_first`) — i.e. a normal play, or an editor Test launched from the start. A
  mid-song editor Test (`StartTime > T_first`) keeps today's plain 3000 ms silent lead-in with no
  ticks.
- **Audio only.** No on-screen countdown. (See "Tuning scene" note below.)

## Approach

Split across two well-bounded pieces, reusing existing machinery rather than adding a new timing
mechanism:

1. **Lead-in duration** — derived in `PlayScreen`, passed into the vendored
   `MasterGameplayClockContainer` (which today hardcodes `LEAD_IN_TIME = 3000`). When the count-in
   applies, the silent pre-roll becomes exactly one bar so the decoupled clock starts one bar
   before the first beat and every count-in tick is reachable.
2. **Tick playback** — a new single-purpose drawable, `CountInMetronome`, added inside the
   gameplay-clock subtree. It reuses the beat-polling pattern already proven in
   `Edit/Screens/Timing/MetronomeDisplay` and the existing `Editor/metronome-tick` /
   `Editor/metronome-downbeat` samples.

Rejected alternative: put everything inside `MasterGameplayClockContainer`. It is a faithfully
vendored osu.Game clock (ppy MIT header) and must not grow chart/timing awareness or audio
playback. Deviations there stay minimal and noted.

## Component 1 — lead-in derivation

### `PlayScreen.load`

Read the first timing point and compute the pre-roll:

```csharp
var firstTiming = chart.ControlPointInfo?.TimingPoints.FirstOrDefault();
bool countInApplies = firstTiming != null && StartTime <= firstTiming.Time;
double oneBar = countInApplies
    ? firstTiming!.BeatLength * firstTiming.TimeSignature.Numerator
    : 0;
double leadIn = countInApplies ? oneBar : MasterGameplayClockContainer.LEAD_IN_TIME;
```

Pass `leadIn` to the clock container. When `countInApplies`, also add a `CountInMetronome`
(configured with `firstTiming`) into the gameplay-clock subtree, alongside `GarbusInputManager` /
`DesignOverlay`.

Because `StartTime ≤ T_first`, a one-bar pre-roll guarantees the internal clock start
(`GameplayStartTime − oneBar`) is at or before the first count-in tick (`T_first − oneBar`), so the
whole bar is reachable regardless of where `T_first` sits relative to the audio.

If there is no timing data, the count-in is skipped and the existing 3000 ms behavior is unchanged.

### `MasterGameplayClockContainer`

Add an optional lead-in parameter (a minimal, noted deviation — extend the existing adaptation
comment; keep the ppy MIT header; read the original in `docs/code-reference/osu` first):

```csharp
public MasterGameplayClockContainer(Track track, double gameplayStartTime,
                                    double leadInTime = LEAD_IN_TIME)
{
    ...
    GameplayStartTime = gameplayStartTime;
    StartTime = gameplayStartTime - leadInTime;   // was: gameplayStartTime - LEAD_IN_TIME
}
```

`LEAD_IN_TIME` stays as the default so existing callers and mid-song starts are unchanged.

## Component 2 — `CountInMetronome`

New file (Garbus code, not a vendored osu file → a "Modeled on … MetronomeDisplay" note like
`MetronomeDisplay` carries, not a ppy header). Lives inside the gameplay-clock subtree, so its own
`Time.Current` **is** gameplay time (`GameplayClockContainer` sets `Content.Clock = GameplayClock`)
— no clock resolve needed, unlike `MetronomeDisplay` which resolves `EditorClock`.

Constructed with the first `TimingControlPoint`. On load it precomputes the bar's tick grid:

- `tickTimes[i] = T_first − oneBar + i × BeatLength`, for `i ∈ [0, Numerator)`.
- These span `[T_first − oneBar, T_first)`. The last tick is at `T_first − BeatLength`; **nothing
  plays at `T_first`** (the song's first beat takes over). `i == 0` is the downbeat.

Playback (mirrors `MetronomeDisplay.Update`):

```csharp
protected override void Update()
{
    double now = Time.Current;
    if (now < lastTime - epsilon)               // backward jump (restart / rewind)
        nextTick = firstTickIndexAtOrAfter(now);
    while (nextTick < tickTimes.Length && now >= tickTimes[nextTick])
    {
        play(nextTick == 0 ? downbeat : tick);  // downbeat sample on the bar's first tick
        nextTick++;
    }
    lastTime = now;
}
```

- **Restart-safe:** R restart (`Reset` → seek to internal `StartTime`) jumps `now` backward; the
  component re-arms and the count-in replays.
- **Pause-safe:** a stopped clock doesn't advance `Time.Current`, so no ticks fire; resume
  continues.
- Samples loaded via the DI `ISampleStore` and wrapped in `DrawableSample`, consistent with
  `HitSoundContainer`, so gameplay audio adjustments flow through the drawable hierarchy.

**Test seam:** expose `TickCount` and `DownbeatCount` counters (incremented in `play`) so headless
tests assert timing/counts without audio.

## Data flow

```
chart.ControlPointInfo (first TimingControlPoint)
        │
        ├─ PlayScreen: leadIn = oneBar ─────────► MasterGameplayClockContainer
        │                                          StartTime = GameplayStartTime − oneBar
        └─ PlayScreen: new CountInMetronome(firstTiming) ─► gameplay-clock subtree
                                                            │
   gameplay time crosses each grid time in [T_first−oneBar, T_first)
                                                            │
                                              tick / downbeat sample plays
                                                            │
                                              song's first beat at T_first
```

## Tuning scene

The count-in is **audio-only**, so no Tuning scene is required (that rule targets *visual*
elements). This is a deliberate scope decision, not an omission — if an on-screen "3-2-1" is ever
added it would ship with a Tuning scene per the repo rule.

## Testing

Headless NUnit over test scenes. **Critical constraint** (from the lead-in spec, gotcha #2): the
real lead-in advances on a *realtime reference* clock while decoupled — jumping a `ManualClock` to
negative time does **not** drive it. So the count-in is unit-tested in isolation, and the
integration path uses the running clock.

1. **`CountInMetronome` unit (isolated):** host the component under a plain `ManualClock` container
   (no decoupling). Step the clock forward across `[T_first − oneBar, T_first]` and assert exactly
   `Numerator` ticks fire, `DownbeatCount == 1`, the downbeat is first, and none fire at or after
   `T_first`. Step backward and forward again → the count-in replays (restart safety).
2. **Lead-in derivation (deterministic):** `MasterGameplayClockContainer(track, 0, leadInTime)` →
   `StartTime == −leadInTime`. Default constructor still `−LEAD_IN_TIME`.
3. **Gating (running clock, integration):** mid-song start (`StartTime > T_first`) → no
   `CountInMetronome` present / `TickCount` stays 0. Normal start → `AddUntilStep` until
   `CurrentTime > T_first`, then `TickCount == Numerator`.

### Existing test to update

`TestScenePlayScreen.TestLeadInBeginsBeforeGameplayStart`
(`Garbus.Game.Tests/Visual/TestScenePlayScreen.cs:79`) asserts the internal
`StartTime == −MasterGameplayClockContainer.LEAD_IN_TIME` for normal play. With the count-in
active, normal-play `StartTime` becomes `−oneBar` of the bundled test chart's first timing point.
Update the assertion to derive the expected value from that timing point (compute `oneBar`), not
the fixed constant — keeping it robust if the test chart's tempo changes.

## Domain docs to update (as work lands)

- `docs/agents/timing-audio.md` — the gameplay clock stack / lead-in section: lead-in is one bar
  when a count-in applies, otherwise `LEAD_IN_TIME`.
- `docs/agents/screens.md` — the play loop: `PlayScreen` derives the lead-in and hosts
  `CountInMetronome`.

Present-tense, no history / phase framing (repo rule).

## Out of scope (YAGNI)

Config toggle for the count-in, per-chart or multi-bar count-in length, on-screen countdown
visuals, count-in for mid-song starts, distinct gameplay-specific tick samples (reuse the editor
metronome samples).
