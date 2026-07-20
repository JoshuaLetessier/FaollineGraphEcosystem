using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Inspector for an <see cref="UnloadSceneAction"/> asset: pick the target scene from a dropdown of the
    /// project's scenes (instead of typing a name), mirroring <see cref="LoadSceneActionEditor"/>. No
    /// Build-Settings check here — unloading targets a scene that is already loaded, whatever loaded it. The
    /// runtime stores the scene NAME, unchanged.
    /// </summary>
    [CustomEditor(typeof(UnloadSceneAction))]
    public class UnloadSceneActionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sceneNameProp = serializedObject.FindProperty("_sceneName");
            DrawSceneDropdown(sceneNameProp);

            EditorGUILayout.HelpBox(
                "Unloads an ADDITIVELY loaded scene. Unity cannot unload the last remaining scene — at " +
                "runtime the loader logs an error and skips it (the flow continues).", MessageType.None);

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

            int current    = names.FindIndex(n => n == sceneNameProp.stringValue);
            int popupIndex = current < 0 ? 0 : current + 1;

            int newIndex = EditorGUILayout.Popup(new GUIContent("Scene"), popupIndex, labels.ToArray());
            if (newIndex != popupIndex)
                sceneNameProp.stringValue = newIndex == 0 ? string.Empty : names[newIndex - 1];

            // If the stored name was typed by hand and matches no project scene, surface it so it is not lost.
            if (current < 0 && !string.IsNullOrEmpty(sceneNameProp.stringValue))
                EditorGUILayout.HelpBox($"Current value \"{sceneNameProp.stringValue}\" matches no project scene.", MessageType.None);
        }
    }
}
