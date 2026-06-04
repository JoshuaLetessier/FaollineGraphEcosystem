using System;
using System.Collections.Generic;
using System.Globalization;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Typed parameter blackboard for graph execution. Stores <c>bool</c>, <c>int</c>,
    /// <c>float</c>, and <c>string</c> values by string key. Supports per-key change
    /// notifications, deep cloning (values only, no subscribers), and initialization
    /// from a <see cref="BaseGraph"/>'s declared parameters.
    /// Subclass to add domain-specific state; override <see cref="DeepClone"/> and
    /// <see cref="CreateCloneInstance"/> to preserve additional fields across history snapshots.
    /// </summary>
    public class BaseContext
    {
        private readonly Dictionary<string, object> _params = new Dictionary<string, object>();
        private readonly Dictionary<string, List<Action<object>>> _subs =
            new Dictionary<string, List<Action<object>>>();

        // ── Local-context overlay (0.3.0) ──────────────────────────────────────
        // The persistent global bucket is _params. When a local context is open, _local is a
        // transient overlay: reads resolve local-first then fall through to global; writes route
        // to where the key resolves (local shadow → global → undeclared defaults to local).
        // When no local context is open everything collapses to _params-only (identical to 0.2.0).
        private Dictionary<string, object> _local;
        private bool _localActive;

        // ── Supported types ────────────────────────────────────────────────────

        private static readonly HashSet<Type> _supportedTypes = new HashSet<Type>
        {
            typeof(bool), typeof(int), typeof(float), typeof(string)
        };

        // ── Parameter accessors ────────────────────────────────────────────────

        /// <summary>
        /// Sets a typed parameter value. Fires <see cref="OnParameterChanged"/> subscribers.
        /// <typeparamref name="T"/> must be <c>bool</c>, <c>int</c>, <c>float</c>, or <c>string</c>.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            if (!_supportedTypes.Contains(typeof(T)))
                throw new ArgumentException(
                    $"[GraphCore] Unsupported parameter type: {typeof(T).Name}. " +
                    "Supported types: bool, int, float, string.");

            ResolveWriteBucket(key)[key] = value;
            FireSubscribers(key, value);
        }

        /// <summary>
        /// Returns the bucket a write to <paramref name="key"/> must target: when a local context is
        /// open, the local bucket if it already holds the key (shadow), else the global bucket if it
        /// holds the key (durable global write), else the local bucket (an undeclared key defaults to
        /// local). With no local context open, always the global bucket.
        /// </summary>
        private Dictionary<string, object> ResolveWriteBucket(string key)
        {
            if (_localActive)
            {
                if (_local.ContainsKey(key)) return _local;
                if (_params.ContainsKey(key)) return _params;
                return _local;
            }
            return _params;
        }

        /// <summary>
        /// Returns the typed parameter value for <paramref name="key"/>.
        /// Throws <see cref="KeyNotFoundException"/> if the key is absent.
        /// </summary>
        public T Get<T>(string key)
        {
            if (_localActive && _local.TryGetValue(key, out var localRaw))
                return (T)localRaw;
            if (!_params.TryGetValue(key, out var raw))
                throw new KeyNotFoundException($"[GraphCore] Parameter key not found: '{key}'.");
            return (T)raw;
        }

        /// <summary>
        /// Tries to get the typed parameter value. Returns <c>false</c> and
        /// <c>default(T)</c> when the key is absent; never throws.
        /// </summary>
        public bool TryGet<T>(string key, out T value)
        {
            if (_localActive && _local.TryGetValue(key, out var localRaw))
            {
                value = (T)localRaw;
                return true;
            }
            if (_params.TryGetValue(key, out var raw))
            {
                value = (T)raw;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="key"/> resolves to a stored value — in the active
        /// local context or, failing that, in the global context.
        /// </summary>
        public bool Has(string key)
            => (_localActive && _local.ContainsKey(key)) || _params.ContainsKey(key);

        /// <summary>
        /// Returns a read-only snapshot of the <em>global</em> parameter values (key → boxed value).
        /// Used for serialization (e.g. save/restore). Transient local-context values are deliberately
        /// excluded, so a save taken while a local context is open captures durable global state only.
        /// Types are limited to bool, int, float, string.
        /// </summary>
        public IReadOnlyDictionary<string, object> GetAllParameters()
            => new System.Collections.ObjectModel.ReadOnlyDictionary<string, object>(_params);

        // ── Local-context overlay ──────────────────────────────────────────────

        /// <summary>
        /// <c>true</c> while a local context overlay is open. While open, reads resolve local-first
        /// then fall through to global, and writes route per the resolve-and-write rule.
        /// </summary>
        public bool HasLocalContext => _localActive;

        /// <summary>
        /// Opens a fresh, empty local context layered over the global context. If one is already open
        /// it is discarded and replaced (a <c>[GraphCore]</c> warning is logged — nested local contexts
        /// are not supported).
        /// </summary>
        public void BeginLocalContext()
        {
            if (_localActive)
                UnityEngine.Debug.LogWarning(
                    "[GraphCore] BeginLocalContext called while a local context is already open; " +
                    "discarding the existing one (nested local contexts are not supported).");
            _local = new Dictionary<string, object>();
            _localActive = true;
        }

        /// <summary>
        /// As <see cref="BeginLocalContext()"/>, then seeds the new local context from
        /// <paramref name="seedFrom"/>'s declared parameters (same parsing as
        /// <see cref="InitFromGraph"/>, written into the local overlay). A <c>null</c> graph seeds nothing.
        /// </summary>
        public void BeginLocalContext(BaseGraph seedFrom)
        {
            BeginLocalContext();
            if (seedFrom != null)
                SeedFromGraph(seedFrom, _local);
        }

        /// <summary>
        /// Discards the current local context and all values written into it. Global values are
        /// untouched. No-op (with a <c>[GraphCore]</c> warning) when none is open.
        /// </summary>
        public void EndLocalContext()
        {
            if (!_localActive)
            {
                UnityEngine.Debug.LogWarning(
                    "[GraphCore] EndLocalContext called with no local context open; ignored.");
                return;
            }
            _local = null;
            _localActive = false;
        }

        // ── Change notifications ───────────────────────────────────────────────

        /// <summary>
        /// Subscribes <paramref name="handler"/> to changes on <paramref name="key"/>.
        /// The handler receives the new value boxed as <c>object</c>.
        /// </summary>
        public void OnParameterChanged(string key, Action<object> handler)
        {
            if (!_subs.TryGetValue(key, out var list))
            {
                list = new List<Action<object>>();
                _subs[key] = list;
            }
            list.Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/> from the subscriber list for <paramref name="key"/>.</summary>
        public void OffParameterChanged(string key, Action<object> handler)
        {
            if (_subs.TryGetValue(key, out var list))
                list.Remove(handler);
        }

        private void FireSubscribers(string key, object value)
        {
            if (!_subs.TryGetValue(key, out var list)) return;
            // Iterate over a snapshot to handle re-entrant unsubscribe
            var snapshot = new List<Action<object>>(list);
            foreach (var handler in snapshot)
                handler(value);
        }

        // ── Graph initialization ──────────────────────────────────────────────

        /// <summary>
        /// Populates this context from <paramref name="graph"/>'s declared parameters,
        /// converting each <see cref="ParameterData.DefaultValue"/> string to the correct type.
        /// Parse failures use <c>default(T)</c> and log a <c>[GraphCore]</c> warning.
        /// </summary>
        public void InitFromGraph(BaseGraph graph) => SeedFromGraph(graph, _params);

        /// <summary>
        /// Parses <paramref name="graph"/>'s declared parameters into <paramref name="target"/>,
        /// converting each <see cref="ParameterData.DefaultValue"/> string to the correct type.
        /// Parse failures use <c>default(T)</c> and log a <c>[GraphCore]</c> warning. Shared by
        /// <see cref="InitFromGraph"/> (seeds global) and local-context seeding (seeds the overlay).
        /// </summary>
        private static void SeedFromGraph(BaseGraph graph, Dictionary<string, object> target)
        {
            foreach (var param in graph.Parameters)
            {
                switch (param.Type)
                {
                    case ParameterType.Bool:
                        if (bool.TryParse(param.DefaultValue, out bool b))
                            target[param.Key] = b;
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[GraphCore] Cannot parse bool default '{param.DefaultValue}' for key '{param.Key}'. Using default.");
                            target[param.Key] = default(bool);
                        }
                        break;

                    case ParameterType.Int:
                        if (int.TryParse(param.DefaultValue, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int i))
                            target[param.Key] = i;
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[GraphCore] Cannot parse int default '{param.DefaultValue}' for key '{param.Key}'. Using default.");
                            target[param.Key] = default(int);
                        }
                        break;

                    case ParameterType.Float:
                        if (float.TryParse(param.DefaultValue, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out float f))
                            target[param.Key] = f;
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[GraphCore] Cannot parse float default '{param.DefaultValue}' for key '{param.Key}'. Using default.");
                            target[param.Key] = default(float);
                        }
                        break;

                    case ParameterType.String:
                        target[param.Key] = param.DefaultValue ?? string.Empty;
                        break;
                }
            }
        }

        // ── Cloning ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a new instance of this context type via <see cref="CreateCloneInstance"/>,
        /// with all parameter values copied. Subscriptions are intentionally NOT copied.
        /// Override in subclasses alongside <see cref="CreateCloneInstance"/> to copy
        /// domain-specific fields.
        /// </summary>
        public virtual BaseContext DeepClone()
        {
            var clone = CreateCloneInstance();
            foreach (var kvp in _params)
                clone._params[kvp.Key] = kvp.Value;
            if (_localActive)
            {
                clone._local = new Dictionary<string, object>();
                foreach (var kvp in _local)
                    clone._local[kvp.Key] = kvp.Value;
                clone._localActive = true;
            }
            return clone;
        }

        /// <summary>
        /// Creates the blank instance used by <see cref="DeepClone"/>. Override in subclasses
        /// to return the correct derived type so that <c>base.DeepClone()</c> produces an
        /// instance that can be safely cast to the subclass.
        /// </summary>
        protected virtual BaseContext CreateCloneInstance() => new BaseContext();

        // ── Internal restore helper ───────────────────────────────────────────

        /// <summary>
        /// Replaces all parameter values in this context with those from <paramref name="source"/>.
        /// Subscribers are preserved. Used by <see cref="BaseRunner"/> to restore a history
        /// snapshot into the live context object without changing its reference.
        /// </summary>
        internal void CopyValuesFrom(BaseContext source)
        {
            _params.Clear();
            foreach (var kvp in source._params)
                _params[kvp.Key] = kvp.Value;

            // Restore the local-context overlay (and its open/closed state) in place, so step-back
            // across a scope boundary reproduces the exact runtime state. Subscribers are preserved.
            if (source._localActive)
            {
                _local = new Dictionary<string, object>();
                foreach (var kvp in source._local)
                    _local[kvp.Key] = kvp.Value;
                _localActive = true;
            }
            else
            {
                _local = null;
                _localActive = false;
            }
        }
    }
}
