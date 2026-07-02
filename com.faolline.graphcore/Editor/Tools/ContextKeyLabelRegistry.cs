using System.Collections.Generic;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Turns an opaque context key or collection entry into a human-readable label for editor tooling
    /// (e.g. <see cref="ContextWatchWindow"/>). Downstream libs implement this to name their own scoped keys
    /// — graphcore never needs to know a domain's key scheme.
    /// </summary>
    public interface IContextLabelResolver
    {
        /// <summary>A friendly label for a parameter/collection <paramref name="key"/>, or null if unrecognized.</summary>
        string LabelForKey(string key);

        /// <summary>A friendly label for an <paramref name="entry"/> inside collection <paramref name="collectionKey"/>,
        /// or null if unrecognized (the raw entry is then shown).</summary>
        string LabelForEntry(string collectionKey, string entry);
    }

    /// <summary>
    /// Opt-in registry of <see cref="IContextLabelResolver"/>s (mirrors <c>NodeTypeColorRegistry</c>). Editor
    /// tooling asks the registry for a label; each registered resolver is tried in turn and the first non-empty
    /// answer wins. Empty by default, so graphcore stays domain-neutral — downstream libs register their own
    /// (typically from an <c>[InitializeOnLoad]</c> hook).
    /// </summary>
    public static class ContextKeyLabelRegistry
    {
        private static readonly List<IContextLabelResolver> _resolvers = new List<IContextLabelResolver>();

        /// <summary>Registers <paramref name="resolver"/> (idempotent; nulls ignored).</summary>
        public static void Register(IContextLabelResolver resolver)
        {
            if (resolver != null && !_resolvers.Contains(resolver)) _resolvers.Add(resolver);
        }

        /// <summary>Removes <paramref name="resolver"/>.</summary>
        public static void Unregister(IContextLabelResolver resolver) => _resolvers.Remove(resolver);

        /// <summary>The first resolver's label for <paramref name="key"/>, or null when none recognize it.</summary>
        public static string LabelForKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var r in _resolvers)
            {
                var label = r?.LabelForKey(key);
                if (!string.IsNullOrEmpty(label)) return label;
            }
            return null;
        }

        /// <summary>The first resolver's label for <paramref name="entry"/> in <paramref name="collectionKey"/>, or null.</summary>
        public static string LabelForEntry(string collectionKey, string entry)
        {
            if (string.IsNullOrEmpty(entry)) return null;
            foreach (var r in _resolvers)
            {
                var label = r?.LabelForEntry(collectionKey, entry);
                if (!string.IsNullOrEmpty(label)) return label;
            }
            return null;
        }
    }
}
