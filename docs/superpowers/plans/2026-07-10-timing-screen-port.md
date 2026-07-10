# Timing Screen Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port the remainder of osu's editor Timing screen into Garbus's Timing tab: attribute-chip table rows with a header, keyboard selection, time-signature/omit-barline editing, hold-to-repeat nudge steppers, a "use current time" group move, section-wide object adjustment on timing changes, and adjust buttons under the tap-timing metronome.

**Architecture:** All work lands in the existing `Garbus.Game/Edit/Screens/Timing/` components (`TimingPointList`, `TimingPointSettings`, `TapTimingControl`) plus two new model-layer pieces: `EditorChart.PerformOnRange` and a `TimingSectionAdjustments` static class (vendored from osu's math). A shared `TimingPointChanges` helper centralises the move-group / change-BPM undo transactions so the settings panel and tap-timing control don't duplicate them. UI stays on osu-framework `Basic*` widgets.

**Tech Stack:** C# / .NET, osu-framework (no osu.Game dependency), NUnit headless test scenes (`ManualInputManager` real-click patterns).

**Reference:** osu's originals live at `C:\Users\zachd\Code\BAC\LocalDependencies\osu\osu.Game\Screens\Edit\Timing\` (`ControlPointTable.cs`, `RowAttribute.cs`, `LabelledTimeSignature.cs`, `GroupSection.cs`, `TimingSectionAdjustments.cs`, `DiscreteAdjustmentControl.cs`). Vendor faithfully; deviate minimally and note why in the "Adapted for Garbus:" header line.

## Out of scope (deliberately not ported)

- **EffectSection / SliderVelocityAdjustmentControl / IndeterminateSliderWithTextBoxInput** — Garbus's `ControlPointInfo` is timing-only (no kiai/SV points) and gameplay scroll is constant. Nothing for these to edit.
- **TimingScreen shell / VirtualisedListContainer** — `TimingTab` already fills the screen role; Garbus chart sizes don't need row virtualisation, keep the plain `FillFlowContainer`.
- **Tap-BPM object adjustment** — `TapButton` writes BPM directly and is used while initially timing a chart, before objects exist. Its writes deliberately do NOT move objects (unlike osu's tap path). Documented in the `TapButton` header in Task 8.

## Global Constraints

- Build: `dotnet build Garbus.Desktop.slnf` (never the iOS slnf). Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- Windows / PowerShell — use `\` paths and PowerShell syntax in commands.
- This is an experimental project: NO backwards compatibility, NO version bumps, NO historical context in docs or comments ("previously X, now Y" is banned — describe only the current behaviour).
- UI uses osu-framework `Basic*` widgets only — no `OverlayColourProvider`, no `OsuFont`, no osu.Game UI classes.
- Vendored/adapted osu.Game files keep the ppy MIT attribution header plus an "Adapted for Garbus:" line; when a task changes what an existing file does, its "Adapted for Garbus:" line must be updated to stay true.
- Nullability is enabled solution-wide; DI-resolved / BDL-initialised fields use `= null!`.
- Event-subscription gotcha: never subscribe a lambda to a model bindable/event from a drawable without a dispose path. Use `GetBoundCopy()` stored in a **field of the drawable** (framework auto-unbinds fields on dispose) or unsubscribe in `Dispose`.
- Tests are headless NUnit scenes in `Garbus.Game.Tests\Editor\`; UI behaviour regression-guards use REAL mouse clicks via `ManualInputManager` (see existing `TestSceneTimingTab` patterns), not just `TriggerClick()`.
- Commit after every task. Never `--no-verify`.

---

### Task 1: Table upgrade — header row + attribute-chip rows

Replaces the single concatenated-string `TimingPointRow` with osu's `ControlPointTable` layout: a fixed header row ("Time" / "Attributes"), a fixed-width time column, and per-point attribute chips (BPM chip, time-signature chip, and a "no barline" chip that fades with `OmitFirstBarLine`). Also fixes a latent leak: the old row subscribed lambdas directly to the timing point's bindables and never unsubscribed.

**Files:**
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointList.cs` (whole file shown below; the `TimingPointRow` class is rewritten, the list gains a header)
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: `TimingControlPoint.BeatLengthBindable / TimeSignatureBindable / OmitFirstBarLineBindable` (existing), `TimingPointSettings.SetBpmAndCommit(double)` (existing test seam).
- Produces: `TimingPointRow : ClickableContainer` with unchanged public surface (`IsSelected`, `new Action<ControlPointGroup>? Action`, ctor `(ControlPointGroup, TimingControlPoint)`), `TimingPointRow.TIME_COLUMN_WIDTH` const, and public nested `TimingPointRow.AttributeChip` with a get/set `Text` property (Tasks 4's test and future tests query chips by text).

- [ ] **Step 1: Write the failing test**

Append to `TestSceneTimingTab` (inside the class, after `TestFourTaps500MsApartGivesBpm120`'s region is fine — order doesn't matter):

```csharp
        // ------------------------------------------------------------------
        // 7. Table rows render attribute chips that live-update
        // ------------------------------------------------------------------

        [Test]
        public void TestRowShowsAttributeChips()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("row has 120 BPM chip", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Any(c => c.Text.ToString() == "120 BPM"));

            AddAssert("row has signature chip", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Any(c => c.Text.ToString() == "4/4"));

            AddAssert("no-barline chip hidden by default", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Single(c => c.Text.ToString() == "no barline").Alpha == 0);

            AddStep("set BPM to 180 via settings seam", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetBpmAndCommit(180));

            AddUntilStep("BPM chip updated in place", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Any(c => c.Text.ToString() == "180 BPM"));
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestRowShowsAttributeChips"`
Expected: BUILD FAILURE — `'TimingPointRow' does not contain a definition for 'AttributeChip'`.

- [ ] **Step 3: Implement header + chip rows**

Replace the entire contents of `Garbus.Game\Edit\Screens\Timing\TimingPointList.cs` with:

```csharp
// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/ControlPointList.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: stripped to timing-only (one control point type); rebuilt UI on Basic* widgets;
// no OverlayColourProvider; plain flow instead of VirtualisedListContainer (chart sizes don't need
// virtualisation) with osu's ControlPointTable layout (header row, fixed time column, attribute
// chips); object-shifting on timing change does NOT apply.

using System;
using System.Linq;
using Garbus.Game.Charts.Timing;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osuTK;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Left panel of the Timing tab: header row + one row per timing control point, each showing the
    /// point's time and attribute chips (BPM, time signature, no-barline).
    /// Selecting a row seeks the editor clock to the point's time.
    /// </summary>
    public partial class TimingPointList : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        /// <summary>The currently selected timing control point group (shared with settings panel).</summary>
        public readonly Bindable<ControlPointGroup?> SelectedGroup = new Bindable<ControlPointGroup?>();

        private const float header_height = 24;

        private FillFlowContainer<TimingPointRow> rowContainer = null!;
        private BasicButton addButton = null!;
        private BasicButton deleteButton = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Text = "Time",
                            X = 8,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                        new SpriteText
                        {
                            Text = "Attributes",
                            X = TimingPointRow.TIME_COLUMN_WIDTH,
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Font = FontUsage.Default.With(size: 14),
                        },
                    },
                },
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = header_height, Bottom = 40 },
                    Child = rowContainer = new FillFlowContainer<TimingPointRow>
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 1),
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Children = new Drawable[]
                    {
                        addButton = new BasicButton
                        {
                            Text = "Add",
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Action = addAtPlayhead,
                        },
                        deleteButton = new BasicButton
                        {
                            Text = "Delete",
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            Action = deleteSelected,
                        },
                    }
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            // Keep the buttons' enabled state honest so an impossible action reads as a greyed-out
            // button instead of a silent no-op (fresh chart: playhead parked at 0 on the initial
            // point made both buttons look dead — ISSUES.md).
            //
            // osu semantics: Add is "add or focus the group at the playhead" — it only greys out when
            // that group is already the selected one (selecting a row seeks onto the point, so a
            // plain "no group here" check would grey Add after every selection — ISSUES.md).
            double snapped = editorChart.ControlPointInfo.GetClosestSnappedTime(editorClock.CurrentTime);
            var groupAtPlayhead = editorChart.ControlPointInfo.GroupAt(snapped);
            addButton.Enabled.Value = groupAtPlayhead == null || SelectedGroup.Value != groupAtPlayhead;
            deleteButton.Enabled.Value = SelectedGroup.Value != null && editorChart.ControlPointInfo.TimingPoints.Count > 1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            editorChart.ControlPointInfo.ControlPointsChanged += scheduleRefresh;
            refreshRows();
        }

        private void scheduleRefresh() => Scheduler.AddOnce(refreshRows);

        private void refreshRows()
        {
            rowContainer.Clear();

            foreach (var group in editorChart.ControlPointInfo.Groups)
            {
                var tp = group.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();
                if (tp == null) continue;

                var row = new TimingPointRow(group, tp)
                {
                    IsSelected = { BindTarget = SelectedGroup },
                    Action = g =>
                    {
                        // Re-clicking the selected row deselects it (ISSUES.md: there was no way to
                        // clear the selection at all).
                        if (SelectedGroup.Value == g)
                        {
                            SelectedGroup.Value = null;
                            return;
                        }

                        SelectedGroup.Value = g;
                        editorClock.Seek(g.Time);
                    },
                };
                rowContainer.Add(row);
            }

            // Reselect if the previously selected group still exists.
            if (SelectedGroup.Value != null)
            {
                var stillExists = editorChart.ControlPointInfo.Groups
                    .FirstOrDefault(g => Math.Abs(g.Time - SelectedGroup.Value.Time) < 1);
                SelectedGroup.Value = stillExists;
            }

            // Auto-select first if nothing is selected.
            if (SelectedGroup.Value == null && editorChart.ControlPointInfo.Groups.Count > 0)
            {
                var firstGroup = editorChart.ControlPointInfo.Groups[0];
                SelectedGroup.Value = firstGroup;
            }
        }

        private void addAtPlayhead()
        {
            double time = editorChart.ControlPointInfo.GetClosestSnappedTime(editorClock.CurrentTime);

            // A group already at the playhead: focus it instead of silently replacing its point
            // in place (osu's "+" semantics).
            var existing = editorChart.ControlPointInfo.GroupAt(time);
            if (existing != null)
            {
                SelectedGroup.Value = existing;
                return;
            }

            // Copy BeatLength from the active point at that time (or use default 500ms = 120 BPM).
            var prevPoint = editorChart.ControlPointInfo.TimingPointAt(time);
            double beatLength = editorChart.ControlPointInfo.TimingPoints.Count > 0
                ? prevPoint.BeatLength
                : 500;

            changeHandler.BeginChange();

            var newPoint = new TimingControlPoint
            {
                BeatLength = beatLength,
            };

            editorChart.ControlPointInfo.Add(time, newPoint);
            editorChart.SaveState();

            changeHandler.EndChange();

            // Select the newly added group.
            var addedGroup = editorChart.ControlPointInfo.GroupAt(time);
            SelectedGroup.Value = addedGroup;
        }

        private void deleteSelected()
        {
            if (SelectedGroup.Value == null)
                return;

            // Don't delete the only timing point.
            if (editorChart.ControlPointInfo.TimingPoints.Count <= 1)
                return;

            changeHandler.BeginChange();
            editorChart.ControlPointInfo.RemoveGroup(SelectedGroup.Value);
            editorChart.SaveState();
            changeHandler.EndChange();

            SelectedGroup.Value = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (editorChart != null)
                editorChart.ControlPointInfo.ControlPointsChanged -= scheduleRefresh;
        }
    }

    /// <summary>
    /// A single row in the timing point list: time column + attribute chips (BPM, time signature,
    /// and a "no barline" chip shown only while <see cref="TimingControlPoint.OmitFirstBarLine"/> is set).
    /// </summary>
    public partial class TimingPointRow : ClickableContainer
    {
        public const float TIME_COLUMN_WIDTH = 110;

        private static readonly Colour4 row_background = new Colour4(42, 42, 48, 255);
        private static readonly Colour4 selected_background = new Colour4(70, 90, 140, 255);

        private readonly ControlPointGroup group;

        /// <summary>Bindable to the parent list's SelectedGroup — drives visual selection state.</summary>
        public readonly Bindable<ControlPointGroup?> IsSelected = new Bindable<ControlPointGroup?>();

        public new Action<ControlPointGroup>? Action;

        // Bound copies stored as fields so drawable disposal auto-unbinds them. (Subscribing lambdas
        // directly to the point's bindables would keep every discarded row alive after each list
        // refresh — the lambda-leak gotcha.)
        private readonly IBindable<double> beatLength;
        private readonly IBindable<TimeSignature> timeSignature;
        private readonly IBindable<bool> omitFirstBarLine;

        private Box background = null!;
        private AttributeChip bpmChip = null!;
        private AttributeChip signatureChip = null!;
        private AttributeChip omitBarLineChip = null!;

        public TimingPointRow(ControlPointGroup group, TimingControlPoint timingPoint)
        {
            this.group = group;

            beatLength = timingPoint.BeatLengthBindable.GetBoundCopy();
            timeSignature = timingPoint.TimeSignatureBindable.GetBoundCopy();
            omitFirstBarLine = timingPoint.OmitFirstBarLineBindable.GetBoundCopy();

            RelativeSizeAxes = Axes.X;
            Height = 32;

            base.Action = () => Action?.Invoke(group);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = row_background,
                },
                new SpriteText
                {
                    Text = $"{group.Time:0}ms",
                    X = 8,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(4, 0),
                    X = TIME_COLUMN_WIDTH,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Children = new Drawable[]
                    {
                        bpmChip = new AttributeChip(),
                        signatureChip = new AttributeChip(),
                        omitBarLineChip = new AttributeChip { Text = "no barline", Alpha = 0 },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            IsSelected.BindValueChanged(e =>
            {
                bool selected = e.NewValue != null && Math.Abs(e.NewValue.Time - group.Time) < 1;
                background.Colour = selected ? selected_background : row_background;
                Alpha = selected ? 1f : 0.85f;
            }, true);

            beatLength.BindValueChanged(_ => bpmChip.Text = $"{60000 / beatLength.Value:0.##} BPM", true);
            timeSignature.BindValueChanged(_ => signatureChip.Text = $"{timeSignature.Value.Numerator}/4", true);
            omitFirstBarLine.BindValueChanged(e => omitBarLineChip.Alpha = e.NewValue ? 1 : 0, true);
        }

        /// <summary>
        /// A small rounded pill showing one attribute of the timing point (osu's RowAttribute,
        /// simplified: no representing-colour circle, plain SpriteText).
        /// </summary>
        public partial class AttributeChip : CompositeDrawable
        {
            private readonly SpriteText text;

            public LocalisableString Text
            {
                get => text.Text;
                set => text.Text = value;
            }

            public AttributeChip()
            {
                AutoSizeAxes = Axes.X;
                Height = 20;
                Anchor = Anchor.CentreLeft;
                Origin = Anchor.CentreLeft;
                Masking = true;
                CornerRadius = 3;

                InternalChildren = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Colour4(25, 25, 30, 255),
                    },
                    text = new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Margin = new MarginPadding { Horizontal = 6 },
                        Font = FontUsage.Default.With(size: 14),
                    },
                };
            }
        }
    }
}
```

- [ ] **Step 4: Run the new test and the whole timing suite to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab"`
Expected: PASS — all pre-existing tests (row click/deselect semantics unchanged: `TimingPointRow` is still clickable with the same `Action` contract) plus `TestRowShowsAttributeChips`.

- [ ] **Step 5: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\TimingPointList.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs
git commit -m "Add header row and attribute chips to timing point table"
```

---

### Task 2: Keyboard selection in the timing point list

Up/Down arrows move the selection to the previous/next timing group and seek to it. When nothing is selected, Down selects the first group and Up the last. Consumes the key so the editor-level divisor binding (Up/Down changes beat divisor) is shadowed while the Timing tab is visible — deliberate, matching the "table owns arrows" feel from the source plan.

**Files:**
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointList.cs` (add `OnKeyDown` + `moveSelection` to `TimingPointList`)
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: Task 1's `TimingPointList` (unchanged public surface).
- Produces: `setupEditor(double initialBpm = 120.0, double? secondPointTime = null)` — extended test helper that later tasks (5, 6, 9) use to get a two-point chart wrapped in the `ManualInputManager`.

- [ ] **Step 1: Extend the test setup helper**

In `TestSceneTimingTab`, replace the existing `setupEditor` method:

```csharp
        private void setupEditor(double initialBpm = 120.0) => Schedule(() =>
```

…and its body, with:

```csharp
        private void setupEditor(double initialBpm = 120.0, double? secondPointTime = null) => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 60000.0 / initialBpm });
            if (secondPointTime != null)
                chart.ControlPointInfo.Add(secondPointTime.Value, new TimingControlPoint { BeatLength = 500 });

            var chartFile = new ChartFile(chart);
            editor = new GarbusEditor(chartFile);
            Child = input = new osu.Framework.Testing.Input.ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });
```

- [ ] **Step 2: Write the failing test**

Append to `TestSceneTimingTab`:

```csharp
        // ------------------------------------------------------------------
        // 8. Arrow keys move the selection
        // ------------------------------------------------------------------

        [Test]
        public void TestArrowKeysMoveSelection()
        {
            setupEditor(secondPointTime: 2000);
            switchToTimingTab();

            AddUntilStep("first group auto-selected", () =>
                timingList().SelectedGroup.Value?.Time == 0);

            AddStep("press Down", () =>
            {
                input.PressKey(osuTK.Input.Key.Down);
                input.ReleaseKey(osuTK.Input.Key.Down);
            });

            AddUntilStep("second group selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);
            AddUntilStep("clock seeked to selection", () =>
                Math.Abs(editor.ChildrenOfType<EditorClock>().First().CurrentTime - 2000) < 50);

            AddStep("press Down at last row", () =>
            {
                input.PressKey(osuTK.Input.Key.Down);
                input.ReleaseKey(osuTK.Input.Key.Down);
            });
            AddAssert("selection stays on last", () =>
                timingList().SelectedGroup.Value?.Time == 2000);

            AddStep("press Up", () =>
            {
                input.PressKey(osuTK.Input.Key.Up);
                input.ReleaseKey(osuTK.Input.Key.Up);
            });
            AddUntilStep("first group selected again", () =>
                timingList().SelectedGroup.Value?.Time == 0);
        }

        private TimingPointList timingList() => editor.ChildrenOfType<TimingPointList>().First();
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestArrowKeysMoveSelection"`
Expected: FAIL at "second group selected" — pressing Down currently changes the beat divisor (the editor-level handler), not the selection.

- [ ] **Step 4: Implement key handling**

In `TimingPointList.cs`, add two usings at the top:

```csharp
using osu.Framework.Input.Events;
using osuTK.Input;
```

Then add to the `TimingPointList` class (after the `Update` override):

```csharp
        protected override bool OnKeyDown(KeyDownEvent e)
        {
            // Arrow selection deliberately shadows the editor's divisor binding while this tab is
            // visible — the table owns Up/Down here, matching osu's timing-screen feel.
            if (e.Repeat || e.ControlPressed || e.AltPressed || e.ShiftPressed || e.SuperPressed)
                return base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Up:
                    return moveSelection(-1);

                case Key.Down:
                    return moveSelection(1);
            }

            return base.OnKeyDown(e);
        }

        private bool moveSelection(int direction)
        {
            var groups = editorChart.ControlPointInfo.Groups;
            if (groups.Count == 0)
                return false;

            int currentIndex = -1;

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i] == SelectedGroup.Value)
                {
                    currentIndex = i;
                    break;
                }
            }

            int targetIndex = currentIndex == -1
                ? (direction > 0 ? 0 : groups.Count - 1)
                : Math.Clamp(currentIndex + direction, 0, groups.Count - 1);

            var target = groups[targetIndex];

            if (target != SelectedGroup.Value)
            {
                SelectedGroup.Value = target;
                editorClock.Seek(target.Time);
            }

            return true;
        }
```

- [ ] **Step 5: Run the timing suite to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab"`
Expected: PASS (all tests).

- [ ] **Step 6: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\TimingPointList.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs
git commit -m "Add arrow-key selection to timing point list"
```

---

### Task 3: Time-signature numerator textbox (LabelledTimeSignature equivalent)

Replaces the `BasicDropdown<int>` (fixed 1–7) with osu's `LabelledTimeSignature` behaviour: a free numerator textbox next to a "/ 4" label. Any positive integer commits; invalid input restores the current value.

**Files:**
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs`
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: `TimeSignature(int numerator)` ctor (throws below 1 — guard first), `currentTimingPoint` (existing private helper).
- Produces: `TimingPointSettings.SetSignatureAndCommit(string text)` test seam (mirrors `SetBpmAndCommit`).

- [ ] **Step 1: Write the failing test**

Append to `TestSceneTimingTab`:

```csharp
        // ------------------------------------------------------------------
        // 9. Time signature numerator textbox
        // ------------------------------------------------------------------

        [Test]
        public void TestTimeSignatureTextboxCommits()
        {
            setupEditor();
            switchToTimingTab();

            AddUntilStep("settings panel loaded", () =>
                editor.ChildrenOfType<TimingPointSettings>().Any());

            AddStep("set signature to 7", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetSignatureAndCommit("7"));

            AddAssert("numerator is 7", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().TimeSignature.Numerator == 7);

            AddStep("try invalid signature 0", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetSignatureAndCommit("0"));

            AddAssert("numerator still 7", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().TimeSignature.Numerator == 7);

            AddStep("try non-numeric signature", () =>
                editor.ChildrenOfType<TimingPointSettings>().First().SetSignatureAndCommit("abc"));

            AddAssert("numerator still 7 after garbage", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().TimeSignature.Numerator == 7);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestTimeSignatureTextboxCommits"`
Expected: BUILD FAILURE — `'TimingPointSettings' does not contain a definition for 'SetSignatureAndCommit'`.

- [ ] **Step 3: Implement the numerator textbox**

In `TimingPointSettings.cs`:

3a. Replace the field

```csharp
        private BasicDropdown<int> signatureDropdown = null!;
```

with

```csharp
        private BasicTextBox signatureNumeratorBox = null!;
```

3b. In `load()`, replace the Time Signature section

```csharp
                    // --- Time Signature ---
                    new SpriteText { Text = "Time Signature (x/4)" },
                    signatureDropdown = new BasicDropdown<int>
                    {
                        RelativeSizeAxes = Axes.X,
                        Items = new[] { 1, 2, 3, 4, 5, 6, 7 },
                    },
```

with

```csharp
                    // --- Time Signature ---
                    new SpriteText { Text = "Time Signature" },
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(5, 0),
                        Children = new Drawable[]
                        {
                            signatureNumeratorBox = new BasicTextBox
                            {
                                Width = 60,
                                RelativeSizeAxes = Axes.Y,
                                PlaceholderText = "4",
                                CommitOnFocusLost = true,
                            },
                            new SpriteText
                            {
                                Text = "/ 4",
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                            },
                        },
                    },
```

3c. In `LoadComplete()`, replace

```csharp
            signatureDropdown.Current.BindValueChanged(e =>
            {
                if (!updatingFromModel)
                    commitSignature(e.NewValue);
            });
```

with

```csharp
            signatureNumeratorBox.OnCommit += (_, _) => commitSignature();
```

3d. In `updateFromModel()`, replace

```csharp
            signatureDropdown.Current.Disabled = !hasPoint;
```

with

```csharp
            signatureNumeratorBox.ReadOnly = !hasPoint;
```

and replace

```csharp
            signatureDropdown.Current.Value = tp.TimeSignature.Numerator;
```

with

```csharp
            signatureNumeratorBox.Text = tp.TimeSignature.Numerator.ToString(CultureInfo.InvariantCulture);
```

3e. Replace the `commitSignature(int numerator)` method with:

```csharp
        private void commitSignature()
        {
            if (updatingFromModel) return;

            var tp = currentTimingPoint;
            if (tp == null) return;

            if (!int.TryParse(signatureNumeratorBox.Text, out int numerator) || numerator <= 0)
            {
                // Restore the textbox to the current valid value.
                updateFromModel();
                return;
            }

            if (numerator == tp.TimeSignature.Numerator) return;

            changeHandler.BeginChange();
            tp.TimeSignature = new TimeSignature(numerator);
            editorChart.SaveState();
            changeHandler.EndChange();
        }
```

3f. Add the test seam next to `SetBpmAndCommit`:

```csharp
        /// <summary>
        /// Test seam: sets the time-signature numerator textbox text and immediately commits it.
        /// Equivalent to the user typing in the numerator box and pressing Enter.
        /// </summary>
        public void SetSignatureAndCommit(string text)
        {
            signatureNumeratorBox.Text = text;
            commitSignature();
        }
```

3g. Update the file's "Adapted for Garbus:" header line from

```
// Adapted for Garbus: rebuilt UI on Basic* widgets; no osu.Game.Overlays; no object-shifting on
// timing change; offset and BPM text boxes + nudge buttons + time-signature dropdown.
```

to

```
// Adapted for Garbus: rebuilt UI on Basic* widgets; no osu.Game.Overlays; no object-shifting on
// timing change; offset and BPM text boxes + nudge buttons + time-signature numerator textbox
// (LabelledTimeSignature equivalent).
```

- [ ] **Step 4: Run the timing suite to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab"`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs
git commit -m "Replace time-signature dropdown with numerator textbox"
```

---

### Task 4: Omit-first-barline toggle

`TimingControlPoint.OmitFirstBarLine` already exists on the point (metronome/tick display consume it); it just has no editor UI. Add a checkbox with undo support. Task 1's "no barline" chip makes the state visible in the table.

**Files:**
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs`
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: `TimingPointRow.AttributeChip` (Task 1) for the chip-visibility assert; `GarbusChartChangeHandler` via `editor.ChangeHandlerForTests` (existing).
- Produces: a `BasicCheckbox` with `LabelText == "Omit first barline"` inside `TimingPointSettings` (found by label so Task 8's second checkbox doesn't break this test).

- [ ] **Step 1: Write the failing test**

Append to `TestSceneTimingTab`:

```csharp
        // ------------------------------------------------------------------
        // 10. Omit first barline toggle
        // ------------------------------------------------------------------

        [Test]
        public void TestOmitFirstBarLineToggle()
        {
            setupEditor();
            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("checkbox loaded", () =>
                editor.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicCheckbox>()
                      .Any(c => c.LabelText.ToString() == "Omit first barline"));

            AddStep("really click the checkbox", () =>
            {
                var checkbox = editor.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicCheckbox>()
                    .First(c => c.LabelText.ToString() == "Omit first barline");
                input.MoveMouseTo(checkbox);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddAssert("point has OmitFirstBarLine set", () =>
                editor.EditorChart.ControlPointInfo.TimingPoints.First().OmitFirstBarLine);

            AddUntilStep("no-barline chip visible", () =>
                editor.ChildrenOfType<TimingPointRow.AttributeChip>()
                      .Single(c => c.Text.ToString() == "no barline").Alpha == 1);

            AddAssert("undo available", () => changeHandler.CanUndo.Value);
            AddStep("undo", () => changeHandler.Undo());

            AddUntilStep("OmitFirstBarLine cleared", () =>
                !editor.EditorChart.ControlPointInfo.TimingPoints.First().OmitFirstBarLine);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestOmitFirstBarLineToggle"`
Expected: FAIL at "checkbox loaded" — no such checkbox exists yet.

- [ ] **Step 3: Implement the toggle**

In `TimingPointSettings.cs`:

3a. Add a field next to the other controls:

```csharp
        private BasicCheckbox omitBarLineCheckbox = null!;
```

3b. In `load()`, after the Time Signature section (as the last child of the flow), add:

```csharp
                    // --- Omit first barline ---
                    omitBarLineCheckbox = new BasicCheckbox
                    {
                        LabelText = "Omit first barline",
                    },
```

3c. In `LoadComplete()`, add:

```csharp
            omitBarLineCheckbox.Current.BindValueChanged(e =>
            {
                if (!updatingFromModel)
                    commitOmitBarLine(e.NewValue);
            });
```

3d. In `updateFromModel()`, add after the `signatureNumeratorBox.ReadOnly` line (before the `if (!hasPoint) return;`):

```csharp
            omitBarLineCheckbox.Current.Disabled = false;
```

and inside the value-assignment block (after the `signatureNumeratorBox.Text = ...` line, still inside `updatingFromModel = true`):

```csharp
            omitBarLineCheckbox.Current.Value = tp.OmitFirstBarLine;
```

then, at the end of the method after `updatingFromModel = false;`, nothing more; and change the early-out block so the checkbox is disabled when there is no point — the full top of the method becomes:

```csharp
            var tp = currentTimingPoint;
            bool hasPoint = tp != null;

            offsetTextBox.ReadOnly = !hasPoint;
            bpmTextBox.ReadOnly = !hasPoint;
            signatureNumeratorBox.ReadOnly = !hasPoint;
            omitBarLineCheckbox.Current.Disabled = false;

            if (!hasPoint)
            {
                omitBarLineCheckbox.Current.Disabled = true;
                return;
            }
```

(Enable before assigning, disable only in the no-point branch — assigning a `Disabled` bindable throws.)

3e. Add the commit method:

```csharp
        private void commitOmitBarLine(bool omit)
        {
            var tp = currentTimingPoint;
            if (tp == null || tp.OmitFirstBarLine == omit) return;

            changeHandler.BeginChange();
            tp.OmitFirstBarLine = omit;
            editorChart.SaveState();
            changeHandler.EndChange();
        }
```

- [ ] **Step 4: Run the timing suite to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab"`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs
git commit -m "Add omit-first-barline toggle to timing settings"
```

---

### Task 5: Hold-to-repeat nudge steppers for offset and BPM

Ports the useful core of osu's `DiscreteAdjustmentControl`: multi-step nudge buttons (offset ±1/±10 ms, BPM ±0.1/±1) that repeat while held, with the whole hold recorded as ONE undo step (`RepeatingButtonBehaviour.RepeatBegan/RepeatEnded` wrap the hold in a change-handler transaction). `RepeatingButtonBehaviour` is already vendored and unused-in-anger — this puts it to work.

**Files:**
- Create: `Garbus.Game\Edit\Screens\Timing\RepeatNudgeButton.cs`
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs`
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: `RepeatingButtonBehaviour` (existing), `setupEditor(initialBpm, secondPointTime)` (Task 2), existing `commitOffset()`/`commitBpm()`.
- Produces: `RepeatNudgeButton : BasicButton` (ctor `(string text)`; callers set `Name` for test lookup and `Action` for the nudge). Button `Name`s in the settings panel: `offset-minus10`, `offset-minus1`, `offset-plus1`, `offset-plus10`, `bpm-minus1`, `bpm-minus01`, `bpm-plus01`, `bpm-plus1`. Task 9 reuses `RepeatNudgeButton`.

- [ ] **Step 1: Write the failing tests**

Append to `TestSceneTimingTab`:

```csharp
        // ------------------------------------------------------------------
        // 11. Offset / BPM nudge steppers
        // ------------------------------------------------------------------

        [Test]
        public void TestOffsetNudgeButtons()
        {
            setupEditor(secondPointTime: 2000);
            switchToTimingTab();

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);

            AddStep("select second point", () =>
                editor.ChildrenOfType<TimingPointRow>().ElementAt(1).TriggerClick());
            AddUntilStep("second selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);

            AddStep("really click +10", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "offset-plus10");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("group moved to 2010", () =>
                timingList().SelectedGroup.Value?.Time == 2010);

            AddStep("really click -1", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "offset-minus1");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("group moved to 2009", () =>
                timingList().SelectedGroup.Value?.Time == 2009);
        }

        [Test]
        public void TestBpmNudgeButtons()
        {
            setupEditor(); // 120 BPM
            switchToTimingTab();

            AddUntilStep("settings loaded", () =>
                editor.ChildrenOfType<TimingPointSettings>().Any());

            AddStep("really click BPM +1", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "bpm-plus1");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddAssert("BPM is 121", () =>
                Math.Abs(editor.EditorChart.ControlPointInfo.TimingPoints.First().BPM - 121) < 0.01);

            AddStep("really click BPM +0.1", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "bpm-plus01");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddAssert("BPM is 121.1", () =>
                Math.Abs(editor.EditorChart.ControlPointInfo.TimingPoints.First().BPM - 121.1) < 0.01);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestOffsetNudgeButtons|FullyQualifiedName~TestSceneTimingTab.TestBpmNudgeButtons"`
Expected: BUILD FAILURE — `RepeatNudgeButton` does not exist.

- [ ] **Step 3: Create RepeatNudgeButton**

Create `Garbus.Game\Edit\Screens\Timing\RepeatNudgeButton.cs`:

```csharp
// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/DiscreteAdjustmentControl.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: reduced to a single BasicButton with hold-to-repeat (no increment-level grid,
// no sample feedback); the hold is wrapped in one change-handler transaction so a long press is a
// single undo step.

using osu.Framework.Allocation;
using osu.Framework.Graphics.UserInterface;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// A nudge button that repeats its <see cref="BasicButton.Action"/> while held.
    /// The entire hold is recorded as one undo step.
    /// </summary>
    public partial class RepeatNudgeButton : BasicButton
    {
        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        public RepeatNudgeButton(string text)
        {
            Text = text;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // The behaviour swallows mouse-down and fires TriggerClick immediately, then repeats while
            // held — clicks reach Action exactly once per fire (the real click event stops at the
            // behaviour, which handled the mouse-down).
            AddInternal(new RepeatingButtonBehaviour(this)
            {
                RepeatBegan = () => changeHandler.BeginChange(),
                RepeatEnded = () => changeHandler.EndChange(),
            });
        }
    }
}
```

- [ ] **Step 4: Replace the settings panel's nudge buttons**

In `TimingPointSettings.cs`:

4a. Replace the Offset grid (the `GridContainer` under `new SpriteText { Text = "Offset (ms)" }`) with:

```csharp
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(GridSizeMode.Absolute, 40),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                offsetTextBox = new BasicTextBox
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    PlaceholderText = "0",
                                },
                                new RepeatNudgeButton("-10") { Name = "offset-minus10", RelativeSizeAxes = Axes.Both, Action = () => nudgeOffset(-10) },
                                new RepeatNudgeButton("-1") { Name = "offset-minus1", RelativeSizeAxes = Axes.Both, Action = () => nudgeOffset(-1) },
                                new RepeatNudgeButton("+1") { Name = "offset-plus1", RelativeSizeAxes = Axes.Both, Action = () => nudgeOffset(+1) },
                                new RepeatNudgeButton("+10") { Name = "offset-plus10", RelativeSizeAxes = Axes.Both, Action = () => nudgeOffset(+10) },
                            }
                        },
                    },
```

4b. Replace the BPM grid (under `new SpriteText { Text = "BPM" }`) with:

```csharp
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(GridSizeMode.Absolute, 40),
                            new Dimension(GridSizeMode.Absolute, 40),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                bpmTextBox = new BasicTextBox
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    PlaceholderText = "120",
                                },
                                new RepeatNudgeButton("-1") { Name = "bpm-minus1", RelativeSizeAxes = Axes.Both, Action = () => nudgeBpm(-1) },
                                new RepeatNudgeButton("-.1") { Name = "bpm-minus01", RelativeSizeAxes = Axes.Both, Action = () => nudgeBpm(-0.1) },
                                new RepeatNudgeButton("+.1") { Name = "bpm-plus01", RelativeSizeAxes = Axes.Both, Action = () => nudgeBpm(+0.1) },
                                new RepeatNudgeButton("+1") { Name = "bpm-plus1", RelativeSizeAxes = Axes.Both, Action = () => nudgeBpm(+1) },
                            }
                        },
                    },
```

4c. Replace `nudgeOffset(int direction)` and `nudgeBpm(int direction)` with double-taking versions:

```csharp
        private void nudgeOffset(double amount)
        {
            if (SelectedGroup.Value == null) return;

            offsetTextBox.Text = (SelectedGroup.Value.Time + amount).ToString("0.##", CultureInfo.InvariantCulture);
            commitOffset();
        }

        private void nudgeBpm(double amount)
        {
            var tp = currentTimingPoint;
            if (tp == null) return;

            double currentBpm = 60000.0 / tp.BeatLength;
            bpmTextBox.Text = (currentBpm + amount).ToString("0.##", CultureInfo.InvariantCulture);
            commitBpm();
        }
```

4d. Delete the now-unused `private partial class NudgeButton` at the bottom of the file.

- [ ] **Step 5: Run the timing suite to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab"`
Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\RepeatNudgeButton.cs Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs
git commit -m "Add hold-to-repeat nudge steppers for offset and BPM"
```

---

### Task 6: "Use current time" group move (GroupSection equivalent)

osu's `GroupSection` row: a button that moves the selected group to the playhead. The move-group transaction (remove group + re-add points at the new time, one undo step) already exists in `commitOffset` — the button just routes through it.

**Files:**
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs`
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: `commitOffset()` (existing), `editorClock.CurrentTime`, `setupEditor(initialBpm, secondPointTime)` (Task 2).
- Produces: a `BasicButton` with `Text == "Use current time"` inside `TimingPointSettings`.

- [ ] **Step 1: Write the failing test**

Append to `TestSceneTimingTab`:

```csharp
        // ------------------------------------------------------------------
        // 12. "Use current time" moves the selected group to the playhead
        // ------------------------------------------------------------------

        [Test]
        public void TestUseCurrentTimeMovesGroup()
        {
            setupEditor(secondPointTime: 2000);
            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);

            AddStep("select second point", () =>
                editor.ChildrenOfType<TimingPointRow>().ElementAt(1).TriggerClick());
            AddUntilStep("second selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);

            AddStep("seek to 3000", () =>
                editor.ChildrenOfType<EditorClock>().First().Seek(3000));
            AddUntilStep("clock at 3000", () =>
                Math.Abs(editor.ChildrenOfType<EditorClock>().First().CurrentTime - 3000) < 50);

            AddStep("really click Use current time", () =>
            {
                var button = editor.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>()
                    .First(b => b.Text.ToString() == "Use current time");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });

            AddUntilStep("group moved to 3000", () =>
                editor.EditorChart.ControlPointInfo.GroupAt(3000) != null &&
                editor.EditorChart.ControlPointInfo.TimingPoints.Count == 2);

            AddStep("undo", () => changeHandler.Undo());
            AddUntilStep("group back at 2000", () =>
                editor.EditorChart.ControlPointInfo.GroupAt(2000) != null &&
                editor.EditorChart.ControlPointInfo.GroupAt(3000) == null);
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestUseCurrentTimeMovesGroup"`
Expected: FAIL at "really click Use current time" — no button with that text exists (`InvalidOperationException: Sequence contains no matching element`).

- [ ] **Step 3: Implement the button**

In `TimingPointSettings.cs`:

3a. Add a field:

```csharp
        private BasicButton useCurrentTimeButton = null!;
```

3b. In `load()`, immediately after the Offset grid (before the `// --- BPM ---` comment), add:

```csharp
                    useCurrentTimeButton = new BasicButton
                    {
                        Text = "Use current time",
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        Action = useCurrentTime,
                    },
```

3c. Add the handler:

```csharp
        private void useCurrentTime()
        {
            if (SelectedGroup.Value == null) return;

            offsetTextBox.Text = editorClock.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
            commitOffset();
        }
```

3d. In `updateFromModel()`, add with the other enabled-state lines (before the no-point early-out):

```csharp
            useCurrentTimeButton.Enabled.Value = hasPoint;
```

- [ ] **Step 4: Run the timing suite to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab"`
Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs
git commit -m "Add Use-current-time button to move selected timing group"
```

---

### Task 7: Section-wide object adjustments — model layer

Ports osu's `TimingSectionAdjustments` (objects between a timing point and the next shift with offset changes and rescale with BPM changes) plus the `EditorChart.PerformOnRange` helper from the source plan. Design decision resolved per that plan's recommendation: **port it** — this is what charters expect when correcting a mis-timed section. Pure model logic; UI wiring is Task 8.

Garbus-specific deviation from osu's math: `SliderBody.Duration` is derived from its path (the setter is a no-op), so BPM-stretching a slider must scale `Path.ControlPoints[*].TimeOffset` instead of writing `Duration`. `HoldNote.Duration` is a plain settable property and scales directly.

**Files:**
- Create: `Garbus.Game\Edit\Screens\Timing\TimingSectionAdjustments.cs`
- Modify: `Garbus.Game\Edit\EditorChart.cs` (add `PerformOnRange`)
- Create: `Garbus.Game.Tests\Editor\TestTimingSectionAdjustments.cs`

**Interfaces:**
- Consumes: `EditorChart.Update(GarbusHitObject)` / `BeginChange` / `EndChange` (existing), `ControlPointInfo.TimingPoints`, `HoldNote { StartTime, Duration, AngleDeg }`, `SliderBody { StartTime, AngleDeg, Side, Path }`, `GarbusPath { ControlPoints }` (`required` init), `GarbusPathControlPoint { TimeOffset, RotationOffset }`, `CardinalNote { StartTime, AngleDeg }`, `osu.Framework.Utils.Precision`.
- Produces:
  - `EditorChart.PerformOnRange(double start, double end, Action<GarbusHitObject> action)` — acts on objects with `StartTime` in `[start, end)`, one transaction, `Update` per object.
  - `static class TimingSectionAdjustments` (namespace `Garbus.Game.Edit.Screens.Timing`) with `TimingRange(EditorChart, TimingControlPoint)`, `AdjustHitObjectOffset(EditorChart, TimingControlPoint, double adjustment)`, `SetHitObjectBPM(EditorChart, TimingControlPoint, double oldBeatLength)`. **Caller contract:** `AdjustHitObjectOffset` must be called while the point is still at its OLD time (before the group is moved); `SetHitObjectBPM` must be called AFTER writing the new `BeatLength`, passing the old one.

- [ ] **Step 1: Write the failing tests**

Create `Garbus.Game.Tests\Editor\TestTimingSectionAdjustments.cs`:

```csharp
// TDD tests for TimingSectionAdjustments + EditorChart.PerformOnRange.
//
// Contract under test (osu's timing-section semantics):
//   - A timing point's section spans [its time, next timing point's time). The FIRST timing point's
//     section extends back to the start of time (objects before it belong to it).
//   - AdjustHitObjectOffset shifts StartTime of in-section objects by the adjustment; others untouched.
//   - SetHitObjectBPM keeps objects on the same beat: StartTime rescales around the point's time by
//     newBeatLength/oldBeatLength; HoldNote.Duration scales; SliderBody scales its path TimeOffsets
//     (its Duration is derived from the path — the setter is a no-op).

using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens.Timing;
using Garbus.Game.Objects;
using NUnit.Framework;
using osu.Framework.Bindables;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestTimingSectionAdjustments
    {
        private EditorChart editorChart = null!;
        private TimingControlPoint firstPoint = null!;
        private TimingControlPoint secondPoint = null!;

        [SetUp]
        public void SetUp()
        {
            var chart = new GarbusChart();
            firstPoint = new TimingControlPoint { BeatLength = 500 };  // 120 BPM
            secondPoint = new TimingControlPoint { BeatLength = 400 }; // 150 BPM
            chart.ControlPointInfo.Add(0, firstPoint);
            chart.ControlPointInfo.Add(4000, secondPoint);
            editorChart = new EditorChart(chart);
        }

        [Test]
        public void TestAdjustOffsetMovesOnlyObjectsInSection()
        {
            var early = addNote(1000);
            var late = addNote(5000);

            TimingSectionAdjustments.AdjustHitObjectOffset(editorChart, secondPoint, 100);

            Assert.That(early.StartTime, Is.EqualTo(1000).Within(0.001));
            Assert.That(late.StartTime, Is.EqualTo(5100).Within(0.001));
        }

        [Test]
        public void TestFirstSectionExtendsToStartOfTime()
        {
            var beforePoint = addNote(-500);
            var inSection = addNote(1000);
            var nextSection = addNote(5000);

            TimingSectionAdjustments.AdjustHitObjectOffset(editorChart, firstPoint, 50);

            Assert.That(beforePoint.StartTime, Is.EqualTo(-450).Within(0.001));
            Assert.That(inSection.StartTime, Is.EqualTo(1050).Within(0.001));
            Assert.That(nextSection.StartTime, Is.EqualTo(5000).Within(0.001));
        }

        [Test]
        public void TestSetHitObjectBPMRescalesPositions()
        {
            var note = addNote(5000); // 2.5 beats after the 4000ms point at BeatLength 400
            var earlier = addNote(1000);

            double oldBeatLength = secondPoint.BeatLength;
            secondPoint.BeatLength = 200; // 300 BPM
            TimingSectionAdjustments.SetHitObjectBPM(editorChart, secondPoint, oldBeatLength);

            Assert.That(note.StartTime, Is.EqualTo(4500).Within(0.001)); // 4000 + 2.5 * 200
            Assert.That(earlier.StartTime, Is.EqualTo(1000).Within(0.001));
        }

        [Test]
        public void TestSetHitObjectBPMScalesHoldDuration()
        {
            var hold = new HoldNote { StartTime = 4400, Duration = 800, AngleDeg = 0 };
            editorChart.Add(hold);

            double oldBeatLength = secondPoint.BeatLength;
            secondPoint.BeatLength = 200;
            TimingSectionAdjustments.SetHitObjectBPM(editorChart, secondPoint, oldBeatLength);

            Assert.That(hold.StartTime, Is.EqualTo(4200).Within(0.001));
            Assert.That(hold.Duration, Is.EqualTo(400).Within(0.001));
        }

        [Test]
        public void TestSetHitObjectBPMScalesSliderPath()
        {
            var slider = new SliderBody
            {
                StartTime = 4000,
                AngleDeg = 0,
                Side = HorizontalDirection.Right,
                Path = new GarbusPath
                {
                    ControlPoints = new BindableList<GarbusPathControlPoint>
                    {
                        new GarbusPathControlPoint { TimeOffset = 400, RotationOffset = 90 },
                        new GarbusPathControlPoint { TimeOffset = 800, RotationOffset = 180 },
                    },
                },
            };
            editorChart.Add(slider);

            double oldBeatLength = secondPoint.BeatLength;
            secondPoint.BeatLength = 200;
            TimingSectionAdjustments.SetHitObjectBPM(editorChart, secondPoint, oldBeatLength);

            Assert.That(slider.Path.ControlPoints[0].TimeOffset, Is.EqualTo(200).Within(0.001));
            Assert.That(slider.Path.ControlPoints[1].TimeOffset, Is.EqualTo(400).Within(0.001));
            Assert.That(slider.Duration, Is.EqualTo(400).Within(0.001)); // derived from the path
        }

        private CardinalNote addNote(double time)
        {
            var note = new CardinalNote { StartTime = time, AngleDeg = 0 };
            editorChart.Add(note);
            return note;
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestTimingSectionAdjustments"`
Expected: BUILD FAILURE — `TimingSectionAdjustments` does not exist.

- [ ] **Step 3: Add EditorChart.PerformOnRange**

In `Garbus.Game\Edit\EditorChart.cs`, add `using osu.Framework.Utils;` to the usings, then add after `PerformOnSelection`:

```csharp
        /// <summary>
        /// Performs an action on every hit object whose start time falls in [start, end) as a single
        /// transaction, updating each. Boundary semantics match osu's timing sections: inclusive
        /// start, exclusive end, with floating-point tolerance.
        /// </summary>
        public void PerformOnRange(double start, double end, Action<GarbusHitObject> action)
        {
            var affected = hitObjects.Where(h => Precision.AlmostBigger(h.StartTime, start)
                                                 && Precision.DefinitelyBigger(end, h.StartTime)).ToArray();
            if (affected.Length == 0)
                return;

            BeginChange();

            foreach (var h in affected)
            {
                action(h);
                Update(h);
            }

            EndChange();
        }
```

- [ ] **Step 4: Create TimingSectionAdjustments**

Create `Garbus.Game\Edit\Screens\Timing\TimingSectionAdjustments.cs`:

```csharp
// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/TimingSectionAdjustments.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: operates on EditorChart (mutations routed through PerformOnRange so they land
// in one undo transaction with per-object Update); SliderBody duration is derived from its path, so
// BPM stretching scales the path control points' TimeOffsets instead of writing Duration; no
// IHasRepeats (Garbus has none).

using System.Linq;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Gameplay.Objects.Types;
using Garbus.Game.Objects;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// Bulk adjustments of the hit objects in a timing section (the span from a timing point to the
    /// next one). Used to keep objects musically in place when a section's offset or BPM changes.
    /// </summary>
    public static class TimingSectionAdjustments
    {
        /// <summary>
        /// The time range governed by <paramref name="timingControlPoint"/>: from its time (or the
        /// start of time, if it is the first timing point) up to the next timing point (or the end
        /// of time). Call while the point is still registered at the time being asked about.
        /// </summary>
        public static (double start, double end) TimingRange(EditorChart chart, TimingControlPoint timingControlPoint)
        {
            double start = chart.ControlPointInfo.TimingPoints.Any(x => x.Time < timingControlPoint.Time)
                ? timingControlPoint.Time
                : double.MinValue;

            double end = chart.ControlPointInfo.TimingPoints.FirstOrDefault(x => x.Time > timingControlPoint.Time)?.Time
                         ?? double.MaxValue;

            return (start, end);
        }

        /// <summary>
        /// Shifts all objects in the timing section by <paramref name="adjustment"/> milliseconds.
        /// Must be called BEFORE the group is moved (the range is computed from the point's old time).
        /// </summary>
        public static void AdjustHitObjectOffset(EditorChart chart, TimingControlPoint timingControlPoint, double adjustment)
        {
            var (start, end) = TimingRange(chart, timingControlPoint);
            chart.PerformOnRange(start, end, hitObject => hitObject.StartTime += adjustment);
        }

        /// <summary>
        /// Keeps all objects in the timing section on the same beat after a BPM change.
        /// Must be called AFTER the new <see cref="TimingControlPoint.BeatLength"/> has been set,
        /// passing the previous value as <paramref name="oldBeatLength"/>.
        /// </summary>
        public static void SetHitObjectBPM(EditorChart chart, TimingControlPoint timingControlPoint, double oldBeatLength)
        {
            var (start, end) = TimingRange(chart, timingControlPoint);

            chart.PerformOnRange(start, end, hitObject =>
            {
                double beat = (hitObject.StartTime - timingControlPoint.Time) / oldBeatLength;
                hitObject.StartTime = beat * timingControlPoint.BeatLength + timingControlPoint.Time;

                double durationScale = timingControlPoint.BeatLength / oldBeatLength;

                switch (hitObject)
                {
                    case SliderBody slider:
                        // Duration is derived from the furthest path node; stretch the path itself.
                        foreach (var node in slider.Path.ControlPoints)
                            node.TimeOffset *= durationScale;
                        break;

                    case IHasDuration withDuration:
                        withDuration.Duration *= durationScale;
                        break;
                }
            });
        }
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestTimingSectionAdjustments"`
Expected: PASS (5 tests).

- [ ] **Step 6: Run the full suite (EditorChart changed — check nothing else broke)**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all tests).

- [ ] **Step 7: Commit**

```powershell
git add Garbus.Game\Edit\EditorChart.cs Garbus.Game\Edit\Screens\Timing\TimingSectionAdjustments.cs Garbus.Game.Tests\Editor\TestTimingSectionAdjustments.cs
git commit -m "Add TimingSectionAdjustments and EditorChart.PerformOnRange"
```

---

### Task 8: Wire object adjustments into the settings panel

Adds a "Move objects with timing changes" checkbox (default ON, session-scoped) and routes offset moves and BPM commits through a new shared `TimingPointChanges` helper that applies `TimingSectionAdjustments` inside the same undo transaction. This changes the Timing tab's contract — timing edits CAN now move objects — so the stale "does NOT apply" attribution lines and the test-file header get corrected here too.

**Files:**
- Create: `Garbus.Game\Edit\Screens\Timing\TimingPointChanges.cs`
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs`
- Modify: `Garbus.Game\Edit\Screens\Timing\TimingPointList.cs` (header line only)
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`

**Interfaces:**
- Consumes: `TimingSectionAdjustments` + caller contract (Task 7), `commitOffset`/`commitBpm` (existing).
- Produces:
  - `static class TimingPointChanges` with `ControlPointGroup MoveGroup(EditorChart chart, IEditorChangeHandler changeHandler, ControlPointGroup group, double newTime, bool adjustObjects)` and `void ChangeBpm(EditorChart chart, IEditorChangeHandler changeHandler, TimingControlPoint tp, double newBpm, bool adjustObjects)` — Task 9 reuses both.
  - `TimingPointSettings.AdjustObjectsOnTimingChange` (`public readonly BindableBool`, default `true`) — Task 9 binds to it.
  - `TimingPointSettings.SetOffsetAndCommit(double)` test seam.

- [ ] **Step 1: Write the failing tests**

In `TestSceneTimingTab.cs`, add `using Garbus.Game.Objects;` to the usings, replace the file-header comment lines

```csharp
// TapButton.RecordTap(double timestamp) is the injectable test hook — no UI interaction required.
// All timing edits are verified not to move hit objects (none exist in these tests, so the
// ControlPointInfo mutation is the only observable effect).
```

with

```csharp
// TapButton.RecordTap(double timestamp) is the injectable test hook — no UI interaction required.
// Timing edits move the objects in the affected timing section when "Move objects with timing
// changes" is enabled (the default) — see the section-adjustment tests below.
```

then append:

```csharp
        // ------------------------------------------------------------------
        // 13. Timing edits move objects in the affected section
        // ------------------------------------------------------------------

        private void setupEditorWithObjects() => Schedule(() =>
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            chart.ControlPointInfo.Add(4000, new TimingControlPoint { BeatLength = 400 });
            chart.HitObjects.Add(new CardinalNote { StartTime = 1000, AngleDeg = 0 });
            chart.HitObjects.Add(new CardinalNote { StartTime = 5000, AngleDeg = 90 });

            var chartFile = new ChartFile(chart);
            editor = new GarbusEditor(chartFile);
            Child = input = new osu.Framework.Testing.Input.ManualInputManager
            {
                RelativeSizeAxes = Axes.Both,
                Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both },
            };
        });

        private TimingPointSettings settings() => editor.ChildrenOfType<TimingPointSettings>().First();

        [Test]
        public void TestOffsetMoveShiftsObjectsInSection()
        {
            setupEditorWithObjects();
            switchToTimingTab();

            GarbusChartChangeHandler changeHandler = null!;
            AddUntilStep("get change handler", () =>
            {
                if (!editor.IsLoaded) return false;
                changeHandler = editor.ChangeHandlerForTests;
                return true;
            });

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);
            AddStep("select second point", () =>
                editor.ChildrenOfType<TimingPointRow>().ElementAt(1).TriggerClick());
            AddUntilStep("second selected", () =>
                timingList().SelectedGroup.Value?.Time == 4000);

            AddStep("move group to 4100 via seam", () => settings().SetOffsetAndCommit(4100));

            AddAssert("note in section shifted to 5100", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 5100) < 0.01));
            AddAssert("note in earlier section unmoved", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 1000) < 0.01));

            AddStep("undo", () => changeHandler.Undo());
            AddUntilStep("single undo restores point and note", () =>
                editor.EditorChart.ControlPointInfo.GroupAt(4000) != null &&
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 5000) < 0.01));
        }

        [Test]
        public void TestBpmChangeRescalesObjectsInSection()
        {
            setupEditorWithObjects();
            switchToTimingTab();

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);
            AddUntilStep("first point auto-selected", () =>
                timingList().SelectedGroup.Value?.Time == 0);

            // First point: BeatLength 500 → 250 (BPM 120 → 240). The note at 1000 sits on beat 2,
            // so it must land on 2 * 250 = 500. The note at 5000 is in the next section: unmoved.
            AddStep("set BPM to 240 via seam", () => settings().SetBpmAndCommit(240));

            AddAssert("note rescaled to 500", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 500) < 0.01));
            AddAssert("note in next section unmoved", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 5000) < 0.01));
        }

        [Test]
        public void TestAdjustmentToggleOffLeavesObjectsAlone()
        {
            setupEditorWithObjects();
            switchToTimingTab();

            AddUntilStep("first point auto-selected", () =>
                timingList().SelectedGroup.Value?.Time == 0);

            AddStep("disable move-objects toggle", () =>
                settings().AdjustObjectsOnTimingChange.Value = false);

            AddStep("set BPM to 240 via seam", () => settings().SetBpmAndCommit(240));

            AddAssert("BeatLength changed", () =>
                Math.Abs(editor.EditorChart.ControlPointInfo.TimingPoints.First().BeatLength - 250) < 0.01);
            AddAssert("objects unmoved", () =>
                editor.EditorChart.HitObjects.Any(h => Math.Abs(h.StartTime - 1000) < 0.01));
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestOffsetMoveShiftsObjectsInSection|FullyQualifiedName~TestSceneTimingTab.TestBpmChangeRescalesObjectsInSection|FullyQualifiedName~TestSceneTimingTab.TestAdjustmentToggleOffLeavesObjectsAlone"`
Expected: BUILD FAILURE — `SetOffsetAndCommit` / `AdjustObjectsOnTimingChange` do not exist.

- [ ] **Step 3: Create TimingPointChanges**

Create `Garbus.Game\Edit\Screens\Timing\TimingPointChanges.cs`:

```csharp
// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/Timing/GroupSection.cs +
// TapTimingControl.cs (the group-move and BPM-set transactions).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: extracted into a shared static helper (osu inlines this in each caller); the
// adjust-objects flag is a parameter instead of an OsuConfigManager setting.

using System.Linq;
using Garbus.Game.Charts.Timing;
using osu.Framework.Utils;

namespace Garbus.Game.Edit.Screens.Timing
{
    /// <summary>
    /// The shared undo transactions behind every timing point mutation the Timing tab offers.
    /// </summary>
    public static class TimingPointChanges
    {
        /// <summary>
        /// Moves a control point group to a new time inside one undo transaction, preserving its
        /// points. Optionally shifts the objects of the affected timing section by the same amount
        /// (computed from the point's OLD time, before the move). Returns the group now at the new time.
        /// </summary>
        public static ControlPointGroup MoveGroup(EditorChart chart, IEditorChangeHandler changeHandler,
                                                  ControlPointGroup group, double newTime, bool adjustObjects)
        {
            var currentItems = group.ControlPoints.ToArray();

            changeHandler.BeginChange();

            var tp = currentItems.OfType<TimingControlPoint>().FirstOrDefault();
            if (tp != null && adjustObjects)
                TimingSectionAdjustments.AdjustHitObjectOffset(chart, tp, newTime - group.Time);

            chart.ControlPointInfo.RemoveGroup(group);

            foreach (var cp in currentItems)
                chart.ControlPointInfo.Add(newTime, cp);

            chart.SaveState();
            changeHandler.EndChange();

            return chart.ControlPointInfo.GroupAt(newTime);
        }

        /// <summary>
        /// Sets a timing point's BPM inside one undo transaction, optionally keeping the affected
        /// section's objects on the same beats.
        /// </summary>
        public static void ChangeBpm(EditorChart chart, IEditorChangeHandler changeHandler,
                                     TimingControlPoint tp, double newBpm, bool adjustObjects)
        {
            double oldBeatLength = tp.BeatLength;
            double newBeatLength = 60000.0 / newBpm;

            if (Precision.AlmostEquals(oldBeatLength, newBeatLength))
                return;

            changeHandler.BeginChange();
            tp.BeatLength = newBeatLength;

            if (adjustObjects)
                TimingSectionAdjustments.SetHitObjectBPM(chart, tp, oldBeatLength);

            chart.SaveState();
            changeHandler.EndChange();
        }
    }
}
```

- [ ] **Step 4: Wire into TimingPointSettings**

In `TimingPointSettings.cs`:

4a. Add the public toggle and a checkbox field:

```csharp
        /// <summary>
        /// Whether timing edits shift/rescale the objects in the affected timing section
        /// (osu's "adjust existing objects on timing changes"). Session-scoped, default on.
        /// </summary>
        public readonly BindableBool AdjustObjectsOnTimingChange = new BindableBool(true);

        private BasicCheckbox moveObjectsCheckbox = null!;
```

4b. In `load()`, add as the FIRST child of the flow (above the Offset section):

```csharp
                    moveObjectsCheckbox = new BasicCheckbox
                    {
                        LabelText = "Move objects with timing changes",
                        Current = { BindTarget = AdjustObjectsOnTimingChange },
                    },
```

4c. Replace the body of `commitOffset()` with:

```csharp
        private void commitOffset()
        {
            if (updatingFromModel || SelectedGroup.Value == null) return;

            if (!double.TryParse(offsetTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newOffset))
            {
                updateFromModel();
                return;
            }

            double oldTime = SelectedGroup.Value.Time;
            if (Math.Abs(newOffset - oldTime) < 0.01) return;

            SelectedGroup.Value = TimingPointChanges.MoveGroup(
                editorChart, changeHandler, SelectedGroup.Value, newOffset, AdjustObjectsOnTimingChange.Value);

            if (!editorClock.IsRunning)
                editorClock.Seek(newOffset);
        }
```

4d. Replace the body of `commitBpm()` with:

```csharp
        private void commitBpm()
        {
            if (updatingFromModel) return;

            var tp = currentTimingPoint;
            if (tp == null) return;

            if (!double.TryParse(bpmTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double bpm)
                || bpm <= 0)
            {
                updateFromModel();
                return;
            }

            TimingPointChanges.ChangeBpm(editorChart, changeHandler, tp, bpm, AdjustObjectsOnTimingChange.Value);
        }
```

4e. Add the offset test seam next to `SetBpmAndCommit`:

```csharp
        /// <summary>
        /// Test seam: sets the offset textbox text and immediately commits it.
        /// Equivalent to the user typing in the offset box and pressing Enter.
        /// </summary>
        public void SetOffsetAndCommit(double offset)
        {
            offsetTextBox.Text = offset.ToString("0.##", CultureInfo.InvariantCulture);
            commitOffset();
        }
```

4f. Update the file's "Adapted for Garbus:" header line to:

```
// Adapted for Garbus: rebuilt UI on Basic* widgets; no osu.Game.Overlays; object-shifting on timing
// changes via TimingSectionAdjustments behind a session toggle; offset and BPM text boxes + repeat
// nudge buttons + time-signature numerator textbox + omit-first-barline + use-current-time.
```

4g. In `TimingPointList.cs`, update the header's trailing clause `object-shifting on timing change does NOT apply.` to `object-shifting on timing changes lives in TimingSectionAdjustments (applied by the settings panel, not the list).`

- [ ] **Step 5: Run the full suite to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS — including the pre-existing `TestBpmTextboxSetsBeatLength` (same commit path, now via `TimingPointChanges`) and `TestUseCurrentTimeMovesGroup` (routes through the new `commitOffset`).

- [ ] **Step 6: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\TimingPointChanges.cs Garbus.Game\Edit\Screens\Timing\TimingPointSettings.cs Garbus.Game\Edit\Screens\Timing\TimingPointList.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs
git commit -m "Move objects with timing changes (toggleable, single undo step)"
```

---

### Task 9: Adjust buttons under the tap-timing metronome

osu's `TapTimingControl` has offset (±1/±10 ms) and BPM (±0.1/±1) adjust rows under the metronome with repeat-on-hold. Reuses `RepeatNudgeButton` (Task 5) and `TimingPointChanges` (Task 8).

**Files:**
- Modify: `Garbus.Game\Edit\Screens\Timing\TapTimingControl.cs`
- Modify: `Garbus.Game\Edit\Screens\TimingTab.cs` (bind the adjust-objects toggle)
- Test: `Garbus.Game.Tests\Editor\TestSceneTimingTab.cs`
- Modify: `PLAN-port.md` (progress note — final step)

**Interfaces:**
- Consumes: `RepeatNudgeButton` (Task 5), `TimingPointChanges.MoveGroup/ChangeBpm` (Task 8), `TimingPointSettings.AdjustObjectsOnTimingChange` (Task 8).
- Produces: `TapTimingControl.AdjustObjectsOnTimingChange` (`public readonly BindableBool`), buttons named `tap-offset-minus10`, `tap-offset-minus1`, `tap-offset-plus1`, `tap-offset-plus10`, `tap-bpm-minus1`, `tap-bpm-minus01`, `tap-bpm-plus01`, `tap-bpm-plus1`.

- [ ] **Step 1: Write the failing test**

Append to `TestSceneTimingTab`:

```csharp
        // ------------------------------------------------------------------
        // 14. Tap-timing adjust buttons
        // ------------------------------------------------------------------

        [Test]
        public void TestTapTimingAdjustButtons()
        {
            setupEditor(secondPointTime: 2000);
            switchToTimingTab();

            AddUntilStep("rows loaded", () =>
                editor.ChildrenOfType<TimingPointRow>().Count() == 2);
            AddStep("select second point", () =>
                editor.ChildrenOfType<TimingPointRow>().ElementAt(1).TriggerClick());
            AddUntilStep("second selected", () =>
                timingList().SelectedGroup.Value?.Time == 2000);

            AddStep("really click tap offset -10", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "tap-offset-minus10");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("group moved to 1990", () =>
                timingList().SelectedGroup.Value?.Time == 1990);

            AddStep("really click tap BPM +1", () =>
            {
                var button = editor.ChildrenOfType<RepeatNudgeButton>().First(b => b.Name == "tap-bpm-plus1");
                input.MoveMouseTo(button);
                input.Click(osuTK.Input.MouseButton.Left);
            });
            AddAssert("selected point BPM is 121", () =>
            {
                var tp = timingList().SelectedGroup.Value?.ControlPoints
                    .OfType<TimingControlPoint>().First();
                return tp != null && Math.Abs(tp.BPM - 121) < 0.01;
            });
        }
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter "FullyQualifiedName~TestSceneTimingTab.TestTapTimingAdjustButtons"`
Expected: FAIL at "really click tap offset -10" — no button with that name exists.

- [ ] **Step 3: Implement the adjust rows**

In `TapTimingControl.cs`:

3a. Add usings:

```csharp
using System.Linq;
using osu.Framework.Graphics.UserInterface;
```

3b. Add resolved dependencies and the toggle next to the existing `editorClock` field:

```csharp
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        /// <summary>
        /// Bound to TimingPointSettings.AdjustObjectsOnTimingChange by TimingTab so both panels
        /// honour the same "Move objects with timing changes" toggle.
        /// </summary>
        public readonly BindableBool AdjustObjectsOnTimingChange = new BindableBool(true);
```

3c. In `load()`, after the playback-controls `GridContainer` and before `tapButton`, add:

```csharp
                    // Fine adjustment of the selected point, osu's TapTimingControl extras:
                    // offset ±1/±10 ms and BPM ±0.1/±1, repeat-on-hold.
                    new SpriteText { Text = "Offset (ms)" },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new RepeatNudgeButton("-10") { Name = "tap-offset-minus10", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(-10) },
                                new RepeatNudgeButton("-1") { Name = "tap-offset-minus1", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(-1) },
                                new RepeatNudgeButton("+1") { Name = "tap-offset-plus1", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(+1) },
                                new RepeatNudgeButton("+10") { Name = "tap-offset-plus10", RelativeSizeAxes = Axes.Both, Action = () => adjustOffset(+10) },
                            }
                        },
                    },
                    new SpriteText { Text = "BPM" },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        ColumnDimensions = new[]
                        {
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                            new Dimension(),
                        },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new RepeatNudgeButton("-1") { Name = "tap-bpm-minus1", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(-1) },
                                new RepeatNudgeButton("-.1") { Name = "tap-bpm-minus01", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(-0.1) },
                                new RepeatNudgeButton("+.1") { Name = "tap-bpm-plus01", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(+0.1) },
                                new RepeatNudgeButton("+1") { Name = "tap-bpm-plus1", RelativeSizeAxes = Axes.Both, Action = () => adjustBpm(+1) },
                            }
                        },
                    },
```

3d. Add the handlers to `TapTimingControl`:

```csharp
        private void adjustOffset(double amount)
        {
            if (SelectedGroup.Value == null) return;

            double newTime = SelectedGroup.Value.Time + amount;

            SelectedGroup.Value = TimingPointChanges.MoveGroup(
                editorChart, changeHandler, SelectedGroup.Value, newTime, AdjustObjectsOnTimingChange.Value);

            if (!editorClock.IsRunning)
                editorClock.Seek(newTime);
        }

        private void adjustBpm(double amount)
        {
            var tp = SelectedGroup.Value?.ControlPoints.OfType<TimingControlPoint>().FirstOrDefault();
            if (tp == null) return;

            TimingPointChanges.ChangeBpm(editorChart, changeHandler, tp, tp.BPM + amount, AdjustObjectsOnTimingChange.Value);
        }
```

3e. Update the file's "Adapted for Garbus:" header line to append `; offset/BPM adjust rows under the metronome (repeat-on-hold)` before the final period.

3f. In `TimingTab.cs` `LoadComplete()`, after the existing SelectedGroup wiring, add:

```csharp
            // Both panels honour the same "Move objects with timing changes" toggle.
            tapTimingControl.AdjustObjectsOnTimingChange.BindTo(timingPointSettings.AdjustObjectsOnTimingChange);
```

- [ ] **Step 4: Run the full suite to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: PASS (all tests).

- [ ] **Step 5: Update PLAN-port.md**

Add a line to the relevant editor/Phase section of `PLAN-port.md` recording that the Timing tab now has the full osu timing-screen feature set: chip table + keyboard selection, time signature / omit barline editing, repeat nudge steppers, use-current-time group move, section-wide object adjustment (toggleable), and tap-timing adjust rows. Follow the file's existing checklist style; no historical narration.

- [ ] **Step 6: Commit**

```powershell
git add Garbus.Game\Edit\Screens\Timing\TapTimingControl.cs Garbus.Game\Edit\Screens\TimingTab.cs Garbus.Game.Tests\Editor\TestSceneTimingTab.cs PLAN-port.md
git commit -m "Add offset/BPM adjust rows under the tap-timing metronome"
```
