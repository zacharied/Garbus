# Controller Button Remapping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the player remap the 10 controller buttons, persist the choices, and edit them from an expandable "Controls" sub-panel in the settings overlay.

**Architecture:** A `KeyBindingStore` (cached at game level) holds the effective bindings — the hardcoded defaults overlaid with per-action overrides read from `keybindings.json` — and persists overrides on change. `GarbusInputManager` resolves the store and assigns its `KeyBindings` from it, falling back to `DefaultKeyBindings` when no store is cached. A `ControlsPanel` of `KeyBindingRow`s, reached from a "Controls…" button in `SettingsOverlay`, captures the next gamepad press per row and writes through the store.

**Tech Stack:** C# / osu-framework, Newtonsoft.Json, NUnit visual + plain test scenes.

## Global Constraints

- Nullability is enabled solution-wide; DI/BDL-initialised fields use `= null!`.
- No realm, no historical/compat layers, no version increments (experimental project).
- Terminology: charts not beatmaps; `Garbus*` prefixes.
- Gamepad-only remapping; one button per action; replace on rebind; no conflict detection; no keyboard bindings.
- Build: `dotnet build Garbus.Desktop.slnf`. Tests: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`.
- Persisted file: `keybindings.json` in framework `Storage` (`%APPDATA%\Garbus`), holding **only** overrides that differ from defaults, keyed by `GarbusAction` name → `InputKey` name.

## File Structure

- Create `Garbus.Game/Input/KeyBindingStore.cs` — load/rebind/reset/persist + effective bindings.
- Modify `Garbus.Game/Input/GarbusInputManager.cs` — expose a shared `DefaultBindings` map; resolve the store and assign `KeyBindings`.
- Modify `Garbus.Game/GarbusGameBase.cs` — cache the store.
- Create `Garbus.Game/Settings/KeyBindingRow.cs` — one action row: label + current key + click-to-capture.
- Create `Garbus.Game/Settings/ControlsPanel.cs` — 10 rows + Reset + Back.
- Modify `Garbus.Game/Settings/SettingsOverlay.cs` — "Controls…" button and slide-over to the panel.
- Create `Garbus.Game.Tests/Input/TestKeyBindingStore.cs` — plain NUnit over a temp storage.
- Create `Garbus.Game.Tests/Visual/TestSceneKeyBindingInput.cs` — input-manager resolves store.
- Create `Garbus.Game.Tests/Visual/TestSceneControlsPanel.cs` — capture + reset via `ManualInputManager`.

---

### Task 1: KeyBindingStore + shared default map

**Files:**
- Modify: `Garbus.Game/Input/GarbusInputManager.cs`
- Create: `Garbus.Game/Input/KeyBindingStore.cs`
- Test: `Garbus.Game.Tests/Input/TestKeyBindingStore.cs`

**Interfaces:**
- Consumes: `GarbusAction` enum; framework `Storage`, `InputKey`, `KeyBinding`, `IKeyBinding`.
- Produces:
  - `GarbusInputManager.DefaultBindings` — `IReadOnlyDictionary<GarbusAction, InputKey>`.
  - `KeyBindingStore(Storage storage)`.
  - `InputKey KeyBindingStore.GetBinding(GarbusAction action)`.
  - `IEnumerable<IKeyBinding> KeyBindingStore.GetKeyBindings()`.
  - `void KeyBindingStore.Rebind(GarbusAction action, InputKey key)`.
  - `void KeyBindingStore.ResetToDefaults()`.
  - `event Action? KeyBindingStore.Changed`.

- [ ] **Step 1: Extract the default bindings into a shared map on `GarbusInputManager`**

Replace the body of `Garbus.Game/Input/GarbusInputManager.cs` with:

```csharp
// Replaces BigAssCircle's RulesetInputManager<BigAssCircleAction>. A plain framework
// KeyBindingContainer over GarbusAction. Defaults live in the shared DefaultBindings map so the
// KeyBindingStore can overlay per-action overrides on the same source of truth.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;

namespace Garbus.Game.Input
{
    public partial class GarbusInputManager : KeyBindingContainer<GarbusAction>
    {
        public GarbusInputManager()
            : base(SimultaneousBindingMode.All, KeyCombinationMatchingMode.Any)
        {
            RelativeSizeAxes = osu.Framework.Graphics.Axes.Both;
        }

        // One physical button per action. The d-pad drives the "…1" actions and the physically-matching
        // face button drives "…2"; each direction sits at its matching on-screen position. The controller
        // is opened as an SDL gamepad, so face buttons arrive as X=Joystick1, A=Joystick2, B=Joystick3,
        // Y=Joystick4 and the d-pad as JoystickHat1*.
        public static readonly IReadOnlyDictionary<GarbusAction, InputKey> DefaultBindings = new Dictionary<GarbusAction, InputKey>
        {
            // Screen up -> North  (D-pad Up = N1, Y = N2)
            { GarbusAction.ButtonN1, InputKey.JoystickHat1Up },
            { GarbusAction.ButtonN2, InputKey.Joystick4 },

            // Screen right -> East  (D-pad Right = E1, B = E2)
            { GarbusAction.ButtonE1, InputKey.JoystickHat1Right },
            { GarbusAction.ButtonE2, InputKey.Joystick3 },

            // Screen down -> South  (D-pad Down = S1, A = S2)
            { GarbusAction.ButtonS1, InputKey.JoystickHat1Down },
            { GarbusAction.ButtonS2, InputKey.Joystick2 },

            // Screen left -> West  (D-pad Left = W1, X = W2)
            { GarbusAction.ButtonW1, InputKey.JoystickHat1Left },
            { GarbusAction.ButtonW2, InputKey.Joystick1 },

            // Left and right shoulder buttons
            { GarbusAction.ButtonL, InputKey.Joystick5 },
            { GarbusAction.ButtonR, InputKey.Joystick6 },
        };

        public override IEnumerable<IKeyBinding> DefaultKeyBindings =>
            DefaultBindings.Select(b => new KeyBinding(b.Value, b.Key)).ToArray();

        // Assigned from the cached KeyBindingStore when one is present; otherwise the base class falls
        // back to DefaultKeyBindings. canBeNull keeps bare-constructed test instances working.
        [BackgroundDependencyLoader(true)]
        private void load(KeyBindingStore store)
        {
            if (store != null)
                KeyBindings = store.GetKeyBindings().ToList();
        }
    }
}
```

- [ ] **Step 2: Write the failing store tests**

Create `Garbus.Game.Tests/Input/TestKeyBindingStore.cs`:

```csharp
// Plain NUnit over a temp NativeStorage: defaults, partial-file overrides, rebind persistence, reset.

using System.IO;
using System.Linq;
using Garbus.Game.Input;
using NUnit.Framework;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;

namespace Garbus.Game.Tests.Input
{
    [TestFixture]
    public class TestKeyBindingStore
    {
        private string tempDir = null!;
        private NativeStorage storage = null!;

        [SetUp]
        public void SetUp()
        {
            tempDir = Directory.CreateTempSubdirectory("garbus-kb-").FullName;
            storage = new NativeStorage(tempDir);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(tempDir, true);

        [Test]
        public void TestNoFileUsesDefaults()
        {
            var store = new KeyBindingStore(storage);
            Assert.That(store.GetBinding(GarbusAction.ButtonE2), Is.EqualTo(InputKey.Joystick3));
            Assert.That(store.GetBinding(GarbusAction.ButtonL), Is.EqualTo(InputKey.Joystick6));
        }

        [Test]
        public void TestPartialFileOverridesOnlyListedActions()
        {
            File.WriteAllText(Path.Combine(tempDir, "keybindings.json"),
                "{ \"ButtonE2\": \"Joystick1\" }");

            var store = new KeyBindingStore(storage);
            Assert.That(store.GetBinding(GarbusAction.ButtonE2), Is.EqualTo(InputKey.Joystick1));
            // Everything else stays default.
            Assert.That(store.GetBinding(GarbusAction.ButtonL), Is.EqualTo(InputKey.Joystick6));
        }

        [Test]
        public void TestRebindPersistsAndReloads()
        {
            var store = new KeyBindingStore(storage);
            store.Rebind(GarbusAction.ButtonN1, InputKey.Joystick2);

            var reloaded = new KeyBindingStore(storage);
            Assert.That(reloaded.GetBinding(GarbusAction.ButtonN1), Is.EqualTo(InputKey.Joystick2));
        }

        [Test]
        public void TestResetToDefaultsClearsOverrides()
        {
            var store = new KeyBindingStore(storage);
            store.Rebind(GarbusAction.ButtonN1, InputKey.Joystick2);
            store.ResetToDefaults();

            Assert.That(store.GetBinding(GarbusAction.ButtonN1), Is.EqualTo(InputKey.JoystickHat1Up));

            var reloaded = new KeyBindingStore(storage);
            Assert.That(reloaded.GetBinding(GarbusAction.ButtonN1), Is.EqualTo(InputKey.JoystickHat1Up));
        }

        [Test]
        public void TestGetKeyBindingsReflectsOverride()
        {
            var store = new KeyBindingStore(storage);
            store.Rebind(GarbusAction.ButtonE1, InputKey.Joystick2);

            var binding = store.GetKeyBindings()
                .Single(b => (GarbusAction)b.Action == GarbusAction.ButtonE1);
            Assert.That(binding.KeyCombination.Keys.Single(), Is.EqualTo(InputKey.Joystick2));
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestKeyBindingStore`
Expected: FAIL — `KeyBindingStore` does not exist (compile error).

- [ ] **Step 4: Implement `KeyBindingStore`**

Create `Garbus.Game/Input/KeyBindingStore.cs`:

```csharp
// Holds the effective controller bindings: GarbusInputManager.DefaultBindings overlaid with per-action
// overrides read from keybindings.json in Garbus storage. Persists only the overrides that differ from
// defaults, so a stale/partial file can never leave an action unbound. Cached at game level by
// GarbusGameBase; resolved by GarbusInputManager (bindings) and passed into ControlsPanel (editing).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;

namespace Garbus.Game.Input
{
    public class KeyBindingStore
    {
        private const string filename = "keybindings.json";

        private readonly Storage storage;
        private readonly Dictionary<GarbusAction, InputKey> effective;

        /// <summary>Raised after any change to the bindings (rebind or reset).</summary>
        public event Action? Changed;

        public KeyBindingStore(Storage storage)
        {
            this.storage = storage;
            effective = new Dictionary<GarbusAction, InputKey>(GarbusInputManager.DefaultBindings);
            load();
        }

        public InputKey GetBinding(GarbusAction action) => effective[action];

        public IEnumerable<IKeyBinding> GetKeyBindings() =>
            effective.Select(b => new KeyBinding(b.Value, b.Key)).ToArray();

        public void Rebind(GarbusAction action, InputKey key)
        {
            effective[action] = key;
            save();
            Changed?.Invoke();
        }

        public void ResetToDefaults()
        {
            foreach (var b in GarbusInputManager.DefaultBindings)
                effective[b.Key] = b.Value;

            save();
            Changed?.Invoke();
        }

        private void load()
        {
            if (!storage.Exists(filename))
                return;

            string text;
            using (var stream = storage.GetStream(filename))
            using (var reader = new StreamReader(stream))
                text = reader.ReadToEnd();

            var raw = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
            if (raw == null)
                return;

            foreach (var (actionName, keyName) in raw)
            {
                if (Enum.TryParse<GarbusAction>(actionName, out var action)
                    && Enum.TryParse<InputKey>(keyName, out var key))
                    effective[action] = key;
            }
        }

        private void save()
        {
            var overrides = effective
                .Where(b => GarbusInputManager.DefaultBindings[b.Key] != b.Value)
                .ToDictionary(b => b.Key.ToString(), b => b.Value.ToString());

            string text = JsonConvert.SerializeObject(overrides, Formatting.Indented);

            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream);
            writer.Write(text);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestKeyBindingStore`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Input/GarbusInputManager.cs Garbus.Game/Input/KeyBindingStore.cs Garbus.Game.Tests/Input/TestKeyBindingStore.cs
git commit -m "feat: add KeyBindingStore for persisted controller bindings"
```

---

### Task 2: Cache the store and wire it into gameplay input

**Files:**
- Modify: `Garbus.Game/GarbusGameBase.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneKeyBindingInput.cs`

**Interfaces:**
- Consumes: `KeyBindingStore(Storage)`, `GarbusInputManager`, `GarbusInputManager.DefaultBindings`.
- Produces: a DI-cached `KeyBindingStore` reachable by `GarbusInputManager` and `SettingsOverlay`.

- [ ] **Step 1: Write the failing input-manager test**

Create `Garbus.Game.Tests/Visual/TestSceneKeyBindingInput.cs`:

```csharp
// A GarbusInputManager resolves the cached KeyBindingStore and reflects its overrides; a bare one
// (no cached store) still uses DefaultKeyBindings.

using System.IO;
using System.Linq;
using Garbus.Game.Input;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneKeyBindingInput : GarbusTestScene
    {
        private DependencyContainer dependencies = null!;
        private string tempDir = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        [Test]
        public void TestInputManagerReflectsStoreOverride()
        {
            GarbusInputManager inputManager = null!;

            AddStep("cache overriding store + create input manager", () =>
            {
                tempDir = Directory.CreateTempSubdirectory("garbus-kb-").FullName;
                var store = new KeyBindingStore(new NativeStorage(tempDir));
                store.Rebind(GarbusAction.ButtonE1, InputKey.Joystick2);
                dependencies.CacheAs(store);

                Child = inputManager = new GarbusInputManager { RelativeSizeAxes = Axes.Both };
            });

            AddAssert("E1 bound to Joystick2", () =>
                inputManager.KeyBindings.Single(b => (GarbusAction)b.Action == GarbusAction.ButtonE1)
                    .KeyCombination.Keys.Single() == InputKey.Joystick2);

            AddStep("cleanup", () => Directory.Delete(tempDir, true));
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneKeyBindingInput`
Expected: FAIL — the game-level store isn't cached yet, and this test's cached store must be picked up; assertion fails because `KeyBindings` is null/defaults until the store is resolved. (If it compiles-and-fails on the assert, that's the expected red.)

- [ ] **Step 3: Cache the store in `GarbusGameBase`**

In `Garbus.Game/GarbusGameBase.cs`, inside `load(Storage storage)`, add the cache next to the existing `ChartStore` cache (after line 61, `dependencies.Cache(new ChartStore(Resources));`):

```csharp
            dependencies.Cache(new KeyBindingStore(storage));
```

Add the using at the top of the file if not already present:

```csharp
using Garbus.Game.Input;
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneKeyBindingInput`
Expected: PASS.

- [ ] **Step 5: Run the existing input/gameplay tests to confirm no regression**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestScenePlayScreen`
Expected: PASS (bare/cached input manager both still resolve bindings).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/GarbusGameBase.cs Garbus.Game.Tests/Visual/TestSceneKeyBindingInput.cs
git commit -m "feat: cache KeyBindingStore and drive gameplay input from it"
```

---

### Task 3: KeyBindingRow + ControlsPanel with capture and reset

**Files:**
- Create: `Garbus.Game/Settings/KeyBindingRow.cs`
- Create: `Garbus.Game/Settings/ControlsPanel.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneControlsPanel.cs`

**Interfaces:**
- Consumes: `KeyBindingStore`, `GarbusAction`, `GarbusInputManager.DefaultBindings`, `KeyCombination.FromJoystickButton`.
- Produces:
  - `KeyBindingRow(KeyBindingStore store, GarbusAction action)`.
  - `ControlsPanel(KeyBindingStore store, Action onBack)`.

- [ ] **Step 1: Write the failing panel test**

Create `Garbus.Game.Tests/Visual/TestSceneControlsPanel.cs`:

```csharp
// Clicking a row and pressing a gamepad button rebinds that action through the store; Reset restores it.

using System.IO;
using System.Linq;
using Garbus.Game.Input;
using Garbus.Game.Settings;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osuTK.Input;

namespace Garbus.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneControlsPanel : GarbusTestScene
    {
        private string tempDir = null!;
        private KeyBindingStore store = null!;
        private ControlsPanel panel = null!;
        private ManualInputManager manual = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create panel", () =>
            {
                tempDir = Directory.CreateTempSubdirectory("garbus-kb-").FullName;
                store = new KeyBindingStore(new NativeStorage(tempDir));
                Child = manual = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = panel = new ControlsPanel(store, () => { }),
                };
            });
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("cleanup", () => Directory.Delete(tempDir, true));

        private KeyBindingRow rowFor(GarbusAction action) =>
            panel.ChildrenOfType<KeyBindingRow>().Single(r => r.Action == action);

        [Test]
        public void TestClickAndPressRebinds()
        {
            AddStep("click E1 row", () =>
            {
                manual.MoveMouseTo(rowFor(GarbusAction.ButtonE1));
                manual.Click(MouseButton.Left);
            });
            AddStep("press joystick button 2", () => manual.PressJoystickButton(JoystickButton.Button2));
            AddStep("release", () => manual.ReleaseJoystickButton(JoystickButton.Button2));

            AddAssert("store E1 = Joystick2", () =>
                store.GetBinding(GarbusAction.ButtonE1) == InputKey.Joystick2);
        }

        [Test]
        public void TestResetRestoresDefaults()
        {
            AddStep("rebind E1", () => store.Rebind(GarbusAction.ButtonE1, InputKey.Joystick2));
            AddStep("click reset", () =>
            {
                var reset = panel.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "Reset to defaults");
                manual.MoveMouseTo(reset);
                manual.Click(MouseButton.Left);
            });
            AddAssert("store E1 back to default", () =>
                store.GetBinding(GarbusAction.ButtonE1) == InputKey.JoystickHat1Right);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneControlsPanel`
Expected: FAIL — `KeyBindingRow` / `ControlsPanel` do not exist (compile error).

- [ ] **Step 3: Implement `KeyBindingRow`**

Create `Garbus.Game/Settings/KeyBindingRow.cs`:

```csharp
// One remappable action: label on the left, current button on the right. Click to enter listening
// state, then the next gamepad button press is captured and written through the store. Any keyboard
// key (incl. Escape) or losing focus cancels without changing the binding.

using Garbus.Game.Input;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    public partial class KeyBindingRow : CompositeDrawable
    {
        public GarbusAction Action { get; }

        private readonly KeyBindingStore store;

        private Box background = null!;
        private SpriteText keyText = null!;
        private bool listening;

        private static readonly Color4 idle_colour = new Color4(40, 40, 52, 255);
        private static readonly Color4 listening_colour = new Color4(120, 90, 40, 255);

        public KeyBindingRow(KeyBindingStore store, GarbusAction action)
        {
            this.store = store;
            Action = action;

            RelativeSizeAxes = Axes.X;
            Height = 30;
        }

        public override bool AcceptsFocus => true;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both, Colour = idle_colour },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 8 },
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = Action.GetDescription(),
                            Font = FontUsage.Default.With(size: 15),
                            Colour = Color4.White,
                        },
                        keyText = new SpriteText
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Font = FontUsage.Default.With(size: 15),
                            Colour = new Color4(180, 180, 220, 255),
                        },
                    },
                },
            };

            updateText();
            store.Changed += onStoreChanged;
        }

        private void onStoreChanged() => updateText();

        private void updateText()
        {
            if (!listening)
                keyText.Text = store.GetBinding(Action).ToString();
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Clicking a focusable drawable focuses it; enter listening once focused.
            startListening();
            return true;
        }

        private void startListening()
        {
            listening = true;
            background.Colour = listening_colour;
            keyText.Text = "Press a button…";
        }

        private void stopListening()
        {
            listening = false;
            background.Colour = idle_colour;
            updateText();
        }

        protected override bool OnJoystickPress(JoystickPressEvent e)
        {
            if (!listening)
                return base.OnJoystickPress(e);

            store.Rebind(Action, KeyCombination.FromJoystickButton(e.Button));
            stopListening();
            GetContainingFocusManager()?.ChangeFocus(null);
            return true;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!listening)
                return base.OnKeyDown(e);

            // Any key cancels (keyboard binding is out of scope); consume Escape so the overlay stays open.
            stopListening();
            GetContainingFocusManager()?.ChangeFocus(null);
            return true;
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            if (listening)
                stopListening();
        }

        protected override void Dispose(bool isDisposing)
        {
            store.Changed -= onStoreChanged;
            base.Dispose(isDisposing);
        }
    }
}
```

- [ ] **Step 4: Implement `ControlsPanel`**

Create `Garbus.Game/Settings/ControlsPanel.cs`:

```csharp
// The rebind sub-view: a Back button, one KeyBindingRow per GarbusAction, and a Reset-to-defaults
// button. Given the store by its host (SettingsOverlay) so it stays test-constructible without DI.

using System;
using Garbus.Game.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    public partial class ControlsPanel : CompositeDrawable
    {
        private readonly KeyBindingStore store;
        private readonly Action onBack;

        public ControlsPanel(KeyBindingStore store, Action onBack)
        {
            this.store = store;
            this.onBack = onBack;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            flow.Add(new ClickableText("‹ Back", onBack));
            flow.Add(new SpriteText
            {
                Text = "Controls",
                Font = FontUsage.Default.With(size: 24),
                Colour = Color4.White,
            });

            foreach (GarbusAction action in Enum.GetValues<GarbusAction>())
                flow.Add(new KeyBindingRow(store, action));

            flow.Add(new ClickableText("Reset to defaults", store.ResetToDefaults));

            InternalChild = flow;
        }

        // A minimal text button: a label that runs an action on click.
        private partial class ClickableText : CompositeDrawable
        {
            private readonly string label;
            private readonly Action action;

            public ClickableText(string label, Action action)
            {
                this.label = label;
                this.action = action;

                RelativeSizeAxes = Axes.X;
                Height = 28;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = FontUsage.Default.With(size: 15),
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                action();
                return true;
            }
        }
    }
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneControlsPanel`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add Garbus.Game/Settings/KeyBindingRow.cs Garbus.Game/Settings/ControlsPanel.cs Garbus.Game.Tests/Visual/TestSceneControlsPanel.cs
git commit -m "feat: add controls rebind panel with capture and reset"
```

---

### Task 4: Wire the Controls panel into the settings overlay

**Files:**
- Modify: `Garbus.Game/Settings/SettingsOverlay.cs`
- Test: `Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs`

**Interfaces:**
- Consumes: `KeyBindingStore` (DI-cached), `ControlsPanel(KeyBindingStore, Action)`.
- Produces: a "Controls…" entry that swaps the overlay to the rebind view and a Back that returns.

- [ ] **Step 1: Write the failing overlay-navigation test**

Add to `Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs` (new test method inside the existing class; add `using Garbus.Game.Settings;` is already present, add `using osu.Framework.Graphics.Sprites;` and `using System;` if missing):

```csharp
        [Test]
        public void TestControlsButtonShowsRebindPanel()
        {
            AddStep("show", () => overlay.Show());
            AddAssert("no controls panel yet", () => !overlay.ChildrenOfType<ControlsPanel>().Any());

            AddStep("click Controls", () =>
            {
                var controls = overlay.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "Controls…");
                InputManager.MoveMouseTo(controls);
                InputManager.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("controls panel visible", () => overlay.ChildrenOfType<ControlsPanel>().Any());

            AddStep("click Back", () =>
            {
                var back = overlay.ChildrenOfType<SpriteText>().First(t => t.Text.ToString() == "‹ Back");
                InputManager.MoveMouseTo(back);
                InputManager.Click(osuTK.Input.MouseButton.Left);
            });
            AddUntilStep("controls panel gone", () => !overlay.ChildrenOfType<ControlsPanel>().Any());
        }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneSettingsOverlay.TestControlsButtonShowsRebindPanel`
Expected: FAIL — no "Controls…" text / no `ControlsPanel` in the tree.

- [ ] **Step 3: Add the Controls entry and view-swap to `SettingsOverlay`**

In `Garbus.Game/Settings/SettingsOverlay.cs`:

Add usings at the top:

```csharp
using Garbus.Game.Input;
```

Add a resolved store and view fields next to the existing `panel` field (after `private Container panel = null!;`):

```csharp
        [Resolved]
        private KeyBindingStore keyBindings { get; set; } = null!;

        private FillFlowContainer settingsView = null!;
        private ControlsPanel? controlsView;
```

Change the settings `FillFlowContainer` in `load()` to be captured in `settingsView` and add the Controls entry. Replace the `new FillFlowContainer { … }` block (currently the second child of `panel`) with:

```csharp
                    settingsView = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(20),
                        Spacing = new Vector2(0, 18),
                        Children = new Drawable[]
                        {
                            new SpriteText
                            {
                                Text = "Settings",
                                Font = FontUsage.Default.With(size: 28),
                                Colour = Color4.White,
                            },
                            createVolumeRow("Master volume", audio.Volume),
                            createVolumeRow("Music volume", audio.VolumeTrack),
                            createVolumeRow("Hitsound volume", audio.VolumeSample),
                            new SettingsSlider("Scroll speed", config.GetBindable<double>(GarbusSetting.ScrollSpeed), ScrollSpeedMapping.FormatSpeed),
                            new ControlsButton(showControls),
                        },
                    },
```

Add the view-swap methods and a small button type inside the class (e.g. after `percent`):

```csharp
        private void showControls()
        {
            settingsView.Hide();

            controlsView?.Expire();
            panel.Add(controlsView = new ControlsPanel(keyBindings, showSettings)
            {
                Padding = new MarginPadding(20),
            });
        }

        private void showSettings()
        {
            controlsView?.Expire();
            controlsView = null;
            settingsView.Show();
        }

        // A labelled row that opens the controls sub-view.
        private partial class ControlsButton : CompositeDrawable
        {
            private readonly Action onClick;

            public ControlsButton(Action onClick)
            {
                this.onClick = onClick;
                RelativeSizeAxes = Axes.X;
                Height = 30;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    new SpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Padding = new MarginPadding { Left = 8 },
                        Text = "Controls…",
                        Font = FontUsage.Default.With(size: 18),
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                onClick();
                return true;
            }
        }
```

Add the `System` using for `Action` at the top if not present:

```csharp
using System;
```

(Note: `using System;` is already imported in this file — verify before adding to avoid a duplicate.)

Reset the view to settings whenever the overlay opens, so it never re-opens on the controls page. In `PopIn()`, add as the first line:

```csharp
            showSettings();
```

- [ ] **Step 4: Run the new test to verify it passes**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneSettingsOverlay.TestControlsButtonShowsRebindPanel`
Expected: PASS.

- [ ] **Step 5: Run the full settings-overlay suite to confirm no regression**

Run: `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj --filter TestSceneSettingsOverlay`
Expected: PASS (existing volume/scroll tests + the new one).

- [ ] **Step 6: Build and run the whole test suite**

Run: `dotnet build Garbus.Desktop.slnf` then `dotnet test Garbus.Game.Tests\Garbus.Game.Tests.csproj`
Expected: build succeeds; all tests pass.

- [ ] **Step 7: Commit**

```bash
git add Garbus.Game/Settings/SettingsOverlay.cs Garbus.Game.Tests/Visual/TestSceneSettingsOverlay.cs
git commit -m "feat: reach controller remapping from the settings overlay"
```

---

## Self-Review Notes

- **Spec coverage:** store + JSON overrides (Task 1); input wiring + fallback + game cache (Task 2); expandable panel with per-action capture + reset + gamepad-only capture translation (Task 3); "Controls…" entry with slide-over + back (Task 4). All spec sections mapped.
- **Type consistency:** `KeyBindingStore` API (`GetBinding`/`GetKeyBindings`/`Rebind`/`ResetToDefaults`/`Changed`) and `GarbusInputManager.DefaultBindings` are defined in Task 1 and consumed unchanged in Tasks 2–4. `ControlsPanel(KeyBindingStore, Action)` and `KeyBindingRow(KeyBindingStore, GarbusAction)` signatures match between Task 3 definition and Task 4 use.
- **Capture translation:** `KeyCombination.FromJoystickButton` maps face buttons to `Joystick1..6` and d-pad hats to `JoystickHat1*`, exactly the default vocabulary — confirmed against osu-framework source.
- **Deferred:** conflict detection, keyboard bindings, multiple bindings per action, live reload — all explicitly out of scope.
