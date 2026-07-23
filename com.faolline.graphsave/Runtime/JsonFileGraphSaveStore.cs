using System;
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
            if (!TryGetSlotPath(slot, out var path))
            {
                Debug.LogError($"[GraphSave] Slot '{slot}' is not a valid save name (must contain no path separators or filesystem-reserved characters); the snapshot was NOT persisted.");
                return;
            }

            try
            {
                Directory.CreateDirectory(RootPath);
                var json = JsonUtility.ToJson(snapshot, prettyPrint: true);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GraphSave] Could not save slot '{slot}' ({ex.GetType().Name}: {ex.Message}); the snapshot was NOT persisted.");
            }
        }

        /// <inheritdoc/>
        public GraphRunSnapshot Load(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return null;
            if (!TryGetSlotPath(slot, out var path)) return null;
            if (!File.Exists(path)) return null;
            try
            {
                var json = File.ReadAllText(path);
                return JsonUtility.FromJson<GraphRunSnapshot>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GraphSave] Slot '{slot}' could not be read/parsed ({ex.GetType().Name}: {ex.Message}); treating as absent.");
                return null;
            }
        }

        /// <inheritdoc/>
        public bool Exists(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return false;
            return TryGetSlotPath(slot, out var path) && File.Exists(path);
        }

        /// <inheritdoc/>
        public void Delete(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return;
            if (!TryGetSlotPath(slot, out var path)) return;
            if (File.Exists(path)) File.Delete(path);
        }

        private const string Extension = ".json";
        private const int WindowsMaxPath = 259; // MAX_PATH (260) minus 1 for the null terminator.
        private const int HashSuffixLength = 9; // "_" + 8 hex chars, reserved out of the budget below.
        private const int MinSlotBudget = 16; // Always leave room for a truncated name + suffix, even if RootPath alone is very long.

        // Rejects (rather than mangles) a slot that isn't a single, filename-safe path segment — mirrors
        // com.faolline.savesystem.core's JsonSaveSystem, whose own fix for the exact same path-traversal risk
        // chose reject-and-log over sanitize-and-succeed. A legitimate save-name shouldn't need arbitrary
        // characters; constrain that at the consumer's own input field rather than silently rewriting it here.
        private bool TryGetSlotPath(string slot, out string path)
        {
            path = null;

            if (Path.GetFileName(slot) != slot || slot.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return false;

            var name = BoundLength(slot);
            var candidate = Path.GetFullPath(Path.Combine(RootPath, name + Extension));
            var rootPrefix = Path.GetFullPath(RootPath) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                return false; // defense in depth; the character/separator check above should already prevent this.

            path = candidate;
            return true;
        }

        // A long or unicode-heavy (but otherwise valid) slot name can still push the full path past Windows'
        // ~260-char MAX_PATH, where CreateDirectory/WriteAllText throw. Budget off the actual RootPath length
        // (which varies by company/product name and platform) rather than guessing a fixed cap.
        private string BoundLength(string slot)
        {
            var budget = WindowsMaxPath - RootPath.Length - 1 - Extension.Length;
            if (budget < MinSlotBudget) budget = MinSlotBudget;

            if (slot.Length <= budget) return slot;

            // Reserve room for the "_" + hash suffix itself, so the truncated name PLUS suffix still fits
            // the budget (not just the truncated name alone).
            var cut = budget - HashSuffixLength;
            if (cut < 0) cut = 0;
            if (cut > 0 && char.IsHighSurrogate(slot[cut - 1])) cut--;
            // Deterministic (unlike string.GetHashCode(), which .NET may randomize per process) so a later
            // Load() for the same over-length slot still resolves to the same file after a restart.
            return slot.Substring(0, cut) + "_" + StableHash(slot);
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
