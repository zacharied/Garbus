// The editor Mini preview host: a silent, read-only live gameplay preview of the editor's chart.
// Renders the editor's live GarbusHitObject instances as presentation-only autoHit drawables on a
// clock slaved to the EditorClock, so the preview always shows exactly what the chart currently
// contains — including in-flight edits — without any cloning or shadow state.

using System;
using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace Garbus.Game.Edit.Preview
{
    /// <summary>
    /// A silent, read-only live gameplay preview of the editor's chart. Renders the editor's live
    /// <see cref="GarbusHitObject"/> instances as presentation-only <c>autoHit</c> drawables on a clock
    /// slaved to the <see cref="EditorClock"/>. Because auto-hit drawables are pure functions of clock
    /// time, the preview is stateless under seek/rewind and needs no tracking beyond an add/remove map.
    /// Shares editor instances (no clone): safe because auto-hit drawables never mutate their hit object.
    /// </summary>
    public partial class MiniPreview : CompositeDrawable
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private EditorClock editorClock { get; set; } = null!;

        /// <summary>
        /// The logical size the playfield renders at before it is scaled to fit the panel. The hit-object
        /// sprites are fixed pixel sizes (e.g. an 80px cardinal note), so to reproduce real gameplay
        /// proportions the whole playfield is rendered at the canonical draw height (the ring's natural
        /// extent, <see cref="GarbusGameBase"/>'s 1366×768 target) and scaled down uniformly to fit.
        /// </summary>
        internal const float ReferenceSize = 768f;

        private GarbusPlayfield playfield = null!;
        private Container scaleContainer = null!;
        private readonly Dictionary<GarbusHitObject, DrawableHitObject> drawableMap = new Dictionary<GarbusHitObject, DrawableHitObject>();

        internal GarbusPlayfield PlayfieldForTests => playfield;
        internal float ContentScaleForTests => scaleContainer.Scale.X;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            // Clip the playfield to the preview bounds. The reference-sized playfield is scaled down and
            // centred, so slider paths / warning arcs near the playfield edge would otherwise draw out to the
            // panel border and fight its rounded stroke. The CornerRadius matches the host panel chrome
            // (InlineChartPreviewPanel) so the clipped content rounds cleanly inside the border.
            Masking = true;
            CornerRadius = 8;

            // Render the playfield at a fixed gameplay-faithful reference size, then scale the whole thing
            // to fit the panel every frame (Update). Slave the subtree to the editor clock (matches
            // ComposeTab's composer wiring).
            InternalChild = scaleContainer = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(ReferenceSize),
                Clock = editorClock,
                Child = playfield = new GarbusPlayfield(interactive: false, miniStyle: true) { RelativeSizeAxes = Axes.Both },
            };

            foreach (var hitObject in editorChart.HitObjects)
                addDrawable(hitObject);

            // Feed the playfield's chord highlighter / warning schedule so chord notes tint correctly.
            refreshChartState();

            editorChart.HitObjectAdded += onHitObjectAdded;
            editorChart.HitObjectRemoved += onHitObjectRemoved;
            editorChart.HitObjectUpdated += onHitObjectUpdated;
            // Adds/removes/updates each rebuild the chord index below; the drawable itself refreshes in
            // place via the shared instance's DefaultsApplied → DrawableHitObject.onDefaultsApplied.
        }

        protected override void Update()
        {
            base.Update();

            // Fit the reference-sized playfield uniformly into the panel (square panel → min dimension).
            float fit = MathF.Min(DrawWidth, DrawHeight) / ReferenceSize;
            scaleContainer.Scale = new Vector2(fit);
        }

        private void onHitObjectAdded(GarbusHitObject hitObject)
        {
            addDrawable(hitObject);
            refreshChartState();
        }

        private void onHitObjectRemoved(GarbusHitObject hitObject)
        {
            removeDrawable(hitObject);
            refreshChartState();
        }

        // A move/re-default can change chord membership (same start time), so rebuild the index; the
        // drawable re-applies itself in place via DefaultsApplied.
        private void onHitObjectUpdated(GarbusHitObject hitObject) => refreshChartState();

        // Rebuild the chord highlight index and warning schedule from the live editor objects. Cheap and
        // idempotent (both just replace their snapshot), so it is safe to call on every edit.
        private void refreshChartState() => playfield.SetHitObjects(editorChart.HitObjects);

        private void addDrawable(GarbusHitObject hitObject)
        {
            if (drawableMap.ContainsKey(hitObject))
                return;

            var drawable = PlayScreen.CreateDrawableRepresentation(hitObject, autoHit: true);
            drawableMap[hitObject] = drawable;
            playfield.Add(drawable);
        }

        private void removeDrawable(GarbusHitObject hitObject)
        {
            if (!drawableMap.Remove(hitObject, out var drawable))
                return;

            playfield.Remove(drawable);
            // Non-pooled: the container detaches with RemoveInternal(..., false) and does NOT dispose.
            // Dispose explicitly, or the drawable stays subscribed to DefaultsApplied and re-applies forever.
            drawable.Dispose();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (editorChart != null)
            {
                editorChart.HitObjectAdded -= onHitObjectAdded;
                editorChart.HitObjectRemoved -= onHitObjectRemoved;
                editorChart.HitObjectUpdated -= onHitObjectUpdated;
            }

            base.Dispose(isDisposing);
        }
    }
}
