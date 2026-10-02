namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Optional companion of <see cref="ILocalizedAssetProvider"/>: resolves a localized asset in the
    /// designated table's asset tables only. Callers go through <see cref="TableScopedLookup.ResolveAsset{T}"/>,
    /// which falls back to <see cref="ILocalizedAssetProvider.ResolveAsset{T}"/> for providers that don't
    /// implement this.
    /// </summary>
    public interface ITableScopedLocalizedAssetProvider
    {
        /// <summary>
        /// Resolves the asset for <paramref name="key"/> in the asset tables of <paramref name="table"/>
        /// (base name, see <see cref="LocalizationTableNames"/>). Null when unknown or unassigned.
        /// </summary>
        T ResolveAssetInTable<T>(string table, string key) where T : UnityEngine.Object;
    }
}
