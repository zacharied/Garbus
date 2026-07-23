using System;
using Garbus.Game.Input;
using Garbus.Resources;
using NUnit.Framework;
using osu.Framework.IO.Stores;

namespace Garbus.Game.Tests.Input
{
    [TestFixture]
    public class GamepadButtonIconsTest
    {
        [Test]
        public void ResolvesExpectedPath()
        {
            Assert.That(GamepadButtonIcons.ResolveTexturePath(GamepadButton.FaceSouth, GamepadType.DualSense),
                Is.EqualTo("Icons/Gamepad/DS5/cross"));
        }

        [Test]
        public void DefaultTypeIsDualSense()
        {
            Assert.That(GamepadButtonIcons.ResolveTexturePath(GamepadButton.FaceEast),
                Is.EqualTo(GamepadButtonIcons.ResolveTexturePath(GamepadButton.FaceEast, GamepadType.DualSense)));
        }

        [Test]
        public void EveryDualSenseButtonResolves([Values] GamepadButton button)
        {
            Assert.That(GamepadButtonIcons.ResolveTexturePath(button, GamepadType.DualSense),
                Is.Not.Null, $"DualSense has no icon mapping for {button}");
        }

        // The rename is only correct if every resolved path names a real embedded texture. This fails
        // loudly if an asset is missing or misnamed relative to the resolver's file map.
        [Test]
        public void EveryResolvedIconExistsAsResource([Values] GamepadButton button)
        {
            string? path = GamepadButtonIcons.ResolveTexturePath(button, GamepadType.DualSense);
            Assert.That(path, Is.Not.Null);

            using var resources = new DllResourceStore(typeof(GarbusResources).Assembly);
            byte[]? data = resources.Get($"Textures/{path}.png");

            Assert.That(data, Is.Not.Null.And.Length.GreaterThan(0),
                $"Missing texture resource for {button}: Textures/{path}.png");
        }

        // Guards the input bridge: every game action must map to a button (the switch throws otherwise).
        [Test]
        public void EveryGarbusActionMapsToAButton([Values] GarbusAction action)
        {
            Assert.DoesNotThrow(() => action.ToGamepadButton());
        }

        [Test]
        public void ActionMappingSplitsDpadAndFace()
        {
            Assert.That(GarbusAction.ButtonS1.ToGamepadButton(), Is.EqualTo(GamepadButton.DpadSouth));
            Assert.That(GarbusAction.ButtonS2.ToGamepadButton(), Is.EqualTo(GamepadButton.FaceSouth));
        }
    }
}
