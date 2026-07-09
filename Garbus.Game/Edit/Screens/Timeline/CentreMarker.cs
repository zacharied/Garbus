// Bespoke for Garbus (modeled on osu.Game/Screens/Edit/Compose/Components/Timeline/CentreMarker.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: removed OverlayColourProvider/OsuFont dependencies; uses plain Colour4 white
// and a Box instead of the custom VerticalTriangles sprite; placed as a non-scrolling overlay in
// TimelineStrip so the centre playhead marker stays fixed at the container's horizontal midpoint.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Timeline
{
    /// <summary>
    /// A fixed marker centred on the <see cref="TimelineStrip"/> indicating the current playhead position.
    /// Does not scroll with the timeline content — it is added directly to the outer scroll container.
    /// </summary>
    public partial class CentreMarker : CompositeDrawable
    {
        public CentreMarker()
        {
            Anchor = Anchor.TopCentre;
            Origin = Anchor.TopCentre;
            RelativeSizeAxes = Axes.Y;
            Width = 2;

            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = new Color4(255, 220, 80, 220),
            };
        }
    }
}
