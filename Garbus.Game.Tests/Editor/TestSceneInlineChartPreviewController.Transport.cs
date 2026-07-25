using System.Diagnostics;
using System.Linq;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor;

public partial class TestSceneInlineChartPreviewController
{
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
            appliedMessages.Count == 1
            && !appliedMessages.Single().Upserts.IsEmpty
            && appliedMessages.Single().Transport.HasValue);
        AddAssert("object and transport share one atomic batch", () => appliedMessages, () => Has.Count.EqualTo(1));
        AddAssert("transport observes updated object", () => objectUpdatedWhenTransportApplied);
        AddAssert("transport applies seek time", () => preview.ClockTimeForTests,
            () => Is.EqualTo(2500).Within(0.001));
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
            holdTimestamp();
        });
        AddStep("seek while running", () => editorClock.Seek(4000));
        AddUntilStep("discrete seek transport applied immediately", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Count() == 1);
        AddStep("defer cadence and change rate", () =>
        {
            holdTimestamp();
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
        AddStep("defer transport cadence", holdTimestamp);
        AddStep("begin and retarget stopped smooth seek", () =>
        {
            editorClock.SeekSmoothlyTo(2000);
            editorClock.SeekSmoothlyTo(2100);
        });
        AddWaitStep("process below-cadence seeks", 1);
        AddAssert("no early smooth-seek transport", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue), () => Is.Empty);
        AddStep("make cadence due", advanceTimestamp);
        AddUntilStep("one smooth-seek transport applied", () =>
            appliedMessages.Where(batch => batch.Transport.HasValue).Count() == 1);
        AddStep("defer cadence and retarget again", () =>
        {
            holdTimestamp();
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
            holdTimestamp();
        });
        AddWaitStep("run frames below cadence", 5);
        AddAssert("no early running transport", () => preview.AcceptedRevision,
            () => Is.EqualTo(revisionAfterStart));
        AddStep("make cadence due", advanceTimestamp);
        AddUntilStep("one cadence transport applied", () =>
            preview.AcceptedRevision == revisionAfterStart + 1);
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
            holdTimestamp();
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
}
