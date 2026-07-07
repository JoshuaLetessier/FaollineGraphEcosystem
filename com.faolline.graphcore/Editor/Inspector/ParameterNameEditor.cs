using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="ParameterName"/>: shows the display Name, the Type, and ONLY the single
    /// default field that matches the chosen type (the other six typed defaults stay hidden), plus the read-only
    /// stable GUID. Mirrors the old <c>ParameterDataDrawer</c> behaviour for the asset form.
    /// </summary>
    [CustomEditor(typeof(ParameterName))]
    public sealed class ParameterNameEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_name"), new GUIContent("Name"));

            var typeProp = serializedObject.FindProperty("_type");
            EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));

            var defaultProp = serializedObject.FindProperty(DefaultFieldName((ParameterType)typeProp.enumValueIndex));
            if (defaultProp != null)
                EditorGUILayout.PropertyField(defaultProp, new GUIContent("Default"));

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Stable Id (GUID)", serializedObject.FindProperty("_id").stringValue);

            serializedObject.ApplyModifiedProperties();
        }

        // The typed-default field backing each ParameterType (the only one the inspector shows).
        private static string DefaultFieldName(ParameterType type)
        {
            switch (type)
            {
                case ParameterType.Int:     return "_intDefault";
                case ParameterType.Float:   return "_floatDefault";
                case ParameterType.String:  return "_stringDefault";
                case ParameterType.Vector2: return "_vector2Default";
                case ParameterType.Vector3: return "_vector3Default";
                case ParameterType.Color:   return "_colorDefault";
                default:                    return "_boolDefault";   // ParameterType.Bool
            }
        }
    }
}
