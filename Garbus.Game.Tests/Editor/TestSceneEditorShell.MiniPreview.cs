// Tests for the GarbusEditor shell: tab switching and dirty-state tracking.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Dialogs;
using Garbus.Game.Edit.Screens.Verify;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Framework.Testing.Input;
using osuTK.Input;

namespace Garbus.Game.Tests.Editor
{
        public partial class TestSceneEditorShell : GarbusTestScene
    {
        [Test]
        public void TestPreviewModesViaViewMenu()
        {
            InlineChartPreviewPanel panel = null!;
            CardinalNote note = null!;

            AddUntilStep("compose visible", () => editor.ChildrenOfType<ComposeTab>().Single().State.Value == Visibility.Visible);
            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            openViewMenu();

            AddAssert("mini preview checkbox exists", () => menuItem("Mini Preview"), () => Is.TypeOf<ToggleMenuItem>());
            AddAssert("mini preview checked by default", () => toggleItem("Mini Preview").State.Value);
            AddAssert("no preview submenu", () => editor.ChildrenOfType<Menu.DrawableMenuItem>()
                .All(i => i.Item.Text.Value.ToString() is not "Preview" and not "Hide" and not "Mini"));
            AddAssert("mini panel visible in compose", () =>
                panel.Alpha, () => Is.EqualTo(1));

            AddStep("add object before disabling mini preview", () =>
                editor.EditorChart.Add(note = new CardinalNote { StartTime = 1000, AngleDeg = 90 }));
            AddUntilStep("mini receives object before disabling", () => panel.ViewForTests.ObjectCountForTests == 1);

            clickMenuItem("Mini Preview");
            AddUntilStep("checkbox disables mini", () =>
                !toggleItem("Mini Preview").State.Value
                && panel.Alpha == 0);
            AddStep("remove object while mini disabled", () => editor.EditorChart.Remove(note));
            AddAssert("closed mini ignores edit while disabled", () => panel.ViewForTests.ObjectCountForTests, () => Is.EqualTo(1));

            clickMenuItem("Mini Preview");
            AddUntilStep("checkbox reopens mini with current state", () =>
                toggleItem("Mini Preview").State.Value
                && panel.Alpha == 1
                && panel.ViewForTests.ObjectCountForTests == 0);
        }

        [Test]
        public void TestMiniClosesAndResyncsAcrossInactiveModes()
        {
            InlineChartPreviewPanel panel = null!;
            CardinalNote note = null!;

            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);

            AddStep("add object before disabling mini preview", () =>
                editor.EditorChart.Add(note = new CardinalNote { StartTime = 1000, AngleDeg = 90 }));
            AddUntilStep("mini receives object before disabling", () => panel.ViewForTests.ObjectCountForTests == 1);

            setMiniPreviewEnabledStep(false);
            AddUntilStep("disabling closes mini", () => panel.Alpha == 0);
            AddStep("remove object while hidden", () => editor.EditorChart.Remove(note));
            AddAssert("closed mini ignores edit while disabled", () => panel.ViewForTests.ObjectCountForTests, () => Is.EqualTo(1));

            setMiniPreviewEnabledStep(true);
            AddUntilStep("enabling resyncs mini", () => panel.Alpha == 1 && panel.ViewForTests.ObjectCountForTests == 0);
        }

        [Test]
        public void TestMiniPreviewSupportsSharedSongTiming()
        {
            InlineChartPreviewPanel panel = null!;

            AddStep("load shared-timing song", () =>
            {
                GarbusSong song = GarbusSong.CreateDefault();
                song.Charts[0].HitObjects.Add(new CardinalNote { StartTime = 1000, AngleDeg = 90 });

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = stack = new ScreenStack(editor = new GarbusEditor(new SongFile(song)))
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            });

            AddUntilStep("shared-timing mini loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            AddUntilStep("shared-timing mini remains active", () =>
                panel.Alpha == 1 && panel.ViewForTests.ObjectCountForTests == 1);
        }

        [Test]
        public void TestMiniPreviewResyncsAndTracksSelectedChartStructure()
        {
            InlineChartPreviewPanel panel = null!;
            GarbusChart replacement = null!;

            AddStep("load two-chart song", () =>
            {
                var firstTiming = new ControlPointInfo();
                firstTiming.Add(0, new TimingControlPoint { BeatLength = 500 });
                var first = new GarbusChart
                {
                    ControlPointInfo = firstTiming,
                    HitObjects = [new CardinalNote { StartTime = 1000, AngleDeg = 90 }],
                };

                var replacementTiming = new ControlPointInfo();
                replacementTiming.Add(500, new TimingControlPoint { BeatLength = 300 });
                replacement = new GarbusChart
                {
                    ControlPointInfo = replacementTiming,
                    HitObjects = [new CardinalNote { StartTime = 3000, AngleDeg = 180 }],
                };
                replacement.DesignPointInfo.Add(new TutorialMessage
                {
                    StartTime = 0,
                    EndTime = 10000,
                    Text = "replacement",
                });

                var song = new GarbusSong
                {
                    ControlPointInfo = null,
                    Charts = [first, replacement],
                };

                Child = input = new ManualInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    UseParentInput = false,
                    Child = stack = new ScreenStack(editor = new GarbusEditor(new SongFile(song)))
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                };
            });

            AddUntilStep("two-chart mini loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true
                && panel.ViewForTests.ObjectCountForTests == 1);
            AddStep("select replacement chart", () => editor.EditorSong.SelectChart(replacement.ChartId));
            AddUntilStep("replacement mini state applied", () =>
                panel.ViewForTests.PlayfieldForTests.AllHitObjects.SingleOrDefault()?.HitObject.StartTime == 3000
                && panel.ViewForTests.DesignOverlayForTests.MessageTextForTests == "replacement");

            AddStep("change replacement design", () =>
            {
                replacement.DesignPointInfo.Clear();
                replacement.DesignPointInfo.Add(new TutorialMessage
                {
                    StartTime = 0,
                    EndTime = 10000,
                    Text = "updated replacement",
                });
            });
            AddUntilStep("replacement mini structure remains subscribed", () =>
                panel.ViewForTests.DesignOverlayForTests.MessageTextForTests == "updated replacement");
        }

        [Test]
        public void TestOnSuspendingClosesAndRestoresMiniPreview()
        {
            InlineChartPreviewPanel panel = null!;
            long revisionBeforeSuspension = 0;
            var reopenedFullStates = new List<ChartPreviewSnapshot>();

            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            AddStep("capture mini lifecycle state", () =>
            {
                revisionBeforeSuspension = panel.ViewForTests.AcceptedRevision;
                panel.ViewForTests.SnapshotReceivedForTests += reopenedFullStates.Add;
            });
            AddStep("push unrelated screen", () => stack.Push(new PreviewSuspendingScreen()));
            AddUntilStep("unrelated screen pushed", () => stack.CurrentScreen is PreviewSuspendingScreen);
            AddAssert("suspension disables mini preview", () => !editor.MiniPreviewEnabled.Value);
            AddAssert("suspension closes mini", () => panel.Alpha, () => Is.Zero);
            AddStep("edit chart while mini suspended", () =>
                editor.EditorChart.Add(new CardinalNote { StartTime = 2000, AngleDeg = 180 }));
            AddAssert("closed mini ignores suspended edit", () => panel.ViewForTests.ObjectCountForTests, () => Is.Zero);

            AddStep("exit unrelated screen", () => stack.CurrentScreen.Exit());
            AddUntilStep("editor resumed", () => ReferenceEquals(stack.CurrentScreen, editor));
            AddUntilStep("mini restored with current state", () =>
                editor.MiniPreviewEnabled.Value
                && panel.Alpha == 1
                && panel.ViewForTests.ObjectCountForTests == 1
                && reopenedFullStates.Count == 1);
            AddAssert("restored state advances revision", () => reopenedFullStates[0].Revision,
                () => Is.GreaterThan(revisionBeforeSuspension));
        }

        [Test]
        public void TestMiniPreviewFailureDisablesCheckboxAndShowsDialog()
        {
            InlineChartPreviewPanel panel = null!;

            AddUntilStep("mini preview loaded", () =>
                (panel = editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()!)?.ViewForTests.IsLoaded == true);
            AddStep("fail mini preview", () => panel.FailForTests("Fake mini failure."));
            AddUntilStep("one failure dialog", () =>
                editor.ChildrenOfType<ConfirmDialog>().Count(d => d.State.Value == Visibility.Visible) == 1);
            AddAssert("failure disables mini preview", () => !editor.MiniPreviewEnabled.Value);
            AddAssert("dialog uses Mini terminology", () =>
                editor.ChildrenOfType<SpriteText>().Any(text => text.Text.ToString().Contains("Mini preview failed")));
        }

        [Test]
        public void TestInlinePreviewContentDisposesWithEditorHierarchy()
        {
            ChartPreviewContent content = null!;
            ChartFile chartFile = null!;
            bool disposedBeforeEditorCleanup = false;
            int disposeCount = 0;

            AddUntilStep("inline preview content loaded", () =>
                editor.ChildrenOfType<InlineChartPreviewPanel>().SingleOrDefault()?.ViewForTests.IsLoaded == true);
            AddStep("capture inline content and chart file", () =>
            {
                content = editor.ChildrenOfType<InlineChartPreviewPanel>().Single().ViewForTests;
                chartFile = editor.ChartFile;
                Action onDispose = () =>
                {
                    disposeCount++;
                    disposedBeforeEditorCleanup = !chartFile.IsDisposed;
                };
                typeof(Drawable).GetEvent("OnDispose",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!
                    .GetAddMethod(true)!
                    .Invoke(content, new object[] { onDispose });
            });
            AddStep("dispose editor hierarchy", () => input.Child = new Container());
            AddUntilStep("editor and inline content disposed", () => chartFile.IsDisposed && isDisposed(content));
            AddAssert("inline content disposed once within editor cleanup", () =>
            {
                return disposeCount == 1 && disposedBeforeEditorCleanup;
            });
        }
    }
}
