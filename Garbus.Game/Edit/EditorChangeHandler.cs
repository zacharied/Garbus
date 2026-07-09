// Vendored from osu.Game (https://github.com/ppy/osu) — osu.Game/Screens/Edit/EditorChangeHandler.cs
// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See https://github.com/ppy/osu/blob/master/LICENCE for full licence text.
// Adapted for Garbus: removed osu.Game.Rulesets.Objects.HitObject reference; replaced
// stream.ComputeSHA2Hash() with a local SHA-256 helper (avoids pulling osu.Framework.Extensions
// into the public API surface); namespace changed to Garbus.Game.Edit; Undo/Redo are added as
// thin wrappers around RestoreState(-1)/RestoreState(+1) for ergonomics.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using osu.Framework.Bindables;

namespace Garbus.Game.Edit
{
    /// <summary>
    /// Abstract base that maintains a snapshot stack (up to <see cref="MAX_SAVED_STATES"/> entries)
    /// and exposes undo/redo over serialized state bytes.
    /// Concrete subclasses implement <see cref="WriteCurrentStateToStream"/> (serialize) and
    /// <see cref="ApplyStateChange"/> (patch the live model from two byte arrays).
    /// </summary>
    public abstract partial class EditorChangeHandler : TransactionalCommitComponent, IEditorChangeHandler
    {
        public readonly Bindable<bool> CanUndo = new Bindable<bool>();
        public readonly Bindable<bool> CanRedo = new Bindable<bool>();

        public event Action? OnStateChange;

        private readonly List<byte[]> savedStates = new List<byte[]>();

        private int currentState = -1;

        /// <summary>
        /// A SHA-256 hex string representing the current visible editor state.
        /// Used for dirty-tracking (compare against a baseline hash to detect unsaved changes).
        /// </summary>
        public string CurrentStateHash
        {
            get
            {
                ensureStateSaved();
                return computeHash(savedStates[currentState]);
            }
        }

        /// <summary>Guard that prevents <see cref="UpdateState"/> from pushing a new snapshot while we are
        /// in the middle of applying a restore.</summary>
        private bool isRestoring;

        public const int MAX_SAVED_STATES = 50;

        public override void BeginChange()
        {
            ensureStateSaved();
            base.BeginChange();
        }

        private void ensureStateSaved()
        {
            if (savedStates.Count == 0)
                SaveState();
        }

        protected override void UpdateState()
        {
            if (isRestoring)
                return;

            using (var stream = new MemoryStream())
            {
                WriteCurrentStateToStream(stream);
                byte[] newState = stream.ToArray();

                // Skip if the new state is binary-equal to the current one — unless this is the very first state.
                if (savedStates.Count > 0 && newState.SequenceEqual(savedStates[currentState]))
                    return;

                // Discard any redo history ahead of the current position.
                if (currentState < savedStates.Count - 1)
                    savedStates.RemoveRange(currentState + 1, savedStates.Count - currentState - 1);

                // Evict the oldest entry when the cap is reached.
                if (savedStates.Count > MAX_SAVED_STATES)
                    savedStates.RemoveAt(0);

                savedStates.Add(newState);
                currentState = savedStates.Count - 1;

                OnStateChange?.Invoke();
                updateBindables();
            }
        }

        /// <summary>Undo one step (convenience wrapper around <see cref="RestoreState"/>).</summary>
        public void Undo() => RestoreState(-1);

        /// <summary>Redo one step (convenience wrapper around <see cref="RestoreState"/>).</summary>
        public void Redo() => RestoreState(+1);

        public void RestoreState(int direction)
        {
            if (TransactionActive)
                return;

            if (savedStates.Count == 0)
                return;

            int newState = Math.Clamp(currentState + direction, 0, savedStates.Count - 1);
            if (currentState == newState)
                return;

            isRestoring = true;

            ApplyStateChange(savedStates[currentState], savedStates[newState]);

            currentState = newState;
            isRestoring = false;

            OnStateChange?.Invoke();
            updateBindables();
        }

        /// <summary>
        /// Write a serialized snapshot of the current state to <paramref name="stream"/>.
        /// </summary>
        protected abstract void WriteCurrentStateToStream(MemoryStream stream);

        /// <summary>
        /// Apply <paramref name="newState"/> to the live model, starting from <paramref name="previousState"/>.
        /// </summary>
        protected abstract void ApplyStateChange(byte[] previousState, byte[] newState);

        private void updateBindables()
        {
            CanUndo.Value = savedStates.Count > 0 && currentState > 0;
            CanRedo.Value = currentState < savedStates.Count - 1;
        }

        private static string computeHash(byte[] data)
        {
            byte[] hash = SHA256.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
