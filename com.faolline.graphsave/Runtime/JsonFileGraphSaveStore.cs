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
        private readonly string _subFolder;
        private string _rootPath;

        // persistentDataPath is resolved LAZILY (first Save/Load/Exists/Delete), not in the constructor:
        // Unity forbids calling it during MonoBehaviour construction, so `new JsonFileGraphSaveStore()`
        // as a field initializer threw. The constructor stores only the plain sub-folder string.
        private string RootPath => _rootPath ??= Path.Combine(Application.persistentDataPath, _subFolder);

        /// <summary>
        /// Creates a store writing to <see cref="Application.persistentDataPath"/>/<paramref name="subFolder"/>.
        /// Safe to use as a MonoBehaviour field initializer — the path is resolved on first use, not here.
        /// </summary>
        /// <param name="subFolder">Subfolder under persistentDataPath (default <c>"GraphSaves"</c>).</param>
        public JsonFileGraphSaveStore(string subFolder = "GraphSaves")
        {
            _subFolder = subFolder;
        }

        /// <inheritdoc/>
        public void Save(string slot, GraphRunSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(slot) || snapshot == null) return;
            Directory.CreateDirectory(RootPath);
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

        // Slot names become file names: replace path separators and invalid filename characters so a slot
        // like "../other" or "a/b" cannot escape the store folder or fail on Windows.
        private string SlotPath(string slot)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = slot.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (System.Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
                    chars[i] = '_';
            return Path.Combine(RootPath, new string(chars) + ".json");
        }
    }
}
