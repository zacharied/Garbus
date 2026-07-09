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
//   TimelineTimeRange = EditorClock.TrackLength / CurrentZoom / 2  (BAC's exact formula).
//
// AutoSeekOnPlacement: wired from GarbusConfigManager.EditorAutoSeekOnPlacement to the composer's
//   HitObjectPlacementBlueprint.AutoSeekOnPlacement via GarbusHitObjectComposer.AutoSeekOnPlacement.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using Garbus.Game.Configuration;
using Garbus.Game.Edit.Screens.Timeline;

namespace Garbus.Game.Edit.Screens
{
    public partial class ComposeTab : EditorTabScreen
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private GarbusHitObjectComposer composer = null!;
        private TimelineStrip timelineStrip = null!;

        [BackgroundDependencyLoader]
        private void load(GarbusConfigManager config)
        {
            RelativeSizeAxes = Axes.Both;

            // Timeline strip above, composer fills the rest.
            // Use a plain Container with Padding (same pattern as GarbusEditor's tab container) to
            // avoid the FillFlowContainer + RelativeSizeAxes.Both collapse issue.
            const float ZOOM_BUTTON_WIDTH = 26;

            InternalChildren = new Drawable[]
            {
                timelineStrip = new TimelineStrip(),
                // Zoom-out button (–) at the right edge of the timeline strip.
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = ZOOM_BUTTON_WIDTH,
                    Height = TimelineStrip.HEIGHT / 2,
                    Text = "–",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value - 1f,
                },
                // Zoom-in button (+) just to the left of the zoom-out button.
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Width = ZOOM_BUTTON_WIDTH,
                    Height = TimelineStrip.HEIGHT / 2,
                    Position = new osuTK.Vector2(-ZOOM_BUTTON_WIDTH, 0),
                    Text = "+",
                    Action = () => timelineStrip.Zoom = timelineStrip.CurrentZoom.Value + 1f,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = TimelineStrip.HEIGHT },
                    // Setting Clock = editorClock wires the entire composer subtree to the EditorClock.
                    // The playfield's scrolling hitobject container reads Clock.CurrentTime for layout,
                    // so this freezes it when the clock is stopped (matching the selection harness pattern).
                    Clock = editorClock,
                    Child = composer = new GarbusHitObjectComposer { RelativeSizeAxes = Axes.Both },
                },
            };

            // Zoom sync: TimelineTimeRange = TrackLength / CurrentZoom / 2 (BAC's formula).
            timelineStrip.CurrentZoom.BindValueChanged(e =>
            {
                double trackLength = editorClock.TrackLength;
                if (trackLength > 0 && e.NewValue > 0)
                    composer.TimelineTimeRange.Value = trackLength / e.NewValue / 2;
            });

            // AutoSeekOnPlacement config → composer.
            var autoSeek = config.GetBindable<bool>(GarbusSetting.EditorAutoSeekOnPlacement);
            autoSeek.BindValueChanged(e => composer.AutoSeekOnPlacement.Value = e.NewValue, true);
        }

        protected override void Update()
        {
            base.Update();

            // Re-apply zoom formula each frame so TrackLength changes (track load/switch) are picked up.
            float zoom = timelineStrip.CurrentZoom.Value;
            double trackLength = editorClock.TrackLength;
            if (zoom > 0 && trackLength > 0)
                composer.TimelineTimeRange.Value = trackLength / zoom / 2;
        }
    }
}
