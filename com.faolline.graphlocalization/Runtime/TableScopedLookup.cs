namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Looks a key up in a designated table when the provider supports it, otherwise through the classic,
    /// table-less call. The one entry point callers that know their table (a dialogue's graph, a speaker's
    /// group, a quest's graph) use — so a table-aware provider opens only that table, and every other
    /// provider behaves exactly as before.
    /// </summary>
    public static class TableScopedLookup
    {
        /// <summary>
        /// <see cref="ITableScopedLocalizationProvider.ResolveInTable"/> when <paramref name="provider"/>
        /// implements it and <paramref name="table"/> is non-empty, else
        /// <see cref="ILocalizationProvider.Resolve"/>. Returns <c>#key</c> when the key is missing (or when
        /// there is no provider).
        /// </summary>
        public static string Resolve(ILocalizationProvider provider, string table, string key, string locale)
        {
            if (provider == null) return $"#{key}";
            if (!string.IsNullOrEmpty(table) && provider is ITableScopedLocalizationProvider scoped)
                return scoped.ResolveInTable(table, key, locale);
            return provider.Resolve(key, locale);
        }

        /// <summary>
        /// <see cref="ITableScopedLocalizedAssetProvider.ResolveAssetInTable{T}"/> when supported and
        /// <paramref name="table"/> is non-empty, else <see cref="ILocalizedAssetProvider.ResolveAsset{T}"/>.
        /// Null without a provider.
        /// </summary>
        public static T ResolveAsset<T>(ILocalizedAssetProvider provider, string table, string key) where T : UnityEngine.Object
        {
            if (provider == null) return null;
            if (!string.IsNullOrEmpty(table) && provider is ITableScopedLocalizedAssetProvider scoped)
                return scoped.ResolveAssetInTable<T>(table, key);
            return provider.ResolveAsset<T>(key);
        }
    }
}
