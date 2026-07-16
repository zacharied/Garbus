# Multi-value-aware inspector controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the editor inspector's controls multi-select aware — show `<multiple>` when a parameter differs across the selection, and apply an edit to the whole selection as one undo step.

**Architecture:** A pure aggregation helper (`MultiValue`) computes shared-vs-mixed from the selection; two thin control wrappers consume it — `MultiValueEnumDropdown<T>` (a `BasicDropdown<T?>` with a transient `<multiple>` sentinel) and `MultiValueCheckbox` (a tri-state box driven by the existing `TernaryState`). `Inspector.addControls` is retrofitted onto the kit, gating each control on the whole selection sharing the property.

**Tech Stack:** C# / osu-framework, NUnit headless visual tests (`Garbus.Game.Tests`).

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- New kit files live flat in `Garbus.Game/Edit/` (namespace `Garbus.Game.Edit`), matching the flat layout of the rest of `Edit/`.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- A control renders only when **every** selected object exposes the property (common-property rule).
- The checkbox ships tested but **not wired** to any live property — no boolean hit-object property is invented.
- Enum facts: `HorizontalDirection` (`Garbus.Game.Core`) Left=-1/Right=1; `RotationalDirection` (`Garbus.Game.Core`) Clockwise=1/Anticlockwise=-1; `Easing` (`osu.Framework.Graphics`). `TernaryState` lives in `Garbus.Game.Edit.Compose`.
- Property facts: `IHasSide.Side` is a `HorizontalDirection` property; `GarbusSlamEdge.Direction` is a `RotationalDirection` **field**; `GarbusPathControlPoint.SweepEasing` is an `Easing` **field**.

---

### Task 1: `MultiValue<T>` aggregation helper

**Files:**
- Create: `Garbus.Game/Edit/MultiValue.cs`
- Test: `Garbus.Game.Tests/Editor/TestMultiValue.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `readonly struct MultiValue<T> { bool IsMixed; T Value; }`
  - `static class MultiValue { static MultiValue<T> Aggregate<TObj, T>(IReadOnlyList<TObj> objs, Func<TObj, T> get); }`
  - `MultiValue<T>` also constructible directly: `new MultiValue<bool>(isMixed: true, default)` — used by the checkbox tests.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestMultiValue.cs`:

```csharp
using System.Collections.Generic;
using Garbus.Game.Edit;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestMultiValue
    {
        [Test]
        public void AllAgree_NotMixed_SharedValue()
        {
            var result = MultiValue.Aggregate(new[] { 5, 5, 5 }, x => x);
            Assert.That(result.IsMixed, Is.False);
            Assert.That(result.Value, Is.EqualTo(5));
        }

        [Test]
        public void Differing_IsMixed()
        {
            var result = MultiValue.Aggregate(new[] { 5, 6, 5 }, x => x);
            Assert.That(result.IsMixed, Is.True);
        }

        [Test]
        public void SingleElement_NotMixed()
        {
            var result = MultiValue.Aggregate(new[] { 42 }, x => x);
            Assert.That(result.IsMixed, Is.False);
            Assert.That(result.Value, Is.EqualTo(42));
        }

        [Test]
        public void ProjectsThroughGetter()
        {
            var result = MultiValue.Aggregate(new[] { (a: 1, b: 9), (a: 2, b: 9) }, t => t.b);
            Assert.That(result.IsMixed, Is.False);
            Assert.That(result.Value, Is.EqualTo(9));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestMultiValue`
Expected: FAIL (compile error — `MultiValue` does not exist).

- [ ] **Step 3: Write minimal implementation**

Create `Garbus.Game/Edit/MultiValue.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// The aggregate of one parameter across a multi-object selection: either a single shared
    /// <see cref="Value"/> (all agree) or <see cref="IsMixed"/> when the targets disagree.
    /// </summary>
    public readonly struct MultiValue<T>
    {
        /// <summary>The selected targets hold differing values for this parameter.</summary>
        public readonly bool IsMixed;

        /// <summary>The shared value; meaningful only when <see cref="IsMixed"/> is false.</summary>
        public readonly T Value;

        public MultiValue(bool isMixed, T value)
        {
            IsMixed = isMixed;
            Value = value;
        }
    }

    public static class MultiValue
    {
        /// <summary>
        /// Collapses a per-object parameter to a single <see cref="MultiValue{T}"/>. Must not be called
        /// on an empty list — callers don't render a control for an empty selection.
        /// </summary>
        public static MultiValue<T> Aggregate<TObj, T>(IReadOnlyList<TObj> objs, Func<TObj, T> get)
        {
            var comparer = EqualityComparer<T>.Default;
            T first = get(objs[0]);

            for (int i = 1; i < objs.Count; i++)
            {
                if (!comparer.Equals(get(objs[i]), first))
                    return new MultiValue<T>(isMixed: true, first);
            }

            return new MultiValue<T>(isMixed: false, first);
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestMultiValue`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/MultiValue.cs Garbus.Game.Tests/Editor/TestMultiValue.cs
git commit -m "feat: add MultiValue selection-aggregation helper"
```

---

### Task 2: `MultiValueEnumDropdown<T>`

**Files:**
- Create: `Garbus.Game/Edit/MultiValueEnumDropdown.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneMultiValueDropdown.cs`

**Interfaces:**
- Consumes: `MultiValue<T>` (Task 1).
- Produces:
  - `partial class MultiValueEnumDropdown<T> : BasicDropdown<T?> where T : struct, Enum`
  - Constructor `MultiValueEnumDropdown(MultiValue<T> state, Action<T> onChange)`.
  - `public const string MixedText = "<multiple>";`
  - When `state.IsMixed`, `Items` contains a leading `null` sentinel and `Current.Value == null`. Selecting a real value invokes `onChange(value)`; the `null` sentinel is never re-selectable by the caller and is dropped on the next rebuild.

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneMultiValueDropdown.cs`:

```csharp
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Edit;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneMultiValueDropdown : GarbusTestScene
    {
        private MultiValueEnumDropdown<HorizontalDirection> dropdown = null!;
        private HorizontalDirection? lastChange;

        private void build(MultiValue<HorizontalDirection> state)
        {
            lastChange = null;
            Child = dropdown = new MultiValueEnumDropdown<HorizontalDirection>(state, v => lastChange = v)
            {
                RelativeSizeAxes = Axes.X,
            };
        }

        [Test]
        public void Mixed_SelectsNullSentinel()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddAssert("current is null", () => dropdown.Current.Value == null);
            AddAssert("items include null", () => dropdown.Items.Any(i => i == null));
        }

        [Test]
        public void Shared_SelectsValue_NoSentinel()
        {
            AddStep("build shared Right",
                () => build(new MultiValue<HorizontalDirection>(isMixed: false, HorizontalDirection.Right)));
            AddAssert("current is Right", () => dropdown.Current.Value == HorizontalDirection.Right);
            AddAssert("no null item", () => dropdown.Items.All(i => i != null));
        }

        [Test]
        public void SelectingValue_FiresOnChange()
        {
            AddStep("build mixed", () => build(new MultiValue<HorizontalDirection>(isMixed: true, default)));
            AddStep("pick Right", () => dropdown.Current.Value = HorizontalDirection.Right);
            AddAssert("onChange got Right", () => lastChange == HorizontalDirection.Right);
        }

        [Test]
        public void MixedTextConstant()
        {
            Assert.That(MultiValueEnumDropdown<HorizontalDirection>.MixedText, Is.EqualTo("<multiple>"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneMultiValueDropdown`
Expected: FAIL (compile error — `MultiValueEnumDropdown` does not exist).

- [ ] **Step 3: Write minimal implementation**

Create `Garbus.Game/Edit/MultiValueEnumDropdown.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// A <see cref="BasicDropdown{T}"/> over a nullable enum where <c>null</c> is a transient
    /// "<multiple>" sentinel shown when the selection holds differing values. Picking a real value
    /// invokes the supplied callback; the sentinel disappears once the caller re-aggregates.
    /// </summary>
    public partial class MultiValueEnumDropdown<T> : BasicDropdown<T?> where T : struct, Enum
    {
        public const string MixedText = "<multiple>";

        public MultiValueEnumDropdown(MultiValue<T> state, Action<T> onChange)
        {
            var items = new List<T?>();
            if (state.IsMixed)
                items.Add(null);
            items.AddRange(Enum.GetValues<T>().Select(v => (T?)v));

            Items = items;
            Current.Value = state.IsMixed ? null : state.Value;

            // Bound AFTER the initial value is set, so only user selections fire the callback.
            Current.BindValueChanged(e =>
            {
                if (e.NewValue is T v)
                    onChange(v);
            });
        }

        protected override LocalisableString GenerateItemText(T? item)
            => item.HasValue ? base.GenerateItemText(item) : MixedText;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneMultiValueDropdown`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/MultiValueEnumDropdown.cs Garbus.Game.Tests/Editor/TestSceneMultiValueDropdown.cs
git commit -m "feat: add multi-value-aware enum dropdown"
```

---

### Task 3: `MultiValueCheckbox` (tri-state)

**Files:**
- Create: `Garbus.Game/Edit/MultiValueCheckbox.cs`
- Test: `Garbus.Game.Tests/Editor/TestSceneMultiValueCheckbox.cs`

**Interfaces:**
- Consumes: `MultiValue<bool>` (Task 1), `TernaryState` (`Garbus.Game.Edit.Compose`).
- Produces:
  - `partial class MultiValueCheckbox : CompositeDrawable`
  - Constructor `MultiValueCheckbox(string label, MultiValue<bool> state, Action<bool> onChange)`.
  - `public TernaryState State { get; }` — `Indeterminate` when mixed, else `True`/`False`.
  - `internal static bool NextValue(TernaryState state)` — click resolution (True→false, else→true).

- [ ] **Step 1: Write the failing test**

Create `Garbus.Game.Tests/Editor/TestSceneMultiValueCheckbox.cs`:

```csharp
using Garbus.Game.Edit;
using Garbus.Game.Edit.Compose;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public class TestSceneMultiValueCheckbox
    {
        [Test]
        public void Mixed_MapsToIndeterminate()
        {
            var cb = new MultiValueCheckbox("On", new MultiValue<bool>(isMixed: true, default), _ => { });
            Assert.That(cb.State, Is.EqualTo(TernaryState.Indeterminate));
        }

        [Test]
        public void True_MapsToTrue()
        {
            var cb = new MultiValueCheckbox("On", new MultiValue<bool>(isMixed: false, true), _ => { });
            Assert.That(cb.State, Is.EqualTo(TernaryState.True));
        }

        [Test]
        public void False_MapsToFalse()
        {
            var cb = new MultiValueCheckbox("On", new MultiValue<bool>(isMixed: false, false), _ => { });
            Assert.That(cb.State, Is.EqualTo(TernaryState.False));
        }

        [Test]
        public void NextValue_TrueGoesFalse_OthersGoTrue()
        {
            Assert.That(MultiValueCheckbox.NextValue(TernaryState.True), Is.False);
            Assert.That(MultiValueCheckbox.NextValue(TernaryState.False), Is.True);
            Assert.That(MultiValueCheckbox.NextValue(TernaryState.Indeterminate), Is.True);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneMultiValueCheckbox`
Expected: FAIL (compile error — `MultiValueCheckbox` does not exist).

- [ ] **Step 3: Write minimal implementation**

Create `Garbus.Game/Edit/MultiValueCheckbox.cs`:

```csharp
using System;
using Garbus.Game.Edit.Compose;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// A tri-state checkbox for the inspector: <see cref="TernaryState.Indeterminate"/> (a dash) shows
    /// when the selection holds differing boolean values. Clicking a True box unchecks the selection;
    /// clicking a False or Indeterminate box checks it. Not yet wired to any hit-object property.
    /// </summary>
    public partial class MultiValueCheckbox : CompositeDrawable
    {
        public TernaryState State { get; }

        private readonly string label;
        private readonly Action<bool> onChange;

        private Box checkMark = null!;
        private Box dash = null!;

        public MultiValueCheckbox(string label, MultiValue<bool> state, Action<bool> onChange)
        {
            this.label = label;
            this.onChange = onChange;
            State = state.IsMixed ? TernaryState.Indeterminate
                : state.Value ? TernaryState.True : TernaryState.False;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        /// <summary>Click resolution: a checked box unchecks; anything else checks.</summary>
        internal static bool NextValue(TernaryState state) => state != TernaryState.True;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new Container
                    {
                        Size = new Vector2(16),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = new Colour4(40, 40, 48, 255) },
                            checkMark = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Scale = new Vector2(0.6f),
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Colour = Colour4.White,
                                Alpha = State == TernaryState.True ? 1 : 0,
                            },
                            dash = new Box
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 3,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Colour = new Colour4(180, 180, 190, 255),
                                Alpha = State == TernaryState.Indeterminate ? 1 : 0,
                            },
                        },
                    },
                    new SpriteText
                    {
                        Text = label,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Font = FontUsage.Default.With(size: 12),
                        Colour = new Colour4(180, 180, 190, 255),
                    },
                },
            };
        }

        protected override bool OnClick(ClickEvent e)
        {
            onChange(NextValue(State));
            return true;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneMultiValueCheckbox`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add Garbus.Game/Edit/MultiValueCheckbox.cs Garbus.Game.Tests/Editor/TestSceneMultiValueCheckbox.cs
git commit -m "feat: add tri-state multi-value checkbox"
```

---

### Task 4: Retrofit `Inspector.addControls` onto the kit

**Files:**
- Modify: `Garbus.Game/Edit/Inspector.cs` (replace `addControls` body + the private `addEnumDropdown` helper)
- Test: `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (add one integration test)

**Interfaces:**
- Consumes: `MultiValue.Aggregate` (Task 1), `MultiValueEnumDropdown<T>` (Task 2).
- Produces: no new public surface — the Inspector now renders Side / Direction / Easing for the whole selection with `<multiple>` support.

- [ ] **Step 1: Write the failing test**

Add to `Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs` (inside the class; needed usings — `Garbus.Game.Edit`, `System.Linq`, `osu.Framework.Testing` for `ChildrenOfType` — are already imported in that file):

```csharp
[Test]
public void TestSideDropdownMultiSelectMixedAppliesToAll()
{
    GarbusSlamEdge a = null!;
    GarbusSlamEdge b = null!;

    waitForComposer();

    AddStep("add two slam edges with differing sides", () =>
    {
        a = new GarbusSlamEdge { AngleDeg = 0, Side = HorizontalDirection.Left, StartTime = 1000 };
        b = new GarbusSlamEdge { AngleDeg = 90, Side = HorizontalDirection.Right, StartTime = 2000 };
        editorChart.Add(a);
        editorChart.Add(b);
        editorChart.SelectedHitObjects.Add(a);
        editorChart.SelectedHitObjects.Add(b);
    });

    MultiValueEnumDropdown<HorizontalDirection> sideDropdown() =>
        composer.ChildrenOfType<MultiValueEnumDropdown<HorizontalDirection>>().Single();

    AddUntilStep("Side dropdown appears", () =>
        composer.ChildrenOfType<MultiValueEnumDropdown<HorizontalDirection>>().Any());
    AddAssert("shows <multiple>", () => sideDropdown().Current.Value == null);

    AddStep("pick Right for all", () => sideDropdown().Current.Value = HorizontalDirection.Right);
    AddAssert("both now Right", () =>
        a.Side == HorizontalDirection.Right && b.Side == HorizontalDirection.Right);
    AddAssert("single undo step restores mix", () =>
    {
        changeHandler.RestoreState(-1);
        return a.Side == HorizontalDirection.Left && b.Side == HorizontalDirection.Right;
    });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSideDropdownMultiSelectMixedAppliesToAll`
Expected: FAIL — with the current single-object gating the Side dropdown never appears for a 2-object selection, so `AddUntilStep "Side dropdown appears"` times out.

- [ ] **Step 3: Rewrite `addControls` and drop `addEnumDropdown`**

In `Garbus.Game/Edit/Inspector.cs`, replace the entire `addControls` method (currently `Inspector.cs:212-281`) and the `addEnumDropdown` helper (currently `Inspector.cs:283-311`) with:

```csharp
        private void addControls(GarbusHitObject[] objects, HashSet<GarbusPathControlPoint> selectedNodes)
        {
            // Side: every selected object must carry a mutable Side (slider + both slam types).
            if (objects.Length > 0 && objects.All(o => o is IHasSide))
            {
                var sided = objects.Cast<IHasSide>().ToArray();
                var state = MultiValue.Aggregate(sided, s => s.Side);

                addMultiValueDropdown("Side", state, value =>
                {
                    if (!state.IsMixed && EqualityComparer<HorizontalDirection>.Default.Equals(state.Value, value))
                        return;

                    changeHandler?.BeginChange();
                    foreach (var s in sided) s.Side = value;
                    foreach (var o in objects) editorChart.Update(o);
                    changeHandler?.EndChange();
                });
            }

            // Direction: every selected object must be a GarbusSlamEdge.
            if (objects.Length > 0 && objects.All(o => o is GarbusSlamEdge))
            {
                var slams = objects.Cast<GarbusSlamEdge>().ToArray();
                var state = MultiValue.Aggregate(slams, s => s.Direction);

                addMultiValueDropdown("Direction", state, value =>
                {
                    if (!state.IsMixed && EqualityComparer<RotationalDirection>.Default.Equals(state.Value, value))
                        return;

                    changeHandler?.BeginChange();
                    foreach (var s in slams) s.Direction = value;
                    foreach (var s in slams) editorChart.Update(s);
                    changeHandler?.EndChange();
                });
            }

            // Easing: shown whenever one or more slider control-point nodes are picked.
            if (selectedNodes.Count > 0)
            {
                var nodes = selectedNodes.ToArray();
                var state = MultiValue.Aggregate(nodes, n => n.SweepEasing);

                var affectedSliders = editorChart.HitObjects.OfType<SliderBody>()
                    .Where(s => s.Path.ControlPoints.Any(cp => selectedNodes.Contains(cp)))
                    .ToArray();

                addMultiValueDropdown("Easing", state, value =>
                {
                    if (!state.IsMixed && EqualityComparer<Easing>.Default.Equals(state.Value, value))
                        return;

                    changeHandler?.BeginChange();
                    foreach (var n in nodes) n.SweepEasing = value;
                    foreach (var s in affectedSliders) editorChart.Update(s);
                    changeHandler?.EndChange();
                });
            }
        }

        private void addMultiValueDropdown<T>(string label, MultiValue<T> state, Action<T> onChange)
            where T : struct, Enum
        {
            var dropdown = new MultiValueEnumDropdown<T>(state, onChange)
            {
                RelativeSizeAxes = Axes.X,
            };

            controlsFlow.Add(new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = label,
                        Font = FontUsage.Default.With(size: 12),
                        Colour = new Colour4(180, 180, 190, 255),
                    },
                    dropdown,
                },
            });
        }
```

No new using directives are needed: `Inspector.cs` already imports `Garbus.Game.Core` (for `HorizontalDirection`/`RotationalDirection`), `osu.Framework.Graphics` (for `Easing`), and `System.Collections.Generic` (for `EqualityComparer`). `System`, `System.Linq`, and `MultiValue`/`MultiValueEnumDropdown` (same `Garbus.Game.Edit` namespace) are likewise already in scope.

- [ ] **Step 4: Run the integration test + full editor suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSideDropdownMultiSelectMixedAppliesToAll`
Expected: PASS.

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneComposeSelection`
Expected: PASS (existing selection tests still green — no regression from the control changes).

- [ ] **Step 5: Build the desktop solution**

Run: `dotnet build Garbus.Desktop.slnf`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Edit/Inspector.cs Garbus.Game.Tests/Editor/TestSceneComposeSelection.cs
git commit -m "feat: make inspector controls multi-select aware"
```

---

### Task 5: Full regression pass

**Files:** none (verification only).

- [ ] **Step 1: Run the whole test suite**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: all tests pass (existing editor/gameplay suites plus the four new fixtures).

- [ ] **Step 2: Manual smoke (optional but recommended)**

Run: `dotnet run --project Garbus.Desktop`, open the editor, select two sliders with different Sides, confirm the Side dropdown reads `<multiple>` and that picking a value updates both (undo reverts in one step).

---

## Notes for the implementer

- `changeHandler.RestoreState(-1)` is `GarbusChartChangeHandler`'s undo (steps one state back); it's already used by the undo test elsewhere in `TestSceneComposeSelection`. Confirm the exact method name against the existing undo test in that file and match it.
- `GarbusSlamEdge.Direction` and `GarbusPathControlPoint.SweepEasing` are **fields**, so the `s => s.Direction` / `n => n.SweepEasing` getters and `s.Direction = value` setters compile directly — no property accessor needed.
- The `<multiple>` sentinel is display-only: after an edit unifies the values, the Inspector's `HitObjectUpdated`/`SelectedHitObjects` rebuild re-aggregates to a non-mixed state, so the sentinel is simply not rebuilt.
```
