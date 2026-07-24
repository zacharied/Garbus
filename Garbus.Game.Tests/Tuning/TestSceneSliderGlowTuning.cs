// Interactive tuning scene for the slider-body glow: every visual parameter is a slider in the
// test browser's step sidebar, and the playfield loops two endless streams so changes are visible
// immediately — long duration tubes on the right, a dense trickle of little head-only discs on the
// left. Glow parameters are baked into the path's cross-section texture at construction, so each
// change rebuilds the playfield drawables (the loop clock survives rebuilds, so the view doesn't
// jump). [Explicit] so it never runs in a headless "run all"; pick it in the test browser.

using System.Collections.Generic;
using System.Linq;
using Garbus.Game.Core;
using Garbus.Game.Input;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.Tests.Visual;
using Garbus.Game.UI;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Timing;
using osuTK;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneSliderGlowTuning : GarbusTestScene
    {
        private const double stream_start = 2000;
        private const double stream_end = 14_000;

        // Right: long duration tubes, spaced out so at most a couple share the ring at once.
        private const double duration_length = 2000;
        private const double duration_gap = 600;

        // Left: little head-only sliders (glow discs), ~3 per second.
        private const double little_gap = 350;

        private readonly ManualClock manualClock = new ManualClock { Rate = 1 };

        private List<GarbusHitObject>? hitObjects;
        private GarbusPlayfield playfield = null!;

        private double loopStart;
        private double loopEnd;

        // Defaults mirror DrawableSliderBody's current init-property defaults; tweak there once a
        // combination is chosen here.
        private float thickness = 7.5f;
        private float glowSigma = 3.6f;
        private float glowStrength = 27.15f;
        private float glowFalloff = 0.3f;
        private bool showLine;
        private float escapeFadeScale = 1.3f;
        private float uncaughtDim = 0.4f;
        private float playbackRate = 1;
        private float timeRange = 700;

        [Resolved]
        private Gameplay.UI.Scrolling.GarbusScrollingInfo scrollingInfo { get; set; } = null!;

        public TestSceneSliderGlowTuning()
        {
            AddSliderStep("thickness", 1f, 60f, thickness, v => { thickness = v; scheduleRebuild(); });
            AddSliderStep("glow sigma", 0.5f, 60f, glowSigma, v => { glowSigma = v; scheduleRebuild(); });
            AddSliderStep("glow strength", 0.1f, 40f, glowStrength, v => { glowStrength = v; scheduleRebuild(); });
            AddSliderStep("glow falloff", 0.1f, 4f, glowFalloff, v => { glowFalloff = v; scheduleRebuild(); });
            AddToggleStep("show crisp line", v => { showLine = v; scheduleRebuild(); });
            AddSliderStep("escape fade scale", 1.06f, 2f, escapeFadeScale, v => { escapeFadeScale = v; scheduleRebuild(); });
            AddSliderStep("uncaught dim", 0f, 1f, uncaughtDim, v => { uncaughtDim = v; scheduleRebuild(); });

            // Playback/scroll controls act live — no rebuild needed.
            AddSliderStep("playback rate", 0f, 2f, 1f, v => playbackRate = v);
            AddSliderStep("scroll time range", 200f, 2000f, 700f, v =>
            {
                timeRange = v;
                if (IsLoaded) scrollingInfo.TimeRange.Value = v;
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var objects = buildStreams().ToList();

            foreach (var hitObject in objects)
                hitObject.ApplyDefaults();

            // Loop from just before the first object emerges to just after the last one fades out.
            loopStart = objects.Min(o => o.StartTime) - 1000;
            loopEnd = objects.Max(o => o is SliderBody b ? b.EndTime : o.StartTime) + 1500;

            scrollingInfo.TimeRange.Value = timeRange;
            manualClock.CurrentTime = loopStart;

            hitObjects = objects;
            rebuild();
        }

        private static IEnumerable<GarbusHitObject> buildStreams()
        {
            for (double t = stream_start; t < stream_end; t += duration_length + duration_gap)
            {
                yield return new SliderBody
                {
                    AngleDeg = 0,
                    StartTime = t,
                    Side = HorizontalDirection.Right,
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>(new[]
                        {
                            new GarbusPathControlPoint { RotationOffset = 0, TimeOffset = duration_length / 2 },
                            new GarbusPathControlPoint { RotationOffset = 90, TimeOffset = duration_length, SweepEasing = Easing.None },
                        }),
                    },
                };
            }

            for (double t = stream_start; t < stream_end; t += little_gap)
            {
                yield return new SliderBody
                {
                    AngleDeg = 180,
                    StartTime = t,
                    Side = HorizontalDirection.Left,
                    // No control points → head-only slider, rendered as a glow disc.
                    Path = new GarbusPath
                    {
                        ControlPoints = new BindableList<GarbusPathControlPoint>(),
                    },
                };
            }
        }

        private void scheduleRebuild() => Scheduler.AddOnce(rebuild);

        private void rebuild()
        {
            if (hitObjects == null)
                return;

            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = new FramedClock(manualClock),
                Child = new GarbusInputManager
                {
                    Child = playfield = new GarbusPlayfield
                    {
                        Size = Vector2.One,
                    },
                },
            };

            foreach (var hitObject in hitObjects)
                playfield.Add(createDrawable(hitObject));
        }

        private Gameplay.Objects.Drawables.DrawableHitObject createDrawable(GarbusHitObject hitObject) => hitObject switch
        {
            SliderBody body => new Objects.Drawables.DrawableSliderBody(body)
            {
                Thickness = thickness,
                GlowBlurSigma = glowSigma,
                GlowStrength = glowStrength,
                GlowFalloff = glowFalloff,
                ShowLine = showLine,
                EscapeFadeScale = escapeFadeScale,
                UncaughtDimAlpha = uncaughtDim,
            },
            _ => PlayScreen.CreateDrawableRepresentation(hitObject),
        };

        protected override void Update()
        {
            base.Update();

            if (hitObjects == null)
                return;

            manualClock.CurrentTime += Time.Elapsed * playbackRate;

            if (manualClock.CurrentTime > loopEnd)
                manualClock.CurrentTime = loopStart;
        }
    }
}
