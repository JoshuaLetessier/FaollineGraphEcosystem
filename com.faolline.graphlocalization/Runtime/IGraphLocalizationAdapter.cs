namespace Faolline.GraphLocalization
{
    /// <summary>
    /// Registered by each graph lib so the central builder can discover and index all localization
    /// keys in the project in a single pass, without the builder knowing any domain-specific types.
    ///
    /// Implement this interface in an Editor-side adapter class and register it with
    /// <see cref="GraphLocalizationAdapterRegistry"/> at editor load time (via [InitializeOnLoad]).
    ///
    /// The builder calls <see cref="ScanAndIndex"/> per adapter, then hands the resulting database
    /// to the syncer which creates collections under
    /// <c>Assets/Localization/Collections/&lt;LibName&gt;/</c>.
    /// </summary>
    public interface IGraphLocalizationAdapter
    {
        /// <summary>
        /// Human-readable name of this graph lib. Used as the subfolder name under the Collections
        /// root (e.g. "GraphDialogue" → Assets/Localization/Collections/GraphDialogue/).
        /// Must be a valid directory name.
        /// </summary>
        string LibName { get; }

        /// <summary>
        /// Scans all graph assets of this lib type in the project (via AssetDatabase) and populates
        /// <paramref name="database"/> with their localization key entries.
        /// Called on the main thread from the editor menu.
        /// </summary>
        void ScanAndIndex(LocalizationDatabase database);
    }
}
