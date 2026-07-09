// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableShoulderNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using Garbus.Game.Core;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.UI;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables;

/// <summary>
/// A shoulder note — the analog-shoulder counterpart of <see cref="DrawableCardinalNote"/>. It behaves
/// exactly like a cardinal note (travels out from the centre along its lane's direction and is judged on a
/// timed press), but is drawn with the curved "paddle" sprite instead of a square.
///
/// A left shoulder lives in the West lane, a right shoulder in the East lane (see
/// <see cref="ShoulderNote.Direction"/>). The paddle art is drawn as a segment of the ring facing East,
/// so it is rotated to face its lane's direction (180° for West), and auto-sized so its curvature radius
/// matches the ring: the art's curve radius is ≈ its own height, so setting the height to the ring radius
/// (<see cref="GarbusScrollingHitObjectContainer.ScrollLength"/>) makes the arc concentric with the
/// outer ring when the note reaches it.
/// </summary>
public partial class DrawableShoulderNote : DrawableNote<ShoulderNote>
{
    /// <summary>Paddle texture aspect ratio (width / height), used to size the sprite without distortion.</summary>
    private const float paddle_aspect = 636f / 1912f;

    /// <summary>
    /// The paddle's curvature radius expressed as a multiple of its texture height. The art is drawn with
    /// a curve radius ≈ its height, so a value of 1 makes <c>height = ringRadius</c> align the arc with the
    /// ring. Tweak to nudge the paddle in/out relative to the ring.
    /// </summary>
    private const float curve_radius_over_height = 1.0f;

    private readonly Sprite sprite;

    [Resolved]
    private Ring ring { get; set; } = null!;

    [Resolved]
    private GarbusScrollingHitObjectContainer scrollingContainer { get; set; } = null!;

    public DrawableShoulderNote(ShoulderNote hitObject)
        : base(hitObject)
    {
        Origin = Anchor.Centre;
        Colour = Colour4.Purple;
        sprite = new Sprite
        {
            RelativeSizeAxes = Axes.Both,
            FillMode = FillMode.Fit,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };
    }

    [BackgroundDependencyLoader]
    private void load(TextureStore textures)
    {
        sprite.Texture = textures.Get("paddle");
        AddInternal(sprite);

        // The paddle art faces East; rotate it to face this note's lane direction (screen rotation is
        // clockwise-positive while the polar angle is counter-clockwise, hence the negation). West → 180°.
        Rotation = -HitObject.Direction.ToDegrees();
    }

    protected override void Update()
    {
        base.Update();

        // Size the paddle so its curvature matches the outer ring (see class remarks). The ring radius is
        // resolved from the scrolling container, so it tracks the playfield size.
        float height = scrollingContainer.ScrollLength / curve_radius_over_height;
        Size = new Vector2(height * paddle_aspect, height);
    }

    protected override void PrepareForUse()
    {
        // Apply note spawn effect
        sprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
    }

    protected override void UpdateHitStateTransforms(ArmedState state)
    {
        const double duration = 1000;

        switch (state)
        {
            case ArmedState.Hit:
                sprite
                    .FadeOut(350, Easing.OutQuint)
                    .ScaleTo(new Vector2(1.4f), 350, Easing.OutQuint)
                    .OnComplete(_ => Expire());
                break;

            case ArmedState.Miss:
                sprite.FadeColour(Color4.Red, duration);
                sprite.FadeOut(duration, Easing.InQuint).OnComplete(_ => Expire());
                break;
        }
    }
}
