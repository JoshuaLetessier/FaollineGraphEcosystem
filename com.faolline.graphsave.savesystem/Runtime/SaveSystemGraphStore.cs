using System;
using SaveSystem;
using UnityEngine;

namespace Faolline.GraphSave.UnitySaveSystem
{
    /// <summary>
    /// Adapts a UnitySaveSystem backend (<c>ISaveSystem&lt;T&gt;</c> from <c>com.faolline.savesystem.core</c>) to
    /// graphsave's <see cref="IGraphSaveStore"/>, so a <see cref="GraphRunSnapshot"/> persists through your save
    /// backends. Wrap whichever backend you added — e.g. the JSON one:
    /// <code>
    /// var store = new SaveSystemGraphStore(new SaveSystem.SSJson.JsonSaveSystem&lt;GraphRunSnapshot&gt;());
    /// store.Save("slot0", GraphRunSnapshot.Capture(runner, context));
    /// </code>
    /// This bridge depends only on the save-system CORE; pick the concrete backend sub-package (json,
    /// playerprefs, …) yourself — exactly the "choose your sub-packages" model UnitySaveSystem uses.
    /// </summary>
    public sealed class SaveSystemGraphStore : IGraphSaveStore
    {
        private readonly ISaveSystem<GraphRunSnapshot> _backend;

        /// <summary>Wraps any UnitySaveSystem backend (Json, PlayerPrefs, …) registered for <see cref="GraphRunSnapshot"/>.</summary>
        public SaveSystemGraphStore(ISaveSystem<GraphRunSnapshot> backend)
            => _backend = backend ?? throw new ArgumentNullException(nameof(backend));

        /// <inheritdoc/>
        public void Save(string slot, GraphRunSnapshot snapshot)
        {
            try
            {
                _backend.Save(slot, snapshot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GraphSave] Backend threw while saving slot '{slot}' ({ex.GetType().Name}: {ex.Message}); the snapshot was NOT persisted.");
            }
        }

        /// <inheritdoc/>
        public GraphRunSnapshot Load(string slot)
        {
            try
            {
                return Exists(slot) ? _backend.Load(slot) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GraphSave] Backend threw while loading slot '{slot}' ({ex.GetType().Name}: {ex.Message}); treating as absent.");
                return null;
            }
        }

        /// <inheritdoc/>
        public bool Exists(string slot)
        {
            try
            {
                return _backend.Exists(slot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GraphSave] Backend threw while checking slot '{slot}' ({ex.GetType().Name}: {ex.Message}); reporting as absent.");
                return false;
            }
        }

        /// <inheritdoc/>
        public void Delete(string slot)
        {
            try
            {
                _backend.Delete(slot);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GraphSave] Backend threw while deleting slot '{slot}' ({ex.GetType().Name}: {ex.Message}); ignored.");
            }
        }
    }
}
