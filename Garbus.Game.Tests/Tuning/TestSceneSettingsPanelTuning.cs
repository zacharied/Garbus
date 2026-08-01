// Interactive tuning scene for the settings panel's floating header and section styling: every
// visual parameter is a slider in the test browser's step sidebar and applies live to the open
// overlay. The panel is deliberately short so the content overflows and the header shadow has rows
// to fall on. [Explicit] so it never runs in a headless "run all"; pick it in the test browser.

using System;
using Garbus.Game.Settings;
using Garbus.Game.Tests.Visual;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osuTK.Graphics;

namespace Garbus.Game.Tests.Tuning
{
    [TestFixture]
    [Explicit]
    public partial class TestSceneSettingsPanelTuning : GarbusTestScene
    {
        private const float panel_height = 380;

        private SettingsOverlay overlay = null!;

        // Defaults mirror SettingsPanelHeader's and SettingsSection's current constants; tweak there
        // once a combination is chosen here.
        private float headerHeight = 56;
        private float headerBrightness = 34;
        private float shadowRadius = 12;
        private float shadowOffsetY = 1.15f;
        private float shadowAlpha = 0.55f;
        private float labelBrightness = 150;
        private float dividerAlpha = 0.47f;

        public TestSceneSettingsPanelTuning()
        {
            AddSliderStep("header height", 32f, 120f, headerHeight, v => { headerHeight = v; apply(); });
            AddSliderStep("header brightness", 0f, 120f, headerBrightness, v => { headerBrightness = v; apply(); });
            AddSliderStep("shadow radius", 0f, 40f, shadowRadius, v => { shadowRadius = v; apply(); });
            AddSliderStep("shadow offset Y", -10f, 20f, shadowOffsetY, v => { shadowOffsetY = v; apply(); });
            AddSliderStep("shadow alpha", 0f, 1f, shadowAlpha, v => { shadowAlpha = v; apply(); });
            AddSliderStep("section label brightness", 60f, 255f, labelBrightness, v => { labelBrightness = v; apply(); });
            AddSliderStep("divider alpha", 0f, 1f, dividerAlpha, v => { dividerAlpha = v; apply(); });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Child = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = panel_height,
                Child = overlay = new SettingsOverlay(),
            };

            overlay.Show();

            // The overlay builds its drawables asynchronously; apply once they exist.
            Schedule(apply);
        }

        private void apply()
        {
            if (overlay?.IsLoaded != true)
                return;

            overlay.HeaderHeight = headerHeight;

            foreach (var header in overlay.ChildrenOfType<SettingsPanelHeader>())
            {
                // The header reads a touch bluer than neutral, matching the panel body's tint.
                header.BackgroundColour = tinted(headerBrightness, 1.4f);
                header.ShadowColour = new Color4(0, 0, 0, toByte(shadowAlpha * 255));
                header.ShadowRadius = shadowRadius;
                header.ShadowOffsetY = shadowOffsetY;
            }

            foreach (var section in overlay.ChildrenOfType<SettingsSection>())
            {
                section.LabelColour = tinted(labelBrightness, 1.17f);
                section.DividerColour = new Color4(90, 90, 115, toByte(dividerAlpha * 255));
            }
        }

        // A neutral grey pushed toward blue by scaling only its blue channel.
        private static Color4 tinted(float brightness, float blueScale) =>
            new Color4(toByte(brightness), toByte(brightness), toByte(brightness * blueScale), 255);

        private static byte toByte(float v) => (byte)Math.Clamp(v, 0, 255);
    }
}
