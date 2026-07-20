# Judgement Feedback Halo Implementation Plan

> **For implementers:** Execute this plan task-by-task in the written test-first order. Keep each
> checkpoint independently buildable, and use the environment's safe commit mechanism when creating
> the proposed commits.

**Goal:** Show short-lived judgement rank and early/late feedback around an invisible inner circle at
the judged object's angle, with radial collision stacking and correct rewind behaviour.

**Architecture:** `Ring` owns one `JudgementFeedbackDisplay` above its hit-object layers. The display
observes the `Ring.NewResult` / `Ring.RevertResult` stream, filters it through the existing scoring and
`DisplayResult` contracts, and owns every message's layout and lifetime. A small drawable contract
distinguishes timing-bearing results from duration results so the UI never labels a frame-delayed hold
or slider grade as “late”.

**Tech stack:** C# / osu-framework graphics and transforms; NUnit plus framework `TestScene` tests.

**Source design:** `docs/superpowers/specs/2026-07-19-judgement-feedback-design.md`

**Final content policy:** Discrete buttons suppress Critical Perfect, show Perfect as white
`EARLY`/`LATE` only, show Near with its direction, and show Miss without a direction. Perfect results
from hold parents, slider heads, and slider children are suppressed. These presentation filters apply
before the general message construction described below.

## Global constraints

- Preserve the gameplay polar convention: 0° right, 90° up, 180° left, 270° down.
- The feedback circle is layout-only. Do not add a visible circle, mask, or ring asset.
- Use `HitResult.GetDescription()` for rank names rather than duplicating display strings.
- Filter with `result.Type.IsScorable()` and `drawable.DisplayResult`; never display the Ignore pair.
- Keep scoring in `PlayScreen` untouched. The feedback layer is a read-only result observer.
- Rewind keys messages by `JudgementResult` reference, not by object/time/value equality.
- Do not import `Edit.EditorAngleMapping` into gameplay UI. Keep the small circular-angle helper local
  to the feedback display.
- Nullability is enabled in project-owned files. The vendored `DrawableHitObject` file remains under
  its existing `#nullable disable` convention and keeps its attribution header.
- Build with `dotnet build Garbus.Desktop.slnf`; test with
  `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.

---

## File structure

**New files:**

- `Garbus.Game/UI/JudgementFeedbackMessage.cs` — one upright two-line result label and its polar target.
- `Garbus.Game/UI/JudgementFeedbackDisplay.cs` — filtering, clustering, layout, lifetime, clearing,
  and rewind removal.
- `Garbus.Game.Tests/Visual/TestSceneJudgementFeedback.cs` — focused behaviour and visual-tuning scene.

**Modified files:**

- `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs` — presentation contract indicating
  whether `TimeOffset` has timing meaning.
- `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs` — duration result opts out of early/late text.
- `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs` — duration result opts out of early/late text.
- `Garbus.Game/UI/Ring.cs` — own, draw, and wire the feedback display.
- `Garbus.Game.Tests/Visual/TestSceneGameplay.cs` — end-to-end result routing and rewind coverage.
- `PLAN-port.md` and `CLAUDE.md` — record the landed gameplay feedback feature.
- `docs/superpowers/specs/2026-07-19-judgement-feedback-design.md` — mark implemented after validation.

---

## Task 1: Define timing semantics and build one message

**Files:**

- Modify: `Garbus.Game/Gameplay/Objects/Drawables/DrawableHitObject.cs` near `DisplayResult`
- Modify: `Garbus.Game/Objects/Drawables/DrawableHoldNote.cs` near the class properties
- Modify: `Garbus.Game/Objects/Drawables/DrawableSliderChild.cs` near the class properties
- Create: `Garbus.Game/UI/JudgementFeedbackMessage.cs`
- Create: `Garbus.Game.Tests/Visual/TestSceneJudgementFeedback.cs`

**Produces:**

- `DrawableHitObject.DisplayTimingOffset -> bool` (virtual, default `true`)
- `JudgementFeedbackMessage(JudgementResult, float angleDeg, bool displayTimingOffset)`
- Test-facing message state: `Result`, `AngleDeg`, `RankText`, `TimingText`, `StackIndex`, and
  `TargetPosition`

- [ ] **Step 1: Write failing message-content tests**

Create `TestSceneJudgementFeedback` with a `ManualClock` and helper methods that construct a
`CardinalNote`, apply defaults, create a lightweight test drawable, and create a `JudgementResult`.
The test assembly has `InternalsVisibleTo`, so the helper may set `TimeOffset` directly.

Add cases for:

```csharp
[TestCase(HitResult.CriticalPerfect, "CRITICAL PERFECT")]
[TestCase(HitResult.Perfect, "PERFECT")]
[TestCase(HitResult.Near, "NEAR")]
[TestCase(HitResult.Bad, "BAD")]
[TestCase(HitResult.Miss, "MISS")]
public void TestRankText(HitResult result, string expected)

[TestCase(-18, "EARLY")]
[TestCase(18, "LATE")]
[TestCase(0, "")]
public void TestTimingDirection(double offset, string expected)

[Test]
public void TestDurationMessageOmitsDirection()
```

The duration case passes a non-zero positive offset with `displayTimingOffset: false` and still expects
an empty `TimingText`. Add a zero epsilon boundary case so only floating-point noise around zero is
suppressed.

- [ ] **Step 2: Run the focused test and verify failure**

Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneJudgementFeedback
```

Expected: FAIL because `JudgementFeedbackMessage` and `DisplayTimingOffset` do not exist.

- [ ] **Step 3: Add the display-timing contract**

Immediately after `DrawableHitObject.DisplayResult`, add:

```csharp
/// <summary>
/// Whether this result's signed time offset represents player input timing and may be shown as
/// early/late feedback. Duration judgements override this because their application time is not
/// their quality measurement.
/// </summary>
public virtual bool DisplayTimingOffset => true;
```

Override it to `false` in the generic `DrawableHoldNote<THitObject, THead>` base and in
`DrawableSliderChild`. Do not change `DrawableHoldNoteHead.DisplayResult`; its existing opt-out remains
authoritative. The slider body's ignored result is already removed by the scorable-result filter.

- [ ] **Step 4: Implement `JudgementFeedbackMessage`**

Create an upright `CompositeDrawable` containing a vertical, auto-sized pair of centered
`SpriteText`s:

- bold Inter rank line;
- smaller secondary timing line;
- `Anchor.Centre` and `Origin.Centre` on the message and text container;
- no rotation at any angle.

Use `result.Type.GetDescription().ToUpperInvariant()` for the primary text. Derive the secondary text
only when `displayTimingOffset` is true. Keep the zero epsilon as a named, tiny constant used only for
floating-point stability.

Map ranks to initial colours in one private method: Critical Perfect warm white/gold, Perfect cyan,
Near yellow, Bad orange, Miss red. The timing line should be a subdued neutral colour so rank remains
the primary cue.

Add an internal `SetPolarTarget(float radius, double duration = 0)` method. It computes the offset from
the parent centre with:

```csharp
new Vector2(MathF.Cos(radians) * radius, -MathF.Sin(radians) * radius)
```

It stores that value in `TargetPosition` and either sets or transforms `Position`. `StackIndex` is set
by the owning display, not inferred by the message.

- [ ] **Step 5: Run the focused test and verify pass**

Expected: all message-content cases pass.

- [ ] **Step 6: Commit checkpoint**

Proposed message: `feat: add judgement feedback message semantics`

---

## Task 2: Add display filtering and polar placement

**Files:**

- Create: `Garbus.Game/UI/JudgementFeedbackDisplay.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneJudgementFeedback.cs`

**Produces:**

- `JudgementFeedbackDisplay.Show(DrawableHitObject, JudgementResult) -> bool`
- `JudgementFeedbackDisplay.Revert(JudgementResult)`
- `JudgementFeedbackDisplay.ClearMessages()`
- `JudgementFeedbackDisplay.DisplayJudgements` bindable
- Test-facing `ActiveMessages` read-only collection

- [ ] **Step 1: Write failing filtering and placement tests**

Add tests which load an 800×800 `JudgementFeedbackDisplay` and call `Show` directly:

- a scorable, opted-in angled result is accepted;
- `IgnoreHit` and `IgnoreMiss` are rejected;
- a test drawable overriding `DisplayResult => false` is rejected;
- a test hit object without `IHasAngle` is rejected;
- setting `DisplayJudgements` false clears current messages and rejects new ones;
- re-enabling allows later messages;
- 0°, 90°, 180°, and 270° produce targets in the right/up/left/down quadrants;
- 450° normalises to the same target as 90°;
- every message retains `Rotation == 0`.

Use test drawables with constructor-controlled `DisplayResult` and `DisplayTimingOffset` rather than
loading texture-dependent gameplay drawables for filter-only tests.

- [ ] **Step 2: Run the focused test and verify failure**

Expected: FAIL because `JudgementFeedbackDisplay` does not exist.

- [ ] **Step 3: Implement the display's single-message pipeline**

Make the display fill its parent. Keep children positioned from `Anchor.Centre` so each message's
`Position` is a polar offset, not an absolute draw-rectangle coordinate.

Use named layout constants as initial tuning values:

```csharp
private const float radius_ratio = 0.20f; // fraction of playfield diameter
private const float minimum_radius = 96;
private const float maximum_radius = 180;
```

Compute base radius from `MathF.Min(DrawWidth, DrawHeight)` and clamp it. Reflow active messages when
the display's required parent size changes; use a `LayoutValue` rather than comparing dimensions every
frame.

`Show` must reject in this order:

1. feedback disabled;
2. non-scorable result;
3. `DisplayResult == false`;
4. hit object does not implement `IHasAngle`.

On acceptance, create a message using `drawable.DisplayTimingOffset`, add it internally, add it to the
active list, and lay it out at stack slot zero. Return `true`; rejected calls return `false`.

Bind `DisplayJudgements` changes locally. A false value clears messages and sets display alpha to zero;
a true value restores alpha but does not recreate historical messages.

- [ ] **Step 4: Run the focused test and verify pass**

- [ ] **Step 5: Commit checkpoint**

Proposed message: `feat: place filtered judgement feedback by object angle`

---

## Task 3: Implement radial clustering, lifetime, and revert

**Files:**

- Modify: `Garbus.Game/UI/JudgementFeedbackDisplay.cs`
- Modify: `Garbus.Game/UI/JudgementFeedbackMessage.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneJudgementFeedback.cs`

**Produces:**

- Nearby-angle connected-component clustering across the 0°/360° seam
- Newest-first stack slots with a maximum of three messages per cluster
- Approximately 800 ms message lifecycle
- Exact result-reference removal on rewind

- [ ] **Step 1: Write failing stack tests**

Add deterministic tests using the scene's `ManualClock`:

- three results at the same angle receive slots 0/1/2 newest-first;
- the newest target radius is smallest and older target radii increase by one stack spacing;
- a fourth same-angle result removes and disposes the oldest, leaving three;
- 359° and 1° join the same cluster;
- angles outside the threshold remain at slot zero;
- removing the slot-one result closes the remaining gap;
- reverting a result removes only the message holding the same object reference;
- passing a distinct but value-equivalent `JudgementResult` does not remove anything.

Use named constants in expectations: `COLLISION_ANGLE_DEG = 15`, `MAX_CLUSTER_MESSAGES = 3`, and a
single stack-spacing constant exposed internally if necessary.

- [ ] **Step 2: Write failing lifetime tests**

Add tests which:

- insert one message;
- advance the manual clock through the fade-in and readable interval;
- advance beyond total lifetime and wait until the active list is empty;
- verify a remaining older message moves inward after its neighbour expires;
- clear while transforms are running and verify no children or active references remain.

Assert lifecycle state and target layout, not intermediate pixel values or easing samples.

- [ ] **Step 3: Run the focused test and verify failure**

Expected: FAIL because clustering and expiry are not implemented.

- [ ] **Step 4: Implement circular clustering and stack reflow**

Use the shortest absolute circular difference:

```csharp
double difference = Math.Abs((a - b + 180) % 360);
if (difference > 180)
    difference = 360 - difference;
```

Treat live messages as vertices and “within 15°” as edges. Build connected components so a chain of
nearby messages is one collision cluster rather than producing overlapping independent stacks. Lists
are tiny and short-lived, so a straightforward visited-set traversal is preferable to cached or
incremental complexity.

For each component:

1. sort by monotonically increasing creation sequence, newest first;
2. retire entries after index 2;
3. assign surviving indices as stack slots;
4. target `baseRadius + slot * stackSpacing`;
5. animate changed targets over about 100 ms.

Batch removals before re-running layout so cap enforcement cannot recurse during enumeration.

- [ ] **Step 5: Implement message lifetime and disposal**

Use named initial timings matching the design:

- `FADE_IN_DURATION = 100`;
- `READ_DURATION = 450`;
- `FADE_OUT_DURATION = 250`;
- total ≈ 800 ms.

Animate from roughly 0.85 scale, hold, then fade while drifting slightly outward. Completion calls one
display-owned removal path that:

- removes from the active collection;
- removes and disposes the drawable;
- reflows surviving messages.

The same removal path serves expiry, cluster-cap retirement, and `Revert`. Make removal idempotent so a
revert racing an expiry is harmless. `ClearMessages` must cancel/finish transforms as appropriate and
dispose every child immediately.

- [ ] **Step 6: Run the focused test and verify pass**

- [ ] **Step 7: Add the frozen visual-tuning state**

In `TestSceneJudgementFeedback`, add a visual test which creates representative ranks at the four
cardinal angles plus a three-message same-angle burst and 359°/1° seam pair. After the fade-in, freeze
the `ManualClock` during the readable interval so the browser can display the state indefinitely for
font, radius, colour, and spacing tuning.

- [ ] **Step 8: Commit checkpoint**

Proposed message: `feat: stack and animate judgement feedback messages`

---

## Task 4: Wire the feedback halo into `Ring`

**Files:**

- Modify: `Garbus.Game/UI/Ring.cs`
- Modify: `Garbus.Game.Tests/Visual/TestSceneGameplay.cs`

**Consumes:** `Playfield.NewResult`, `Playfield.RevertResult`, `Playfield.DisplayJudgements`

**Produces:** `Ring.JudgementFeedback` test-facing property

- [ ] **Step 1: Write the failing end-to-end gameplay test**

Extend `TestSceneGameplay` with a test that uses the existing chart and manual input path:

1. play through to 100 ms before the first 90° cardinal note;
2. press and release North;
3. wait for the note to judge;
4. assert `playfield.ChildrenOfType<JudgementFeedbackDisplay>().Single()` has one message;
5. assert that message references the note's real `JudgementResult`, has angle 90°, rank `NEAR`, and
   timing text `EARLY`;
6. seek back before the press time;
7. wait for the existing playfield rewind flow to revert the judgement;
8. assert the feedback message disappears.

Add a second test or continuation which sets `playfield.DisplayJudgements.Value = false`, verifies the
display clears, performs another judgement, and verifies no message is added while disabled.

- [ ] **Step 2: Run the gameplay scene and verify failure**

Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestSceneGameplay
```

Expected: FAIL because `Ring` does not contain or wire the display.

- [ ] **Step 3: Add and layer the display in `Ring`**

Create one readonly `JudgementFeedbackDisplay` field. Insert it into the `AddRangeInternal` list after
`HitObjectContainer` and `laneContainer`, before the outer `Arc`. Update the back-to-front comment to
mention feedback above hit objects.

Expose:

```csharp
public JudgementFeedbackDisplay JudgementFeedback => judgementFeedback;
```

Wire named handlers:

- `NewResult` → `judgementFeedback.Show`;
- `RevertResult` → `judgementFeedback.Revert`;
- bind `judgementFeedback.DisplayJudgements` to `Ring.DisplayJudgements`.

Use named handler methods where event unsubscription is required. Unsubscribe in `Dispose`; unbind the
feedback bindable there or in the child's own disposal. Do not subscribe to individual lanes—the
existing `AddNested` forwarding already reaches the ring event exactly once.

- [ ] **Step 4: Run the focused display and gameplay scenes**

Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneJudgementFeedback|FullyQualifiedName~TestSceneGameplay"
```

Expected: PASS.

- [ ] **Step 5: Run the play-screen smoke scene**

Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter FullyQualifiedName~TestScenePlayScreen
```

Expected: PASS, proving normal screen construction and result scoring remain unaffected.

- [ ] **Step 6: Commit checkpoint**

Proposed message: `feat: show judgement feedback around the gameplay centre`

---

## Task 5: Record the feature and validate the repository

**Files:**

- Modify: `PLAN-port.md`
- Modify: `CLAUDE.md`
- Modify: `docs/superpowers/specs/2026-07-19-judgement-feedback-design.md`

- [ ] **Step 1: Update project documentation**

In `PLAN-port.md`, add a completed gameplay-chrome bullet describing the invisible inner feedback halo,
angle-local messages, early/late timing labels, radial collision stack, and rewind removal.

In `CLAUDE.md`, add the new `JudgementFeedbackDisplay` to the Phase 2 gameplay/UI summary and record the
important contract:

- `DisplayResult` controls whether a meaningful result surfaces;
- `DisplayTimingOffset` distinguishes timing results from duration grades;
- result references are required for rewind removal.

Change the source design's `Status: approved` to `Status: implemented` only after all tests below pass.

- [ ] **Step 2: Run formatting and focused checks**

Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneJudgementFeedback|FullyQualifiedName~TestSceneGameplay|FullyQualifiedName~TestScenePlayScreen"
git diff --check
```

Expected: all focused tests pass and `git diff --check` prints no errors.

- [ ] **Step 3: Run the complete headless test suite**

Run:

```powershell
dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 4: Build the desktop target**

Run:

```powershell
dotnet build Garbus.Desktop.slnf
```

Expected: build succeeds with no new warnings.

- [ ] **Step 5: Review the visual tuning scene**

Open `TestSceneJudgementFeedback` in the visual test browser and inspect:

- labels clear the centre combo;
- all text stays upright;
- cardinal and seam positions match the hit-object travel directions;
- rank and timing lines remain readable over hit objects;
- three-message stacks do not collide;
- fade and drift feel immediate without lingering through later patterns.

Visual tuning may adjust the named colour, font, radius, spacing, and duration constants. Behavioural
thresholds and tests should not be weakened to accommodate presentation changes.

- [ ] **Step 6: Commit checkpoint**

Proposed message: `docs: record judgement feedback halo implementation`

---

## Completion criteria

- Every scorable, opted-in angled result produces exactly one message.
- Timing-bearing results show the rank plus correct early/late direction without milliseconds.
- Duration results show rank only.
- Nearby results stack newest-first, across the angular seam, with a three-message cap.
- Messages expire and dispose without accumulating children.
- Rewind removes the exact corresponding live message and replay can create a fresh one.
- Disabling `DisplayJudgements` clears and suppresses feedback.
- Existing score/combo/accuracy and gameplay tests remain green.
- The full headless suite and desktop build pass.
