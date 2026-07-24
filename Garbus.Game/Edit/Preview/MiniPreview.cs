// The editor Mini preview host: a silent, read-only live gameplay preview of the editor's chart.
// Renders the editor's live GarbusHitObject instances as presentation-only autoHit drawables on a
// clock slaved to the EditorClock, so the preview always shows exactly what the chart currently
// contains — including in-flight edits — without any cloning or shadow state.

using System.Collections.Generic;
using Garbus.Game.Gameplay.Objects.Drawables;
using Garbus.Game.Objects;
using Garbus.Game.Screens;
using Garbus.Game.UI;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

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

        private GarbusPlayfield playfield = null!;
        private readonly Dictionary<GarbusHitObject, DrawableHitObject> drawableMap = new Dictionary<GarbusHitObject, DrawableHitObject>();

        internal GarbusPlayfield PlayfieldForTests => playfield;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            // Slave the whole preview subtree to the editor clock (matches ComposeTab's composer wiring).
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Clock = editorClock,
                Child = playfield = new GarbusPlayfield(interactive: false) { RelativeSizeAxes = Axes.Both },
            };

            foreach (var hitObject in editorChart.HitObjects)
                addDrawable(hitObject);

            editorChart.HitObjectAdded += addDrawable;
            editorChart.HitObjectRemoved += removeDrawable;
            // Updated needs no explicit work: the shared instance's ApplyDefaults fires DefaultsApplied,
            // which re-applies the drawable in place (DrawableHitObject.onDefaultsApplied).
        }

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
                editorChart.HitObjectAdded -= addDrawable;
                editorChart.HitObjectRemoved -= removeDrawable;
            }

            base.Dispose(isDisposing);
        }
    }
}
