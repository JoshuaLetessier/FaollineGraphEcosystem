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

            _params[key] = value;
            FireSubscribers(key, value);
        }

        /// <summary>
        /// Returns the typed parameter value for <paramref name="key"/>.
        /// Throws <see cref="KeyNotFoundException"/> if the key is absent.
        /// </summary>
        public T Get<T>(string key)
        {
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
            if (_params.TryGetValue(key, out var raw))
            {
                value = (T)raw;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>Returns <c>true</c> when <paramref name="key"/> has a stored value.</summary>
        public bool Has(string key) => _params.ContainsKey(key);

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
        public void InitFromGraph(BaseGraph graph)
        {
            foreach (var param in graph.Parameters)
            {
                switch (param.Type)
                {
                    case ParameterType.Bool:
                        if (bool.TryParse(param.DefaultValue, out bool b))
                            _params[param.Key] = b;
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[GraphCore] Cannot parse bool default '{param.DefaultValue}' for key '{param.Key}'. Using default.");
                            _params[param.Key] = default(bool);
                        }
                        break;

                    case ParameterType.Int:
                        if (int.TryParse(param.DefaultValue, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out int i))
                            _params[param.Key] = i;
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[GraphCore] Cannot parse int default '{param.DefaultValue}' for key '{param.Key}'. Using default.");
                            _params[param.Key] = default(int);
                        }
                        break;

                    case ParameterType.Float:
                        if (float.TryParse(param.DefaultValue, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out float f))
                            _params[param.Key] = f;
                        else
                        {
                            UnityEngine.Debug.LogWarning(
                                $"[GraphCore] Cannot parse float default '{param.DefaultValue}' for key '{param.Key}'. Using default.");
                            _params[param.Key] = default(float);
                        }
                        break;

                    case ParameterType.String:
                        _params[param.Key] = param.DefaultValue ?? string.Empty;
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
        }
    }
}
