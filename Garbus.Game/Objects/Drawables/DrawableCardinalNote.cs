// Ported from BigAssCircle (osu.Game.Rulesets.BigAssCircle/Objects/Drawables/DrawableCardinalNote.cs).
// Original carries the ppy template MIT header:
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects; // ChordColours, ChordHighlighter
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Objects.Drawables
{
    public partial class DrawableCardinalNote : DrawableNote<CardinalNote>
    {
        private readonly Sprite sprite;

        [Resolved]
        private ChordHighlighter chords { get; set; } = null!;

        public DrawableCardinalNote(CardinalNote hitObject)
            : base(hitObject)
        {
            Size = new Vector2(80);
            sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre
            };
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            sprite.Texture = textures.Get("square");
            AddInternal(sprite);
        }

        protected override void PrepareForUse()
        {
            if (IsInPreview)
                return;

            // Pooled/reused drawables must set an explicit colour every time (yellow if this note shares its
            // start time with another cardinal note, else reset to white).
            updateChordColour();

            // Apply note spawn effect
            sprite.ScaleTo(0).ScaleTo(1, 125, Easing.In);
        }

        protected override void OnApply()
        {
            base.OnApply();

            if (chords != null)
                updateChordColour();
        }

        private void updateChordColour() => Colour = chords.IsInChord(HitObject) ? ChordColours.Highlight : Color4.White;

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            const double duration = 1000;

            switch (state)
            {
                case ArmedState.Hit:
                    sprite
                        .Spin(700, RotationDirection.Clockwise)
                        .FadeOut(350, Easing.OutQuint)
                        .ScaleTo(new Vector2(2), 350, Easing.OutQuint)
                        .OnComplete(_ => ExpireAfterTransforms());
                    break;

                case ArmedState.Miss:
                    sprite.FadeColour(Color4.Red, duration);
                    sprite.FadeOut(duration, Easing.InQuint).OnComplete(_ => ExpireAfterTransforms());
                    break;
            }
        }
    }
}
