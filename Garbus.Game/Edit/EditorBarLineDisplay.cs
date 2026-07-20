using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using Garbus.Game.Gameplay.Objects;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Gameplay.UI.Scrolling;
using Garbus.Game.Charts.Timing;
using osuTK.Graphics;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Always-visible measure bar lines in the editor compose playfield: one full-width horizontal
    /// line per measure (from <see cref="BarLineGenerator"/>), each labelled with its measure number.
    /// Regenerates whenever the timing changes. Distinct from <see cref="Compose.BeatSnapGrid"/>, whose
    /// lines are transient and near the cursor only.
    /// </summary>
    public partial class EditorBarLineDisplay : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        private readonly ScrollingHitObjectContainer lines = new ScrollingHitObjectContainer();

        private readonly List<BarLine> barLines = new List<BarLine>();
        private ControlPointInfo controlPointInfo = null!;

        /// <summary>The last generated set of bar lines (independent of on-screen culling).</summary>
        public IReadOnlyList<BarLine> BarLines => barLines;

        /// <summary>The track length used for the most recent <see cref="regenerate"/> call.</summary>
        private double lastGeneratedTrackLength;

        public EditorBarLineDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            // Inset the scrolling line container's trailing edge by the hit-zone height so the bar
            // lines share the raised judgement line with the hit objects; lines that have scrolled
            // past it remain visible in the hit zone below.
            Padding = new MarginPadding { Bottom = GarbusEditorPlayfield.JUDGEMENT_LINE_OFFSET };
            InternalChild = lines;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            bindControlPointInfo();
            editorChart.ChartChanged += onChartChanged;
            editorClock.TrackChanged += regenerate;
            regenerate();
        }

        private void onChartChanged(Charts.GarbusChart _, Charts.GarbusChart __) => bindControlPointInfo();

        private void bindControlPointInfo()
        {
            if (controlPointInfo != null)
                controlPointInfo.ControlPointsChanged -= regenerate;
            controlPointInfo = editorChart.ControlPointInfo;
            controlPointInfo.ControlPointsChanged += regenerate;
            regenerate();
        }

        protected override void Update()
        {
            base.Update();

            if (editorClock.TrackLength != lastGeneratedTrackLength)
                regenerate();
        }

        private void regenerate()
        {
            lines.Clear();
            barLines.Clear();
            lastGeneratedTrackLength = editorClock.TrackLength;
            barLines.AddRange(BarLineGenerator.Generate(controlPointInfo, editorClock.TrackLength));

            foreach (var barLine in barLines)
                lines.Add(new DrawableBarLine(barLine));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (editorChart.IsNotNull())
                editorChart.ChartChanged -= onChartChanged;
            if (controlPointInfo.IsNotNull())
                controlPointInfo.ControlPointsChanged -= regenerate;
            if (editorClock.IsNotNull())
                editorClock.TrackChanged -= regenerate;
        }

        private partial class DrawableBarLine : DrawableHitObject
        {
            [Resolved]
            private IScrollingInfo scrollingInfo { get; set; } = null!;

            private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

            public new BarLine HitObject => (BarLine)base.HitObject;

            public DrawableBarLine(BarLine barLine)
                : base(barLine)
            {
                RelativeSizeAxes = Axes.X;
                Height = 2;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                AddInternal(new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White, Alpha = 0.6f });
                AddInternal(new SpriteText
                {
                    Text = HitObject.MeasureIndex.ToString(),
                    Colour = Color4.White,
                    Font = FontUsage.Default.With(size: 12),
                    Margin = new MarginPadding { Left = 4, Bottom = 2 },
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                });

                direction.BindTo(scrollingInfo.Direction);
                direction.BindValueChanged(onDirectionChanged, true);
            }

            private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> dir)
            {
                switch (dir.NewValue)
                {
                    case ScrollingDirection.Up:
                        Anchor = Anchor.TopLeft;
                        Origin = Anchor.CentreLeft;
                        break;

                    case ScrollingDirection.Down:
                        Anchor = Anchor.BottomLeft;
                        Origin = Anchor.CentreLeft;
                        break;
                }
            }

            // Do not fade or clamp lifetime here: the ScrollingHitObjectContainer manages
            // LifetimeStart/LifetimeEnd from the entry so bar lines scroll on/off across the track.
            protected override void UpdateInitialTransforms()
            {
            }
        }
    }
}
