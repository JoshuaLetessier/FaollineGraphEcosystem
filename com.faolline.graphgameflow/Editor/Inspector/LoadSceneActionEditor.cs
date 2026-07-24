using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Inspector for a <see cref="LoadSceneAction"/> asset: pick the target scene from a dropdown (Build
    /// Settings scenes, or — when Addressables is in the project — registered Addressable keys), with an
    /// inline Single-vs-Additive explanation and a Build-Settings check (a scene must be in Build Settings to
    /// load at runtime via <see cref="UnitySceneLoader"/>/<see cref="AsyncSceneLoader"/> — otherwise the
    /// loader logs and skips it; this check does not apply when resolving through an Addressable key). The
    /// runtime stores whatever identifier was picked, unchanged.
    /// </summary>
    [CustomEditor(typeof(LoadSceneAction))]
    public class LoadSceneActionEditor : UnityEditor.Editor
    {
        private int _sourceMode = -1;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sceneNameProp = serializedObject.FindProperty("_sceneName");
            var modeProp      = serializedObject.FindProperty("_mode");
            var setActiveProp = serializedObject.FindProperty("_setActiveOnLoad");

            SceneNameFieldDrawer.Draw(sceneNameProp, ref _sourceMode, showBuildSettingsCheck: true);
            DrawModeWithHint(modeProp);
            if ((LoadSceneMode)modeProp.enumValueIndex == LoadSceneMode.Additive)
                EditorGUILayout.PropertyField(setActiveProp, new GUIContent("Set Active On Load",
                    "Make this scene the ACTIVE scene once it finishes loading (its lighting/fog settings apply; new objects parent into it)."));

            serializedObject.ApplyModifiedProperties();
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
    }
}
