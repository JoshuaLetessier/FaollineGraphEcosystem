using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    [CustomEditor(typeof(LocalizationSettingsAsset))]
    public sealed class LocalizationSettingsAssetEditor : UnityEditor.Editor
    {
        private SerializedProperty _mode;
        private SerializedProperty _tableName;
        private SerializedProperty _localeValidation;
        private SerializedProperty _playerStrictMode;

        private static readonly string[] _validationHelp =
        {
            "Permissive — Gaps accepted silently. Use during early development when translations are not ready.",
            "Warn — Log a warning for each untranslated locale. Default: catches problems without blocking iteration.",
            "Strict — Log gaps as errors. Use as a pre-release QA gate to enforce complete translations.",
        };
        private static readonly MessageType[] _validationMsgType = { MessageType.None, MessageType.Warning, MessageType.Error };

        private static readonly string[] _strictModeHelp =
        {
            "Permissive — Missing keys fall back to #key silently. Safe for production builds.",
            "Audit — Fall back to #key, log a warning, and record missing keys in MissingKeys. Default.",
            "Strict — Throw LocalizationException on the first missing key. Use in automated QA or CI test runs.",
        };
        private static readonly MessageType[] _strictModeMsgType = { MessageType.None, MessageType.Warning, MessageType.Error };

        private SerializedProperty _csvLocales;
        private SerializedProperty _csvOutputFolder;

        private void OnEnable()
        {
            _mode = serializedObject.FindProperty("_mode");
            _tableName = serializedObject.FindProperty("_unityLocalizationTableName");
            _localeValidation = serializedObject.FindProperty("_localeValidation");
            _playerStrictMode = serializedObject.FindProperty("_playerStrictMode");
            _csvLocales = serializedObject.FindProperty("_csvLocales");
            _csvOutputFolder = serializedObject.FindProperty("_csvOutputFolder");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Provider", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_mode);
            if (_mode.enumValueIndex == (int)LocalizationMode.UnityLocalization)
            {
                EditorGUILayout.PropertyField(_tableName);
            }
            else // Csv
            {
                EditorGUILayout.PropertyField(_csvLocales, new GUIContent("CSV Locales"));
                EditorGUILayout.PropertyField(_csvOutputFolder, new GUIContent("CSV Output Folder"));
                EditorGUILayout.HelpBox("Build All Tables writes one CSV per lib here. The first locale " +
                    "is the source column (pre-filled from node/choice/speaker text).", MessageType.None);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Build-time Validation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_localeValidation, new GUIContent("Locale Validation"));
            var valIdx = Mathf.Clamp(_localeValidation.enumValueIndex, 0, _validationHelp.Length - 1);
            EditorGUILayout.HelpBox(_validationHelp[valIdx], _validationMsgType[valIdx]);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Runtime Playback", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_playerStrictMode, new GUIContent("Player Strict Mode"));
            var strictIdx = Mathf.Clamp(_playerStrictMode.enumValueIndex, 0, _strictModeHelp.Length - 1);
            EditorGUILayout.HelpBox(_strictModeHelp[strictIdx], _strictModeMsgType[strictIdx]);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
