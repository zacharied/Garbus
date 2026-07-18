// Renders active design-point effects during gameplay. For a TutorialMessage active at the current
// gameplay time, dims the screen with a translucent black box and shows its text centered on top.
// Stateless per frame (recomputed from Clock.CurrentTime) so it is rewind-safe with no revert
// bookkeeping. v1 assumes non-overlapping tutorial messages (first active one wins).

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Design;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Garbus.Game.Screens
{
    public partial class DesignOverlay : CompositeDrawable
    {
        private readonly IReadOnlyList<DesignPoint> designPoints;

        private Box dim = null!;
        private TextFlowContainer message = null!;

        // TextFlowContainer.Text is write-only, so track the displayed text ourselves (for the
        // change guard and the test seam).
        private string currentText = string.Empty;

        public DesignOverlay(GarbusChart chart)
        {
            designPoints = chart.DesignPointInfo.DesignPoints;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                dim = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                    Alpha = 0,
                },
                message = new TextFlowContainer(t => t.Font = FontUsage.Default.With(size: 32))
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    TextAnchor = Anchor.Centre,
                    RelativeSizeAxes = Axes.X,
                    Width = 0.6f,
                    AutoSizeAxes = Axes.Y,
                    Alpha = 0,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            var active = activeMessage(Clock.CurrentTime);

            if (active != null)
            {
                dim.Alpha = TutorialMessage.OVERLAY_OPACITY;
                message.Alpha = 1;
                if (currentText != active.Text)
                {
                    currentText = active.Text;
                    message.Text = currentText;
                }
            }
            else
            {
                dim.Alpha = 0;
                message.Alpha = 0;
            }
        }

        private TutorialMessage? activeMessage(double time) =>
            designPoints.OfType<TutorialMessage>()
                        .FirstOrDefault(m => time >= m.StartTime && time < m.EndTime);

        /// <summary>Test seam: current dim-overlay alpha.</summary>
        public float DimAlphaForTests => dim.Alpha;

        /// <summary>Test seam: whether the message text is currently shown.</summary>
        public bool MessageVisibleForTests => message.Alpha > 0;

        /// <summary>Test seam: the currently displayed message text.</summary>
        public string MessageTextForTests => currentText;
    }
}
