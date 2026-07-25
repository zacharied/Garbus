using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit.Preview;
using Garbus.Game.Objects;
using NUnit.Framework;

namespace Garbus.Game.Tests.Editor;

public partial class TestSceneInlineChartPreviewController
{
    [Test]
    public void TestSnapshotsAndBatchesOwnDetachedState()
    {
        openMini();
        AddAssert("snapshot object is detached", () => fullStates.Single().Objects.Single().HitObject,
            () => Is.Not.SameAs(initialNote));
        AddAssert("snapshot metadata is detached", () => fullStates.Single().Structure.Metadata,
            () => Is.Not.SameAs(editorChart.Metadata));
        AddAssert("snapshot timing is detached", () => fullStates.Single().Structure.ControlPointInfo,
            () => Is.Not.SameAs(editorChart.ControlPointInfo));
        AddAssert("snapshot design is detached", () => fullStates.Single().Structure.DesignPointInfo,
            () => Is.Not.SameAs(editorChart.DesignPointInfo));

        AddStep("change object and timing in one frame", () =>
        {
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
            editorChart.ControlPointInfo.Add(2000, new TimingControlPoint { BeatLength = 400 });
        });
        AddUntilStep("detached typed batch applied", () =>
            appliedMessages.Count == 1
            && !appliedMessages.Single().Upserts.IsEmpty
            && appliedMessages.Single().Structure != null);
        AddAssert("batch object is detached", () => appliedMessages.Single().Upserts.Single().HitObject,
            () => Is.Not.SameAs(initialNote));
        AddAssert("batch timing is detached", () => appliedMessages.Single().Structure!.ControlPointInfo,
            () => Is.Not.SameAs(editorChart.ControlPointInfo));
    }

    [Test]
    public void TestRejectedBatchCommitsNothingAndImmediatelyResnapshots()
    {
        var added = new CardinalNote { StartTime = 2000, AngleDeg = 90 };
        long producerRevisionAtRejection = 0;
        long consumerRevisionAtRejection = 0;
        int trackedObjectsAtRejection = 0;
        long rejectedCandidateId = 0;

        openMini();
        AddStep("prepare one rejected batch", () =>
        {
            previewSink.Attempts.Clear();
            previewSink.RejectNextBatch = true;
            previewSink.BatchAttempted = batch =>
            {
                producerRevisionAtRejection = controllerRevision();
                consumerRevisionAtRejection = preview.AcceptedRevision;
                trackedObjectsAtRejection = objectIdCount();
                rejectedCandidateId = batch.Upserts.Single().Id.Value;
            };
        });
        AddStep("add object for rejected batch", () => editorChart.Add(added));
        AddUntilStep("authoritative recovery applied", () =>
            fullStates.Count == 2 && preview.ObjectCountForTests == 2);

        AddAssert("rejected apply did not advance producer", () => producerRevisionAtRejection, () => Is.EqualTo(1));
        AddAssert("rejected apply did not advance consumer", () => consumerRevisionAtRejection, () => Is.EqualTo(1));
        AddAssert("rejected apply did not commit source bookkeeping", () => trackedObjectsAtRejection, () => Is.EqualTo(1));
        AddAssert("one batch followed by one snapshot", () => previewSink.Attempts.Select(attempt => attempt.Kind),
            () => Is.EqualTo(new[] { "batch", "snapshot" }));
        AddAssert("recovery happened in same synchronization frame", () => previewSink.Attempts[1].Time,
            () => Is.EqualTo(previewSink.Attempts[0].Time));
        AddAssert("recovery uses unadvanced candidate revision", () => fullStates.Last().Revision, () => Is.EqualTo(2));
        AddAssert("rejected candidate id is not reused", () => fullStates.Last().Objects.Single(state => state.HitObject.StartTime == 2000).Id.Value,
            () => Is.GreaterThan(rejectedCandidateId));
        AddAssert("accepted recovery commits both source references", objectIdCount, () => Is.EqualTo(2));
    }

    [Test]
    public void TestRejectedSnapshotClosesControllerOnce()
    {
        var failures = new List<string>();

        AddStep("reject initial snapshot", () =>
        {
            previewSink.RejectNextSnapshot = true;
            controller.PreviewFailed += failures.Add;
            controller.Open();
        });

        AddAssert("snapshot rejection closes controller", () => controller.Enabled, () => Is.False);
        AddAssert("snapshot rejection reports once", () => failures, () => Has.Count.EqualTo(1));
        AddAssert("snapshot rejection attempted once", () => previewSink.Attempts.Select(attempt => attempt.Kind),
            () => Is.EqualTo(new[] { "snapshot" }));
    }

    [Test]
    public void TestSnapshotApplyFailureClosesAndUnsubscribesOnce()
    {
        var failures = new List<string>();

        AddStep("throw from initial snapshot boundary", () =>
        {
            previewSink.ThrowOnNextSnapshot = true;
            controller.PreviewFailed += failures.Add;
            controller.Open();
        });
        AddAssert("snapshot exception closes controller", () => controller.Enabled, () => Is.False);
        AddAssert("snapshot exception reports once", () => failures, () => Has.Count.EqualTo(1));
        AddAssert("snapshot exception attempted once", () => previewSink.Attempts.Select(attempt => attempt.Kind),
            () => Is.EqualTo(new[] { "snapshot" }));
        AddStep("mutate source after snapshot failure", () =>
        {
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
        });
        AddWaitStep("allow post-failure frames", 2);
        AddAssert("snapshot failure released subscriptions", () => previewSink.Attempts, () => Has.Count.EqualTo(1));
        AddAssert("snapshot failure receives no source callbacks", () => controller.HasPendingStateForTests, () => Is.False);
    }

    [Test]
    public void TestCloneFailureClosesAndUnsubscribesOnce()
    {
        var failures = new List<string>();
        var unsupported = new DerivedCardinalNote { StartTime = 2000, AngleDeg = 90 };

        openMini();
        AddStep("add unsupported source subtype", () =>
        {
            previewSink.Attempts.Clear();
            controller.PreviewFailed += failures.Add;
            editorChart.Add(unsupported);
        });
        AddUntilStep("clone failure closes controller", () => !controller.Enabled);
        AddAssert("clone failure reports once", () => failures, () => Has.Count.EqualTo(1));
        AddAssert("clone failure reaches no sink boundary", () => previewSink.Attempts, () => Is.Empty);
        AddStep("mutate source after clone failure", () =>
        {
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
        });
        AddWaitStep("allow post-clone-failure frames", 2);
        AddAssert("clone failure released subscriptions", () => previewSink.Attempts, () => Is.Empty);
        AddAssert("clone failure receives no source callbacks", () => controller.HasPendingStateForTests, () => Is.False);
    }

    [Test]
    public void TestBatchApplyFailureClosesControllerOnce()
    {
        var failures = new List<string>();

        openMini();
        AddStep("throw from typed batch boundary", () =>
        {
            previewSink.Attempts.Clear();
            previewSink.ThrowOnNextBatch = true;
            controller.PreviewFailed += failures.Add;
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
        });
        AddUntilStep("batch failure closes controller", () => !controller.Enabled);

        AddAssert("batch failure reports once", () => failures, () => Has.Count.EqualTo(1));
        AddAssert("batch failure does not retry", () => previewSink.Attempts.Select(attempt => attempt.Kind),
            () => Is.EqualTo(new[] { "batch" }));
    }

    [Test]
    public void TestSameFrameObjectChangesUseOneAtomicBatch()
    {
        var first = new CardinalNote { StartTime = 2000, AngleDeg = 90 };
        var second = new CardinalNote { StartTime = 3000, AngleDeg = 135 };
        long revisionBeforeChanges = 0;

        openMini();
        AddStep("capture atomic batch baseline", () =>
        {
            revisionBeforeChanges = preview.AcceptedRevision;
            appliedMessages.Clear();
        });
        AddStep("change multiple objects in one frame", () =>
        {
            editorChart.Remove(initialNote);
            editorChart.Add(first);
            editorChart.Add(second);
        });
        AddUntilStep("atomic object state applied", () =>
            preview.ObjectCountForTests == 2
            && preview.AcceptedRevision > revisionBeforeChanges);

        AddAssert("one object batch emitted", () => appliedMessages, () => Has.Count.EqualTo(1));
        AddAssert("one revision allocated", () => preview.AcceptedRevision,
            () => Is.EqualTo(revisionBeforeChanges + 1));
        AddAssert("batch contains remove and all upserts", () =>
            appliedMessages.Single().Removes.Length == 1
            && appliedMessages.Single().Upserts.Length == 2);
        AddAssert("atomic batch includes transport", () => appliedMessages.Single().Transport.HasValue);
    }

    [Test]
    public void TestObjectBatchCapturesTransportAfterChartChanges()
    {
        openMini();
        AddStep("clear initial typed batches", appliedMessages.Clear);
        AddStep("update object and seek in one frame", () =>
        {
            initialNote.AngleDeg = 180;
            editorChart.Update(initialNote);
            editorClock.Seek(2500);
        });
        AddUntilStep("combined object transport batch applied", () =>
            preview.ClockTimeForTests == 2500
            && appliedMessages.Count > 0);

        AddAssert("one combined batch emitted", () => appliedMessages, () => Has.Count.EqualTo(1));
        AddAssert("combined batch contains final object", () =>
            ((CardinalNote)appliedMessages.Single().Upserts.Single().HitObject).AngleDeg,
            () => Is.EqualTo(180));
        AddAssert("combined batch contains post-change transport", () =>
            appliedMessages.Single().Transport?.Time,
            () => Is.EqualTo(2500).Within(0.001));
    }

    private sealed class DerivedCardinalNote : CardinalNote
    {
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
        AddUntilStep("both deltas applied atomically", () =>
            appliedMessages.Count == 1
            && !appliedMessages.Single().Removes.IsEmpty
            && !appliedMessages.Single().Upserts.IsEmpty);
        AddAssert("typed batch assigns removal and upsert arrays", () =>
            appliedMessages.Single().Removes.Single() == fullStates[0].Objects.Single().Id
            && appliedMessages.Single().Upserts.Single().HitObject.StartTime == replacement.StartTime);
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
}
