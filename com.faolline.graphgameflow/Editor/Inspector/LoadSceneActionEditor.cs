using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Inspector for a <see cref="LoadSceneAction"/> asset: pick the target scene from a dropdown of the
    /// project's scenes (instead of typing a name), with an inline Single-vs-Additive explanation and a
    /// Build-Settings check (a scene must be in Build Settings to load at runtime — otherwise the loader
    /// logs and skips it). The runtime stores the scene NAME, unchanged.
    /// </summary>
    [CustomEditor(typeof(LoadSceneAction))]
    public class LoadSceneActionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sceneNameProp = serializedObject.FindProperty("_sceneName");
            var modeProp      = serializedObject.FindProperty("_mode");
            var setActiveProp = serializedObject.FindProperty("_setActiveOnLoad");

            DrawSceneDropdown(sceneNameProp);
            DrawModeWithHint(modeProp);
            if ((LoadSceneMode)modeProp.enumValueIndex == LoadSceneMode.Additive)
                EditorGUILayout.PropertyField(setActiveProp, new GUIContent("Set Active On Load",
                    "Make this scene the ACTIVE scene once it finishes loading (its lighting/fog settings apply; new objects parent into it)."));
            DrawBuildSettingsCheck(sceneNameProp.stringValue);

            serializedObject.ApplyModifiedProperties();
        }

        private static List<string> ProjectScenePaths() =>
            AssetDatabase.FindAssets("t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith("Assets/"))
                .Distinct()
                .OrderBy(p => p)
                .ToList();

        private static void DrawSceneDropdown(SerializedProperty sceneNameProp)
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

            int current   = names.FindIndex(n => n == sceneNameProp.stringValue);
            int popupIndex = current < 0 ? 0 : current + 1;

            int newIndex = EditorGUILayout.Popup(new GUIContent("Scene"), popupIndex, labels.ToArray());
            if (newIndex != popupIndex)
                sceneNameProp.stringValue = newIndex == 0 ? string.Empty : names[newIndex - 1];

            // If the stored name was typed by hand and matches no project scene, surface it so it is not lost.
            if (current < 0 && !string.IsNullOrEmpty(sceneNameProp.stringValue))
                EditorGUILayout.HelpBox($"Current value \"{sceneNameProp.stringValue}\" matches no project scene.", MessageType.None);
        }

        private static void DrawModeWithHint(SerializedProperty modeProp)
        {
            EditorGUILayout.PropertyField(modeProp, new GUIContent("Mode"));
            var mode = (LoadSceneMode)modeProp.enumValueIndex;
            EditorGUILayout.HelpBox(
                mode == LoadSceneMode.Single
                    ? "Single — replaces the current scene(s) with this one."
                    : "Additive — loads this scene on top of the current one(s); nothing is unloaded.",
                MessageType.None);
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
                Debug.LogWarning($"[GraphGameFlow] No project scene named '{sceneName}' to add to Build Settings.");
                return;
            }

            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == path)) return;
            list.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
            Debug.Log($"[GraphGameFlow] Added '{path}' to Build Settings.");
        }
    }
}
