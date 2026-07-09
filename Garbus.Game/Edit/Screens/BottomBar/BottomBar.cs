// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/BottomBar.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: four fixed-width columns (150 | flex | 220 | 90); no osu.Game
// OverlayColourProvider/EdgeEffect/TestGameplayButton wiring; Test button wired in Task 19.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.BottomBar
{
    /// <summary>
    /// The fixed 60 px bar at the bottom of the editor.
    /// Columns: TimeInfoDisplay (150 px) | SummaryTimeline (flex) | PlaybackControl (220 px) | Test button (90 px).
    /// </summary>
    public partial class BottomBar : CompositeDrawable
    {
        private TestButton testButton = null!;

        public BottomBar()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Height = 60;
        }

        [BackgroundDependencyLoader]
        private void load(GarbusEditor editor)
        {
            testButton = new TestButton
            {
                Enabled = { Value = editor.HasRealTrack },
                Action = editor.StartTestMode,
            };

            // Bind enabled state to whether the editor has a real track.
            editor.TrackIsReal.BindValueChanged(e => testButton.Enabled.Value = e.NewValue, true);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(20, 20, 28, 255),
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 150),   // TimeInfoDisplay
                        new Dimension(),                               // SummaryTimeline (flex)
                        new Dimension(GridSizeMode.Absolute, 220),    // PlaybackControl
                        new Dimension(GridSizeMode.Absolute, 90),     // Test button
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new TimeInfoDisplay { RelativeSizeAxes = Axes.Both },
                            new SummaryTimeline { RelativeSizeAxes = Axes.Both },
                            new PlaybackControl { RelativeSizeAxes = Axes.Both },
                            testButton,
                        },
                    },
                },
            };
        }
    }
}

