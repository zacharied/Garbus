// A label + BasicTextBox row used in the Setup tab's MetadataSection.
// Commit fires on Enter and on focus loss (CommitOnFocusLost).
// The caller wires the commit callback in the constructor.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens.Setup
{
    /// <summary>
    /// A horizontal row with a label and a <see cref="BasicTextBox"/>.
    /// The commit callback fires when the user commits (Enter key or focus loss)
    /// and the value has changed.
    /// </summary>
    public partial class FormRow : FillFlowContainer
    {
        private readonly CommittableTextBox textBox;
        private readonly Action<string> onCommit;
        private string lastCommittedValue;

        /// <summary>Exposes the textbox so tests can read the current value.</summary>
        public BasicTextBox TextBox => textBox;

        public FormRow(string label, string initialValue, Action<string> commitCallback)
        {
            onCommit = commitCallback;
            lastCommittedValue = initialValue;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Horizontal;
            Spacing = new Vector2(8, 0);
            Padding = new MarginPadding { Vertical = 4 };

            textBox = new CommittableTextBox
            {
                Width = 300,
                Height = 30,
                CommitOnFocusLost = true,
                Text = initialValue,
            };

            // OnCommit fires on Enter key and on focus loss (when CommitOnFocusLost is true).
            textBox.OnCommit += (_, _) =>
            {
                string current = textBox.Text;
                // Skip if the value didn't change since the last commit.
                if (current == lastCommittedValue)
                    return;

                lastCommittedValue = current;
                onCommit(current);
            };

            InternalChildren = new Drawable[]
            {
                new SpriteText
                {
                    Text = label,
                    Font = FontUsage.Default.With(size: 16),
                    Width = 160,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
                textBox,
            };
        }

        /// <summary>
        /// Programmatically commits the current textbox value and fires the commit callback if changed.
        /// Intended for tests; production code uses focus loss / Enter key.
        /// </summary>
        public void TriggerCommit()
        {
            string current = textBox.Text;
            if (current == lastCommittedValue)
                return;

            lastCommittedValue = current;
            onCommit(current);
        }

        /// <summary>Subclass that exposes <see cref="Commit"/> publicly so tests can call it.</summary>
        private partial class CommittableTextBox : BasicTextBox
        {
            public new void Commit() => base.Commit();
        }
    }
}
