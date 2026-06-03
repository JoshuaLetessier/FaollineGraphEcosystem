using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="Speaker"/>: identity + name fallback, a fallback expression asset,
    /// and a reorderable list of expressions (key → presentation asset). Replaces Unity's default list UI
    /// with an add/remove/reorder list whose rows show the key and asset side by side.
    /// </summary>
    [CustomEditor(typeof(Speaker))]
    public class SpeakerEditor : UnityEditor.Editor
    {
        private SerializedProperty _speakerId;
        private SerializedProperty _displayNameFallback;
        private SerializedProperty _fallbackExpression;
        private SerializedProperty _expressions;
        private ReorderableList _expressionsList;

        private void OnEnable()
        {
            _speakerId = serializedObject.FindProperty("_speakerId");
            _displayNameFallback = serializedObject.FindProperty("_displayNameFallback");
            _fallbackExpression = serializedObject.FindProperty("_fallbackExpression");
            _expressions = serializedObject.FindProperty("_expressions");

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

            EditorGUILayout.Space(6);
            if (_fallbackExpression != null)
                EditorGUILayout.PropertyField(_fallbackExpression, new GUIContent("Fallback Expression", "Used when a requested expression key is unknown."));

            EditorGUILayout.Space(6);
            _expressionsList?.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
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
