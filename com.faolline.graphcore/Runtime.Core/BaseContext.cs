using System;
using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Typed parameter blackboard for graph execution. Stores <c>bool</c>, <c>int</c>, <c>float</c>,
    /// <c>string</c>, and any type registered via <see cref="BaseContextTypeRegistry"/> (the Unity engine
    /// layer registers <c>Vector2</c>, <c>Vector3</c>, and <c>Color</c> — see
    /// <c>GraphCoreUnityBootstrap</c> in the Runtime assembly) by string key. Supports per-key change
    /// notifications, deep cloning (values only, no subscribers), and seeding from graph-declared
    /// parameters (see <c>BaseContextGraphExtensions.InitFromGraph</c> in the Runtime assembly — graph
    /// assets are a Unity concept, so that seeding lives there, not here).
    /// <para>
    /// <b>Boxing note:</b> values are stored in a <c>Dictionary&lt;string, object&gt;</c>, so every
    /// <see cref="Set{T}"/> of a value type allocates a box, and every <see cref="Get{T}"/> unboxes. This
    /// is negligible at narrative rhythm (once per node transition) but generates GC pressure if called
    /// per-frame. For hot-loop state, prefer <see cref="RaiseSignal(string)">signals</see> (transient,
    /// never cloned/saved) or a typed field on a <see cref="BaseContext"/> subclass.
    /// </para>
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

        // ── Signal channel (0.4.0) ─────────────────────────────────────────────
        // Signals are TRANSIENT events, kept deliberately separate from the typed-parameter store
        // (_params): they never appear in GetAllVariables/DeepClone/CopyValuesFrom, so they never
        // pollute saves or history snapshots. Both dictionaries are lazily allocated, so a context that
        // never touches signals pays nothing.
        //
        // _raisedSignals (0.22.0): DURABLE history of every signal name that has ever been raised in
        // this context. Unlike _lastSignals (a per-name cache of the last args), this set accumulates
        // across the whole run and IS captured by DeepClone/CopyValuesFrom/GetAllRaisedSignals so that
        // save/restore and history step-back can reproduce "has this signal been raised?" checks.
        private Dictionary<string, List<Action<SignalArgs>>> _signalSubs;
        private Dictionary<string, SignalArgs> _lastSignals;
        private HashSet<string> _raisedSignals;

        // ── Collections (0.5.0; ordered + quantities since 0.31.0) ──────────────
        // Named string collections, in a keyspace independent from _params. DURABLE state (unlike signals):
        // captured by DeepClone/CopyValuesFrom and exposed via GetAllCollections for saving. Global-only:
        // never routed through the local-context overlay. Both dictionaries are lazily allocated.
        //
        // Each bucket is an ORDERED multiset: distinct items in insertion order, each with a quantity ≥ 1.
        // The two-tier API keeps the original set semantics as the default and adds stacking as an
        // explicit opt-in:
        //   • AddToCollection(key, item)        — ensure-present, idempotent (unchanged since 0.5.0).
        //   • AddToCollection(key, item, count) — additive: +count to the item's quantity (stacking).
        //   • RemoveFromCollection(key, item)        — remove the item entirely, whatever its quantity.
        //   • RemoveFromCollection(key, item, count) — decrement by count, removing at/under zero.
        // CollectionCount stays the DISTINCT item count (unchanged meaning); CollectionItemCount reads one
        // item's quantity. GetCollection now yields distinct items in insertion order (was arbitrary
        // hash-set order) — no consumer visibly depended on the old order, so this is not a behavior break.
        private Dictionary<string, CollectionBucket> _collections;
        private Dictionary<string, List<Action<string>>> _collectionSubs;

        // Ordered multiset: a List for insertion order + a Dictionary for O(1) quantity lookups. Collections
        // are small in practice (dozens of entries, narrative rhythm, never per-frame), so the O(n)
        // list-removal on a full item removal is not a concern at this scale.
        private sealed class CollectionBucket
        {
            private readonly List<string> _order = new List<string>();
            private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

            public int DistinctCount => _order.Count;
            public IReadOnlyList<string> Order => _order;

            public int CountOf(string item) => _counts.TryGetValue(item, out var c) ? c : 0;
            public bool Contains(string item) => _counts.ContainsKey(item);

            /// <summary>Ensures the item is present at quantity ≥ 1. Returns true only if newly inserted.</summary>
            public bool EnsurePresent(string item)
            {
                if (_counts.ContainsKey(item)) return false;
                _counts[item] = 1;
                _order.Add(item);
                return true;
            }

            /// <summary>Adds <paramref name="count"/> (≥ 1) to the item's quantity, inserting it if absent.</summary>
            public void Increment(string item, int count)
            {
                if (_counts.TryGetValue(item, out var cur)) _counts[item] = cur + count;
                else { _counts[item] = count; _order.Add(item); }
            }

            /// <summary>Removes the item entirely, whatever its quantity. Returns true if it was present.</summary>
            public bool RemoveAll(string item)
            {
                if (!_counts.Remove(item)) return false;
                _order.Remove(item);
                return true;
            }

            /// <summary>Decrements the item's quantity by <paramref name="count"/> (≥ 1), removing it entirely
            /// at or under zero. Returns true when the quantity actually changed (item was present).</summary>
            public bool Decrement(string item, int count)
            {
                if (!_counts.TryGetValue(item, out var cur)) return false;
                var next = cur - count;
                if (next <= 0) { _counts.Remove(item); _order.Remove(item); }
                else _counts[item] = next;
                return true;
            }

            public void Clear() { _order.Clear(); _counts.Clear(); }

            public CollectionBucket Clone()
            {
                var c = new CollectionBucket();
                c._order.AddRange(_order);
                foreach (var kv in _counts) c._counts[kv.Key] = kv.Value;
                return c;
            }
        }

        // ── Supported types ────────────────────────────────────────────────────
        // Core only knows the four true primitives. Vector2/Vector3/Color (and any other engine-specific
        // value type) are added at runtime by BaseContextTypeRegistry — Core must stay compilable in a
        // noEngineReferences assembly, so it cannot name UnityEngine types itself. See
        // GraphCoreUnityBootstrap (Runtime assembly) for where Vector2/Vector3/Color get registered.

        private static readonly HashSet<Type> _supportedTypes = new HashSet<Type>
        {
            typeof(bool), typeof(int), typeof(float), typeof(string)
        };

        private static bool IsSupportedType(Type type) =>
            _supportedTypes.Contains(type) || BaseContextTypeRegistry.IsRegistered(type);

        // ── Variable accessors ────────────────────────────────────────────────

        /// <summary>
        /// Sets a typed parameter value. Fires <see cref="OnVariableChanged"/> subscribers.
        /// <typeparamref name="T"/> must be <c>bool</c>, <c>int</c>, <c>float</c>, <c>string</c>, or a
        /// type registered via <see cref="BaseContextTypeRegistry"/>.
        /// </summary>
        public void Set<T>(string key, T value)
        {
            if (!IsSupportedType(typeof(T)))
                throw new ArgumentException(
                    $"[GraphCore] Unsupported parameter type: {typeof(T).Name}. " +
                    "Supported types: bool, int, float, string, plus any type registered via BaseContextTypeRegistry.");

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
                throw new KeyNotFoundException($"[GraphCore] Variable key not found: '{key}'.");
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
        /// Types are limited to bool, int, float, string, plus any type registered via
        /// <see cref="BaseContextTypeRegistry"/>.
        /// </summary>
        public IReadOnlyDictionary<string, object> GetAllVariables()
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
                GraphLog.Warning(
                    "[GraphCore] BeginLocalContext called while a local context is already open; " +
                    "discarding the existing one (nested local contexts are not supported).");
            _local = new Dictionary<string, object>();
            _localActive = true;
        }

        /// <summary>
        /// Discards the current local context and all values written into it. Global values are
        /// untouched. No-op (with a <c>[GraphCore]</c> warning) when none is open.
        /// </summary>
        public void EndLocalContext()
        {
            if (!_localActive)
            {
                GraphLog.Warning(
                    "[GraphCore] EndLocalContext called with no local context open; ignored.");
                return;
            }
            _local = null;
            _localActive = false;
        }

        // ── Signal channel ─────────────────────────────────────────────────────

        /// <summary>
        /// Raises a transient signal with no payload. Every current subscriber of <paramref name="name"/>
        /// is notified (broadcast). Raising a name with no subscribers is a no-op (the last-signal store is
        /// still updated). A null/empty name logs a <c>[GraphCore]</c> warning and is ignored.
        /// </summary>
        public void RaiseSignal(string name) => RaiseSignalInternal(name, false, null);

        /// <summary>
        /// Raises a transient signal carrying a single scalar payload. <typeparamref name="T"/> must be
        /// <c>bool</c>, <c>int</c>, <c>float</c>, <c>string</c>, or a type registered via
        /// <see cref="BaseContextTypeRegistry"/> (parity with <see cref="Set{T}"/>).
        /// Delivery and naming rules match <see cref="RaiseSignal(string)"/>.
        /// </summary>
        public void RaiseSignal<T>(string name, T payload)
        {
            if (!IsSupportedType(typeof(T)))
                throw new ArgumentException(
                    $"[GraphCore] Unsupported signal payload type: {typeof(T).Name}. " +
                    "Supported types: bool, int, float, string, plus any type registered via BaseContextTypeRegistry.");
            RaiseSignalInternal(name, true, payload);
        }

        private void RaiseSignalInternal(string name, bool hasPayload, object payload)
        {
            if (string.IsNullOrEmpty(name))
            {
                GraphLog.Warning(
                    "[GraphCore] RaiseSignal called with a null or empty name; ignored.");
                return;
            }

            (_raisedSignals ??= new HashSet<string>()).Add(name);
            var args = new SignalArgs(name, hasPayload, payload);
            (_lastSignals ??= new Dictionary<string, SignalArgs>())[name] = args;

            if (_signalSubs != null && _signalSubs.TryGetValue(name, out var list) && list.Count > 0)
            {
                // Iterate a snapshot so subscribe/unsubscribe during delivery is re-entrant safe.
                var snapshot = new List<Action<SignalArgs>>(list);
                foreach (var handler in snapshot)
                    handler(args);
            }

            // Wildcard delivery, AFTER per-name handlers (mirrors OnAnyVariableChanged). Lets a
            // reactive consumer (e.g. QuestEvaluator.EnableAutoEvaluate) re-evaluate on any signal
            // without knowing the name in advance.
            if (_anySignalSubs != null && _anySignalSubs.Count > 0)
            {
                var snapshot = new List<Action<string>>(_anySignalSubs);
                foreach (var handler in snapshot)
                    handler(name);
            }
        }

        private List<Action<string>> _anySignalSubs;

        /// <summary>
        /// Subscribes <paramref name="handler"/> to ANY raised signal. The handler receives the signal
        /// name and fires AFTER the per-name <see cref="OnSignal"/> handlers. Multiple handlers supported.
        /// Mirrors <see cref="OnAnyVariableChanged"/> / <see cref="OnAnyCollectionChanged"/>.
        /// </summary>
        public void OnAnySignalRaised(Action<string> handler)
        {
            if (handler == null) return;
            (_anySignalSubs ??= new List<Action<string>>()).Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/> from the wildcard signal list.</summary>
        public void OffAnySignalRaised(Action<string> handler)
        {
            _anySignalSubs?.Remove(handler);
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to the signal named <paramref name="name"/>. Many
        /// handlers may listen to one name. A null/empty name (or null handler) is ignored
        /// (<c>[GraphCore]</c> warning on a bad name).
        /// </summary>
        public void OnSignal(string name, Action<SignalArgs> handler)
        {
            if (string.IsNullOrEmpty(name))
            {
                GraphLog.Warning(
                    "[GraphCore] OnSignal called with a null or empty name; ignored.");
                return;
            }
            if (handler == null) return;

            _signalSubs ??= new Dictionary<string, List<Action<SignalArgs>>>();
            if (!_signalSubs.TryGetValue(name, out var list))
            {
                list = new List<Action<SignalArgs>>();
                _signalSubs[name] = list;
            }
            list.Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/> from the subscriber list for <paramref name="name"/>.</summary>
        public void OffSignal(string name, Action<SignalArgs> handler)
        {
            if (string.IsNullOrEmpty(name))
            {
                GraphLog.Warning(
                    "[GraphCore] OffSignal called with a null or empty name; ignored.");
                return;
            }
            if (_signalSubs != null && _signalSubs.TryGetValue(name, out var list))
                list.Remove(handler);
        }

        /// <summary>
        /// Reads the last <see cref="SignalArgs"/> delivered for <paramref name="name"/>. Returns
        /// <c>false</c> with <c>default</c> when the name has never been raised. The store is transient
        /// (not persisted, not captured by history).
        /// </summary>
        public bool TryGetLastSignal(string name, out SignalArgs args)
        {
            if (!string.IsNullOrEmpty(name) && _lastSignals != null &&
                _lastSignals.TryGetValue(name, out args))
                return true;
            args = default;
            return false;
        }

        // ── Signal history ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> when the signal <paramref name="name"/> has been raised at least once in
        /// this context. Unlike <see cref="TryGetLastSignal"/>, this check persists across the run and is
        /// captured by save/restore. Use <see cref="ForgetSignal"/> to clear a name from the history.
        /// </summary>
        public bool HasSignalBeenRaised(string name)
            => !string.IsNullOrEmpty(name) && _raisedSignals != null && _raisedSignals.Contains(name);

        /// <summary>
        /// Removes <paramref name="name"/> from the raised-signal history so that a subsequent
        /// <see cref="HasSignalBeenRaised"/> returns <c>false</c>. Useful for replay, dialogue restart,
        /// or any flow that must treat the signal as "not yet seen" again. No-op when absent.
        /// </summary>
        public void ForgetSignal(string name)
        {
            if (!string.IsNullOrEmpty(name))
                _raisedSignals?.Remove(name);
        }

        /// <summary>
        /// Returns a snapshot of every signal name that has been raised at least once in this context.
        /// Used for serialization (see <c>GraphRunSnapshot</c>). Never null.
        /// </summary>
        public IReadOnlyCollection<string> GetAllRaisedSignals()
            => _raisedSignals != null
                ? (IReadOnlyCollection<string>)new List<string>(_raisedSignals)
                : System.Array.Empty<string>();

        /// <summary>Restores a previously saved raised-signal history without firing any subscribers.</summary>
        public void RestoreSignalHistory(System.Collections.Generic.IEnumerable<string> names)
        {
            if (names == null) return;
            _raisedSignals ??= new HashSet<string>();
            foreach (var n in names)
                if (!string.IsNullOrEmpty(n))
                    _raisedSignals.Add(n);
        }

        // ── Change notifications ───────────────────────────────────────────────

        private List<Action<string>> _anyParamSubs;

        /// <summary>
        /// Subscribes <paramref name="handler"/> to changes on ANY parameter key. The handler
        /// receives the changed key. Fires AFTER per-key handlers. Multiple handlers supported.
        /// </summary>
        public void OnAnyVariableChanged(Action<string> handler)
        {
            if (handler == null) return;
            (_anyParamSubs ??= new List<Action<string>>()).Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/> from the wildcard parameter change list.</summary>
        public void OffAnyVariableChanged(Action<string> handler)
        {
            _anyParamSubs?.Remove(handler);
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to changes on <paramref name="key"/>.
        /// The handler receives the new value boxed as <c>object</c>.
        /// </summary>
        public void OnVariableChanged(string key, Action<object> handler)
        {
            if (!_subs.TryGetValue(key, out var list))
            {
                list = new List<Action<object>>();
                _subs[key] = list;
            }
            list.Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/> from the subscriber list for <paramref name="key"/>.</summary>
        public void OffVariableChanged(string key, Action<object> handler)
        {
            if (_subs.TryGetValue(key, out var list))
                list.Remove(handler);
        }

        private void FireSubscribers(string key, object value)
        {
            if (_subs.TryGetValue(key, out var list))
            {
                var snapshot = new List<Action<object>>(list);
                foreach (var handler in snapshot)
                    handler(value);
            }
            if (_anyParamSubs != null && _anyParamSubs.Count > 0)
            {
                var snapshot = new List<Action<string>>(_anyParamSubs);
                foreach (var handler in snapshot)
                    handler(key);
            }
        }

        // ── Seeding primitives ─────────────────────────────────────────────────
        // Graph assets are a Unity concept (BaseGraph/VariableDef are ScriptableObject), so graph-aware
        // seeding (InitFromGraph, the graph-seeded BeginLocalContext overload) lives in
        // BaseContextGraphExtensions (Runtime assembly) as extension methods over these two primitives —
        // same call syntax at every existing call site, no engine reference needed here in Core.

        /// <summary>
        /// Sets the <em>global</em> bucket's <paramref name="key"/> to <paramref name="value"/> only if
        /// not already present. Fires no subscribers — intended for silent default-seeding (e.g. from a
        /// graph's declared parameters), not for gameplay writes (use <see cref="Set{T}"/> for those).
        /// </summary>
        public void SeedGlobalIfAbsent(string key, object value)
        {
            if (!_params.ContainsKey(key))
                _params[key] = value;
        }

        /// <summary>
        /// As <see cref="SeedGlobalIfAbsent"/> but targets the currently-open local overlay. Throws
        /// <see cref="InvalidOperationException"/> when no local context is open (call
        /// <see cref="BeginLocalContext()"/> first).
        /// </summary>
        public void SeedLocalIfAbsent(string key, object value)
        {
            if (!_localActive)
                throw new InvalidOperationException(
                    "[GraphCore] SeedLocalIfAbsent called with no local context open.");
            if (!_local.ContainsKey(key))
                _local[key] = value;
        }

        // ── Collections ────────────────────────────────────────────────────────

        /// <summary>
        /// Ensures <paramref name="item"/> is present in the named collection <paramref name="key"/>
        /// (created on first add), at quantity ≥ 1. Idempotent — adding an element already present is a
        /// no-op (its quantity is untouched). Fires <see cref="OnCollectionChanged"/> only when the element
        /// is newly added. Collections are global state, independent of the local-context overlay.
        /// <para>
        /// For stacking (recording repeat pickups of the same item, e.g. inventory quantities), use
        /// <see cref="AddToCollection(string, string, int)"/> instead — this overload always stays a plain
        /// membership check, exactly as before quantities existed.
        /// </para>
        /// </summary>
        public void AddToCollection(string key, string item)
        {
            if (string.IsNullOrEmpty(key) || item == null)
            {
                GraphLog.Warning(
                    "[GraphCore] AddToCollection called with a null/empty key or null item; ignored.");
                return;
            }
            _collections ??= new Dictionary<string, CollectionBucket>();
            if (!_collections.TryGetValue(key, out var bucket))
                _collections[key] = bucket = new CollectionBucket();
            if (bucket.EnsurePresent(item))
                FireCollectionChanged(key);
        }

        /// <summary>
        /// Adds <paramref name="count"/> units of <paramref name="item"/> to collection <paramref name="key"/>
        /// (creating both on first use), stacking onto any quantity already there. Unlike the 2-argument
        /// overload this is NOT idempotent — every call with <paramref name="count"/> ≥ 1 is a real quantity
        /// change and fires <see cref="OnCollectionChanged"/>, even when the item already existed. A
        /// <paramref name="count"/> ≤ 0 logs a warning and is a no-op. Read the running total with
        /// <see cref="CollectionItemCount"/>; <see cref="CollectionCount"/> still reports the DISTINCT item
        /// count, unaffected by quantity.
        /// </summary>
        public void AddToCollection(string key, string item, int count)
        {
            if (string.IsNullOrEmpty(key) || item == null || count <= 0)
            {
                GraphLog.Warning(
                    "[GraphCore] AddToCollection called with a null/empty key, null item, or non-positive " +
                    "count; ignored.");
                return;
            }
            _collections ??= new Dictionary<string, CollectionBucket>();
            if (!_collections.TryGetValue(key, out var bucket))
                _collections[key] = bucket = new CollectionBucket();
            bucket.Increment(item, count);
            FireCollectionChanged(key);
        }

        /// <summary>
        /// Removes <paramref name="item"/> from collection <paramref name="key"/> entirely, whatever its
        /// quantity. No-op when the item or the collection is absent. Fires <see cref="OnCollectionChanged"/>
        /// only when an element is actually removed. For decrementing a stack by a specific amount instead
        /// of clearing it outright, use <see cref="RemoveFromCollection(string, string, int)"/>.
        /// </summary>
        public void RemoveFromCollection(string key, string item)
        {
            if (string.IsNullOrEmpty(key) || item == null)
            {
                GraphLog.Warning(
                    "[GraphCore] RemoveFromCollection called with a null/empty key or null item; ignored.");
                return;
            }
            if (_collections != null && _collections.TryGetValue(key, out var bucket) && bucket.RemoveAll(item))
                FireCollectionChanged(key);
        }

        /// <summary>
        /// Removes <paramref name="count"/> units of <paramref name="item"/> from collection
        /// <paramref name="key"/>, clamped at zero — the item is dropped entirely once its quantity reaches
        /// zero. No-op when absent. A <paramref name="count"/> ≤ 0 logs a warning and is a no-op. Fires
        /// <see cref="OnCollectionChanged"/> whenever the quantity actually changes.
        /// </summary>
        public void RemoveFromCollection(string key, string item, int count)
        {
            if (string.IsNullOrEmpty(key) || item == null || count <= 0)
            {
                GraphLog.Warning(
                    "[GraphCore] RemoveFromCollection called with a null/empty key, null item, or " +
                    "non-positive count; ignored.");
                return;
            }
            if (_collections != null && _collections.TryGetValue(key, out var bucket) && bucket.Decrement(item, count))
                FireCollectionChanged(key);
        }

        /// <summary>Returns <c>true</c> when collection <paramref name="key"/> contains <paramref name="item"/> (quantity ≥ 1).</summary>
        public bool CollectionContains(string key, string item)
            => item != null && _collections != null &&
               _collections.TryGetValue(key, out var bucket) && bucket.Contains(item);

        /// <summary>Returns the number of DISTINCT items in collection <paramref name="key"/> (0 when absent) — unaffected by quantity.</summary>
        public int CollectionCount(string key)
            => (_collections != null && _collections.TryGetValue(key, out var bucket)) ? bucket.DistinctCount : 0;

        /// <summary>Returns the quantity of <paramref name="item"/> in collection <paramref name="key"/> (0 when absent).</summary>
        public int CollectionItemCount(string key, string item)
            => (item != null && _collections != null && _collections.TryGetValue(key, out var bucket))
                ? bucket.CountOf(item) : 0;

        /// <summary>
        /// Returns a read-only snapshot (copy) of the DISTINCT members of collection <paramref name="key"/>,
        /// in insertion order. Empty (never null) when the collection is absent. Mutating the result never
        /// affects context state. Pair with <see cref="CollectionItemCount"/> for per-item quantities, or
        /// use <see cref="GetCollectionWithCounts"/> for both in one call.
        /// </summary>
        public IReadOnlyList<string> GetCollection(string key)
        {
            if (_collections != null && _collections.TryGetValue(key, out var bucket))
                return new List<string>(bucket.Order);
            return System.Array.Empty<string>();
        }

        /// <summary>
        /// Returns a read-only snapshot of collection <paramref name="key"/> as (item, quantity) pairs, in
        /// insertion order. Empty (never null) when the collection is absent.
        /// </summary>
        public IReadOnlyList<(string Item, int Count)> GetCollectionWithCounts(string key)
        {
            if (_collections == null || !_collections.TryGetValue(key, out var bucket))
                return System.Array.Empty<(string, int)>();
            var result = new List<(string, int)>(bucket.DistinctCount);
            foreach (var item in bucket.Order)
                result.Add((item, bucket.CountOf(item)));
            return result;
        }

        /// <summary>
        /// Empties collection <paramref name="key"/>. No-op when already empty or absent; fires
        /// <see cref="OnCollectionChanged"/> when it had at least one member. Drops every item's quantity,
        /// not just its membership.
        /// </summary>
        public void ClearCollection(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                GraphLog.Warning(
                    "[GraphCore] ClearCollection called with a null or empty key; ignored.");
                return;
            }
            if (_collections != null && _collections.TryGetValue(key, out var bucket) && bucket.DistinctCount > 0)
            {
                bucket.Clear();
                FireCollectionChanged(key);
            }
        }

        /// <summary>
        /// Returns a read-only snapshot of all collections (key → distinct members in insertion order, as
        /// copies) for serialization. Parallel to <see cref="GetAllVariables"/>, which remains scalar-only.
        /// Empty when none exist.
        /// <para>
        /// <b>Quantities are not captured here</b> — this mirrors the pre-0.31.0 shape (distinct membership
        /// only) so existing save-format consumers (e.g. <c>GraphRunSnapshot</c>) keep working unchanged.
        /// A stacked item's quantity beyond 1 is NOT round-tripped through this method; a consumer that
        /// needs quantities in its own persistence must read <see cref="GetCollectionWithCounts"/> directly.
        /// </para>
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> GetAllCollections()
        {
            var result = new Dictionary<string, IReadOnlyCollection<string>>();
            if (_collections != null)
                foreach (var kvp in _collections)
                    result[kvp.Key] = new List<string>(kvp.Value.Order);
            return new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyCollection<string>>(result);
        }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to membership changes of collection <paramref name="key"/>.
        /// The handler receives the collection key (re-query the set for its new state). Fires only on a real
        /// change — idempotent add and no-op remove are silent.
        /// </summary>
        public void OnCollectionChanged(string key, Action<string> handler)
        {
            if (string.IsNullOrEmpty(key))
            {
                GraphLog.Warning(
                    "[GraphCore] OnCollectionChanged called with a null or empty key; ignored.");
                return;
            }
            if (handler == null) return;

            _collectionSubs ??= new Dictionary<string, List<Action<string>>>();
            if (!_collectionSubs.TryGetValue(key, out var list))
            {
                list = new List<Action<string>>();
                _collectionSubs[key] = list;
            }
            list.Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/> from the change subscribers of collection <paramref name="key"/>.</summary>
        public void OffCollectionChanged(string key, Action<string> handler)
        {
            if (string.IsNullOrEmpty(key))
            {
                GraphLog.Warning(
                    "[GraphCore] OffCollectionChanged called with a null or empty key; ignored.");
                return;
            }
            if (_collectionSubs != null && _collectionSubs.TryGetValue(key, out var list))
                list.Remove(handler);
        }

        private List<Action<string>> _anyCollectionSubs;

        /// <summary>
        /// Subscribes <paramref name="handler"/> to changes on ANY collection key. The handler
        /// receives the changed key. Fires AFTER per-key handlers. Multiple handlers supported.
        /// </summary>
        public void OnAnyCollectionChanged(Action<string> handler)
        {
            if (handler == null) return;
            (_anyCollectionSubs ??= new List<Action<string>>()).Add(handler);
        }

        /// <summary>Removes <paramref name="handler"/> from the wildcard collection change list.</summary>
        public void OffAnyCollectionChanged(Action<string> handler)
        {
            _anyCollectionSubs?.Remove(handler);
        }

        private void FireCollectionChanged(string key)
        {
            if (_collectionSubs != null && _collectionSubs.TryGetValue(key, out var list))
            {
                var snapshot = new List<Action<string>>(list);
                foreach (var handler in snapshot)
                    handler(key);
            }
            if (_anyCollectionSubs != null && _anyCollectionSubs.Count > 0)
            {
                var snapshot = new List<Action<string>>(_anyCollectionSubs);
                foreach (var handler in snapshot)
                    handler(key);
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
            // Collections are durable state — deep-copy each bucket (order + quantities), independent of the source.
            if (_collections != null)
            {
                clone._collections = new Dictionary<string, CollectionBucket>();
                foreach (var kvp in _collections)
                    clone._collections[kvp.Key] = kvp.Value.Clone();
            }
            // Signal history is durable state — copy the set.
            if (_raisedSignals != null)
                clone._raisedSignals = new HashSet<string>(_raisedSignals);
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
        /// Subscribers are preserved. Used by <c>BaseRunner</c> (Runtime assembly) to restore a history
        /// snapshot into the live context object without changing its reference — internal visibility
        /// extends to that assembly via <c>InternalsVisibleTo</c> (see AssemblyInfo.cs).
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

            // Restore collections (durable state) as independent copies. Subscribers are preserved.
            if (source._collections != null)
            {
                _collections = new Dictionary<string, CollectionBucket>();
                foreach (var kvp in source._collections)
                    _collections[kvp.Key] = kvp.Value.Clone();
            }
            else
            {
                _collections = null;
            }

            // Restore signal history (durable state). Subscribers are preserved.
            if (source._raisedSignals != null)
                _raisedSignals = new HashSet<string>(source._raisedSignals);
            else
                _raisedSignals = null;
        }
    }
}
