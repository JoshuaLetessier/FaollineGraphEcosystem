using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Faolline.GraphGameFlow.Editor;

namespace Faolline.GraphGameFlow.Addressables.Editor
{
    /// <summary>
    /// Plugs registered Addressable scene entries into the <c>LoadSceneAction</c>/<c>UnloadSceneAction</c>
    /// inspector dropdown, via graphgameflow's <see cref="SceneKeySourceRegistry"/> — the editor-time mirror
    /// of <see cref="AddressablesSceneLoader"/>'s runtime role. graphgameflow core never references
    /// <c>com.unity.addressables</c> directly; this adapter package does, per the ecosystem's port/adapter
    /// rule (see <c>ARCHITECTURE.md</c>).
    /// </summary>
    public sealed class AddressablesSceneKeyProvider : ISceneKeySourceProvider
    {
        public string SourceLabel => "Addressable";

        public IReadOnlyList<string> GetKeys()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return System.Array.Empty<string>();

            return settings.groups
                .Where(g => g != null)
                .SelectMany(g => g.entries)
                .Where(e => AssetDatabase.GetMainAssetTypeAtPath(e.AssetPath) == typeof(SceneAsset))
                .Select(e => e.address)
                .Distinct()
                .OrderBy(a => a)
                .ToList();
        }

        public bool CanPromote(string projectScenePath, string sceneName) => true;

        public void Promote(string projectScenePath, string sceneName)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogWarning(
                    "[GraphGameFlow] No AddressableAssetSettings found in the project; open Window > Asset " +
                    "Management > Addressables > Groups once to create it, then try again.");
                return;
            }

            var guid  = AssetDatabase.AssetPathToGUID(projectScenePath);
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = sceneName;
            Debug.Log($"[GraphGameFlow] Marked '{projectScenePath}' as Addressable with key '{sceneName}'.");
        }

        [InitializeOnLoadMethod]
        private static void Register() => SceneKeySourceRegistry.Register(new AddressablesSceneKeyProvider());
    }
}
