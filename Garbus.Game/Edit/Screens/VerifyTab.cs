// Verify tab: runs all ICheck implementations over the current chart and displays findings.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Edit.Screens.Verify;
using Garbus.Game.Edit.Screens.Verify.Checks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace Garbus.Game.Edit.Screens
{
    public partial class VerifyTab : EditorTabScreen
    {
        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorSong editorSong { get; set; } = null!;

        [Resolved]
        private SongFile songFile { get; set; } = null!;

        private IssueTable issueTable = null!;

        /// <summary>All checks run on Refresh, in display order.</summary>
        private static readonly IReadOnlyList<ICheck> AllChecks = new ICheck[]
        {
            new CheckAudioPresent(),
            new CheckBackgroundPresent(),
            new CheckObjectsBeforeTimeZero(),
            new CheckObjectsBeyondTrackEnd(),
        };

        public VerifyTab()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            issueTable = new IssueTable(time => editorClock.SeekSmoothlyTo(time));

            // Header: title + refresh button. AutoSize to fit content.
            var header = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(12, 0),
                Padding = new MarginPadding { Horizontal = 12, Vertical = 12 },
                Children = new Drawable[]
                {
                    new SpriteText
                    {
                        Text = "Verify",
                        Font = FontUsage.Default.With(size: 22),
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    new BasicButton
                    {
                        Text = "Refresh",
                        Width = 90,
                        Height = 28,
                        Action = refresh,
                    },
                },
            };

            InternalChildren = new Drawable[]
            {
                header,
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = header.Height, Horizontal = 12, Bottom = 12 },
                    Child = issueTable,
                },
            };
        }

        private void refresh()
        {
            var context = new CheckContext(
                Song: editorSong.Song,
                Chart: editorChart.Chart,
                SongFile: songFile,
                ControlPointInfo: editorChart.ControlPointInfo,
                TrackLength: editorClock.TrackLength);

            var issues = AllChecks
                .SelectMany(c => c.Run(context))
                .ToList();

            issueTable.SetIssues(issues);
        }
    }
}
