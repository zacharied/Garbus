// Right-panel details editor for the selected design point. Start and End rows each pair a text box
// with an inline "Now" button (sets that field to the editor clock's current time). For a
// TutorialMessage a message text box is shown. Edits go through the change handler (one undo step
// each): position edits via DesignPointInfo.MoveDesignPoint (structural, so the timeline overlay and
// list refresh off the single event); text edits set the Text bindable in place.

using System;
using System.Globalization;
using Garbus.Game.Charts.Design;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens.Design
{
    public partial class DesignPointSettings : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private IEditorChangeHandler changeHandler { get; set; } = null!;

        public readonly Bindable<DesignPoint?> SelectedPoint = new Bindable<DesignPoint?>();

        private BasicTextBox startBox = null!;
        private BasicTextBox endBox = null!;
        private BasicButton startNowButton = null!;
        private BasicButton endNowButton = null!;
        private BasicTextBox messageBox = null!;

        private bool updatingFromModel;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding(12);

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Children = new Drawable[]
                {
                    new SpriteText { Text = "Start time (ms)" },
                    timeRow(out startBox, out startNowButton, "start-now", useCurrentStart),

                    new SpriteText { Text = "End time (ms)" },
                    timeRow(out endBox, out endNowButton, "end-now", useCurrentEnd),

                    new SpriteText { Text = "Message" },
                    messageBox = new BasicTextBox
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        PlaceholderText = @"Message text (use \n for a line break)",
                        CommitOnFocusLost = true,
                    },
                    new SpriteText
                    {
                        Text = @"Tip: type \n where you want a line break.",
                        Font = FontUsage.Default.With(size: 14),
                        Colour = new Colour4(180, 180, 180, 255),
                    },
                },
            };
        }

        // A textbox that fills the row with a fixed-width "Now" button inline to its right.
        private Drawable timeRow(out BasicTextBox box, out BasicButton nowButton, string buttonName, Action nowAction)
        {
            box = new BasicTextBox { RelativeSizeAxes = Axes.Both, PlaceholderText = "0", CommitOnFocusLost = true };
            nowButton = new BasicButton
            {
                Name = buttonName,
                RelativeSizeAxes = Axes.Y,
                Width = 60,
                Text = "Now",
                Action = nowAction,
            };

            return new GridContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 30,
                ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute, 66), // 60 button + 6 spacing
                },
                Content = new[]
                {
                    new Drawable[]
                    {
                        box,
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Left = 6 },
                            Child = nowButton,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            SelectedPoint.BindValueChanged(_ => updateFromModel(), true);

            startBox.OnCommit += (_, _) => commitStart();
            endBox.OnCommit += (_, _) => commitEnd();
            messageBox.OnCommit += (_, _) => commitText();
        }

        private void updateFromModel()
        {
            var p = SelectedPoint.Value;
            bool has = p != null;

            startBox.ReadOnly = !has;
            endBox.ReadOnly = !has;
            messageBox.ReadOnly = !has;
            startNowButton.Enabled.Value = has;
            endNowButton.Enabled.Value = has;

            if (!has)
            {
                updatingFromModel = true;
                startBox.Text = string.Empty;
                endBox.Text = string.Empty;
                messageBox.Text = string.Empty;
                updatingFromModel = false;
                return;
            }

            updatingFromModel = true;
            startBox.Text = p!.StartTime.ToString("0", CultureInfo.InvariantCulture);
            endBox.Text = p.EndTime.ToString("0", CultureInfo.InvariantCulture);
            messageBox.Text = p is TutorialMessage tm ? tm.Text : string.Empty;
            updatingFromModel = false;
        }

        private void commitStart()
        {
            var p = SelectedPoint.Value;
            if (updatingFromModel || p == null) return;

            if (!double.TryParse(startBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newStart)
                || newStart >= p.EndTime)
            {
                updateFromModel();
                return;
            }

            if (Math.Abs(newStart - p.StartTime) < 0.01) return;

            move(p, newStart, p.EndTime);
        }

        private void commitEnd()
        {
            var p = SelectedPoint.Value;
            if (updatingFromModel || p == null) return;

            if (!double.TryParse(endBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double newEnd)
                || newEnd <= p.StartTime)
            {
                updateFromModel();
                return;
            }

            if (Math.Abs(newEnd - p.EndTime) < 0.01) return;

            move(p, p.StartTime, newEnd);
        }

        private void commitText()
        {
            if (updatingFromModel || SelectedPoint.Value is not TutorialMessage tm) return;
            if (tm.Text == messageBox.Text) return;

            changeHandler.BeginChange();
            tm.Text = messageBox.Text;
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private void move(DesignPoint p, double start, double end)
        {
            changeHandler.BeginChange();
            editorChart.DesignPointInfo.MoveDesignPoint(p, start, end);
            editorChart.SaveState();
            changeHandler.EndChange();
        }

        private void useCurrentStart()
        {
            if (SelectedPoint.Value == null) return;
            startBox.Text = editorClock.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
            commitStart();
        }

        private void useCurrentEnd()
        {
            if (SelectedPoint.Value == null) return;
            endBox.Text = editorClock.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
            commitEnd();
        }

        /// <summary>Test seam: set the start textbox and commit (as if typing + Enter).</summary>
        public void SetStartAndCommit(double start)
        {
            startBox.Text = start.ToString("0.##", CultureInfo.InvariantCulture);
            commitStart();
        }

        /// <summary>Test seam: set the end textbox and commit.</summary>
        public void SetEndAndCommit(double end)
        {
            endBox.Text = end.ToString("0.##", CultureInfo.InvariantCulture);
            commitEnd();
        }

        /// <summary>Test seam: set the message textbox and commit.</summary>
        public void SetTextAndCommit(string text)
        {
            messageBox.Text = text;
            commitText();
        }
    }
}
