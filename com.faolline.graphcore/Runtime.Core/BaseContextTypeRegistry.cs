using System;
using System.Collections.Generic;

namespace Faolline.GraphCore
{
    /// <summary>
    /// Extends the set of value types <see cref="BaseContext"/> accepts for <see cref="BaseContext.Set{T}"/>/
    /// <see cref="BaseContext.Get{T}"/>/<see cref="BaseContext.RaiseSignal{T}"/>, beyond the four built-in
    /// primitives (<c>bool</c>/<c>int</c>/<c>float</c>/<c>string</c>). Core itself never references an
    /// engine-specific type (e.g. <c>UnityEngine.Vector2</c>) — that would break its use from a
    /// <c>noEngineReferences</c> assembly — so an engine layer registers its own value types here instead.
    /// The Unity engine layer does this once via <c>GraphCoreUnityBootstrap</c> (Runtime assembly),
    /// registering <c>Vector2</c>, <c>Vector3</c>, and <c>Color</c>.
    /// </summary>
    public static class BaseContextTypeRegistry
    {
        private static readonly HashSet<Type> _extraTypes = new HashSet<Type>();

        /// <summary>
        /// Adds <typeparamref name="T"/> to the set of supported variable/signal-payload types. Idempotent —
        /// safe to call more than once (e.g. across a domain reload).
        /// </summary>
        public static void RegisterSupportedType<T>() => _extraTypes.Add(typeof(T));

        /// <summary>Returns <c>true</c> when <paramref name="type"/> was previously registered.</summary>
        internal static bool IsRegistered(Type type) => _extraTypes.Contains(type);
    }
}
