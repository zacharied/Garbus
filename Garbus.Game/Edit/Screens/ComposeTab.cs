// The Compose tab: timeline strip on top + GarbusHitObjectComposer below.
// The editor shell (GarbusEditor) DI-caches EditorChart / EditorClock / IEditorChangeHandler /
// BindableBeatDivisor, which the composer tree resolves.
//
// Clock wiring (MUST-DO from Task 16 review):
//   The composer's scrolling playfield must advance on the EditorClock, not the frame clock.
//   We wrap the composer in a Container whose Clock is set to the resolved EditorClock — the same
//   pattern the TestSceneComposeSelection harness already uses for its ManualInputManager.Clock.
//   This ensures the playfield is frozen while the EditorClock is stopped.
//
// Zoom sync: TimelineStrip.CurrentZoom drives the composer's TimelineTimeRange via the formula
//   TimelineTimeRange = EditorClock.TrackLength / CurrentZoom / 2
//
// AutoSeekOnPlacement: wired from GarbusConfigManager.EditorAutoSeekOnPlacement to the composer's
//   HitObjectPlacementBlueprint.AutoSeekOnPlacement via GarbusHitObjectComposer.AutoSeekOnPlacement.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using Garbus.Game.Configuration;
using Garbus.Game.Edit.Compose;
using Garbus.Game.Edit.Screens.Timeline;

namespace Garbus.Game.Edit.Screens
{
    public partial class ComposeTab : EditorTabScreen
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private GarbusHitObjectComposer composer = null!;
        private TimelineStrip timelineStrip = null!;

        // Stored as a field so the config bound copy is not garbage-collected after load() returns —
        // GetBindable's copy is held by the config only via a weak reference, so a local variable would
        // be collected and the menu toggle would stop propagating until an editor reload.
        private Bindable<bool>? autoSeekOnPlacement;

        [BackgroundDependencyLoader]
        private void load(GarbusConfigManager config)
        {
            RelativeSizeAxes = Axes.Both;

            // Top region: timeline strip (flex) + a reserved 35px zoom column + a 120px beat-divisor
            // control, laid out in a horizontal grid so the strip is no longer full-width.
            // Composer fills the rest below, via a plain Container with Padding (same pattern as
            // GarbusEditor's tab container) to avoid the FillFlowContainer + RelativeSizeAxes.Both
            // collapse issue.
            // Wrapped in a PopoverContainer so GarbusBeatDivisorControl's custom-divisor popover
            // (Task 4) has an ancestor to attach to.
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
                            // Setting Clock = editorClock wires the entire composer subtree to the
                            // EditorClock. The playfield's scrolling hitobject container reads
                            // Clock.CurrentTime for layout, so this freezes it when the clock is
                            // stopped (matching the selection harness pattern).
                            Clock = editorClock,
                            // The scrolling container positions grid/bar lines outside the visible
                            // window with no masking of its own — clip here so they can't draw over
                            // the timeline strip above.
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

            // AutoSeekOnPlacement config → composer.
            autoSeekOnPlacement = config.GetBindable<bool>(GarbusSetting.EditorAutoSeekOnPlacement);
            autoSeekOnPlacement.BindValueChanged(e => composer.AutoSeekOnPlacement.Value = e.NewValue, true);
        }

        // Vertical zoom stack: "+" on top half, "–" on bottom half.
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
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value + 5f,
                },
                new BasicButton
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.5f,
                    Text = "–",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value - 5f,
                },
            },
        };

        // Ctrl+scroll anywhere in the compose view zooms the timeline, matching the behaviour when the
        // cursor is directly over the waveform strip (ZoomableScrollContainer.OnScroll). Scroll over the
        // composer below the strip otherwise bubbles up to GarbusEditor.OnScroll, which ignores ctrl.
        protected override bool OnScroll(ScrollEvent e)
        {
            if (e.ControlPressed)
            {
                timelineStrip.AdjustZoomRelatively(e.ScrollDelta.Y);
                return true;
            }

            return base.OnScroll(e);
        }

        protected override void Update()
        {
            base.Update();

            // Update() is the single source of truth for the zoom formula (TrackLength / zoom / 2).
            // Using Update() rather than a BindValueChanged on CurrentZoom means TrackLength changes
            // (e.g. track reload via File → Open) are also picked up without a separate callback.
            float zoom = timelineStrip.CurrentZoom.Value;
            double trackLength = editorClock.TrackLength;
            if (zoom > 0 && trackLength > 0)
                composer.TimelineTimeRange.Value = trackLength / zoom / 2;
        }
    }
}
