# Timing & audio

## Purpose & scope

The clock stack that drives gameplay time, the offset layers, where audio latency actually lives,
and hitsound playback. The playfield and judgement that consume this clock are in
[gameplay.md](gameplay.md); the timing *data* (control points) is in [charts.md](charts.md).

## The clock stack

`Garbus.Game/Timing/` holds the gameplay clock stack, vendored from osu.Game and trimmed of realm and
mod hooks:

- `FramedChartClock.cs` (osu's `FramedBeatmapClock`) — the canonical stack: track → decoupling →
  interpolating → three offset layers. The realm per-beatmap offset became the plain **`ChartOffset`**
  property.
- `OffsetCorrectionClock.cs` — a user/platform/chart offset scaled by playback rate so the real-time
  offset stays constant across rate changes.
- `GameplayClockContainer.cs` / `MasterGameplayClockContainer.cs` — pause/resume/seek/lead-in
  orchestration. `MasterGameplayClockContainer` takes a `Track` directly (no `WorkingBeatmap`).
  `IGameplayClock.cs` is the DI contract gameplay components resolve.
- `BackgroundSeekingClock.cs` — supports editor/background seeking.

### Offset layers

`FramedChartClock` composes three `OffsetCorrectionClock` layers — **chart**, **user global**, and
**platform** — and reports their rate-adjusted sum. Platform offset constants (osu's, verbatim):
`WINDOWS_BASE_AUDIO_OFFSET = 15` ms, plus `WINDOWS_EXPERIMENTAL_AUDIO_OFFSET = -25` ms under the
experimental low-latency WASAPI mode; other platforms default to 0. The user global offset comes from
`GarbusConfigManager.AudioOffset` (`Configuration/GarbusSetting.cs`, `garbus.ini`).

## The `Content.Clock = GameplayClock` deviation

**This is a deliberate deviation from osu and a load-bearing line.** In osu, the `DrawableRuleset`'s
`FrameStabilityContainer` applies the gameplay clock to the playfield subtree. Garbus dropped
`DrawableRuleset`, so nothing re-applies it — `GameplayClockContainer` sets `Content.Clock =
GameplayClock` itself. Without this line the playfield silently runs on the ambient wall-time clock:
object lifetimes compare against app-session time, so hit objects never appear once the app has been
open longer than the chart, and clock resets don't affect gameplay. (Cross-linked from the
[gameplay.md](gameplay.md) gotchas.)

### FrameStabilityContainer — not vendored

`FrameStabilityContainer` did two jobs in osu: (1) apply the gameplay clock to the playfield — now
done directly by the line above; (2) *frame stability proper* — re-running updates in fixed ≤16 ms
sub-steps so judgement sees deterministic elapsed times at low/erratic frame rates, which osu needs
for replay determinism. Garbus has no replays, so (2) was consciously skipped. If low-FPS judgement
precision ever matters, the class is vendorable standalone
(`osu.Game/Rulesets/UI/FrameStabilityContainer.cs`, in `docs/code-reference/osu`) and would wrap the
playfield inside `PlayScreen`'s clock container.

## Audio latency lives in the framework — do not retune

Almost everything latency-critical is in osu-framework, not here:

- The BASS stack (`AudioManager`, `TrackBass`, `SampleBass`) and all its tuning in
  `AudioManager.InitBass()` — device buffer length, update period, `TruePlayPosition`, the
  experimental WASAPI mode.
- Clock smoothing (`InterpolatingFramedClock` smooths BASS's ~5–10 ms position granularity so scroll
  motion doesn't jitter; `DecouplingFramedClock` lets gameplay time run before the track starts for
  lead-ins).

Do **not** change these values without hardware latency data. Garbus gets osu's audio latency for
free by building on the framework.

## Hitsounds

`Gameplay/Audio/` — `HitSoundContainer.cs` plays samples via the thin `DrawableSample` wrapper that
replaces osu's skin-entangled `SkinnableSound`. Samples are fixed per hit-object type
(`HitsoundFamily`, `GarbusHitSample`), not author-configured — there is one hitsound bank.

## osu-framework background

`IFrameBasedClock`/`IAdjustableClock`, the interpolating/decoupling framework clocks, and BASS audio.
Read the vendored clock originals in `docs/code-reference/osu` before editing; deviate minimally and
note why (the `Content.Clock` line is the model example).

## Gotchas

- **Never retune the BASS/latency constants without hardware data** (see above) — they are osu's and
  carry its tuning.
- **The `Content.Clock = GameplayClock` line is mandatory** — removing it makes the playfield run on
  wall time and objects vanish once the app outlives the chart. Pinned indirectly by the PlayScreen
  playfield-clock assertion.
