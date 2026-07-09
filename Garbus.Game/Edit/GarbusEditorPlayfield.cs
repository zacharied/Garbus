// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/BacEditorPlayfield.cs).
// BacEditorPlayfield → GarbusEditorPlayfield; BigAssCircleHitObjectComposer → (not resolved here;
// AngleSnap default 45 is inlined). OsuSpriteText/OsuFont/OsuColour → osu.Framework SpriteText.
// IScrollingInfo is resolved from DI; the composer-cached EditorScrollingInfo wins (no double-cache).

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.UI.Scrolling;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit;

/// <summary>
/// The rectangular editor timeline playfield: y is time (a standard vertically scrolling container),
/// x is the circle unrolled per <see cref="EditorAngleMapping"/>. Hosts the angle grid, the two
/// shoulder-note lane strips at the quadrant boundaries, and the darkened ghost wrap-around bands.
/// </summary>
public partial class GarbusEditorPlayfield : ScrollingPlayfield
{
    /// <summary>Absolute angle of the Left-shoulder lane strip (the West–South quadrant boundary).</summary>
    public const int LEFT_SHOULDER_ANGLE_DEG = 225;

    /// <summary>Absolute angle of the Right-shoulder lane strip (the East–North quadrant boundary).</summary>
    public const int RIGHT_SHOULDER_ANGLE_DEG = 45;

    /// <summary>Visual width of a shoulder lane strip, in degrees.</summary>
    public const float SHOULDER_STRIP_DEGREES = 16;

    /// <summary>Target container for the beat snap grid's scrolling line containers.</summary>
    public Container UnderlayElements { get; } = new Container { RelativeSizeAxes = Axes.Both };

    /// <summary>The absolute angle of a side's shoulder lane strip.</summary>
    public static int ShoulderAngle(HorizontalDirection side) =>
        side == HorizontalDirection.Left ? LEFT_SHOULDER_ANGLE_DEG : RIGHT_SHOULDER_ANGLE_DEG;

    /// <summary>The x-fraction (of the full editor width) of a side's shoulder lane strip.</summary>
    public static float ShoulderXFraction(HorizontalDirection side) => EditorAngleMapping.ToX(ShoulderAngle(side));

    [BackgroundDependencyLoader]
    private void load()
    {
        const float ghost_frac = (float)EditorAngleMapping.GHOST_DEGREES / EditorAngleMapping.TOTAL_DEGREES;

        InternalChildren = new Drawable[]
        {
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
                Alpha = 0.3f,
            },
            UnderlayElements,
            new AngleGrid { RelativeSizeAxes = Axes.Both },
            shoulderStrip(LEFT_SHOULDER_ANGLE_DEG),
            shoulderStrip(RIGHT_SHOULDER_ANGLE_DEG),
            // masked to the timeline bounds so slider wrap copies (and anything else) don't paint outside
            // it; the ghost bands lie within the bounds, so their clones still show.
            new Container { RelativeSizeAxes = Axes.Both, Masking = true, Child = HitObjectContainer },
            // ghost band dimming, above the hit objects so their clones read as "faded copies".
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Width = ghost_frac,
                Colour = Color4.Black,
                Alpha = 0.5f,
            },
            new Box
            {
                RelativeSizeAxes = Axes.Both,
                Width = ghost_frac,
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight,
                Colour = Color4.Black,
                Alpha = 0.5f,
            },
        };
    }

    private static Drawable shoulderStrip(int angleDeg) => new Box
    {
        RelativeSizeAxes = Axes.Both,
        RelativePositionAxes = Axes.X,
        Width = SHOULDER_STRIP_DEGREES / EditorAngleMapping.TOTAL_DEGREES,
        X = EditorAngleMapping.ToX(angleDeg),
        Origin = Anchor.TopCentre,
        Colour = Color4.MediumPurple,
        Alpha = 0.12f,
    };

    /// <summary>
    /// Vertical angle demarcations: bright lines at the cardinal (quadrant) boundaries with letter
    /// labels, medium lines every 45°, and faint lines at the current angle-snap increment. Lines
    /// continue through the ghost bands.
    ///
    /// Adapted for Garbus: OsuSpriteText/OsuFont/OsuColour replaced by osu.Framework SpriteText;
    /// AngleSnap is not resolved here — the default 45° is used for the grid lines.
    /// </summary>
    private partial class AngleGrid : CompositeDrawable
    {
        protected override void LoadComplete()
        {
            base.LoadComplete();
            regenerate();
        }

        private void regenerate()
        {
            ClearInternal();

            var lines = new List<Drawable>();
            const int snap = 45; // default; Task 15's composer will expose AngleSnap

            for (int gridDeg = -EditorAngleMapping.GHOST_DEGREES; gridDeg <= 360 + EditorAngleMapping.GHOST_DEGREES; gridDeg += 1)
            {
                int absolute = EditorAngleMapping.NormalizeDeg(gridDeg + EditorAngleMapping.ANGLE_ORIGIN);

                bool cardinal = absolute % 90 == 0;
                bool major = absolute % 45 == 0;
                bool snapLine = absolute % snap == 0;

                if (!cardinal && !major && !snapLine)
                    continue;

                float x = (EditorAngleMapping.GHOST_DEGREES + gridDeg) / (float)EditorAngleMapping.TOTAL_DEGREES;

                lines.Add(new Box
                {
                    RelativeSizeAxes = Axes.Y,
                    RelativePositionAxes = Axes.X,
                    X = x,
                    Origin = Anchor.TopCentre,
                    Width = cardinal ? 2 : 1,
                    Colour = Color4.White,
                    Alpha = cardinal ? 0.4f : major ? 0.2f : 0.08f,
                });

                if (cardinal)
                {
                    lines.Add(new SpriteText
                    {
                        RelativePositionAxes = Axes.X,
                        X = x,
                        Y = 4,
                        Origin = Anchor.TopCentre,
                        Text = CardinalDirectionExtensions.FromAngle(absolute).ToString()[..1],
                        Colour = Color4.Yellow,
                        Font = FontUsage.Default.With(size: 16),
                    });
                }
            }

            InternalChildren = lines.ToArray();
        }
    }
}
