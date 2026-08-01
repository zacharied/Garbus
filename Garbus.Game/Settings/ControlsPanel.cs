// The rebind sub-view: one KeyBindingRow per GarbusAction and a Reset-to-defaults button. The title
// and the back affordance live on the shared SettingsPanelHeader, not here. Given the store by its
// host (SettingsOverlay) so it stays test-constructible without DI.

using System;
using Garbus.Game.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    public partial class ControlsPanel : CompositeDrawable
    {
        private readonly KeyBindingStore store;

        public ControlsPanel(KeyBindingStore store)
        {
            this.store = store;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding { Horizontal = 20 };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            foreach (GarbusAction action in Enum.GetValues<GarbusAction>())
                flow.Add(new KeyBindingRow(store, action));

            flow.Add(new ClickableText("Reset to defaults", store.ResetToDefaults));

            InternalChild = flow;
        }

        // A minimal text button: a label that runs an action on click.
        private partial class ClickableText : CompositeDrawable
        {
            private readonly string label;
            private readonly Action action;

            public ClickableText(string label, Action action)
            {
                this.label = label;
                this.action = action;

                RelativeSizeAxes = Axes.X;
                Height = 28;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(60, 60, 78, 255) },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = FontUsage.Default.With(size: 15),
                        Colour = Color4.White,
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                action();
                return true;
            }
        }
    }
}
