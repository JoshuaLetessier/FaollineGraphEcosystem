namespace Faolline.GraphDialogue
{
    /// <summary>
    /// Provider-agnostic interface for syncing LocalizationDatabase to a localization backend.
    /// Implementations live in locale-specific adapters (e.g., Localization.Unity adapter).
    /// This keeps the Editor assembly free of external dependencies.
    /// </summary>
    public interface ILocalizationDatabaseSyncer
    {
        /// <summary>
        /// Syncs all keys from the database to the provider backend.
        /// Called by DialogueLocalizationBuilder after indexing.
        /// </summary>
        void SyncDatabase(LocalizationDatabase database, LocalizationMode mode);
    }
}
