using System.Collections.Generic;
using Faolline.GraphLocalization;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Speaker"/>: identity + name fallback, the localization group — picked from the
    /// groups already used in the project (or created once via "New group…"), with the derived table shown — a
    /// fallback expression asset, and a reorderable list of expressions (key → presentation asset). Replaces Unity's
    /// default list UI with an add/remove/reorder list whose rows show the key and asset side by side. Several
    /// speakers can be edited at once (e.g. to put a whole cast in one group); the expressions list needs a single one.
    /// </summary>
    [CustomEditor(typeof(Speaker)), CanEditMultipleObjects]
    public class SpeakerEditor : UnityEditor.Editor
    {
        private SerializedProperty _speakerId;
        private SerializedProperty _displayNameFallback;
        private SerializedProperty _nameColor;
        private SerializedProperty _localizationGroup;
        private SerializedProperty _fallbackExpression;
        private SerializedProperty _expressions;
        private ReorderableList _expressionsList;

        private IReadOnlyList<string> _groups;
        private bool _typingNewGroup;
        private string _newGroupName = string.Empty;

        private void OnEnable()
        {
            _speakerId = serializedObject.FindProperty("_speakerId");
            _displayNameFallback = serializedObject.FindProperty("_displayNameFallback");
            _nameColor = serializedObject.FindProperty("_nameColor");
            _localizationGroup = serializedObject.FindProperty("_localizationGroup");
            _fallbackExpression = serializedObject.FindProperty("_fallbackExpression");
            _expressions = serializedObject.FindProperty("_expressions");
            _groups = SpeakerGroupCatalog.Collect();

            _expressionsList = new ReorderableList(serializedObject, _expressions, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Expressions (key → asset)"),
                elementHeight = EditorGUIUtility.singleLineHeight + 6f,
                drawElementCallback = DrawExpressionElement,
                onAddCallback = OnAddExpression
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_speakerId != null)
                EditorGUILayout.PropertyField(_speakerId, new GUIContent("Speaker Id", "Logical id referenced by line nodes. Not translated."));
            if (_displayNameFallback != null)
                EditorGUILayout.PropertyField(_displayNameFallback, new GUIContent("Display Name Fallback", "Shown (and used as source text) when the localized name cannot resolve."));
            if (_nameColor != null)
                EditorGUILayout.PropertyField(_nameColor, new GUIContent("Name Color", "Tint applied to this speaker's name in the built-in views."));

            if (_localizationGroup != null)
            {
                EditorGUILayout.Space(6);
                DrawLocalizationGroup();
            }

            EditorGUILayout.Space(6);
            if (_fallbackExpression != null)
                EditorGUILayout.PropertyField(_fallbackExpression, new GUIContent("Fallback Expression", "Used when a requested expression key is unknown."));

            EditorGUILayout.Space(6);
            if (targets.Length == 1)
                _expressionsList?.DoLayoutList();
            else
                EditorGUILayout.HelpBox("Select a single speaker to edit its expressions.", MessageType.None);

            serializedObject.ApplyModifiedProperties();
        }

        private static readonly GUIContent GroupLabel = new GUIContent("Localization Group",
            "Optional table group (e.g. a chapter): the display name is built into its own table so it can be " +
            "packaged/loaded separately. Pick a group already used by a speaker, or create one with \"New group…\". " +
            "(None) = the default speakers table.");

        private void DrawLocalizationGroup()
        {
            if (_typingNewGroup)
            {
                DrawNewGroupField();
            }
            else
            {
                bool mixed = _localizationGroup.hasMultipleDifferentValues;
                var popup = SpeakerGroupCatalog.BuildPopup(_groups, mixed ? null : _localizationGroup.stringValue);

                EditorGUI.showMixedValue = mixed;
                EditorGUI.BeginChangeCheck();
                int index = EditorGUILayout.Popup(GroupLabel, popup.Selected, popup.Labels);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                {
                    if (index == popup.NewGroupIndex)
                    {
                        _typingNewGroup = true;
                        _newGroupName = string.Empty;
                    }
                    else
                    {
                        _localizationGroup.stringValue = popup.Values[index];
                    }
                }
            }

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(new GUIContent("Localization Table", "Table holding the display name (derived)."),
                    _localizationGroup.hasMultipleDifferentValues
                        ? "—"
                        : DialogueLocalizationKeys.ForSpeakerGroupTable(_localizationGroup.stringValue));
        }

        // Inline name entry for "New group…": typed once, then the group is in the dropdown for every speaker.
        private void DrawNewGroupField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName("SpeakerNewGroupName");
                _newGroupName = EditorGUILayout.TextField(new GUIContent("New Group", "Name of the new localization group, e.g. Chapitre3."), _newGroupName);
                var name = LocalizationTableNames.NormalizeGroup(_newGroupName);
                using (new EditorGUI.DisabledScope(name == null))
                {
                    if (GUILayout.Button("Create", GUILayout.Width(60)))
                    {
                        _localizationGroup.stringValue = name;
                        _typingNewGroup = false;
                        var groups = new List<string>(_groups) { name };
                        groups.Sort(System.StringComparer.OrdinalIgnoreCase);
                        _groups = groups;
                        GUI.FocusControl(null);
                    }
                }
                if (GUILayout.Button("Cancel", GUILayout.Width(60)))
                {
                    _typingNewGroup = false;
                    GUI.FocusControl(null);
                }
            }
        }

        private void DrawExpressionElement(Rect rect, int index, bool active, bool focused)
        {
            var element = _expressions.GetArrayElementAtIndex(index);
            var keyProp = element.FindPropertyRelative("_key");
            var assetProp = element.FindPropertyRelative("_asset");

            rect.y += 3f;
            rect.height = EditorGUIUtility.singleLineHeight;

            float keyWidth = rect.width * 0.4f;
            var keyRect = new Rect(rect.x, rect.y, keyWidth - 4f, rect.height);
            var assetRect = new Rect(rect.x + keyWidth, rect.y, rect.width - keyWidth, rect.height);

            if (keyProp != null) EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
            if (assetProp != null) EditorGUI.PropertyField(assetRect, assetProp, GUIContent.none);
        }

        private void OnAddExpression(ReorderableList list)
        {
            _expressions.arraySize++;
            var element = _expressions.GetArrayElementAtIndex(_expressions.arraySize - 1);
            var keyProp = element.FindPropertyRelative("_key");
            var assetProp = element.FindPropertyRelative("_asset");
            if (keyProp != null) keyProp.stringValue = "neutral";
            if (assetProp != null) assetProp.objectReferenceValue = null;
        }
    }
}
