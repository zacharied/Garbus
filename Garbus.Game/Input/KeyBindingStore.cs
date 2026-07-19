// Holds the effective controller bindings: GarbusInputManager.DefaultBindings overlaid with per-action
// overrides read from keybindings.json in Garbus storage. Persists only the overrides that differ from
// defaults, so a stale/partial file can never leave an action unbound. Cached at game level by
// GarbusGameBase; resolved by GarbusInputManager (bindings) and passed into ControlsPanel (editing).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Framework.Input.Bindings;
using osu.Framework.Platform;

namespace Garbus.Game.Input
{
    public class KeyBindingStore
    {
        private const string filename = "keybindings.json";

        private readonly Storage storage;
        private readonly Dictionary<GarbusAction, InputKey> effective;

        /// <summary>Raised after any change to the bindings (rebind or reset).</summary>
        public event Action? Changed;

        public KeyBindingStore(Storage storage)
        {
            this.storage = storage;
            effective = new Dictionary<GarbusAction, InputKey>(GarbusInputManager.DefaultBindings);
            load();
        }

        public InputKey GetBinding(GarbusAction action) => effective[action];

        public IEnumerable<IKeyBinding> GetKeyBindings() =>
            effective.Select(b => new KeyBinding(b.Value, b.Key)).ToArray();

        public void Rebind(GarbusAction action, InputKey key)
        {
            effective[action] = key;
            save();
            Changed?.Invoke();
        }

        public void ResetToDefaults()
        {
            foreach (var b in GarbusInputManager.DefaultBindings)
                effective[b.Key] = b.Value;

            save();
            Changed?.Invoke();
        }

        private void load()
        {
            if (!storage.Exists(filename))
                return;

            string text;
            using (var stream = storage.GetStream(filename))
            using (var reader = new StreamReader(stream))
                text = reader.ReadToEnd();

            var raw = JsonConvert.DeserializeObject<Dictionary<string, string>>(text);
            if (raw == null)
                return;

            foreach (var (actionName, keyName) in raw)
            {
                if (Enum.TryParse<GarbusAction>(actionName, out var action)
                    && Enum.TryParse<InputKey>(keyName, out var key))
                    effective[action] = key;
            }
        }

        private void save()
        {
            var overrides = effective
                .Where(b => GarbusInputManager.DefaultBindings[b.Key] != b.Value)
                .ToDictionary(b => b.Key.ToString(), b => b.Value.ToString());

            string text = JsonConvert.SerializeObject(overrides, Formatting.Indented);

            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream);
            writer.Write(text);
        }
    }
}
