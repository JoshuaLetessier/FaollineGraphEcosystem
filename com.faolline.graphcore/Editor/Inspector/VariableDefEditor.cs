using UnityEditor;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="VariableDef"/>: shows the display Name, the Type, and ONLY the single
    /// default field that matches the chosen type (the other six typed defaults stay hidden), plus the read-only
    /// stable GUID. Mirrors the old <c>ParameterDataDrawer</c> behaviour for the asset form.
    /// </summary>
    [CustomEditor(typeof(VariableDef))]
    public sealed class VariableDefEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_name"), new GUIContent("Name"));

            var typeProp = serializedObject.FindProperty("_type");
            EditorGUILayout.PropertyField(typeProp, new GUIContent("Type"));

            var defaultProp = serializedObject.FindProperty(DefaultFieldName((VariableType)typeProp.enumValueIndex));
            if (defaultProp != null)
                EditorGUILayout.PropertyField(defaultProp, new GUIContent("Default"));

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("Stable Id (GUID)", serializedObject.FindProperty("_id").stringValue);

            serializedObject.ApplyModifiedProperties();
        }

        // The typed-default field backing each VariableType (the only one the inspector shows).
        private static string DefaultFieldName(VariableType type)
        {
            switch (type)
            {
                case VariableType.Int:     return "_intDefault";
                case VariableType.Float:   return "_floatDefault";
                case VariableType.String:  return "_stringDefault";
                case VariableType.Vector2: return "_vector2Default";
                case VariableType.Vector3: return "_vector3Default";
                case VariableType.Color:   return "_colorDefault";
                default:                    return "_boolDefault";   // VariableType.Bool
            }
        }
    }
}
