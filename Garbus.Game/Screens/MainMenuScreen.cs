// Main menu — entry point for the game. Three actions: Play, New Chart, Open Chart.

using System;
using Garbus.Game.Charts;
using Garbus.Game.Charts.Timing;
using Garbus.Game.Edit.Screens;
using Garbus.Game.Edit.Screens.Dialogs;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;

namespace Garbus.Game.Screens
{
    public partial class MainMenuScreen : Screen
    {
        private Container dialogOverlay = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = new Color4(18, 18, 26, 255),
                },
                new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Children = new Drawable[]
                    {
                        new SpriteText
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "Garbus",
                            Font = FontUsage.Default.With(size: 48),
                            Colour = Color4.White,
                        },
                        new BasicButton
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "Play",
                            Size = new Vector2(200, 40),
                            Action = onPlay,
                        },
                        new BasicButton
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "New Chart",
                            Size = new Vector2(200, 40),
                            Action = onNewChart,
                        },
                        new BasicButton
                        {
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Text = "Open Chart",
                            Size = new Vector2(200, 40),
                            Action = onOpenChart,
                        },
                    },
                },
                dialogOverlay = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
            };
        }

        private void onPlay()
        {
            this.Push(new PlayScreen());
        }

        private void onNewChart()
        {
            var chart = new GarbusChart();
            chart.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            var chartFile = new ChartFile(chart);
            this.Push(new GarbusEditor(chartFile));
        }

        private void onOpenChart()
        {
            var dialog = new OpenChartDialog(path =>
            {
                try
                {
                    var chartFile = ChartFile.Load(path);
                    this.Push(new GarbusEditor(chartFile));
                }
                catch (Exception ex)
                {
                    showError($"Failed to open chart:\n{ex.Message}");
                }
            });

            dialogOverlay.Child = dialog;
            dialog.Show();
        }

        private void showError(string message)
        {
            var dialog = new ConfirmDialog(message, ("OK", () => { }));
            dialogOverlay.Child = dialog;
            dialog.Show();
        }
    }
}
