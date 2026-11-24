using System;
using System.Collections.Generic;
using CityHighways.Core;

namespace CityHighways.Presentation
{
    /// <summary>
    /// A bounded, ephemeral stack of pre-Undo game states. Only Undo/Redo
    /// transitions use this stack; new actions and sessions clear it.
    /// No state is persisted or resurrected without GameRedo.TryRedo.
    /// </summary>
    public sealed class RedoHistory
    {
        public const int DefaultCapacity = 8;

        readonly List<Game> _states = new List<Game>();
        public int Capacity { get; }
        public int Count => _states.Count;
        public Game Next => _states.Count == 0 ? null : _states[_states.Count - 1];

        public RedoHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public void Clear() => _states.Clear();

        /// <summary>Record a successfully undone command's former state.</summary>
        public void Push(Game beforeUndo)
        {
            if (beforeUndo == null) throw new ArgumentNullException(nameof(beforeUndo));
            _states.Add(beforeUndo);
            if (_states.Count > Capacity)
                _states.RemoveAt(0);
        }

        /// <summary>
        /// Consume exactly the candidate that was successfully replayed.
        /// A stale caller cannot consume a different action out of order.
        /// </summary>
        public bool Consume(Game expected)
        {
            if (expected == null || !ReferenceEquals(Next, expected))
                return false;
            _states.RemoveAt(_states.Count - 1);
            return true;
        }
    }
}
