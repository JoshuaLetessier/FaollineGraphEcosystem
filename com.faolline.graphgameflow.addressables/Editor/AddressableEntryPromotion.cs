using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using Faolline.GraphLogging;


namespace Faolline.GraphGameFlow.Addressables.Editor
{
    /// <summary>
    /// The "Mark as Addressable" rule shared by <see cref="AddressablesSceneKeyProvider"/> and
    /// <see cref="AddressablesGraphKeyProvider"/>: give an asset its key without disturbing the project's
    /// group layout.
    /// <list type="bullet">
    /// <item>An asset that already has an explicit entry keeps its group — only its address changes.</item>
    /// <item>A new entry goes into the group of its nearest Addressable ancestor folder (the asset is usually
    /// already implicitly addressable through it; the explicit entry only overrides its address).</item>
    /// <item>Only when no ancestor folder is Addressable does it fall back to the default group.</item>
    /// </list>
    /// Before 0.6.1 both providers called <c>CreateOrMoveEntry(guid, DefaultGroup)</c>, which MOVES an
    /// existing entry — promoting an already-grouped asset silently pulled it out of its per-chapter/per-zone
    /// group into the default one.
    /// </summary>
    internal static class AddressableEntryPromotion
    {
        /// <returns>The promoted entry, or <c>null</c> if nothing was done (no settings, unknown asset).</returns>
        internal static AddressableAssetEntry Promote(AddressableAssetSettings settings, string assetPath, string address)
        {
            if (settings == null)
            {
                Logging.Warning("GraphGameFlow", "[GraphGameFlow] No AddressableAssetSettings found in the project; open Window > Asset " +
                    "Management > Addressables > Groups once to create it, then try again.");
                return null;
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Logging.Warning("GraphGameFlow", $"[GraphGameFlow] Cannot mark '{assetPath}' as Addressable: no asset at that path.");
                return null;
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
                entry = settings.CreateOrMoveEntry(guid, FolderEntryGroupOf(settings, assetPath) ?? settings.DefaultGroup);

            entry.address = address;
            Logging.Info("GraphGameFlow", $"[GraphGameFlow] Marked '{assetPath}' as Addressable with key '{address}' " +
                $"(group '{entry.parentGroup.Name}').");
            return entry;
        }

        /// <summary>Group of the nearest ancestor folder that is itself an explicit Addressable entry, or <c>null</c>.</summary>
        internal static AddressableAssetGroup FolderEntryGroupOf(AddressableAssetSettings settings, string assetPath)
        {
            for (var dir = Path.GetDirectoryName(assetPath); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                var folderEntry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(dir.Replace('\\', '/')));
                if (folderEntry != null) return folderEntry.parentGroup;
            }
            return null;
        }
    }
}
