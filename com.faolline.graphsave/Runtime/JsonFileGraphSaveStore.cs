using System.IO;
using UnityEngine;

namespace Faolline.GraphSave
{
    /// <summary>
    /// Batteries-included <see cref="IGraphSaveStore"/> that persists snapshots as JSON files
    /// under <see cref="Application.persistentDataPath"/>/<see cref="SubFolder"/>. Each slot
    /// is one <c>.json</c> file. Thread-safe enough for single-threaded Unity usage.
    /// <para>
    /// For production games needing encryption, cloud sync, or platform-specific backends,
    /// implement <see cref="IGraphSaveStore"/> directly or use the
    /// <c>com.faolline.graphsave.savesystem</c> bridge.
    /// </para>
    /// </summary>
    public class JsonFileGraphSaveStore : IGraphSaveStore
    {
        private readonly string _rootPath;

        /// <summary>
        /// Creates a store writing to <see cref="Application.persistentDataPath"/>/<paramref name="subFolder"/>.
        /// </summary>
        /// <param name="subFolder">Subfolder under persistentDataPath (default <c>"GraphSaves"</c>).</param>
        public JsonFileGraphSaveStore(string subFolder = "GraphSaves")
        {
            _rootPath = Path.Combine(Application.persistentDataPath, subFolder);
        }

        /// <inheritdoc/>
        public void Save(string slot, GraphRunSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(slot) || snapshot == null) return;
            Directory.CreateDirectory(_rootPath);
            var json = JsonUtility.ToJson(snapshot, prettyPrint: true);
            File.WriteAllText(SlotPath(slot), json);
        }

        /// <inheritdoc/>
        public GraphRunSnapshot Load(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            var path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<GraphRunSnapshot>(json);
        }

        /// <inheritdoc/>
        public bool Exists(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return false;
            return File.Exists(SlotPath(slot));
        }

        /// <inheritdoc/>
        public void Delete(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            var path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        private string SlotPath(string slot) => Path.Combine(_rootPath, slot + ".json");
    }
}
