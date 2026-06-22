using System;
using System.Collections.Generic;
using Faolline.GraphCore;

namespace Faolline.GraphStandard
{
    /// <summary>
    /// Fires an action when a condition holds continuously for a given duration. Headless, no
    /// MonoBehaviour — call <see cref="Tick"/> each frame with elapsed time.
    /// <para>
    /// Use cases: progressive hints (reveal after X seconds of inactivity on a puzzle), delayed
    /// events (NPC appears after 30s in a zone), timed reveals, bonus timers.
    /// </para>
    /// <para>
    /// Each trigger entry is one-shot: once fired, it stays dormant until <see cref="Reset"/>
    /// re-arms it. If the condition becomes false before the delay, the timer resets and starts
    /// over when the condition becomes true again.
    /// </para>
    /// </summary>
    public sealed class TimedTrigger
    {
        private readonly BaseContext _context;
        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>Raised when a trigger fires. Receives the trigger id.</summary>
        public event Action<string> OnTriggered;

        public TimedTrigger(BaseContext context) => _context = context;

        /// <summary>
        /// Registers a trigger: when <paramref name="condition"/> evaluates true continuously for
        /// <paramref name="delaySeconds"/>, <paramref name="action"/> is executed once against the
        /// context. A null condition is treated as always true (unconditional delayed fire).
        /// </summary>
        public void Add(string id, BaseCondition condition, float delaySeconds, BaseAction action)
        {
            if (string.IsNullOrEmpty(id)) return;
            _entries[id] = new Entry
            {
                Condition = condition,
                Delay = delaySeconds,
                Action = action,
                Elapsed = 0f,
                Armed = false,
                Fired = false
            };
        }

        /// <summary>Removes a trigger by id. No-op when absent.</summary>
        public void Remove(string id)
        {
            if (!string.IsNullOrEmpty(id)) _entries.Remove(id);
        }

        /// <summary>Re-arms a trigger that has already fired so it can fire again.</summary>
        public void Reset(string id)
        {
            if (!string.IsNullOrEmpty(id) && _entries.TryGetValue(id, out var e))
            {
                e.Fired = false;
                e.Elapsed = 0f;
                e.Armed = false;
            }
        }

        /// <summary>
        /// Advances all triggers by <paramref name="deltaSeconds"/>. For each: evaluates the
        /// condition, accumulates time while true, resets on false, fires once the delay is reached.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            foreach (var kvp in _entries)
            {
                var e = kvp.Value;
                if (e.Fired) continue;

                bool holds = e.Condition == null || e.Condition.Evaluate(_context);
                if (holds)
                {
                    e.Armed = true;
                    e.Elapsed += deltaSeconds;
                    if (e.Elapsed >= e.Delay)
                    {
                        e.Fired = true;
                        e.Action?.Execute(_context);
                        OnTriggered?.Invoke(kvp.Key);
                    }
                }
                else if (e.Armed)
                {
                    e.Elapsed = 0f;
                    e.Armed = false;
                }
            }
        }

        /// <summary>True when the trigger with <paramref name="id"/> has already fired.</summary>
        public bool HasFired(string id)
            => !string.IsNullOrEmpty(id) && _entries.TryGetValue(id, out var e) && e.Fired;

        /// <summary>Seconds elapsed since the condition became true (0 when not armed or after fire).</summary>
        public float GetElapsed(string id)
            => !string.IsNullOrEmpty(id) && _entries.TryGetValue(id, out var e) && e.Armed ? e.Elapsed : 0f;

        private sealed class Entry
        {
            public BaseCondition Condition;
            public float Delay;
            public BaseAction Action;
            public float Elapsed;
            public bool Armed;
            public bool Fired;
        }
    }
}
