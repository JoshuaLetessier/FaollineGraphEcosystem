using UnityEditor;
using Faolline.GraphGameFlow;

namespace Faolline.GraphGameFlow.Editor
{
    /// <summary>
    /// Inspector for an <see cref="UnloadSceneAction"/> asset: pick the target scene from a dropdown (Build
    /// Settings scenes, or — when Addressables is in the project — registered Addressable keys), mirroring
    /// <see cref="LoadSceneActionEditor"/>. No Build-Settings check here — unloading targets a scene that is
    /// already loaded, whatever loaded it. The runtime stores whatever identifier was picked, unchanged.
    /// </summary>
    [CustomEditor(typeof(UnloadSceneAction))]
    public class UnloadSceneActionEditor : UnityEditor.Editor
    {
        private int _sourceMode = -1;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var sceneNameProp = serializedObject.FindProperty("_sceneName");
            SceneNameFieldDrawer.Draw(sceneNameProp, ref _sourceMode, showBuildSettingsCheck: false);

            EditorGUILayout.HelpBox(
                "Unloads an ADDITIVELY loaded scene. Unity cannot unload the last remaining scene — at " +
                "runtime the loader logs an error and skips it (the flow continues).", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
