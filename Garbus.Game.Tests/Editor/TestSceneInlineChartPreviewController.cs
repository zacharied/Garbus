using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using Garbus.Game.Charts.Format;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Objects;
using Garbus.Game.Tests.Visual;
using Garbus.Game.Timing;
using NUnit.Framework;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Editor;

[TestFixture]
public partial class TestSceneInlineChartPreviewController : GarbusTestScene
{
    private const int max_pending_object_deltas = 4096;

    private readonly CardinalNote initialNote = new() { StartTime = 1000, AngleDeg = 45 };

    private EditorChart editorChart = null!;
    private EditorClock editorClock = null!;
    private GarbusChartChangeHandler changeHandler = null!;
    private GarbusScrollingInfo scrollingInfo = null!;
    private ChartPreviewContent preview = null!;
    private InlineChartPreviewController controller = null!;
    private readonly List<ChartPreviewSnapshot> fullStates = new();
    private readonly List<ChartPreviewBatch> appliedMessages = new();
    private BindableDouble rateAdjustment = null!;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create mini controller", () =>
        {
            fullStates.Clear();
            appliedMessages.Clear();
            initialNote.StartTime = 1000;
            initialNote.AngleDeg = 45;

            var chart = new GarbusChart
            {
                HitObjects = [initialNote],
            };
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });

            editorChart = new EditorChart(chart);
            editorClock = new EditorClock(chart.ControlPointInfo, 60000);
            editorClock.ChangeSource(new TrackVirtual(60000));
            rateAdjustment = new BindableDouble(1) { MinValue = 0.25, MaxValue = 2 };
            editorClock.AudioAdjustments.AddAdjustment(AdjustableProperty.Tempo, rateAdjustment);
            changeHandler = new GarbusChartChangeHandler(editorChart);
            scrollingInfo = new GarbusScrollingInfo();
            preview = new ChartPreviewContent
            {
                Size = new Vector2(ChartPreviewContent.TARGET_DRAW_SIZE),
            };
            preview.SnapshotReceivedForTests += fullStates.Add;
            preview.BatchAppliedForTests += appliedMessages.Add;
            controller = new InlineChartPreviewController(
                editorChart,
                editorClock,
                changeHandler,
                scrollingInfo,
                preview);

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = [editorClock, preview, controller],
            };
        });
        AddUntilStep("mini content loaded", () => preview.IsLoaded);
    }

    [Test]
    public void TestFullStateUsesEffectiveSharedTiming()
    {
        AddStep("rebind chart to shared timing", () =>
        {
            var sharedTiming = new ControlPointInfo();
            sharedTiming.Add(250, new TimingControlPoint { BeatLength = 400 });
            var sharedChart = new GarbusChart
            {
                ControlPointInfo = null,
                HitObjects = [new CardinalNote { StartTime = 2000, AngleDeg = 90 }],
            };

            editorChart.Rebind(sharedChart, sharedTiming);
            editorClock.ControlPointInfo = sharedTiming;
        });

        openMini();
        AddAssert("full state contains effective timing", () =>
            fullStates.Single().Structure.ControlPointInfo.TimingPoints.Single().Time,
            () => Is.EqualTo(250));
    }

    [Test]
    public void TestTransportCapturedAfterPendingChartChanges()
    {
        bool objectUpdatedWhenTransportApplied = false;

        openMini();
        AddStep("observe transport apply", () => preview.BatchAppliedForTests += batch =>
        {
            if (batch.Transport.HasValue)
            {
                objectUpdatedWhenTransportApplied =
                    ((CardinalNote)preview.PlayfieldForTests.AllHitObjects.Single().HitObject).AngleDeg == 180;
            }
        });
        AddStep("update object and seek in one frame", () =>
        {
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
            editorClock.Seek(2500);
        });
        AddUntilStep("object and transport applied", () =>
            appliedMessages.Any(batch => !batch.Upserts.IsEmpty)
            && appliedMessages.Any(batch => batch.Transport.HasValue));
        AddAssert("object applied before transport", () =>
            appliedMessages.FindIndex(batch => !batch.Upserts.IsEmpty),
            () => Is.LessThan(appliedMessages.FindIndex(batch => batch.Transport.HasValue)));
        AddAssert("transport observes updated object", () => objectUpdatedWhenTransportApplied);
        AddAssert("transport applies seek time", () => preview.ClockTimeForTests,
            () => Is.EqualTo(2500).Within(0.001));
    }

    [Test]
    public void TestChartRebindSendsAuthoritativeFullState()
    {
        GarbusChart replacement = null!;

        openMini();
        AddStep("rebind to replacement chart", () =>
        {
            replacement = new GarbusChart
            {
                HitObjects = [new CardinalNote { StartTime = 3000, AngleDeg = 180 }],
            };
            replacement.ControlPointInfo.Add(500, new TimingControlPoint { BeatLength = 300 });
            replacement.DesignPointInfo.Add(new TutorialMessage
            {
                StartTime = 0,
                EndTime = 10000,
                Text = "replacement",
            });

            editorChart.Rebind(replacement, replacement.ControlPointInfo);
            editorClock.ControlPointInfo = replacement.ControlPointInfo;
        });

        AddUntilStep("replacement full state received", () => fullStates.Count == 2);
        AddAssert("replacement structure is authoritative", () =>
        {
            ChartPreviewSnapshot received = fullStates.Last();
            return received.Objects.Single().HitObject.StartTime == 3000
                   && received.Structure.ControlPointInfo.TimingPoints.Single().Time == 500
                   && received.Structure.DesignPointInfo.DesignPoints.OfType<TutorialMessage>().Single().Text == "replacement";
        });
        AddUntilStep("replacement content applied", () =>
            preview.PlayfieldForTests.AllHitObjects.Single().HitObject.StartTime == 3000
            && preview.DesignOverlayForTests.MessageTextForTests == "replacement");
    }

    [Test]
    public void TestSameFrameUpsertsCoalesce()
    {
        long revisionBeforeUpdate = 0;

        openMini();
        AddStep("capture revision", () => revisionBeforeUpdate = preview.AcceptedRevision);
        AddStep("update same object three times", () =>
        {
            initialNote.AngleDeg = 90;
            editorChart.Update(initialNote);
            initialNote.AngleDeg = 135;
            editorChart.Update(initialNote);
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
        });
        AddUntilStep("coalesced upsert applied", () =>
            preview.AcceptedRevision == revisionBeforeUpdate + 1
            && ((CardinalNote)preview.PlayfieldForTests.AllHitObjects.Single().HitObject).AngleDeg == 180);
        AddWaitStep("allow another frame", 2);
        AddAssert("only one revision emitted", () => preview.AcceptedRevision,
            () => Is.EqualTo(revisionBeforeUpdate + 1));
    }

    [Test]
    public void TestPendingDeltaOverflowFallsBackToFullState()
    {
        openMini();
        AddStep("add objects beyond delta bound", () =>
        {
            for (int i = 0; i <= max_pending_object_deltas; i++)
                editorChart.Add(new CardinalNote { StartTime = 2000 + i, AngleDeg = i % 360 });
        });
        AddUntilStep("authoritative replacement received", () => fullStates.Count == 2);
        AddUntilStep("replacement contains every object", () =>
            preview.ObjectCountForTests == max_pending_object_deltas + 2);
        AddAssert("overflow used one full-state revision", () => preview.AcceptedRevision,
            () => Is.EqualTo(2));
    }

    [Test]
    public void TestRemoveBeforeUnsentUpsertDoesNotEmitStaleObject()
    {
        var transient = new CardinalNote { StartTime = 2000, AngleDeg = 90 };
        long revisionBeforeChanges = 0;

        openMini();
        AddStep("capture revision", () => revisionBeforeChanges = preview.AcceptedRevision);
        AddStep("add and remove object in one frame", () =>
        {
            editorChart.Add(transient);
            editorChart.Remove(transient);
        });
        AddWaitStep("allow pending changes to flush", 2);
        AddAssert("transient object was never emitted", () =>
            preview.ObjectCountForTests == 1
            && preview.AcceptedRevision == revisionBeforeChanges);
        AddAssert("unsent reference released", objectIdCount, () => Is.EqualTo(1));
    }

    [Test]
    public void TestObjectIdsUseReferenceIdentityAndRemainStable()
    {
        var firstDuplicate = new CardinalNote { StartTime = 2000, AngleDeg = 90 };
        var secondDuplicate = new CardinalNote { StartTime = 2000, AngleDeg = 90 };
        long firstId = 0;
        long secondId = 0;

        openMini();
        AddStep("add equal-valued objects", () =>
        {
            editorChart.Add(firstDuplicate);
            editorChart.Add(secondDuplicate);
        });
        AddUntilStep("both equal-valued objects applied", () =>
            appliedMessages.SelectMany(batch => batch.Upserts).Count() == 2);
        AddStep("capture distinct ids", () =>
        {
            PreviewObjectState[] additions = appliedMessages.SelectMany(batch => batch.Upserts).ToArray();
            firstId = additions[0].Id.Value;
            secondId = additions[1].Id.Value;
        });
        AddAssert("equal values have distinct reference ids", () => firstId, () => Is.Not.EqualTo(secondId));
        AddStep("update first object", () =>
        {
            firstDuplicate.AngleDeg = 180;
            editorChart.Update(firstDuplicate);
        });
        AddUntilStep("first update keeps id", () =>
            appliedMessages.SelectMany(batch => batch.Upserts).Count(upsert => upsert.Id.Value == firstId) == 2);
        AddStep("remove first object", () => editorChart.Remove(firstDuplicate));
        AddUntilStep("first remove keeps id", () =>
            appliedMessages.SelectMany(batch => batch.Removes).Any(remove => remove.Value == firstId));
    }

    [Test]
    public void TestFlushedRemoveReleasesObjectIdReference()
    {
        var removedNote = new CardinalNote { StartTime = 2000, AngleDeg = 90 };

        openMini();
        AddStep("add note", () => editorChart.Add(removedNote));
        AddUntilStep("note applied", () => preview.ObjectCountForTests == 2);
        AddStep("remove note", () => editorChart.Remove(removedNote));
        AddUntilStep("remove applied", () => preview.ObjectCountForTests == 1);
        AddAssert("removed reference released", objectIdCount, () => Is.EqualTo(1));
    }

    [Test]
    public void TestResyncFullStateReleasesQueuedRemoveIdentity()
    {
        var removedNote = new CardinalNote { StartTime = 2000, AngleDeg = 90 };
        var laterNote = new CardinalNote { StartTime = 3000, AngleDeg = 135 };
        long removedId = 0;

        openMini();
        AddStep("add note", () => editorChart.Add(removedNote));
        AddUntilStep("note applied", () => preview.ObjectCountForTests == 2);
        AddStep("capture removed identity", () => removedId = objectId(removedNote));
        AddStep("queue remove then request resync", () =>
        {
            editorChart.Remove(removedNote);
            Assert.That(preview.Apply(invalidBatch(0)), Is.False);
        });
        AddUntilStep("authoritative replacement received", () => fullStates.Count == 2);
        AddAssert("queued remove reference released", objectIdCount, () => Is.EqualTo(1));
        AddStep("add later note", () => editorChart.Add(laterNote));
        AddUntilStep("later note applied", () => preview.ObjectCountForTests == 2);
        AddAssert("released identity is not reused", () => objectId(laterNote), () => Is.GreaterThan(removedId));
    }

    [Test]
    public void TestRemoveWhileFullStatePendingReleasesIdentity()
    {
        openMini();
        AddStep("request resync then remove note", () =>
        {
            Assert.That(preview.Apply(invalidBatch(0)), Is.False);
            editorChart.Remove(initialNote);
        });
        AddUntilStep("authoritative empty state received", () =>
            fullStates.Count == 2 && preview.ObjectCountForTests == 0);
        AddAssert("removed reference released", objectIdCount, () => Is.Zero);
    }

    [Test]
    public void TestChartReplacementFullStateReconcilesObjectIdentities()
    {
        var replacedNote = new CardinalNote { StartTime = 2000, AngleDeg = 90 };
        var replacementNote = new CardinalNote { StartTime = 3000, AngleDeg = 135 };
        long survivingId = 0;
        long replacedId = 0;

        openMini();
        AddStep("add note to replace", () => editorChart.Add(replacedNote));
        AddUntilStep("note applied", () => preview.ObjectCountForTests == 2);
        AddStep("capture original identities", () =>
        {
            survivingId = objectId(initialNote);
            replacedId = objectId(replacedNote);
        });
        AddStep("rebind with surviving and replacement notes", () =>
        {
            var replacement = new GarbusChart
            {
                HitObjects = [initialNote, replacementNote],
            };
            replacement.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            editorChart.Rebind(replacement, replacement.ControlPointInfo);
            editorClock.ControlPointInfo = replacement.ControlPointInfo;
        });
        AddUntilStep("replacement full state received", () => fullStates.Count == 2);
        AddAssert("replacement owns only current identities", objectIdCount, () => Is.EqualTo(2));
        AddAssert("surviving identity remains stable", () => objectId(initialNote), () => Is.EqualTo(survivingId));
        AddAssert("replacement identity is monotonic", () => objectId(replacementNote), () => Is.GreaterThan(replacedId));
    }

    [Test]
    public void TestRemovesApplyBeforeUpserts()
    {
        var replacement = new CardinalNote { StartTime = 3000, AngleDeg = 135 };

        openMini();
        AddStep("remove and add in one frame", () =>
        {
            editorChart.Remove(initialNote);
            editorChart.Add(replacement);
        });
        AddUntilStep("both deltas applied", () =>
            appliedMessages.Any(batch => !batch.Removes.IsEmpty)
            && appliedMessages.Any(batch => !batch.Upserts.IsEmpty));
        AddAssert("remove precedes upsert", () =>
            appliedMessages.FindIndex(batch => !batch.Removes.IsEmpty),
            () => Is.LessThan(appliedMessages.FindIndex(batch => !batch.Upserts.IsEmpty)));
    }

    [Test]
    public void TestStructuralStateSuppressionAndPropagation()
    {
        openMini();
        AddStep("change only object state", () =>
        {
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
        });
        AddUntilStep("object upsert applied", () =>
            appliedMessages.SelectMany(batch => batch.Upserts).Count() == 1);
        AddWaitStep("allow structural check", 2);
        AddAssert("object-only state emits no structural state", () =>
            appliedMessages.Where(batch => batch.Structure != null), () => Is.Empty);
        AddStep("change timing", () =>
            editorChart.ControlPointInfo.Add(2000, new TimingControlPoint { BeatLength = 400 }));
        AddUntilStep("timing state applied", () =>
            appliedMessages.Where(batch => batch.Structure != null).Count() == 1
            && previewChart().ControlPointInfo!.TimingPoints.Any(point => point.Time == 2000));
        AddStep("change design", () =>
        {
            editorChart.DesignPointInfo.Add(new TutorialMessage
            {
                StartTime = 0,
                EndTime = 10000,
                Text = "updated structure",
            });
        });
        AddUntilStep("design state applied", () =>
            appliedMessages.Where(batch => batch.Structure != null).Count() == 2
            && preview.DesignOverlayForTests.MessageTextForTests == "updated structure");
        AddStep("change metadata", () =>
        {
            editorChart.BeginChange();
            editorChart.Metadata.Title = "Updated title";
            editorChart.SaveState();
            editorChart.EndChange();
        });
        AddUntilStep("metadata state applied", () =>
            appliedMessages.Where(batch => batch.Structure != null).Count() == 3
            && previewChart().Metadata.Title == "Updated title");
    }

    [Test]
    public void TestScrollSpeedPropagates()
    {
        openMini();
        AddStep("change scroll speed", () => scrollingInfo.TimeRange.Value = 1234);
        AddUntilStep("scroll speed applied", () =>
            appliedMessages.Where(batch => batch.TimeRange.HasValue).Count() == 1
            && preview.CurrentTimeRangeForTests == 1234);
    }

    [Test]
    public void TestDiscreteSeekAndRateChangesSendImmediateTransport()
    {
        openMini();
        AddStep("start editor clock", () => editorClock.Start());
        AddUntilStep("start transport applied", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Count() == 1);
        AddStep("defer transport cadence", () =>
        {
            appliedMessages.Clear();
            setLastTransportTimestamp(Stopwatch.GetTimestamp() + Stopwatch.Frequency);
        });
        AddStep("seek while running", () => editorClock.Seek(4000));
        AddUntilStep("discrete seek transport applied immediately", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Count() == 1);
        AddStep("defer cadence and change rate", () =>
        {
            setLastTransportTimestamp(Stopwatch.GetTimestamp() + Stopwatch.Frequency);
            rateAdjustment.Value = 1.5;
        });
        AddUntilStep("rate transport applied immediately", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Count() == 2);
        AddAssert("rate transport has requested rate", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Last().Transport!.Value.Rate,
            () => Is.EqualTo(1.5));
    }

    [Test]
    public void TestStoppedSmoothSeekTransportRemainsBounded()
    {
        openMini();
        AddStep("defer transport cadence", () =>
            setLastTransportTimestamp(Stopwatch.GetTimestamp() + Stopwatch.Frequency));
        AddStep("begin and retarget stopped smooth seek", () =>
        {
            editorClock.SeekSmoothlyTo(2000);
            editorClock.SeekSmoothlyTo(2100);
        });
        AddWaitStep("process below-cadence seeks", 1);
        AddAssert("no early smooth-seek transport", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue), () => Is.Empty);
        AddStep("make cadence due", () =>
            setLastTransportTimestamp(Stopwatch.GetTimestamp() - Stopwatch.Frequency));
        AddUntilStep("one smooth-seek transport applied", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Count() == 1);
        AddStep("defer cadence and retarget again", () =>
        {
            setLastTransportTimestamp(Stopwatch.GetTimestamp() + Stopwatch.Frequency);
            editorClock.SeekSmoothlyTo(2200);
        });
        AddWaitStep("process second below-cadence seek", 1);
        AddAssert("smooth-seek transport remains capped", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Count(), () => Is.EqualTo(1));
    }

    [Test]
    public void TestRunningTransportCadenceRemainsBounded()
    {
        long revisionAfterStart = 0;

        openMini();
        AddStep("start editor clock", () => editorClock.Start());
        AddUntilStep("start transport applied", () => preview.AcceptedRevision == 2);
        AddStep("defer cadence timestamp", () =>
        {
            revisionAfterStart = preview.AcceptedRevision;
            setLastTransportTimestamp(Stopwatch.GetTimestamp() + Stopwatch.Frequency);
        });
        AddWaitStep("run frames below cadence", 5);
        AddAssert("no early running transport", () => preview.AcceptedRevision,
            () => Is.EqualTo(revisionAfterStart));
        AddStep("make cadence due", () => setLastTransportTimestamp(Stopwatch.GetTimestamp() - Stopwatch.Frequency));
        AddUntilStep("one cadence transport applied", () =>
            preview.AcceptedRevision == revisionAfterStart + 1);
    }

    [Test]
    public void TestResyncRequestProducesFullState()
    {
        openMini();
        AddStep("apply stale state", () =>
            Assert.That(preview.Apply(invalidBatch(0)), Is.False));
        AddUntilStep("replacement full state received", () => fullStates.Count == 2);
        AddAssert("replacement full state advances revision", () => fullStates[1].Revision,
            () => Is.GreaterThan(fullStates[0].Revision));
    }

    [Test]
    public void TestRejectedSeeksDoNotAdvanceMiniTransportOrRevision()
    {
        bool seekResult = true;
        int seekingStateChanges = 0;
        long producerRevisionBeforeSeek = 0;
        long acceptedRevisionBeforeSeek = 0;

        openMini();
        AddStep("use stopped rejecting source", () =>
        {
            useRejectingClockSource();
            editorClock.SeekingOrStopped.ValueChanged += _ => seekingStateChanges++;
            producerRevisionBeforeSeek = controllerRevision();
            acceptedRevisionBeforeSeek = preview.AcceptedRevision;
        });
        AddStep("reject stopped mini seek", () => seekResult = editorClock.Seek(1000));
        AddAssert("stopped seek rejected", () => seekResult, () => Is.False);
        AddAssert("stopped rejection does not enter seeking", () => editorClock.IsSeeking, () => Is.False);
        AddAssert("stopped rejection emits no seeking state", () => seekingStateChanges, () => Is.Zero);
        AddWaitStep("allow stopped mini transport update", 2);
        AddAssert("stopped rejection keeps mini producer revision", controllerRevision,
            () => Is.EqualTo(producerRevisionBeforeSeek));
        AddAssert("stopped rejection keeps mini accepted revision", () => preview.AcceptedRevision,
            () => Is.EqualTo(acceptedRevisionBeforeSeek));

        AddStep("start rejecting source", () => editorClock.Start());
        AddUntilStep("mini clock playing without seeking", () => editorClock.IsRunning && !editorClock.SeekingOrStopped.Value);
        AddUntilStep("mini start transport applied", () => preview.AcceptedRevision > acceptedRevisionBeforeSeek);
        AddStep("capture playing mini revisions", () =>
        {
            seekingStateChanges = 0;
            producerRevisionBeforeSeek = controllerRevision();
            acceptedRevisionBeforeSeek = preview.AcceptedRevision;
            setLastTransportTimestamp(Stopwatch.GetTimestamp() + Stopwatch.Frequency);
        });
        AddStep("reject playing mini seek", () => seekResult = editorClock.Seek(2000));
        AddAssert("playing seek rejected", () => seekResult, () => Is.False);
        AddAssert("playing rejection does not enter seeking", () => editorClock.IsSeeking, () => Is.False);
        AddAssert("playing rejection keeps non-seeking state", () => editorClock.SeekingOrStopped.Value, () => Is.False);
        AddAssert("playing rejection emits no seeking state", () => seekingStateChanges, () => Is.Zero);
        AddWaitStep("allow playing mini transport update", 2);
        AddAssert("playing rejection keeps mini producer revision", controllerRevision,
            () => Is.EqualTo(producerRevisionBeforeSeek));
        AddAssert("playing rejection keeps mini accepted revision", () => preview.AcceptedRevision,
            () => Is.EqualTo(acceptedRevisionBeforeSeek));
    }

    private void openMini()
    {
        AddStep("open mini", () => controller.Open());
        AddUntilStep("authoritative full state applied", () =>
            controller.Enabled && fullStates.Count == 1 && preview.AcceptedRevision == 1);
    }

    private GarbusChart previewChart() => preview.CurrentChart;

    private static ChartPreviewBatch invalidBatch(long revision) => new(
        revision,
        ImmutableArray<PreviewObjectId>.Empty,
        ImmutableArray<PreviewObjectState>.Empty,
        null,
        null,
        new PreviewTransportState(1000, false, 1, 0));

    private long controllerRevision() =>
        (long)typeof(InlineChartPreviewController)
            .GetField("revision", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!;

    private int objectIdCount() =>
        ((System.Collections.IDictionary)typeof(InlineChartPreviewController)
            .GetField("objectIds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!).Count;

    private long objectId(GarbusHitObject hitObject) =>
        (long)((System.Collections.IDictionary)typeof(InlineChartPreviewController)
            .GetField("objectIds", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller)!)![hitObject]!;

    private void setLastTransportTimestamp(long timestamp) =>
        typeof(InlineChartPreviewController)
            .GetField("lastTransportTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(controller, timestamp);

    private void useRejectingClockSource()
    {
        editorClock.Stop();

        var underlyingClock = (FramedChartClock)typeof(EditorClock)
            .GetField("underlyingClock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(editorClock)!;
        underlyingClock.ChangeSource(new RejectingAdjustableClock());
        object decoupledTrack = typeof(FramedChartClock)
            .GetField("decoupledTrack", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(underlyingClock)!;
        decoupledTrack.GetType().GetProperty("AllowDecoupling")!.SetValue(decoupledTrack, false);
    }

    private sealed class RejectingAdjustableClock : IAdjustableClock
    {
        public double CurrentTime => 0;

        public bool IsRunning { get; private set; }

        public double Rate { get; set; } = 1;

        public bool Seek(double position) => false;

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Reset() => IsRunning = false;

        public void ResetSpeedAdjustments()
        {
        }
    }
}
