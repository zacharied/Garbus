// Base for modal dialogs that must own the keyboard while they are open.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Edit.Screens.Dialogs
{
    /// <summary>
    /// A dim-backed, centred modal panel that consumes all keyboard input while visible.
    /// <para>
    /// Three mechanisms are needed to actually own the keyboard, and each covers a case the others
    /// miss:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="OnKeyDown"/> returns <c>true</c> unconditionally, so nothing the dialog does
    /// not itself bind propagates to an ancestor — the editor shell's hotkeys included.</item>
    /// <item>Deriving from <see cref="FocusedOverlayContainer"/> takes focus on show. The focus
    /// manager appends the focused drawable to the *end* of the input queue — dispatched first — even
    /// when blocking would otherwise have removed it, so a text box focused behind the dialog would
    /// keep eating keystrokes without this.</item>
    /// <item><see cref="OverlayContainer.BlockNonPositionalInput"/> strips every drawable queued
    /// before this overlay out of the keyboard queue outright, which is what keeps key bindings and
    /// non-ancestor handlers from seeing input.</item>
    /// </list>
    /// </summary>
    public abstract partial class ModalOverlay : FocusedOverlayContainer
    {
        public const string PanelName = "modal dialog panel";

        private static readonly Vector2 default_panel_size = new Vector2(700, 560);

        /// <summary>The panel body. Subclasses add their content here.</summary>
        protected Container Panel { get; private set; } = null!;

        /// <summary>Size of the centred panel. Settable so tuning scenes can drive it live.</summary>
        public Vector2 PanelSize
        {
            get => Panel.Size;
            set => Panel.Size = value;
        }

        protected override bool BlockNonPositionalInput => true;

        protected ModalOverlay()
        {
            RelativeSizeAxes = Axes.Both;

            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Black,
                Alpha = 0.6f,
            });

            AddInternal(Panel = new Container
            {
                Name = PanelName,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = default_panel_size,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(30, 30, 40, 255),
                },
            });
        }

        /// <summary>
        /// Invoked on Enter. The default does nothing; a dialog with an incomplete selection is free
        /// to ignore the press.
        /// </summary>
        protected virtual void Confirm()
        {
        }

        /// <summary>Invoked on Escape. Dismisses the dialog without committing anything.</summary>
        protected virtual void Cancel() => Hide();

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (!e.Repeat)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        Cancel();
                        break;

                    case Key.Enter:
                    case Key.KeypadEnter:
                        Confirm();
                        break;
                }
            }

            // Everything else is swallowed: nothing the dialog does not itself bind may fall through
            // to the screen behind it.
            return true;
        }

        protected override void PopIn() => this.FadeIn(150);
        protected override void PopOut() => this.FadeOut(150);
    }
}
