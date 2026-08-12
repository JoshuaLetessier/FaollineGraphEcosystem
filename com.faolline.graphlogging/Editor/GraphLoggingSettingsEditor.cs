using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLogging.Editor
{
    [CustomEditor(typeof(GraphLoggingSettings))]
    public sealed class GraphLoggingSettingsEditor : UnityEditor.Editor
    {
        private SerializedProperty _categories;

        private void OnEnable()
        {
            _categories = serializedObject.FindProperty("_categories");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Per-category Info/Warning toggles for Logging.Info/Warning calls across the ecosystem. " +
                "A category appears here the first time it logs. Errors are never gated — always visible.",
                MessageType.None);
            EditorGUILayout.Space(4);

            if (_categories.arraySize == 0)
            {
                EditorGUILayout.LabelField("No category has logged yet.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Category", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Info", EditorStyles.boldLabel, GUILayout.Width(40));
                EditorGUILayout.LabelField("Warning", EditorStyles.boldLabel, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();

                foreach (var index in SortedIndices())
                {
                    var entry = _categories.GetArrayElementAtIndex(index);
                    var categoryProp = entry.FindPropertyRelative("Category");
                    var infoProp = entry.FindPropertyRelative("InfoEnabled");
                    var warningProp = entry.FindPropertyRelative("WarningEnabled");

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(categoryProp.stringValue);
                    GUILayout.FlexibleSpace();
                    infoProp.boolValue = EditorGUILayout.Toggle(infoProp.boolValue, GUILayout.Width(40));
                    warningProp.boolValue = EditorGUILayout.Toggle(warningProp.boolValue, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private IEnumerable<int> SortedIndices()
        {
            var indices = Enumerable.Range(0, _categories.arraySize).ToList();
            indices.Sort((a, b) => string.CompareOrdinal(
                _categories.GetArrayElementAtIndex(a).FindPropertyRelative("Category").stringValue,
                _categories.GetArrayElementAtIndex(b).FindPropertyRelative("Category").stringValue));
            return indices;
        }
    }
}
