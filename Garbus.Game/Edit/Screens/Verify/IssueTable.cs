// FillFlowContainer of clickable issue rows for the Verify tab.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens.Verify
{
    /// <summary>
    /// A scrollable list of issue rows. Each row shows the time (mm:ss.fff or "—"), the message,
    /// and the check name. Clicking a row with a non-null time seeks the editor clock to that time.
    /// </summary>
    public partial class IssueTable : BasicScrollContainer
    {
        private readonly FillFlowContainer flow;
        private readonly Action<double> seekTo;

        /// <param name="seekTo">Callback invoked with a chart time (ms) when the user clicks a timed row.</param>
        public IssueTable(Action<double> seekTo)
        {
            this.seekTo = seekTo;

            RelativeSizeAxes = Axes.Both;

            Child = flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 2),
                Padding = new MarginPadding(4),
            };
        }

        /// <summary>Replaces all rows with the supplied issues.</summary>
        public void SetIssues(IReadOnlyList<Issue> issues)
        {
            flow.Clear();

            if (issues.Count == 0)
            {
                flow.Add(new SpriteText
                {
                    Text = "No issues found.",
                    Colour = Color4.LightGreen,
                    Margin = new MarginPadding { Left = 8, Top = 8 },
                });
                return;
            }

            foreach (var issue in issues)
                flow.Add(new IssueRow(issue, seekTo));
        }

        /// <summary>A single clickable row in the issue table.</summary>
        private partial class IssueRow : Container
        {
            private readonly Issue issue;
            private readonly Action<double> seekTo;

            public IssueRow(Issue issue, Action<double> seekTo)
            {
                this.issue = issue;
                this.seekTo = seekTo;

                RelativeSizeAxes = Axes.X;
                Height = 22;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                string timeText = issue.Time.HasValue
                    ? formatTime(issue.Time.Value)
                    : "—";

                Children = new Drawable[]
                {
                    // Time column
                    new SpriteText
                    {
                        X = 0,
                        Width = 90,
                        Text = timeText,
                        Colour = issue.Time.HasValue ? Color4.Yellow : Color4.Gray,
                        Font = FontUsage.Default.With(size: 14),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    // Message column
                    new SpriteText
                    {
                        X = 96,
                        RelativeSizeAxes = Axes.X,
                        Width = 0.6f,
                        Text = issue.Message,
                        Colour = Color4.White,
                        Font = FontUsage.Default.With(size: 14),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    // Check name column
                    new SpriteText
                    {
                        Anchor = Anchor.CentreRight,
                        Origin = Anchor.CentreRight,
                        Width = 180,
                        Text = issue.CheckName,
                        Colour = Color4.LightBlue,
                        Font = FontUsage.Default.With(size: 13),
                    },
                };
            }

            protected override bool OnClick(ClickEvent e)
            {
                if (issue.Time.HasValue)
                    seekTo(issue.Time.Value);
                return true;
            }

            protected override bool OnHover(HoverEvent e)
            {
                if (issue.Time.HasValue)
                    Alpha = 0.75f;
                return true;
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                Alpha = 1f;
            }

            private static string formatTime(double ms)
            {
                // mm:ss.fff
                bool negative = ms < 0;
                double abs = Math.Abs(ms);
                int totalSeconds = (int)(abs / 1000);
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                int milliseconds = (int)(abs % 1000);
                string formatted = $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
                return negative ? "-" + formatted : formatted;
            }
        }
    }
}
