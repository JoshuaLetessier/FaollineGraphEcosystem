namespace Faolline.GraphSave
{
    /// <summary>
    /// Neutral persistence seam for <see cref="GraphRunSnapshot"/>s, keyed by a slot name — the save counterpart
    /// of localization's provider contract. Bring your own backend: implement this against a file, PlayerPrefs,
    /// a cloud save, Steam, … The optional <c>com.faolline.graphsave.savesystem</c> package ships one backed by
    /// <c>com.faolline.savesystem.core</c>. You can also skip this entirely and (de)serialize the snapshot
    /// yourself — it is a plain serializable object.
    /// </summary>
    public interface IGraphSaveStore
    {
        /// <summary>Persists <paramref name="snapshot"/> under <paramref name="slot"/> (overwrites).</summary>
        void Save(string slot, GraphRunSnapshot snapshot);

        /// <summary>Loads the snapshot stored under <paramref name="slot"/>, or null when absent.</summary>
        GraphRunSnapshot Load(string slot);

        /// <summary>True when a snapshot exists under <paramref name="slot"/>.</summary>
        bool Exists(string slot);

        /// <summary>Removes the snapshot under <paramref name="slot"/> (no-op when absent).</summary>
        void Delete(string slot);
    }
}
