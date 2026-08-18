using System;
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

        // Foldout state is per-session only (not serialized) — a group defaults to expanded the
        // first time it is seen, matching the previous always-flat-visible layout.
        private readonly Dictionary<string, bool> _groupExpanded = new Dictionary<string, bool>();

        public override void OnInspectorGUI()
        {
            var settings = (GraphLoggingSettings)target;

            EditorGUILayout.HelpBox(
                "Info/Warning are enabled per GROUP (the prefix before the first '.' in a category, e.g. " +
                "\"GraphCore\" for \"GraphCore.Context\") — flip a whole lib on/off in one place, and any " +
                "category discovered later under it inherits that default automatically. A category row " +
                "only needs its own toggle when it diverges from its group, and clears itself once it " +
                "matches again. A group appears here the first time one of its categories logs. Errors are " +
                "never gated — always visible.",
                MessageType.None);
            EditorGUILayout.Space(4);

            if (settings.Groups.Count == 0)
            {
                EditorGUILayout.LabelField("No category has logged yet.", EditorStyles.miniLabel);
                return;
            }

            foreach (var group in settings.Groups.OrderBy(g => g.Prefix, StringComparer.Ordinal))
                DrawGroup(settings, group);
        }

        private void DrawGroup(GraphLoggingSettings settings, GraphLoggingSettings.GroupEntry group)
        {
            var expanded = _groupExpanded.TryGetValue(group.Prefix, out var e) ? e : true;

            var rowRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            var toggleWidth = InfoColumnWidth + WarningColumnWidth + 4f;
            var foldoutRect = new Rect(rowRect.x, rowRect.y, rowRect.width - toggleWidth, rowRect.height);
            expanded = EditorGUI.Foldout(foldoutRect, expanded, $"{group.Prefix} ({group.KnownCategories.Count})", true, EditorStyles.foldoutHeader);
            _groupExpanded[group.Prefix] = expanded;

            var toggleRect = new Rect(rowRect.xMax - toggleWidth, rowRect.y, InfoColumnWidth, rowRect.height);
            DrawToggle(toggleRect, group.DefaultInfoEnabled, newValue =>
            {
                Undo.RecordObject(settings, "Change Log Group Default");
                settings.SetGroupInfoEnabled(group.Prefix, newValue);
            });
            toggleRect.x += InfoColumnWidth + 4f;
            toggleRect.width = WarningColumnWidth;
            DrawToggle(toggleRect, group.DefaultWarningEnabled, newValue =>
            {
                Undo.RecordObject(settings, "Change Log Group Default");
                settings.SetGroupWarningEnabled(group.Prefix, newValue);
            });

            if (!expanded) return;

            EditorGUI.indentLevel++;
            foreach (var category in group.KnownCategories.OrderBy(c => c, StringComparer.Ordinal))
                DrawCategoryRow(settings, category);
            EditorGUI.indentLevel--;
        }

        private void DrawCategoryRow(GraphLoggingSettings settings, string category)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(category);
            GUILayout.FlexibleSpace();

            DrawToggle(GUILayoutUtility.GetRect(InfoColumnWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(InfoColumnWidth)),
                settings.IsInfoEnabled(category), newValue =>
                {
                    Undo.RecordObject(settings, "Change Log Category Override");
                    settings.SetCategoryInfoEnabled(category, newValue);
                });

            DrawToggle(GUILayoutUtility.GetRect(WarningColumnWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(WarningColumnWidth)),
                settings.IsWarningEnabled(category), newValue =>
                {
                    Undo.RecordObject(settings, "Change Log Category Override");
                    settings.SetCategoryWarningEnabled(category, newValue);
                });

            EditorGUILayout.EndHorizontal();
        }

        private static void DrawToggle(Rect rect, bool value, Action<bool> onChanged)
        {
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUI.Toggle(rect, value);
            if (EditorGUI.EndChangeCheck())
                onChanged(newValue);
        }
    }
}
