// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Edit/BacEditorPlayfield.cs).
// BacEditorPlayfield → GarbusEditorPlayfield; BigAssCircleHitObjectComposer → (not resolved here;
// AngleSnap default 45 is inlined). OsuSpriteText/OsuFont/OsuColour → osu.Framework SpriteText.
// IScrollingInfo is resolved from DI; the composer-cached EditorScrollingInfo wins (no double-cache).

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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

    /// <summary>
    /// Height, in pixels, of the "hit zone" between the judgement line and the bottom of the compose
    /// area (the top of the bottom bar). The time-scrolling layers (hit objects, bar lines, beat-snap
    /// grid) have their trailing edge — the judgement line, where an object rests when
    /// EditorTime == StartTime — raised by this amount; objects continue scrolling past it into the
    /// zone below (already-past times). The static grid backdrop still fills the zone. Marked by the
    /// yellow "Judgement line" box.
    /// </summary>
    public const float JUDGEMENT_LINE_OFFSET = 40;

    /// <summary>Bottom inset applied to every time-scrolling layer so their judgement lines align.</summary>
    private static MarginPadding HitZonePadding => new MarginPadding { Bottom = JUDGEMENT_LINE_OFFSET };

    /// <summary>
    /// Target container for the beat snap grid's scrolling line containers. Inset at the bottom by the
    /// hit-zone height so the snap lines share the raised judgement line with hit objects/bar lines.
    /// </summary>
    public Container UnderlayElements { get; } = new Container { RelativeSizeAxes = Axes.Both, Padding = HitZonePadding };

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
            new EditorBarLineDisplay(),
            // masked to the timeline bounds so slider wrap copies (and anything else) don't paint outside
            // it; the ghost bands lie within the bounds, so their clones still show. The mask stays
            // full height (objects that have scrolled past the judgement line remain visible in the hit
            // zone below it); the inner container insets the scroll surface so its trailing edge — the
            // judgement line — floats HitZonePadding above the bottom.
            new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                Child = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = HitZonePadding,
                    Child = HitObjectContainer,
                },
            },
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
            // The judgement line: an object rests here when EditorTime == StartTime. It floats
            // JUDGEMENT_LINE_OFFSET above the playfield bottom (the top of the hit zone), matching the
            // raised trailing edge of the scroll layers. Drawn last so it reads above the grid, the hit
            // zone's objects, and the ghost dimming.
            new Box
            {
                Name = "Judgement line",
                RelativeSizeAxes = Axes.X,
                Height = 3,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.CentreLeft,
                Y = -JUDGEMENT_LINE_OFFSET,
                Colour = Color4.Yellow,
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
    /// the snap increment binds to the composer's <see cref="GarbusHitObjectComposer.AngleSnap"/>
    /// (resolved from DI, as BAC did) so the faint snap lines track the selected increment live.
    /// </summary>
    private partial class AngleGrid : CompositeDrawable
    {
        [Resolved(CanBeNull = true)]
        private GarbusHitObjectComposer? composer { get; set; }

        private readonly IBindable<int> angleSnap = new BindableInt(45);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (composer != null)
                angleSnap.BindTo(composer.AngleSnap);
            angleSnap.BindValueChanged(_ => regenerate(), true);
        }

        private void regenerate()
        {
            ClearInternal();

            var lines = new List<Drawable>();
            int snap = angleSnap.Value;

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
