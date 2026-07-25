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
    private TestPreviewSink previewSink = null!;
    private InlineChartPreviewController controller = null!;
    private readonly List<ChartPreviewSnapshot> fullStates = new();
    private readonly List<ChartPreviewBatch> appliedMessages = new();
    private BindableDouble rateAdjustment = null!;
    private long timestamp;

    [SetUpSteps]
    public void SetUpSteps()
    {
        AddStep("create mini controller", () =>
        {
            fullStates.Clear();
            appliedMessages.Clear();
            timestamp = Stopwatch.GetTimestamp();
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
            previewSink = new TestPreviewSink(preview, () => Time.Current);
            controller = new InlineChartPreviewController(
                editorChart,
                editorClock,
                changeHandler,
                scrollingInfo,
                previewSink,
                () => timestamp);

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

    private long controllerRevision() => controller.RevisionForTests;

    private int objectIdCount() => controller.TrackedObjectCountForTests;

    private long objectId(GarbusHitObject hitObject) => controller.ObjectIdForTests(hitObject);

    private void holdTimestamp()
    {
    }

    private void advanceTimestamp() => timestamp += Stopwatch.Frequency;

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

    private sealed class TestPreviewSink : IChartPreviewSink
    {
        private readonly ChartPreviewContent content;
        private readonly Func<double> currentTime;

        public TestPreviewSink(ChartPreviewContent content, Func<double> currentTime)
        {
            this.content = content;
            this.currentTime = currentTime;
            content.ResyncRequested += () => ResyncRequested?.Invoke();
        }

        public event Action? ResyncRequested;

        public readonly List<(string Kind, long Revision, double Time)> Attempts = new();

        public Action<ChartPreviewBatch>? BatchAttempted { get; set; }

        public bool RejectNextBatch { get; set; }

        public bool RejectNextSnapshot { get; set; }

        public bool ThrowOnNextBatch { get; set; }

        public bool ThrowOnNextSnapshot { get; set; }

        public bool Apply(ChartPreviewBatch batch)
        {
            Attempts.Add(("batch", batch.Revision, currentTime()));
            BatchAttempted?.Invoke(batch);

            if (ThrowOnNextBatch)
            {
                ThrowOnNextBatch = false;
                throw new InvalidOperationException("test batch apply failure");
            }

            if (RejectNextBatch)
            {
                RejectNextBatch = false;
                return false;
            }

            return content.Apply(batch);
        }

        public bool Replace(ChartPreviewSnapshot snapshot)
        {
            Attempts.Add(("snapshot", snapshot.Revision, currentTime()));

            if (ThrowOnNextSnapshot)
            {
                ThrowOnNextSnapshot = false;
                throw new InvalidOperationException("test snapshot apply failure");
            }

            if (RejectNextSnapshot)
            {
                RejectNextSnapshot = false;
                return false;
            }

            return content.Replace(snapshot);
        }
    }
}
