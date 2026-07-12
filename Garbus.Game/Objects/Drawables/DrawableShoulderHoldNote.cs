// Ported from BigAssCircle (shoulder counterpart of DrawableHoldNote).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// Adapted for Garbus: two-square-plus-arc head (as DrawableShoulderNote) plus a transparent CircularProgress
// sector body; judgement/input come from DrawableHoldNote<,>.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.UserInterface;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.UI;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A held shoulder note: the two-square-plus-arc shoulder head plus a transparent sector body that grows
/// outward from the tail radius to the head radius over the 90° quadrant slice.
/// </summary>
public partial class DrawableShoulderHoldNote : DrawableHoldNote<ShoulderHoldNote, HoldNoteHead<ShoulderHoldNote>>
{
    private const float square_size = 80f;
    private const float arc_thickness = 15f;
    private const float sector_alpha = 0.35f;

    private static readonly Colour4 held_colour = Colour4.Purple;
    private static readonly Colour4 dropped_colour = Colour4.Gray;

    private Sprite squareA = null!;
    private Sprite squareB = null!;
    private Arc arc = null!;
    private CircularProgress sector = null!;

    public DrawableShoulderHoldNote(ShoulderHoldNote hitObject)
        : base(hitObject)
    {
        Anchor = Anchor.Centre;
        Origin = Anchor.Centre;
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        var squareTexture = textures.Get("square");

        // Transparent body behind the head. CircularProgress fills a 90° wedge (Progress 0.25); Size/InnerRadius
        // are set each frame to grow the annulus between tail and head radii.
        AddInternal(sector = new CircularProgress
        {
            RelativeSizeAxes = Axes.None,
            Size = Vector2.Zero,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Progress = 0.25,
            InnerRadius = 0f,
            Colour = held_colour,
            Alpha = sector_alpha,
        });

        AddInternal(arc = new Arc(thickness: arc_thickness)
        {
            RelativeSizeAxes = Axes.None,
            Size = Vector2.Zero,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Colour = held_colour,
        });

        AddInternal(squareA = createSquare(squareTexture));
        AddInternal(squareB = createSquare(squareTexture));
    }

    private static Sprite createSquare(Texture texture) => new Sprite
    {
        Texture = texture,
        Size = new Vector2(square_size),
        FillMode = FillMode.Fit,
        Anchor = Anchor.Centre,
        Origin = Anchor.Centre,
        Colour = held_colour,
    };

    protected override void UpdateVisuals()
    {
        float baseAngleDeg = HitObject.AngleDeg;
        float ring = ScrollingContainer.ScrollLength;
        float outer = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.StartTime), 0f, ring);
        float inner = Math.Clamp(ScrollingContainer.DistanceFromCentreAtTime(HitObject.EndTime), 0f, ring);

        // Head: two squares on the ±45° diagonals + the growing arc, at the head radius.
        squareA.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, outer, +1f);
        squareB.Position = ShoulderNoteGeometry.SquarePosition(baseAngleDeg, outer, -1f);

        arc.Size = new Vector2(2f * outer);
        arc.StartRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg - ShoulderNoteGeometry.DiagonalOffsetDeg);
        arc.EndRadians.Value = ShoulderNoteGeometry.ToRadians(baseAngleDeg + ShoulderNoteGeometry.DiagonalOffsetDeg);

        // Body: the transparent sector fills the annulus [inner, outer] over the 90° slice.
        // CircularProgress.InnerRadius is the fill *thickness* measured inward from the outer edge
        // (0 = invisible, 1 = filled to the centre) — NOT the hole radius. The unfilled hole then has
        // radius (1 - InnerRadius)·outer, which we want to equal the tail distance `inner`; hence the
        // fill fraction is 1 - inner/outer. Using inner/outer directly inverts it, leaving the sector
        // invisible while the tail sits at the centre during the approach (inner == 0 ⇒ InnerRadius 0).
        sector.Size = new Vector2(2f * outer);
        sector.Rotation = ShoulderNoteGeometry.SectorRotationDeg(baseAngleDeg);
        sector.InnerRadius = outer > 0f ? 1f - inner / outer : 0f;

        if (!Judged)
        {
            var trailColour = HoldActive && !Holding ? dropped_colour : held_colour;
            arc.Colour = trailColour;
            sector.Colour = trailColour;
        }
    }

    protected override void OnHeadHit()
    {
        squareA.ScaleTo(1.2f, 80, Easing.OutQuint).Then().ScaleTo(1f, 120, Easing.OutQuint);
        squareB.ScaleTo(1.2f, 80, Easing.OutQuint).Then().ScaleTo(1f, 120, Easing.OutQuint);
    }

    protected override void PrepareForUse()
    {
        base.PrepareForUse();
        this.ScaleTo(0).ScaleTo(1, 125, Easing.In);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                this.FadeOut(350, Easing.OutQuint)
                    .ScaleTo(new Vector2(1.4f), 350, Easing.OutQuint)
                    .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                this.FadeColour(Color4.Red, duration);
                this.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }
}
