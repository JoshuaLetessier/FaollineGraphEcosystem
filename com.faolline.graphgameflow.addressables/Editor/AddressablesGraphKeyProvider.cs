using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphGameFlow.Editor;

namespace Faolline.GraphGameFlow.Addressables.Editor
{
    /// <summary>
    /// Plugs registered Addressable graph entries into <see cref="GraphKeyRegistryWindow"/>, via
    /// graphgameflow's <see cref="GraphKeySourceRegistry"/> — the graph-side mirror of
    /// <see cref="AddressablesSceneKeyProvider"/>. graphgameflow core never references
    /// <c>com.unity.addressables</c> directly; this adapter package does, per the ecosystem's port/adapter rule.
    /// </summary>
    public sealed class AddressablesGraphKeyProvider : IGraphKeySourceProvider
    {
        public string SourceLabel => "Addressable";

        public IReadOnlyList<string> GetKeys()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return System.Array.Empty<string>();

            return settings.groups
                .Where(g => g != null)
                .SelectMany(g => g.entries)
                .Where(e => typeof(BaseGraph).IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(e.AssetPath)))
                .Select(e => e.address)
                .Distinct()
                .OrderBy(a => a)
                .ToList();
        }

        public bool CanPromote(string graphAssetPath, string graphId) => true;

        public void Promote(string graphAssetPath, string graphId)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning(
                    "[GraphGameFlow] No AddressableAssetSettings found in the project; open Window > Asset " +
                    "Management > Addressables > Groups once to create it, then try again.");
                return;
            }

            var guid  = AssetDatabase.AssetPathToGUID(graphAssetPath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = graphId;
            Debug.Log($"[GraphGameFlow] Marked '{graphAssetPath}' as Addressable with key '{graphId}'.");
        }

        public bool TryResolveGuid(string assetGuid, out string key)
        {
            key = null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || string.IsNullOrEmpty(assetGuid)) return false;

            var entry = settings.groups
                .Where(g => g != null)
                .SelectMany(g => g.entries)
                .FirstOrDefault(e => e.guid == assetGuid);
            if (entry == null) return false;

            key = entry.address;
            return true;
        }

        [InitializeOnLoadMethod]
        private static void Register() => GraphKeySourceRegistry.Register(new AddressablesGraphKeyProvider());
    }
}
