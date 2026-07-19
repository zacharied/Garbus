// One remappable action: label on the left, current button on the right. Click to enter listening
// state, then the next gamepad button press is captured and written through the store. Any keyboard
// key (incl. Escape) or losing focus cancels without changing the binding.

using Garbus.Game.Input;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osuTK.Graphics;

namespace Garbus.Game.Settings
{
    public partial class KeyBindingRow : CompositeDrawable
    {
        public GarbusAction Action { get; }

        private readonly KeyBindingStore store;

        private Box background = null!;
        private SpriteText keyText = null!;
        private bool listening;

        private static readonly Color4 idle_colour = new Color4(40, 40, 52, 255);
        private static readonly Color4 listening_colour = new Color4(120, 90, 40, 255);

        public KeyBindingRow(KeyBindingStore store, GarbusAction action)
        {
            this.store = store;
            Action = action;

            RelativeSizeAxes = Axes.X;
            Height = 30;
        }

        public override bool AcceptsFocus => true;

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both, Colour = idle_colour },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Horizontal = 8 },
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Text = Action.GetDescription(),
                            Font = FontUsage.Default.With(size: 15),
                            Colour = Color4.White,
                        },
                        keyText = new SpriteText
                        {
                            Anchor = Anchor.CentreRight,
                            Origin = Anchor.CentreRight,
                            Font = FontUsage.Default.With(size: 15),
                            Colour = new Color4(180, 180, 220, 255),
                        },
                    },
                },
            };

            updateText();
            store.Changed += onStoreChanged;
        }

        private void onStoreChanged() => updateText();

        private void updateText()
        {
            if (!listening)
                keyText.Text = store.GetBinding(Action).ToString();
        }

        protected override bool OnClick(ClickEvent e)
        {
            // Clicking a focusable drawable focuses it; enter listening once focused.
            startListening();
            return true;
        }

        private void startListening()
        {
            listening = true;
            background.Colour = listening_colour;
            keyText.Text = "Press a button…";
        }

        private void stopListening()
        {
            listening = false;
            background.Colour = idle_colour;
            updateText();
        }

        protected override bool OnJoystickPress(JoystickPressEvent e)
        {
            if (!listening)
                return base.OnJoystickPress(e);

            store.Rebind(Action, KeyCombination.FromJoystickButton(e.Button));
            stopListening();
            GetContainingFocusManager()?.ChangeFocus(null);
            return true;
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!listening)
                return base.OnKeyDown(e);

            // Any key cancels (keyboard binding is out of scope); consume Escape so the overlay stays open.
            stopListening();
            GetContainingFocusManager()?.ChangeFocus(null);
            return true;
        }

        protected override void OnFocusLost(FocusLostEvent e)
        {
            if (listening)
                stopListening();
        }

        protected override void Dispose(bool isDisposing)
        {
            store.Changed -= onStoreChanged;
            base.Dispose(isDisposing);
        }
    }
}
