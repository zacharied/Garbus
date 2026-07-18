// Bespoke song select (Phase 5). Scans bundled + AppData-folder charts into a grouped/flat list,
// loops an audio preview for the selected chart, and launches the chosen chart into PlayScreen.
// Replaces PlayScreen's hardcoded bundled-chart load on the main-menu Play path.

using System;
using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Charts;
using Garbus.Game.Configuration;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace Garbus.Game.Screens.SongSelect
{
    public partial class SongSelectScreen : Screen
    {
        private const int preview_fade = 200;

        private ChartLibrary library = null!;
        private DirectoryChartSource directorySource = null!;
        private AudioManager audio = null!;

        private FillFlowContainer list = null!;
        private readonly Dictionary<ChartCard, ChartRow> rows = new Dictionary<ChartCard, ChartRow>();

        private readonly Bindable<bool> grouped = new Bindable<bool>(true);

        // Wrapped in a DrawableTrack (and added to the draw hierarchy) so VolumeTo — which requires
        // IDrawable, see AdjustableAudioComponentExtensions — can drive the preview fade; a raw Track
        // is not itself a Drawable. Matches this codebase's DrawableSample pattern in HitSoundContainer.
        private DrawableTrack? previewTrack;

        /// <summary>The scanned song groups (grouped-view model). Populated in load. Exposed for tests.</summary>
        public IReadOnlyList<SongGroup> Groups { get; private set; } = null!;

        /// <summary>The currently selected chart, or null before any selection. Exposed for tests.</summary>
        public ChartCard? SelectedChart { get; private set; }

        /// <summary>Whether the list is grouped by song (true) or flat by level (false).</summary>
        public bool Grouped
        {
            get => grouped.Value;
            set => grouped.Value = value;
        }

        [BackgroundDependencyLoader]
        private void load(Storage storage, ChartStore charts, ITrackStore resourceTracks, AudioManager audio, GarbusConfigManager config)
        {
            this.audio = audio;

            config.BindWith(GarbusSetting.SongSelectGrouped, grouped);

            var resourceSource = new ResourceChartSource(charts, resourceTracks);
            string songsRoot = storage.GetStorageForDirectory("charts").GetFullPath(string.Empty);
            directorySource = new DirectoryChartSource(songsRoot);
            library = new ChartLibrary(directorySource, resourceSource);
            Groups = library.Scan();

            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(18, 18, 26, 255) },
                new BasicScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 56, Bottom = 12, Horizontal = 40 },
                    Child = list = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 3),
                    },
                },
                new SpriteText
                {
                    Padding = new MarginPadding { Top = 16, Left = 40 },
                    Text = "Select a chart",
                    Font = FontUsage.Default.With(size: 28),
                },
                new BasicButton
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Margin = new MarginPadding { Top = 14, Right = 40 },
                    Size = new Vector2(160, 30),
                    Text = "View: …",
                    Action = () => grouped.Value = !grouped.Value,
                }.With(b => viewButton = b),
            };
        }

        private BasicButton viewButton = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();
            grouped.BindValueChanged(_ => rebuild(), true);
        }

        private void rebuild()
        {
            viewButton.Text = grouped.Value ? "View: Grouped" : "View: Flat";
            list.Clear();
            rows.Clear();

            if (grouped.Value)
            {
                foreach (var group in Groups)
                {
                    list.Add(new ChartRow($"{group.Title} — {group.Artist}", null, header: true));
                    foreach (var card in group.Charts)
                        addRow(card);
                }
            }
            else
            {
                foreach (var card in library.AllCharts())
                    addRow(card);
            }

            // Re-apply highlight after a rebuild.
            if (SelectedChart != null && rows.TryGetValue(SelectedChart, out var row))
                row.Selected = true;
        }

        private void addRow(ChartCard card)
        {
            var row = new ChartRow(card.DisplayName, card.Level) { Action = () => Select(card) };
            rows[card] = row;
            list.Add(row);
        }

        /// <summary>Selects a chart, highlighting its row and starting a looping audio preview.</summary>
        public void Select(ChartCard card)
        {
            if (SelectedChart != null && rows.TryGetValue(SelectedChart, out var previous))
                previous.Selected = false;

            SelectedChart = card;

            if (rows.TryGetValue(card, out var row))
                row.Selected = true;

            startPreview(card);
        }

        private void startPreview(ChartCard card)
        {
            stopPreview();

            try
            {
                var track = card.GetTrack(audio);
                if (track == null)
                    return;

                var drawableTrack = new DrawableTrack(track) { Looping = true };
                drawableTrack.Seek(card.PreviewTime ?? 0);
                drawableTrack.Volume.Value = 0;

                previewTrack = drawableTrack;
                // Must be parented before VolumeTo — the transform needs the hierarchy's clock
                // (see osu.Game's MusicController.changeTrack: AddInternal, then VolumeTo).
                AddInternal(drawableTrack);

                drawableTrack.Start();
                drawableTrack.VolumeTo(1, preview_fade);
            }
            catch (Exception)
            {
                // A missing/undecodable audio file just means no preview — selection still works.
                previewTrack = null;
            }
        }

        private void stopPreview()
        {
            var track = previewTrack;
            previewTrack = null;
            if (track == null)
                return;

            track.VolumeTo(0, preview_fade).OnComplete(_ =>
            {
                track.Stop();
                RemoveInternal(track, true);
            });
        }

        /// <summary>Loads the selected chart + a fresh gameplay track and pushes the play screen.</summary>
        public void Launch()
        {
            if (SelectedChart == null)
                return;

            stopPreview();

            var chart = SelectedChart.LoadChart();
            var track = SelectedChart.GetTrack(audio);
            this.Push(new PlayScreen(chart, track));
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat)
                return base.OnKeyDown(e);

            switch (e.Key)
            {
                case Key.Enter:
                case Key.KeypadEnter:
                    Launch();
                    return true;

                case Key.Escape:
                    this.Exit();
                    return true;
            }

            return base.OnKeyDown(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            // Returning from play: rescan (new charts may exist) and resume the preview.
            Groups = library.Scan();
            rebuild();
            if (SelectedChart != null)
                startPreview(SelectedChart);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            stopPreview();
            return base.OnExiting(e);
        }

        protected override void Dispose(bool isDisposing)
        {
            stopPreview();
            directorySource?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
