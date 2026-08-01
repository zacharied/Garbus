// Regression test for the View menu's config-backed toggles ("Auto-Seek on Placement" and friends)
// silently doing nothing.
//
// Root cause: GarbusEditor.createViewMenuItems() passed `config.GetBindable<bool>(...)` straight into
// the ToggleMenuItem constructor. GetBindable returns a bound copy that the config holds only via a
// WEAK reference, and Bindable.BindTo also links weakly in both directions — so once
// createViewMenuItems() returned, nothing held the copy strongly and the next GC collected it. The
// menu item's own State survived (it is a readonly field), so the checkbox kept flipping visually
// while the write never reached the config. Fix: keep the copies alive in fields on GarbusEditor.
//
// This is the same defect ComposeTab already fixed one leg downstream (see TestSceneComposeTabConfig).
//
// This test hosts the REAL GarbusEditor, forces a GC after load, then flips each toggle's State (what
// clicking the item does via its Action) and asserts the config value follows.

using System;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Configuration;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Testing;

namespace Garbus.Game.Tests.Editor
{
    [TestFixture]
    public partial class TestSceneEditorViewMenuConfig : GarbusTestScene
    {
        [Resolved]
        private GarbusConfigManager config { get; set; } = null!;

        private GarbusEditor editor = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            // The config manager is shared across the test run; start each case from the defaults.
            config.SetValue(GarbusSetting.EditorShowTicks, true);
            config.SetValue(GarbusSetting.EditorShowTimingChanges, true);
            config.SetValue(GarbusSetting.EditorShowDesignRegions, true);
            config.SetValue(GarbusSetting.EditorAutoSeekOnPlacement, true);

            var chart = new GarbusChart();
            chart.ControlPointInfo!.Add(0, new TimingControlPoint { BeatLength = 500 });

            editor = new GarbusEditor(new ChartFile(chart));
            Child = new ScreenStack(editor) { RelativeSizeAxes = Axes.Both };
        });

        [TestCase("Auto-Seek on Placement", GarbusSetting.EditorAutoSeekOnPlacement)]
        [TestCase("Show Beat Ticks", GarbusSetting.EditorShowTicks)]
        [TestCase("Show Timing Changes", GarbusSetting.EditorShowTimingChanges)]
        [TestCase("Show Design Regions", GarbusSetting.EditorShowDesignRegions)]
        public void TestViewToggleWritesToConfigAfterGc(string label, GarbusSetting setting)
        {
            AddUntilStep("editor loaded", () => editor.IsLoaded);

            AddAssert("setting initially on", () => config.Get<bool>(setting));

            // Collect any config bindable copy that is only weakly referenced. If the menu item's
            // bindable is not held strongly anywhere, this severs the wiring to the config.
            AddStep("force GC", () =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            });

            AddStep("toggle off", () => viewToggle(label).State.Value = false);
            AddAssert("config followed to off", () => !config.Get<bool>(setting));

            AddStep("toggle on", () => viewToggle(label).State.Value = true);
            AddAssert("config followed to on", () => config.Get<bool>(setting));
        }

        /// <summary>Finds a <see cref="ToggleMenuItem"/> in the View menu by its displayed text.</summary>
        private ToggleMenuItem viewToggle(string label) =>
            editor.ChildrenOfType<GarbusMenu>().First()
                  .Items.Single(i => i.Text.Value.ToString() == "View")
                  .Items.OfType<ToggleMenuItem>()
                  .Single(i => i.Text.Value.ToString() == label);
    }
}
