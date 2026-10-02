namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Optional companion of <see cref="ILocalizationProvider"/> for providers backed by several tables:
    /// resolves a key directly in the table the caller designates, instead of searching every table.
    /// Callers go through <see cref="TableScopedLookup.Resolve"/>, which falls back to
    /// <see cref="ILocalizationProvider.Resolve"/> for providers that don't implement this — so existing
    /// providers keep working unchanged.
    /// </summary>
    public interface ITableScopedLocalizationProvider
    {
        /// <summary>
        /// Resolves <paramref name="key"/> in the table whose base name is <paramref name="table"/> (see
        /// <see cref="LocalizationTableNames"/>). Same contract as <see cref="ILocalizationProvider.Resolve"/>:
        /// the translated string, or the <c>#key</c> marker when the key is missing — never null.
        /// </summary>
        string ResolveInTable(string table, string key, string locale);
    }
}
