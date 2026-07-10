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

        /// <summary>The last generated set of bar lines (independent of on-screen culling).</summary>
        public IReadOnlyList<BarLine> BarLines => barLines;

        /// <summary>The track length used for the most recent <see cref="regenerate"/> call.</summary>
        private double lastGeneratedTrackLength;

        public EditorBarLineDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            InternalChild = lines;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            editorChart.ControlPointInfo.ControlPointsChanged += regenerate;
            editorClock.TrackChanged += regenerate;
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
            barLines.AddRange(BarLineGenerator.Generate(editorChart.ControlPointInfo, editorClock.TrackLength));

            foreach (var barLine in barLines)
                lines.Add(new DrawableBarLine(barLine));
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            if (editorChart.IsNotNull())
                editorChart.ControlPointInfo.ControlPointsChanged -= regenerate;
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
