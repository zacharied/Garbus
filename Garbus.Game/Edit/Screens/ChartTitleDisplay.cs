// Top-bar label showing "$Title [$ChartName Lv$Level]". Polls ChartFile/EditorChart each frame
// (matches ResourcesSection's convention) since FilePath/metadata can change from Setup or a
// File → Save at any time, with no dedicated change event for either.

using Garbus.Game.Charts;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osuTK.Graphics;

namespace Garbus.Game.Edit.Screens
{
    public partial class ChartTitleDisplay : SpriteText
    {
        [Resolved]
        private EditorChart editorChart { get; set; } = null!;

        [Resolved]
        private ChartFile chartFile { get; set; } = null!;

        private static readonly Color4 normal_colour = Color4.White;
        private static readonly Color4 unsaved_colour = new Color4(140, 140, 140, 255);

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.CentreLeft;
            Origin = Anchor.CentreLeft;
            Font = FontUsage.Default.With(size: 16);

            // Text starts empty, and SpriteText.IsPresent is false while Text is empty. Drawable.Update()
            // is skipped entirely while !IsPresent, so without this the component would never run the
            // Update() below that actually assigns the text — a permanent chicken-and-egg deadlock.
            AlwaysPresent = true;
        }

        protected override void Update()
        {
            base.Update();

            if (chartFile.FilePath == null)
            {
                Text = "New Chart";
                Colour = unsaved_colour;
                return;
            }

            var meta = editorChart.Metadata;
            string levelText = meta.Level == 0 ? "??" : meta.Level.ToString();
            string bracket = string.IsNullOrEmpty(meta.ChartName) ? $"[Lv{levelText}]" : $"[{meta.ChartName} Lv{levelText}]";

            Text = $"{meta.Title} {bracket}";
            Colour = normal_colour;
        }
    }
}
