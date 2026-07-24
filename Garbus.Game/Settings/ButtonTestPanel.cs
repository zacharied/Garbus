// A live input-test panel shown to the right of the Controls (rebind) sub-view. It reflects the
// player's *bound actions*: an embedded GarbusInputManager resolves the cached KeyBindingStore, so
// pressing whatever button is bound to (say) ButtonN1 lights the D-Pad group's North cell. Two NESW
// cross groups (D-Pad "…1" left, Face "…2" right) with L/R shoulder indicators above them, and two
// analog-stick displays below and between the groups fed by an embedded AnalogInputManager.

using System;
using System.Collections.Generic;
using Garbus.Game.Core;
using Garbus.Game.Input;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    public partial class ButtonTestPanel : CompositeDrawable
    {
        private static readonly Color4 background_colour = new Color4(20, 20, 28, 240);

        // The cached bindings-store, if any (present in the running game; a test may cache its own).
        // Resolved indirectly by the embedded input manager too; held here only for the live-reload hook.
        [Resolved(canBeNull: true)]
        private KeyBindingStore? store { get; set; }

        private readonly Dictionary<GarbusAction, ButtonCell> cells = new();
        private readonly Dictionary<HorizontalDirection, StickDisplay> sticks = new();

        private GarbusInputManager inputManager = null!;
        private AnalogInputManager analog = null!;

        public ButtonTestPanel()
        {
            Size = new Vector2(360, 300);
        }

        /// <summary>Whether the cell for <paramref name="action"/> is currently held (inspection surface).</summary>
        public bool IsLit(GarbusAction action) => cells[action].Lit;

        /// <summary>The current dot offset of a stick display from its centre (inspection surface).</summary>
        public Vector2 StickDot(HorizontalDirection side) => sticks[side].DotPosition;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = background_colour },
                // Reads raw stick axes for the two StickDisplays; needs no size, only presence in the tree.
                analog = new AnalogInputManager(),
                inputManager = new GarbusInputManager
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = buildContent(),
                },
            };

            if (store != null)
                store.Changed += onBindingsChanged;
        }

        private Drawable buildContent() => new FillFlowContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            AutoSizeAxes = Axes.Both,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 14),
            Children = new Drawable[]
            {
                new SpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Text = "Button Test",
                    Font = FontUsage.Default.With(size: 20),
                    Colour = Color4.White,
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(28, 0),
                    Children = new Drawable[]
                    {
                        // D-Pad column: L shoulder above, then the "…1" cross.
                        buildColumn("D-Pad", GarbusAction.ButtonL, "L",
                            GarbusAction.ButtonN1, GarbusAction.ButtonE1, GarbusAction.ButtonS1, GarbusAction.ButtonW1),
                        // Face column: R shoulder above, then the "…2" cross.
                        buildColumn("Face", GarbusAction.ButtonR, "R",
                            GarbusAction.ButtonN2, GarbusAction.ButtonE2, GarbusAction.ButtonS2, GarbusAction.ButtonW2),
                    },
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(16, 0),
                    Children = new Drawable[]
                    {
                        makeStick(HorizontalDirection.Left),
                        makeStick(HorizontalDirection.Right),
                    },
                },
            },
        };

        private Drawable buildColumn(string caption, GarbusAction shoulder, string shoulderLabel,
                                     GarbusAction north, GarbusAction east, GarbusAction south, GarbusAction west) =>
            new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    makeCell(shoulder, shoulderLabel, new Vector2(64, 26), Anchor.TopCentre),
                    new SpriteText
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Text = caption,
                        Font = FontUsage.Default.With(size: 14),
                        Colour = new Color4(180, 180, 220, 255),
                    },
                    buildCross(north, east, south, west),
                },
            };

        // A 90×90 plus-shaped group: N top-centre, S bottom-centre, W centre-left, E centre-right.
        private Drawable buildCross(GarbusAction north, GarbusAction east, GarbusAction south, GarbusAction west) =>
            new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Size = new Vector2(90),
                Children = new[]
                {
                    makeCell(north, "N", new Vector2(30), Anchor.TopCentre),
                    makeCell(south, "S", new Vector2(30), Anchor.BottomCentre),
                    makeCell(west, "W", new Vector2(30), Anchor.CentreLeft),
                    makeCell(east, "E", new Vector2(30), Anchor.CentreRight),
                },
            };

        private ButtonCell makeCell(GarbusAction action, string label, Vector2 size, Anchor anchor)
        {
            var cell = new ButtonCell(action, label)
            {
                Size = size,
                Anchor = anchor,
                Origin = anchor,
            };
            cells[action] = cell;
            return cell;
        }

        private StickDisplay makeStick(HorizontalDirection side)
        {
            var stick = new StickDisplay(analog, side);
            sticks[side] = stick;
            return stick;
        }

        private void onBindingsChanged() => inputManager.ReloadBindings();

        protected override void Dispose(bool isDisposing)
        {
            if (store != null)
                store.Changed -= onBindingsChanged;

            base.Dispose(isDisposing);
        }

        // One labelled button that lights while the action bound to it is held. Consumes the press for
        // the action it owns (OnPressed returns true) so button-testing can't leak input to the screen behind.
        private partial class ButtonCell : CompositeDrawable, IKeyBindingHandler<GarbusAction>
        {
            private static readonly Color4 idle_colour = new Color4(40, 40, 52, 255);
            private static readonly Color4 lit_colour = new Color4(120, 200, 255, 255);

            public GarbusAction Action { get; }
            public bool Lit { get; private set; }

            private readonly string label;
            private Box background = null!;

            public ButtonCell(GarbusAction action, string label)
            {
                Action = action;
                this.label = label;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChildren = new Drawable[]
                {
                    background = new Box { RelativeSizeAxes = Axes.Both, Colour = idle_colour },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = label,
                        Font = FontUsage.Default.With(size: 14),
                        Colour = Color4.White,
                    },
                };
            }

            public bool OnPressed(KeyBindingPressEvent<GarbusAction> e)
            {
                if (e.Action != Action)
                    return false;

                Lit = true;
                background.Colour = lit_colour;

                // Consume the action this cell owns: returning true marks the raw key/joystick event
                // handled, so a press tested here stops at the panel instead of reaching the screen behind.
                return true;
            }

            public void OnReleased(KeyBindingReleaseEvent<GarbusAction> e)
            {
                if (e.Action == Action)
                {
                    Lit = false;
                    background.Colour = idle_colour;
                }
            }
        }

        // A bounded circle with a dot tracking one analog stick's raw (x, y) position, plus a faint
        // deadzone ring. The dot brightens once the stick passes the catcher's deadzone.
        private partial class StickDisplay : CompositeDrawable
        {
            private const float diameter = 84;
            private const float dot_size = 16;

            private readonly AnalogInputManager analog;
            private readonly HorizontalDirection side;

            private Circle dot = null!;

            public Vector2 DotPosition => dot.Position;

            public StickDisplay(AnalogInputManager analog, HorizontalDirection side)
            {
                this.analog = analog;
                this.side = side;
                Size = new Vector2(diameter);
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Color4 tint = side.ToColour();

                InternalChildren = new Drawable[]
                {
                    new CircularContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        BorderThickness = 2,
                        BorderColour = new Color4(90, 90, 110, 255),
                        Child = new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(28, 28, 38, 255) },
                    },
                    new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(AnalogInputManager.SliderCatcher.DEADZONE),
                        Masking = true,
                        BorderThickness = 1,
                        BorderColour = new Color4(70, 70, 88, 255),
                        Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0, AlwaysPresent = true },
                    },
                    dot = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(dot_size),
                        Colour = tint,
                        Alpha = 0.5f,
                    },
                };
            }

            protected override void Update()
            {
                base.Update();

                var catcher = analog.SliderCatchers[side];

                // catcher.Position is a System.Numerics.Vector2; pull the raw axes out as plain floats.
                float x = catcher.Position.X;
                float y = catcher.Position.Y;

                // Clamp to the unit disk so a diagonal push can't drive the dot past the ring.
                float length = MathF.Sqrt(x * x + y * y);
                if (length > 1f)
                {
                    x /= length;
                    y /= length;
                }

                // Screen space: +x right, +y down — the raw axis convention, so the dot sits where the
                // stick physically points (SliderCatcher's angle uses -y to convert to maths angles).
                float radius = (diameter - dot_size) / 2f;
                dot.Position = new Vector2(x, y) * radius;
                dot.Alpha = catcher.Activated ? 1f : 0.5f;
            }
        }
    }
}
