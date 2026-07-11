# Editor Top-Right (Zoom Stack + BeatDivisorControl) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port osu's editor top-right region into the Garbus Compose view — a vertical zoom-button stack plus a full beat-divisor control (graphical tick readout, `1/N` selector with custom-value popover, and a Common/Triplets/Custom type selector).

**Architecture:** A new `GarbusBeatDivisorControl` (osu-framework primitives + hardcoded colours, no osu.Game types) is a vertical 3-row `GridContainer`: a display-only tick readout, a `[◄ | 1/N | ►]` divisor row (the `1/N` opens a custom-divisor popover), and a `[◄ | type | ►]` row. `ComposeTab` is restructured so the timeline strip shares a horizontal grid with a 35px vertical zoom column and a 120px divisor-control column, all wrapped in a `PopoverContainer`. The divisor→colour/height palette currently private in `TimelineTickDisplay` is extracted to a shared static so both consumers share one source.

**Tech Stack:** C# / .NET 8, osu-framework (no osu.Game dependency), NUnit visual test scenes.

## Global Constraints

- Target framework `net8.0`; nullability enabled solution-wide — DI-resolved / BDL-initialised fields use `= null!`.
- **No osu.Game UI types.** Use osu-framework primitives (`BasicButton`, `SpriteText`, `Box`, `Circle`, `EquilateralTriangle`, `BasicPopover`, `BasicTextBox`, `PopoverContainer`, `GridContainer`) with hardcoded `Color4` values. Do **not** introduce `OsuColour`, `OverlayColourProvider`, `OsuAnimatedButton`, osu.Game's `IconButton`, `OsuPopover`, `OsuNumberBox`, or Humanizer.
- Vendored-from-osu files keep the ppy MIT attribution header plus an `Adapted for Garbus:` line summarising the trims. Bespoke files get an ordinary header.
- Terminology: "chart" not "beatmap"; `Garbus*` prefixes, not `Bac*`. Do not increment version numbers anywhere.
- `BindableBeatDivisor` is already DI-cached in `GarbusEditor` and in test harnesses; resolve it with `[Resolved]`.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`. `Garbus.Game.Tests` already has `InternalsVisibleTo`, so `internal` members are visible to tests.

---

### Task 1: Shared beat-divisor colour/height palette

Extract the divisor→colour and divisor→height helpers currently private in `TimelineTickDisplay` into a shared static, and point `TimelineTickDisplay` at it. This is the single source the new tick readout will also use.

**Files:**
- Create: `Garbus.Game/Edit/BeatDivisorColours.cs`
- Modify: `Garbus.Game/Edit/Screens/Timeline/TimelineTickDisplay.cs` (replace the private `getColourForDivisor`/`getHeightForDivisor` with calls to the shared helper)
- Test: `Garbus.Game.Tests/Editor/TestBeatDivisorColours.cs`

**Interfaces:**
- Produces: `static Color4 BeatDivisorColours.ColourFor(int divisor)` and `static float BeatDivisorColours.HeightFor(int divisor)`. `ColourFor(0)` and `ColourFor(1)` both return white (bar/whole-beat); `HeightFor(1)` returns `1.0f`.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestBeatDivisorColours.cs`:

```csharp
// Tests for the shared beat-divisor colour/height palette.
using Garbus.Game.Edit;
using NUnit.Framework;
using osuTK.Graphics;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestBeatDivisorColours
    {
        [Test]
        public void TestBarAndWholeBeatAreWhite()
        {
            Assert.That(BeatDivisorColours.ColourFor(0), Is.EqualTo(Color4.White));
            Assert.That(BeatDivisorColours.ColourFor(1), Is.EqualTo(Color4.White));
        }

        [Test]
        public void TestDistinctColoursPerDivisor()
        {
            Assert.That(BeatDivisorColours.ColourFor(2), Is.Not.EqualTo(BeatDivisorColours.ColourFor(4)));
        }

        [Test]
        public void TestHeightDecreasesWithFinerDivision()
        {
            Assert.That(BeatDivisorColours.HeightFor(1), Is.EqualTo(1.0f));
            Assert.That(BeatDivisorColours.HeightFor(4), Is.LessThan(BeatDivisorColours.HeightFor(1)));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestBeatDivisorColours`
Expected: FAIL — compile error, `BeatDivisorColours` does not exist.

- [ ] **Step 3: Create the shared helper**

Create `Garbus.Game/Edit/BeatDivisorColours.cs` (palette copied verbatim from `TimelineTickDisplay`):

```csharp
// Bespoke for Garbus. Shared divisor→colour/height palette used by the timeline tick display and
// the compose beat-divisor control. Hardcoded colours (Garbus drops osu.Game's OsuColour).
using osuTK.Graphics;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Palette keyed on the applicable beat divisor. Bar lines / whole beats are white; finer
    /// subdivisions cycle through a fixed set of colours and shrink in height.
    /// </summary>
    public static class BeatDivisorColours
    {
        /// <summary>Colour for a beat divisor (0 = bar line, treated as white).</summary>
        public static Color4 ColourFor(int divisor) => divisor switch
        {
            0 => Color4.White,          // bar lines
            1 => Color4.White,
            2 => new Color4(220, 100, 100, 255),   // red family
            3 => new Color4(100, 200, 100, 255),   // green
            4 => new Color4(100, 140, 220, 255),   // blue
            6 => new Color4(220, 160, 80, 255),    // orange
            8 => new Color4(160, 100, 220, 255),   // purple
            _ => new Color4(180, 180, 180, 255),   // grey for unusual divisors
        };

        /// <summary>Relative tick height (0..1) for a beat divisor.</summary>
        public static float HeightFor(int divisor) => divisor switch
        {
            1 => 1.0f,
            2 => 0.7f,
            3 => 0.6f,
            4 => 0.5f,
            6 => 0.45f,
            8 => 0.4f,
            _ => 0.35f,
        };
    }
}
```

- [ ] **Step 4: Point `TimelineTickDisplay` at the shared helper**

In `Garbus.Game/Edit/Screens/Timeline/TimelineTickDisplay.cs`, delete the private methods `getColourForDivisor` (lines ~126-141) and `getHeightForDivisor` (lines ~143-155), and replace their two call sites (lines ~89-90):

```csharp
                    Color4 colour = BeatDivisorColours.ColourFor(isBar ? 0 : divisor);
                    float heightFrac = isBar ? 1.0f : BeatDivisorColours.HeightFor(divisor);
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestBeatDivisorColours`
Expected: PASS (3 tests).

Then confirm no regression in the timeline tick tests:
Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneTimeline`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Edit/BeatDivisorColours.cs Garbus.Game/Edit/Screens/Timeline/TimelineTickDisplay.cs Garbus.Game.Tests/Editor/TestBeatDivisorColours.cs
git commit -m "refactor: extract shared beat-divisor colour/height palette"
```

---

### Task 2: `GarbusBeatDivisorControl` — display-only tick readout

Create the control with just its top row: a display-only tick readout that renders ticks for the active preset collection and a marker at the current divisor. No selection interactivity in this row.

**Files:**
- Create: `Garbus.Game/Edit/Compose/GarbusBeatDivisorControl.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneBeatDivisorControl.cs`

**Interfaces:**
- Consumes: `[Resolved] BindableBeatDivisor` (cached in the harness); `BeatDivisorColours.ColourFor/HeightFor`; `BindableBeatDivisor.GetDivisorForBeatIndex(int index, int beatDivisor, int[] validDivisors)`.
- Produces: `public partial class GarbusBeatDivisorControl : CompositeDrawable` with a nested `internal partial class TickDisplay : CompositeDrawable`. The readout renders `largestPreset + 1` `Circle` ticks plus one `EquilateralTriangle` marker. Later tasks append rows below it.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneBeatDivisorControl.cs`:

```csharp
// Visual/headless tests for the compose beat-divisor control.
using System.Linq;
using Garbus.Game.Edit;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osuTK;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneBeatDivisorControl : GarbusTestScene
    {
        private Harness harness = null!;
        private GarbusBeatDivisorControl control => harness.Control;
        private BindableBeatDivisor beatDivisor => harness.BeatDivisor;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = harness = new Harness { RelativeSizeAxes = Axes.Both };
        });

        private void waitForControl() => AddUntilStep("control loaded", () => control?.IsLoaded == true);

        private int tickCount() => control.ChildrenOfType<Circle>().Count();

        [Test]
        public void TestTickCountMatchesLargestPreset()
        {
            waitForControl();
            // COMMON presets {1,2,4,8,16} -> largest 16 -> 17 ticks (indices 0..16 inclusive).
            AddUntilStep("17 ticks for COMMON", () => tickCount() == 17);

            AddStep("switch to TRIPLETS", () => beatDivisor.SetArbitraryDivisor(6, true));
            // TRIPLETS presets {1,3,6,12} -> largest 12 -> 13 ticks.
            AddUntilStep("13 ticks for TRIPLETS", () => tickCount() == 13);
        }

        [Test]
        public void TestMarkerMovesWithDivisor()
        {
            waitForControl();

            float xAt(int divisor)
            {
                beatDivisor.SetArbitraryDivisor(divisor, true);
                return control.ChildrenOfType<EquilateralTriangle>().Single().ScreenSpaceDrawQuad.Centre.X;
            }

            float xLow = 0, xHigh = 0;
            AddStep("marker at 1/2", () => xLow = xAt(2));
            AddStep("marker at 1/16", () => xHigh = xAt(16));
            AddAssert("finer divisor pushes marker right", () => xHigh > xLow);
        }

        private partial class Harness : Container
        {
            public BindableBeatDivisor BeatDivisor { get; } = new BindableBeatDivisor(4);
            public GarbusBeatDivisorControl Control { get; private set; } = null!;
            private DependencyContainer dependencies = null!;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
                dependencies.Cache(BeatDivisor);
                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Control = new GarbusBeatDivisorControl
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(120, 90),
                    },
                };
            }
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneBeatDivisorControl`
Expected: FAIL — compile error, `GarbusBeatDivisorControl` does not exist.

- [ ] **Step 3: Create the control with the tick readout row**

Create `Garbus.Game/Edit/Compose/GarbusBeatDivisorControl.cs`:

```csharp
// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Compose/Components/BeatDivisorControl.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: rewritten on osu-framework primitives with hardcoded colours (drops
// OverlayColourProvider/OsuColour/OsuAnimatedButton/IconButton); the graphical tick row is
// display-only (osu's interactive TickSliderBar is intentionally dropped). Further rows added in
// later tasks: divisor +/- selector with custom-divisor popover, and type +/- selector.
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Compose
{
    public partial class GarbusBeatDivisorControl : CompositeDrawable
    {
        [Resolved]
        private BindableBeatDivisor beatDivisor { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Masking = true;

            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(), // tick display fills the remaining height
                },
                Content = new[]
                {
                    new Drawable[] { new TickDisplay() },
                },
            };
        }

        /// <summary>
        /// Display-only readout: one tick per beat index across the active preset collection, plus a
        /// marker at the current divisor. Selection happens via the chevron/type rows and keys, not here.
        /// </summary>
        internal partial class TickDisplay : CompositeDrawable
        {
            [Resolved]
            private BindableBeatDivisor beatDivisor { get; set; } = null!;

            private Container ticks = null!;
            private EquilateralTriangle marker = null!;

            public TickDisplay()
            {
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 5 },
                    Children = new Drawable[]
                    {
                        ticks = new Container { RelativeSizeAxes = Axes.Both },
                        marker = new EquilateralTriangle
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomCentre,
                            RelativePositionAxes = Axes.X,
                            Size = new Vector2(8, 6.5f),
                            Colour = Color4.White,
                        },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                beatDivisor.ValidDivisors.BindValueChanged(_ => rebuild(), true);
                beatDivisor.BindValueChanged(v => marker.X = mappedPosition(v.NewValue), true);
            }

            private void rebuild()
            {
                ticks.Clear();

                int[] presets = beatDivisor.ValidDivisors.Value.Presets.ToArray();
                int largest = presets.Last();

                for (int i = 0; i <= largest; i++)
                {
                    int divisor = BindableBeatDivisor.GetDivisorForBeatIndex(i, largest, presets);

                    ticks.Add(new Circle
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.Centre,
                        RelativePositionAxes = Axes.X,
                        RelativeSizeAxes = Axes.Y,
                        Width = 2f,
                        Height = BeatDivisorColours.HeightFor(divisor),
                        X = i / (float)largest,
                        Colour = BeatDivisorColours.ColourFor(divisor),
                    });
                }

                marker.X = mappedPosition(beatDivisor.Value);
            }

            // Matches osu's TickSliderBar.getMappedPosition: 1/1 -> 0, finer divisors -> toward 1.
            private static float mappedPosition(int divisor) => 1 - 1f / divisor;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneBeatDivisorControl`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/Compose/GarbusBeatDivisorControl.cs Garbus.Game.Tests/Editor/TestSceneBeatDivisorControl.cs
git commit -m "feat: add beat-divisor control tick readout"
```

---

### Task 3: Divisor row, type row, and Shift+number

Add the two chevron rows below the tick readout: `[◄ | 1/N | ►]` (Prev/Next within the collection) and `[◄ | type | ►]` (cycle Common/Triplets/Custom). Add Shift+1..9 direct divisor entry.

**Files:**
- Modify: `Garbus.Game/Edit/Compose/GarbusBeatDivisorControl.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneBeatDivisorControl.cs` (add tests)

**Interfaces:**
- Consumes: `beatDivisor.SelectNext()`, `beatDivisor.SelectPrevious()`, `beatDivisor.SetArbitraryDivisor(int, bool)`, `beatDivisor.ValidDivisors`.
- Produces: named `BasicButton`s `"divisor-prev"`, `"divisor-next"`, `"type-prev"`, `"type-next"`; a `SpriteText` showing `1/N`; a `SpriteText` showing the lowercased type name. `private void cycleDivisorType(int direction)` faithful to osu.

- [ ] **Step 1: Write the failing tests**

Add to `TestSceneBeatDivisorControl` (inside the class, after the existing tests):

```csharp
        private osu.Framework.Graphics.UserInterface.BasicButton button(string name)
            => control.ChildrenOfType<osu.Framework.Graphics.UserInterface.BasicButton>().Single(b => b.Name == name);

        private bool hasLabel(string text)
            => control.ChildrenOfType<osu.Framework.Graphics.Sprites.SpriteText>().Any(t => t.Text.ToString() == text);

        [Test]
        public void TestDivisorChevronsCycleWithinCollection()
        {
            waitForControl();
            AddAssert("starts at 1/4", () => hasLabel("1/4"));

            AddStep("click next", () => button("divisor-next").Action?.Invoke());
            AddAssert("advances to 1/8", () => beatDivisor.Value == 8 && hasLabel("1/8"));

            AddStep("click prev", () => button("divisor-prev").Action?.Invoke());
            AddAssert("back to 1/4", () => beatDivisor.Value == 4 && hasLabel("1/4"));
        }

        [Test]
        public void TestTypeChevronCyclesCommonTriplets()
        {
            waitForControl();
            AddAssert("type is common", () => hasLabel("common"));

            AddStep("cycle type forward", () => button("type-next").Action?.Invoke());
            AddAssert("triplets, landing on 1/6",
                () => beatDivisor.ValidDivisors.Value.Type == BeatDivisorType.Triplets && beatDivisor.Value == 6 && hasLabel("triplets"));

            AddStep("cycle type forward again", () => button("type-next").Action?.Invoke());
            AddAssert("skips custom back to common, landing on 1/4",
                () => beatDivisor.ValidDivisors.Value.Type == BeatDivisorType.Common && beatDivisor.Value == 4 && hasLabel("common"));
        }

        [Test]
        public void TestShiftNumberSetsDivisor()
        {
            waitForControl();
            AddStep("press Shift+3", () =>
            {
                InputManager.PressKey(osuTK.Input.Key.ShiftLeft);
                InputManager.PressKey(osuTK.Input.Key.Number3);
                InputManager.ReleaseKey(osuTK.Input.Key.Number3);
                InputManager.ReleaseKey(osuTK.Input.Key.ShiftLeft);
            });
            AddAssert("divisor is 3", () => beatDivisor.Value == 3);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneBeatDivisorControl`
Expected: FAIL — no buttons named `divisor-next`/`type-next`, no `1/4` label; Shift+3 does nothing.

- [ ] **Step 3: Add the two chevron rows and Shift+number**

In `GarbusBeatDivisorControl.cs`, update the usings block to add:

```csharp
using System;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osuTK.Input;
```

Replace the `InternalChild = new GridContainer { ... }` assignment in the control's `load()` with:

```csharp
            InternalChild = new GridContainer
            {
                RelativeSizeAxes = Axes.Both,
                RowDimensions = new[]
                {
                    new Dimension(),                              // tick display fills remaining
                    new Dimension(GridSizeMode.Absolute, 20),     // divisor row
                    new Dimension(GridSizeMode.Absolute, 20),     // type row
                },
                Content = new[]
                {
                    new Drawable[] { new TickDisplay() },
                    new Drawable[] { buildDivisorRow() },
                    new Drawable[] { buildTypeRow() },
                },
            };
```

Add these members to the control class (not the nested `TickDisplay`):

```csharp
        private SpriteText divisorText = null!;
        private SpriteText typeText = null!;
        private int? lastCustomDivisor;

        private Drawable buildDivisorRow() => new GridContainer
        {
            RelativeSizeAxes = Axes.Both,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, 20),
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, 20),
            },
            Content = new[]
            {
                new Drawable[]
                {
                    chevron("divisor-prev", "<", beatDivisor.SelectPrevious),
                    divisorText = centredLabel(20),
                    chevron("divisor-next", ">", beatDivisor.SelectNext),
                },
            },
        };

        private Drawable buildTypeRow() => new GridContainer
        {
            RelativeSizeAxes = Axes.Both,
            ColumnDimensions = new[]
            {
                new Dimension(GridSizeMode.Absolute, 20),
                new Dimension(),
                new Dimension(GridSizeMode.Absolute, 20),
            },
            Content = new[]
            {
                new Drawable[]
                {
                    chevron("type-prev", "<", () => cycleDivisorType(-1)),
                    typeText = centredLabel(14),
                    chevron("type-next", ">", () => cycleDivisorType(1)),
                },
            },
        };

        private static BasicButton chevron(string name, string glyph, Action action) => new BasicButton
        {
            Name = name,
            RelativeSizeAxes = Axes.Both,
            Text = glyph,
            Action = action,
            BackgroundColour = new Color4(60, 60, 70, 255),
        };

        private static SpriteText centredLabel(float size) => new SpriteText
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Font = osu.Framework.Graphics.Sprites.FontUsage.Default.With(size: size),
        };

        private void cycleDivisorType(int direction)
        {
            int totalTypes = Enum.GetValues<BeatDivisorType>().Length;
            BeatDivisorType currentType = beatDivisor.ValidDivisors.Value.Type;

            cycleOnce();

            // Skip Custom if we have no recorded custom divisor to return to.
            if (lastCustomDivisor == null && currentType == BeatDivisorType.Custom)
                cycleOnce();

            switch (currentType)
            {
                case BeatDivisorType.Common:
                    beatDivisor.SetArbitraryDivisor(4, true);
                    break;

                case BeatDivisorType.Triplets:
                    beatDivisor.SetArbitraryDivisor(6, true);
                    break;

                case BeatDivisorType.Custom:
                    beatDivisor.SetArbitraryDivisor(lastCustomDivisor!.Value);
                    break;
            }

            void cycleOnce() => currentType = (BeatDivisorType)(((int)currentType + totalTypes + direction) % totalTypes);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            beatDivisor.BindValueChanged(v => divisorText.Text = $"1/{v.NewValue}", true);
            beatDivisor.ValidDivisors.BindValueChanged(valid =>
            {
                typeText.Text = valid.NewValue.Type.ToString().ToLowerInvariant();
                if (valid.NewValue.Type == BeatDivisorType.Custom)
                    lastCustomDivisor = valid.NewValue.Presets.Last();
            }, true);
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.ShiftPressed && e.Key >= Key.Number1 && e.Key <= Key.Number9)
            {
                beatDivisor.SetArbitraryDivisor(e.Key - Key.Number0);
                return true;
            }

            return base.OnKeyDown(e);
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneBeatDivisorControl`
Expected: PASS (5 tests total).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/Compose/GarbusBeatDivisorControl.cs Garbus.Game.Tests/Editor/TestSceneBeatDivisorControl.cs
git commit -m "feat: add beat-divisor +/- and type selectors with shift-number entry"
```

---

### Task 4: Custom-divisor popover on the `1/N` display

Turn the `1/N` label into a button that opens a popover with a digit-restricted text box for entering an arbitrary divisor.

**Files:**
- Modify: `Garbus.Game/Edit/Compose/GarbusBeatDivisorControl.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneBeatDivisorControl.cs` (add tests)

**Interfaces:**
- Consumes: `beatDivisor.SetArbitraryDivisor(int)`; osu-framework `IHasPopover`, `BasicPopover`, `BasicTextBox`, `this.ShowPopover()`/`this.HidePopover()`. Requires a `PopoverContainer` ancestor (already present in the harness; added to `ComposeTab` in Task 5).
- Produces: nested `internal partial class DivisorDisplayButton : BasicButton, IHasPopover` (Name `"divisor-display"`) and `internal partial class CustomDivisorPopover : BasicPopover` exposing `internal bool Commit(string text)` (true when the text parsed to an in-range divisor and was applied).

- [ ] **Step 1: Write the failing tests**

Add to `TestSceneBeatDivisorControl` (add `using osu.Framework.Graphics.UserInterface;` at the top of the file if not already present):

```csharp
        private GarbusBeatDivisorControl.CustomDivisorPopover openPopover()
        {
            AddStep("open divisor popover", () =>
                control.ChildrenOfType<GarbusBeatDivisorControl.DivisorDisplayButton>().Single().Action?.Invoke());
            AddUntilStep("popover shown", () => harness.ChildrenOfType<GarbusBeatDivisorControl.CustomDivisorPopover>().Any());
            return harness.ChildrenOfType<GarbusBeatDivisorControl.CustomDivisorPopover>().Single();
        }

        [Test]
        public void TestCustomDivisorEntry()
        {
            waitForControl();
            GarbusBeatDivisorControl.CustomDivisorPopover popover = null!;
            openPopover();
            AddStep("grab popover", () => popover = harness.ChildrenOfType<GarbusBeatDivisorControl.CustomDivisorPopover>().Single());
            AddStep("commit 5", () => popover.Commit("5"));
            AddAssert("collection is custom, value 5",
                () => beatDivisor.ValidDivisors.Value.Type == BeatDivisorType.Custom && beatDivisor.Value == 5);
            AddAssert("type label shows custom", () => hasLabel("custom"));
        }

        [Test]
        public void TestInvalidCustomEntryIgnored()
        {
            waitForControl();
            GarbusBeatDivisorControl.CustomDivisorPopover popover = null!;
            openPopover();
            AddStep("grab popover", () => popover = harness.ChildrenOfType<GarbusBeatDivisorControl.CustomDivisorPopover>().Single());
            AddAssert("out-of-range rejected", () => popover.Commit("999") == false);
            AddAssert("divisor unchanged", () => beatDivisor.Value == 4);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneBeatDivisorControl`
Expected: FAIL — `DivisorDisplayButton` / `CustomDivisorPopover` do not exist.

- [ ] **Step 3: Replace the divisor label with a popover button**

In `GarbusBeatDivisorControl.cs`, add usings:

```csharp
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
```

In `buildDivisorRow`, replace the middle cell `divisorText = centredLabel(20)` with the new button:

```csharp
                    divisorDisplay = new DivisorDisplayButton(),
```

Change the field declaration `private SpriteText divisorText = null!;` to:

```csharp
        private DivisorDisplayButton divisorDisplay = null!;
```

In `LoadComplete`, replace the `divisorText.Text = ...` binding line with:

```csharp
            beatDivisor.BindValueChanged(v => divisorDisplay.Text = $"1/{v.NewValue}", true);
```

Add the two nested classes inside `GarbusBeatDivisorControl` (alongside `TickDisplay`):

```csharp
        internal partial class DivisorDisplayButton : BasicButton, IHasPopover
        {
            public DivisorDisplayButton()
            {
                Name = "divisor-display";
                RelativeSizeAxes = Axes.Both;
                BackgroundColour = new Color4(45, 45, 55, 255);
                Action = () => this.ShowPopover();
            }

            public Popover GetPopover() => new CustomDivisorPopover();
        }

        internal partial class CustomDivisorPopover : BasicPopover
        {
            [Resolved]
            private BindableBeatDivisor beatDivisor { get; set; } = null!;

            private NumberBox box = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                Child = new FillFlowContainer
                {
                    Width = 150,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = new Drawable[]
                    {
                        box = new NumberBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            PlaceholderText = "Beat divisor",
                        },
                        new SpriteText { Text = "Related divisors are added to the presets." },
                    },
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                box.Text = beatDivisor.Value.ToString();
                box.OnCommit += (_, _) =>
                {
                    if (Commit(box.Text))
                        this.HidePopover();
                    else
                        box.Text = beatDivisor.Value.ToString();
                };
            }

            /// <summary>Applies a typed divisor. Returns false (leaving state unchanged) on a
            /// non-numeric or out-of-range value.</summary>
            internal bool Commit(string text)
                => int.TryParse(text, out int divisor) && beatDivisor.SetArbitraryDivisor(divisor);

            private partial class NumberBox : BasicTextBox
            {
                protected override bool CanAddCharacter(char character) => char.IsAsciiDigit(character);
            }
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneBeatDivisorControl`
Expected: PASS (7 tests total).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/Compose/GarbusBeatDivisorControl.cs Garbus.Game.Tests/Editor/TestSceneBeatDivisorControl.cs
git commit -m "feat: add custom-divisor popover to beat-divisor control"
```

---

### Task 5: Integrate into `ComposeTab` — reserved column, vertical zoom stack, popover host

Restructure `ComposeTab` so the timeline strip shares a horizontal grid with a 35px vertical zoom column and a 120px `GarbusBeatDivisorControl`, wrapped in a `PopoverContainer`. Bump `TimelineStrip.HEIGHT` so three rows read legibly.

**Files:**
- Modify: `Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs:27` (HEIGHT constant)
- Modify: `Garbus.Game/Edit/Screens/ComposeTab.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneTimeline.cs` (add a layout guard) — the existing `TestSceneTimeline` already exercises zoom via `TimelineStrip` directly, so zoom coverage is retained.

**Interfaces:**
- Consumes: `GarbusBeatDivisorControl`; `PopoverContainer`; `timelineStrip.CurrentZoom`, `timelineStrip.Zoom`.
- Produces: `ComposeTab` layout where the timeline strip occupies a flex grid cell (no longer full-width) with a 35px zoom column and 120px divisor column to its right.

- [ ] **Step 1: Bump the strip height**

In `Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs`, change line 27:

```csharp
        public const float HEIGHT = 90;
```

- [ ] **Step 2: Write the failing test**

Add to `TestSceneTimeline` (inside the class):

```csharp
        [Test]
        public void TestBeatDivisorControlPresentInComposeTab()
        {
            var composeTab = new ComposeTab();
            AddStep("load compose tab", () => Child = harness = null!); // reset handled below

            // Re-host a full ComposeTab under the same DI as the harness.
            AddStep("host compose tab", () =>
            {
                var chart = new GarbusChart();
                chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                Child = new ComposeTabHarness(chart) { RelativeSizeAxes = Axes.Both };
            });

            AddUntilStep("beat-divisor control exists",
                () => this.ChildrenOfType<Garbus.Game.Edit.Compose.GarbusBeatDivisorControl>().Any());
            AddUntilStep("popover container hosts it",
                () => this.ChildrenOfType<osu.Framework.Graphics.Cursor.PopoverContainer>().Any());
        }
```

> Note: if adding a second harness type is undesirable, an equivalent guard is to assert
> `this.ChildrenOfType<GarbusBeatDivisorControl>().Any()` after driving the real editor in
> `TestSceneEditorShell`. Choose whichever fits the existing scene; the assertion is the deliverable,
> not the harness. If you reuse `TestSceneEditorShell`, delete the `ComposeTabHarness` scaffolding
> below and place the assertion there instead.

Add a minimal `ComposeTabHarness` mirroring `TimelineHarness` but hosting a `ComposeTab` (only if you did not reuse `TestSceneEditorShell`):

```csharp
        private partial class ComposeTabHarness : Container
        {
            private readonly GarbusChart chart;
            private DependencyContainer dependencies = null!;

            public ComposeTabHarness(GarbusChart chart) => this.chart = chart;

            protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            {
                dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

                var beatDivisor = new BindableBeatDivisor(4);
                var editorChart = new EditorChart(chart);
                var editorClock = new EditorClock(editorChart.ControlPointInfo, 60000, beatDivisor);
                editorClock.ChangeSource(new TrackVirtual(60000));

                dependencies.Cache(editorChart);
                dependencies.Cache(editorClock);
                dependencies.Cache(beatDivisor);
                dependencies.CacheAs<IEditorChangeHandler>(new GarbusChartChangeHandler(editorChart));
                dependencies.CacheAs(new ChartFile(chart));
                dependencies.CacheAs(editorChart.ControlPointInfo);
                return dependencies;
            }

            [BackgroundDependencyLoader]
            private void load() => Child = new ComposeTab { RelativeSizeAxes = Axes.Both };
        }
```

> Simplify: since `TestBeatDivisorControlPresentInComposeTab`'s first two `AddStep`s are awkward,
> collapse them into a single "host compose tab" step (drop the `composeTab`/reset lines). The final
> two `AddUntilStep` assertions are the actual test.

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestBeatDivisorControlPresentInComposeTab`
Expected: FAIL — `ComposeTab` does not yet contain a `GarbusBeatDivisorControl`.

- [ ] **Step 4: Restructure `ComposeTab`**

Replace the `InternalChildren = new Drawable[] { ... }` block in `Garbus.Game/Edit/Screens/ComposeTab.cs` `load()` with the grid + popover layout. Update usings to add:

```csharp
using osu.Framework.Graphics.Cursor;
using Garbus.Game.Edit.Compose;
```

New body (replaces the current `const float ZOOM_BUTTON_WIDTH = 26;` through the end of `InternalChildren`):

```csharp
            const float zoom_column_width = 35;
            const float divisor_column_width = 120;

            InternalChild = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        // Top region: [ timeline (flex) | zoom column | beat-divisor control ].
                        new GridContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = TimelineStrip.HEIGHT,
                            ColumnDimensions = new[]
                            {
                                new Dimension(),
                                new Dimension(GridSizeMode.Absolute, zoom_column_width),
                                new Dimension(GridSizeMode.Absolute, divisor_column_width),
                            },
                            Content = new[]
                            {
                                new Drawable[]
                                {
                                    timelineStrip = new TimelineStrip(),
                                    buildZoomColumn(),
                                    new GarbusBeatDivisorControl { RelativeSizeAxes = Axes.Both },
                                },
                            },
                        },
                        // Composer fills the rest below the top region.
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = TimelineStrip.HEIGHT },
                            Clock = editorClock,
                            Child = new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                Child = composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                            },
                        },
                    },
                },
            };
```

Add the vertical zoom stack helper (`+` on top, `–` on the bottom), replacing the two old overlay `BasicButton`s:

```csharp
        private Drawable buildZoomColumn() => new Container
        {
            RelativeSizeAxes = Axes.Both,
            Children = new Drawable[]
            {
                new BasicButton
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.5f,
                    Text = "+",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value + 1f,
                },
                new BasicButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.5f,
                    Text = "–",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value - 1f,
                },
            },
        };
```

Note: `TimelineStrip` sets `RelativeSizeAxes = Axes.X` and a fixed `Height` in its constructor; in the flex grid cell it takes the cell width and its own height, filling the top region. Leave the `Update()` method (zoom→`TimelineTimeRange` formula) unchanged. Remove the now-unused `using osu.Framework.Graphics.UserInterface;` only if nothing else needs it (the zoom `BasicButton`s keep it required).

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneTimeline`
Expected: PASS — including the new layout guard and the existing `TestZoomSyncsComposerTimeRange`.

Then run the full editor suite to confirm no regression:
Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestScene`
Expected: PASS.

- [ ] **Step 6: Build the desktop app to confirm it compiles and launches**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded. (Optionally `dotnet run --project Garbus.Desktop`, open the editor → Compose tab, and confirm the vertical zoom stack + beat-divisor control render at the top-right.)

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Edit/Screens/ComposeTab.cs Garbus.Game/Edit/Screens/Timeline/TimelineStrip.cs Garbus.Game.Tests/Editor/TestSceneTimeline.cs
git commit -m "feat: reserve editor top-right column for zoom stack and beat-divisor control"
```

---

## Self-Review

**Spec coverage:**

- Reserve-a-column layout → Task 5 (grid `[timeline | 35px zoom | 120px divisor]`).
- Vertical zoom stack → Task 5 `buildZoomColumn` (`+` top / `–` bottom).
- `GarbusBeatDivisorControl` on framework primitives, hardcoded colours → Tasks 2–4.
- Display-only tick readout → Task 2 (`TickDisplay`, no `SliderBar`/drag).
- `1/N` divisor row with chevrons → Task 3; custom popover → Task 4 (choice: keep custom entry).
- Type row with `cycleDivisorType` (Common/Triplets/Custom, skip-Custom logic) → Task 3.
- Shift+number direct entry → Task 3.
- Popover host (`PopoverContainer`, none existed) → Task 4 harness + Task 5 `ComposeTab`.
- Shared divisor palette de-dup → Task 1.
- `TimelineStrip.HEIGHT` bump for legibility → Task 5.
- Testing (default Common, chevron advances value+marker, type cycle lands 4/6, custom commit, invalid ignored, Shift+3, zoom still drives `CurrentZoom`, reserved-column geometry) → covered across Tasks 1–5.

**Placeholder scan:** No TBD/TODO. Every code step shows complete code. The Task 5 test note offers two concrete harness options and tells the implementer to delete the unused scaffolding — not a placeholder, an explicit either/or with full code for the primary path.

**Type consistency:** `BeatDivisorColours.ColourFor/HeightFor` (Task 1) are used verbatim in Task 2. `beatDivisor` field name, `SelectNext/SelectPrevious/SetArbitraryDivisor/ValidDivisors` match `BindableBeatDivisor`. Button `Name`s (`divisor-prev/next`, `type-prev/next`, `divisor-display`) are consistent between control and tests. `CustomDivisorPopover.Commit(string) : bool` and `DivisorDisplayButton` names match between Task 4 code and its tests. `TimelineStrip.HEIGHT` referenced symbolically in `ComposeTab` (both the top-region `Height` and composer `Padding.Top`), so the Task 5 bump propagates automatically.
