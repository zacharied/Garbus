// Modeled on osu.Game (https://github.com/ppy/osu) — osu.Game/Rulesets/Edit/EditorToolboxGroup.cs
// (which derives from osu.Game/Overlays/SettingsToolboxGroup.cs).
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: osu's EditorToolboxGroup extends SettingsToolboxGroup, an animated collapsible
// panel entangled with OsuColour/OsuSpriteText/IconButton and hover state. Rewritten fresh as a plain
// titled box: a header label above a single Child, with hardcoded colours. The single-Child API
// (Child = ...) matches osu's SettingsToolboxGroup so the composer code compiles unchanged.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace Garbus.Game.Edit.Compose
{
    /// <summary>
    /// A titled group within a toolbox column: a header label above its content.
    /// </summary>
    public partial class EditorToolboxGroup : Container
    {
        private readonly Container<Drawable> content;

        protected override Container<Drawable> Content => content;

        public EditorToolboxGroup(string title)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 5),
                Children = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 20,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = new Colour4(30, 30, 38, 255),
                            },
                            new SpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 4,
                                Text = title,
                                Font = FontUsage.Default.With(size: 14),
                                Colour = Colour4.White,
                            },
                        },
                    },
                    content = new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                    },
                },
            };
        }
    }
}
