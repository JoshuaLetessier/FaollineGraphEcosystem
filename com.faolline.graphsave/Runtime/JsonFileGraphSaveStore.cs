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
            try
            {
                Directory.CreateDirectory(RootPath);
                var json = JsonUtility.ToJson(snapshot, prettyPrint: true);
                File.WriteAllText(SlotPath(slot), json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GraphSave] Could not save slot '{slot}' ({ex.GetType().Name}: {ex.Message}); the snapshot was NOT persisted.");
            }
        }

        /// <inheritdoc/>
        public GraphRunSnapshot Load(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            var path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<GraphRunSnapshot>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GraphSave] Slot '{slot}' could not be read/parsed ({ex.GetType().Name}: {ex.Message}); treating as absent.");
                return null;
            }
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

        private const string Extension = ".json";
        private const int WindowsMaxPath = 259; // MAX_PATH (260) minus 1 for the null terminator.
        private const int HashSuffixLength = 9; // "_" + 8 hex chars, reserved out of the budget below.
        private const int MinSlotBudget = 16; // Always leave room for a truncated name + suffix, even if RootPath alone is very long.

        // Slot names become file names: replace path separators and invalid filename characters so a slot
        // like "../other" or "a/b" cannot escape the store folder or fail on Windows.
        private string SlotPath(string slot)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = slot.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (System.Array.IndexOf(invalid, chars[i]) >= 0 || chars[i] == '/' || chars[i] == '\\')
                    chars[i] = '_';
            var sanitized = new string(chars);

            // A long or unicode-heavy slot name can push the full path past Windows' ~260-char MAX_PATH,
            // where CreateDirectory/WriteAllText throw. Budget off the actual RootPath length (which varies
            // by company/product name and platform) rather than guessing a fixed cap.
            var budget = WindowsMaxPath - RootPath.Length - 1 - Extension.Length;
            if (budget < MinSlotBudget) budget = MinSlotBudget;

            if (sanitized.Length > budget)
            {
                // Reserve room for the "_" + hash suffix itself, so the truncated name PLUS suffix still
                // fits the budget (not just the truncated name alone).
                var cut = budget - HashSuffixLength;
                if (cut < 0) cut = 0;
                if (cut > 0 && char.IsHighSurrogate(sanitized[cut - 1])) cut--;
                // Deterministic (unlike string.GetHashCode(), which .NET may randomize per process) so a later
                // Load() for the same over-length slot still resolves to the same file after a restart.
                sanitized = sanitized.Substring(0, cut) + "_" + StableHash(sanitized);
            }

            return Path.Combine(RootPath, sanitized + Extension);
        }

        private static string StableHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in s)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return hash.ToString("x8");
            }
        }
    }
}
