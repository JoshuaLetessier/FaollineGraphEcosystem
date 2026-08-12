using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLogging.Editor
{
    [CustomEditor(typeof(GraphLoggingSettings))]
    public sealed class GraphLoggingSettingsEditor : UnityEditor.Editor
    {
        private const float InfoColumnWidth = 40f;
        private const float WarningColumnWidth = 60f;

        private SerializedProperty _categories;

        // Foldout state is per-session only (not serialized) — a group defaults to expanded the
        // first time it is seen, matching the previous always-flat-visible layout.
        private readonly Dictionary<string, bool> _groupExpanded = new Dictionary<string, bool>();

        private void OnEnable()
        {
            _categories = serializedObject.FindProperty("_categories");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Per-category Info/Warning toggles for Logging.Info/Warning calls across the ecosystem. " +
                "A category appears here the first time it logs, grouped by its prefix (e.g. \"GraphCore\" " +
                "for \"GraphCore.Context\"). Errors are never gated — always visible.",
                MessageType.None);
            EditorGUILayout.Space(4);

            if (_categories.arraySize == 0)
            {
                EditorGUILayout.LabelField("No category has logged yet.", EditorStyles.miniLabel);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            DrawHeaderRow("All categories", Enumerable.Range(0, _categories.arraySize), boldLabel: true);
            EditorGUILayout.Space(6);

            foreach (var group in GroupedByPrefix())
            {
                var expanded = _groupExpanded.TryGetValue(group.Key, out var e) ? e : true;
                var rowRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));

                var toggleWidth = InfoColumnWidth + WarningColumnWidth + 4f;
                var foldoutRect = new Rect(rowRect.x, rowRect.y, rowRect.width - toggleWidth, rowRect.height);
                expanded = EditorGUI.Foldout(foldoutRect, expanded, $"{group.Key} ({group.Value.Count})", true, EditorStyles.foldoutHeader);
                _groupExpanded[group.Key] = expanded;

                var toggleRect = new Rect(rowRect.xMax - toggleWidth, rowRect.y, InfoColumnWidth, rowRect.height);
                DrawMasterToggleAt(toggleRect, group.Value, "InfoEnabled");
                toggleRect.x += InfoColumnWidth + 4f;
                toggleRect.width = WarningColumnWidth;
                DrawMasterToggleAt(toggleRect, group.Value, "WarningEnabled");

                if (expanded)
                {
                    EditorGUI.indentLevel++;
                    foreach (var index in group.Value)
                        DrawCategoryRow(index);
                    EditorGUI.indentLevel--;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Groups category indices by the segment before the first '.', sorted by group then category name.</summary>
        private List<KeyValuePair<string, List<int>>> GroupedByPrefix()
        {
            var groups = new Dictionary<string, List<int>>();
            foreach (var index in Enumerable.Range(0, _categories.arraySize))
            {
                var category = CategoryOf(index);
                var dot = category.IndexOf('.');
                var prefix = dot > 0 ? category.Substring(0, dot) : category;
                if (!groups.TryGetValue(prefix, out var list))
                    groups[prefix] = list = new List<int>();
                list.Add(index);
            }

            foreach (var list in groups.Values)
                list.Sort((a, b) => string.CompareOrdinal(CategoryOf(a), CategoryOf(b)));

            return groups.OrderBy(g => g.Key, System.StringComparer.Ordinal).ToList();
        }

        private string CategoryOf(int index) =>
            _categories.GetArrayElementAtIndex(index).FindPropertyRelative("Category").stringValue;

        private void DrawHeaderRow(string label, IEnumerable<int> indices, bool boldLabel)
        {
            var style = boldLabel ? EditorStyles.boldLabel : EditorStyles.label;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, style);
            GUILayout.FlexibleSpace();
            DrawMasterToggle(indices, "InfoEnabled", InfoColumnWidth);
            DrawMasterToggle(indices, "WarningEnabled", WarningColumnWidth);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawMasterToggle(IEnumerable<int> indices, string propertyName, float width)
        {
            var rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            DrawMasterToggleAt(rect, indices, propertyName);
        }

        /// <summary>A tri-state toggle: on/off when every entry agrees, mixed otherwise. Setting it applies to every entry.</summary>
        private void DrawMasterToggleAt(Rect rect, IEnumerable<int> indices, string propertyName)
        {
            var list = indices.ToList();
            bool? value = null;
            var mixed = false;
            foreach (var index in list)
            {
                var current = _categories.GetArrayElementAtIndex(index).FindPropertyRelative(propertyName).boolValue;
                if (value == null) value = current;
                else if (value != current) { mixed = true; break; }
            }

            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUI.Toggle(rect, value ?? true);
            if (EditorGUI.EndChangeCheck())
                foreach (var index in list)
                    _categories.GetArrayElementAtIndex(index).FindPropertyRelative(propertyName).boolValue = newValue;
            EditorGUI.showMixedValue = false;
        }

        private void DrawCategoryRow(int index)
        {
            var entry = _categories.GetArrayElementAtIndex(index);
            var categoryProp = entry.FindPropertyRelative("Category");
            var infoProp = entry.FindPropertyRelative("InfoEnabled");
            var warningProp = entry.FindPropertyRelative("WarningEnabled");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(categoryProp.stringValue);
            GUILayout.FlexibleSpace();
            infoProp.boolValue = EditorGUILayout.Toggle(infoProp.boolValue, GUILayout.Width(InfoColumnWidth));
            warningProp.boolValue = EditorGUILayout.Toggle(warningProp.boolValue, GUILayout.Width(WarningColumnWidth));
            EditorGUILayout.EndHorizontal();
        }
    }
}
