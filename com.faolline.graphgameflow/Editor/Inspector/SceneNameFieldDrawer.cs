using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Faolline.GraphLogging;


namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Shared "scene name" field UI for <see cref="LoadSceneActionEditor"/>/<see cref="UnloadSceneActionEditor"/>:
    /// a dropdown of project scenes (Build Settings resolution), plus one dropdown per
    /// <see cref="SceneKeySourceRegistry"/> provider (e.g. registered Addressable scene addresses, when that
    /// adapter package is installed and has registered one). The underlying <c>_sceneName</c> string is
    /// unchanged either way: it stores "whatever identifier the active <see cref="ISceneLoader"/> expects" —
    /// this is purely an authoring convenience, never a runtime distinction.
    /// </summary>
    internal static class SceneNameFieldDrawer
    {
        /// <param name="sourceMode">
        /// Persisted per-editor-instance: 0 = Build Settings, N = the Nth registered provider. Pass -1 the
        /// first time to let the drawer guess from the current value; afterwards the caller just threads its
        /// own field through.
        /// </param>
        public static void Draw(SerializedProperty sceneNameProp, ref int sourceMode, bool showBuildSettingsCheck)
        {
            var providers = SceneKeySourceRegistry.Providers;
            if (providers.Count > 0)
            {
                if (sourceMode < 0)
                    sourceMode = GuessInitialMode(sceneNameProp.stringValue, providers);
                sourceMode = Mathf.Clamp(sourceMode, 0, providers.Count);

                var labels = new string[providers.Count + 1];
                labels[0] = "Build Settings";
                for (int i = 0; i < providers.Count; i++) labels[i + 1] = providers[i].SourceLabel;

                sourceMode = GUILayout.Toolbar(sourceMode, labels);
                EditorGUILayout.Space(2);

                if (sourceMode > 0)
                {
                    DrawProviderDropdown(sceneNameProp, providers[sourceMode - 1]);
                    return;
                }
            }
            else
            {
                sourceMode = 0;
            }

            DrawBuildSettingsDropdown(sceneNameProp);
            if (showBuildSettingsCheck) DrawBuildSettingsCheck(sceneNameProp.stringValue);
        }

        private static int GuessInitialMode(string current, IReadOnlyList<ISceneKeySourceProvider> providers)
        {
            if (!string.IsNullOrEmpty(current))
                for (int i = 0; i < providers.Count; i++)
                    if (providers[i].GetKeys().Contains(current)) return i + 1;
            return 0;
        }

        private static List<string> ProjectScenePaths() =>
            AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/"))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

        private static void DrawBuildSettingsDropdown(SerializedProperty sceneNameProp)
        {
            var paths = ProjectScenePaths();
            if (paths.Count == 0)
            {
                EditorGUILayout.HelpBox("No scenes in the project yet — create a scene, then pick it here.", MessageType.Info);
                EditorGUILayout.PropertyField(sceneNameProp, new GUIContent("Scene Name"));
                return;
            }

            var names = paths.Select(Path.GetFileNameWithoutExtension).ToList();

            // Labels are the scene NAME only — never the path: EditorGUILayout.Popup treats '/' as a submenu
            // separator, so a path would nest the whole Assets/… tree into ugly sub-menus. Duplicate names
            // are disambiguated by their parent folder (which contains no '/').
            var nameCounts = names.GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
            var labels = new List<string> { "(none)" };
            for (int i = 0; i < paths.Count; i++)
            {
                var n = names[i];
                labels.Add(nameCounts[n] > 1
                    ? $"{n}   ({Path.GetFileName(Path.GetDirectoryName(paths[i]))})"
                    : n);
            }

            int current    = names.FindIndex(n => n == sceneNameProp.stringValue);
            int popupIndex = current < 0 ? 0 : current + 1;

            int newIndex = EditorGUILayout.Popup(new GUIContent("Scene"), popupIndex, labels.ToArray());
            if (newIndex != popupIndex)
                sceneNameProp.stringValue = newIndex == 0 ? string.Empty : names[newIndex - 1];

            // If the stored name was typed by hand and matches no project scene, surface it so it is not lost.
            if (current < 0 && !string.IsNullOrEmpty(sceneNameProp.stringValue))
                EditorGUILayout.HelpBox($"Current value \"{sceneNameProp.stringValue}\" matches no project scene.", MessageType.None);
        }

        private static void DrawBuildSettingsCheck(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            bool inBuild = EditorBuildSettings.scenes.Any(s =>
                s.enabled && Path.GetFileNameWithoutExtension(s.path) == sceneName);
            if (inBuild) return;

            EditorGUILayout.HelpBox(
                $"Scene '{sceneName}' is not in Build Settings — at runtime the loader will log an error and " +
                "skip it (the flow continues). Add it to actually load the scene.", MessageType.Warning);

            if (GUILayout.Button("Add to Build Settings"))
                AddToBuildSettings(sceneName);
        }

        private static void AddToBuildSettings(string sceneName)
        {
            var path = ProjectScenePaths().FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName);
            if (string.IsNullOrEmpty(path))
            {
                Logging.Warning("GraphGameFlow", $"[GraphGameFlow] No project scene named '{sceneName}' to add to Build Settings.");
                return;
            }

            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
            Logging.Info("GraphGameFlow", $"[GraphGameFlow] Added '{path}' to Build Settings.");
        }

        private static void DrawProviderDropdown(SerializedProperty sceneNameProp, ISceneKeySourceProvider provider)
        {
            var keys = provider.GetKeys().ToList();
            var labels = new List<string> { "(none)" };
            labels.AddRange(keys);

            int current    = keys.FindIndex(k => k == sceneNameProp.stringValue);
            int popupIndex = current < 0 ? 0 : current + 1;

            int newIndex = EditorGUILayout.Popup(new GUIContent(provider.SourceLabel + " Key"), popupIndex, labels.ToArray());
            if (newIndex != popupIndex)
                sceneNameProp.stringValue = newIndex == 0 ? string.Empty : keys[newIndex - 1];

            if (current < 0 && !string.IsNullOrEmpty(sceneNameProp.stringValue))
                EditorGUILayout.HelpBox(
                    $"Current value \"{sceneNameProp.stringValue}\" matches no registered {provider.SourceLabel} entry.",
                    MessageType.None);

            DrawPromoteRow(sceneNameProp.stringValue, keys, provider);
        }

        // Mirrors "Add to Build Settings": lets an author who typed/picked a plain project-scene name promote
        // it to the provider's own source in one click.
        private static void DrawPromoteRow(string sceneName, List<string> knownKeys, ISceneKeySourceProvider provider)
        {
            if (string.IsNullOrEmpty(sceneName) || knownKeys.Contains(sceneName)) return;

            var path = ProjectScenePaths().FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == sceneName);
            if (string.IsNullOrEmpty(path) || !provider.CanPromote(path, sceneName)) return;

            EditorGUILayout.HelpBox($"'{sceneName}' is a project scene but not yet a {provider.SourceLabel} entry.", MessageType.Warning);
            if (GUILayout.Button($"Mark as {provider.SourceLabel}"))
                provider.Promote(path, sceneName);
        }
    }
}
