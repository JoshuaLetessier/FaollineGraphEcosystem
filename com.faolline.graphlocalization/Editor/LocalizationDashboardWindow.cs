using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphLocalization.Editor
{
    /// <summary>
    /// Editor window showing an at-a-glance report of the localization state for every registered
    /// graph lib: last build time, key counts per graph, global keys, and orphan warnings.
    /// Menu: Faolline ▸ Localization ▸ Dashboard
    /// </summary>
    public sealed class LocalizationDashboardWindow : EditorWindow
    {
        [MenuItem("Faolline/Localization/Dashboard")]
        public static void Open()
        {
            var w = GetWindow<LocalizationDashboardWindow>("Localization Dashboard");
            w.minSize = new Vector2(420, 300);
            w.Refresh();
        }

        // ── State ────────────────────────────────────────────────────────────────

        private sealed class LibReport
        {
            public string LibName;
            public string DatabasePath;
            public LocalizationDatabase Database;
            public string LastBuild;
            public bool IsExpanded = true;
            public bool GlobalExpanded;
        }

        private List<LibReport> _reports = new();
        private Vector2 _scroll;
        private double _lastRefresh;

        // ── Styles (lazy) ────────────────────────────────────────────────────────

        private static GUIStyle _headerStyle;
        private static GUIStyle _subheaderStyle;
        private static GUIStyle _keyStyle;
        private static GUIStyle _dimStyle;

        private static void EnsureStyles()
        {
            if (_headerStyle != null) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _subheaderStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            _keyStyle = new GUIStyle(EditorStyles.label) { wordWrap = false };
            _dimStyle = new GUIStyle(EditorStyles.label);
            _dimStyle.normal.textColor = new Color(.55f, .55f, .55f);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void OnEnable() => Refresh();

        private void OnFocus() => Refresh();

        public void Refresh()
        {
            _reports.Clear();
            var adapters = GraphLocalizationAdapterRegistry.Adapters;

            if (adapters.Count == 0)
            {
                _reports.Add(new LibReport { LibName = "(no adapters registered)" });
                return;
            }

            foreach (var adapter in adapters)
            {
                var safeName = SanitizeFileName(adapter.LibName);
                var path = $"Assets/Resources/GraphLocalization_{safeName}.asset";
                var db = AssetDatabase.LoadAssetAtPath<LocalizationDatabase>(path);

                var report = new LibReport
                {
                    LibName = adapter.LibName,
                    DatabasePath = path,
                    Database = db,
                    LastBuild = db != null && db.Metadata.LastBuildTime != DateTime.MinValue
                        ? db.Metadata.LastBuildTime.ToString("yyyy-MM-dd  HH:mm:ss")
                        : "Never built",
                };
                _reports.Add(report);
            }

            _lastRefresh = EditorApplication.timeSinceStartup;
            Repaint();
        }

        // ── GUI ──────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EnsureStyles();

            DrawToolbar();

            if (_reports.Count == 0)
            {
                EditorGUILayout.HelpBox("No adapters registered. Make sure your graph lib assemblies are loaded.", MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var report in _reports)
                DrawLibReport(report);
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button("Build All Tables", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                LocalizationBuilderCore.BuildAll();
                Refresh();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
                Refresh();

            GUILayout.FlexibleSpace();

            var settingsAsset = LocalizationSettingsLoader.Load();
            var modeLabel = settingsAsset != null
                ? $"Mode: {settingsAsset.Mode}  |  Validation: {settingsAsset.LocaleValidation}"
                : "No settings asset";
            GUILayout.Label(modeLabel, _dimStyle ?? EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawLibReport(LibReport report)
        {
            // ── Header ─────────────────────────────────────────────────────────────
            var db = report.Database;
            var headerLabel = db != null
                ? $"{report.LibName}  —  {db.Graphs.Count} graphs  •  {db.Metadata.TotalKeysFound} keys"
                : $"{report.LibName}  —  (no database found — run Build All Tables)";

            report.IsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(report.IsExpanded, headerLabel);

            if (report.IsExpanded)
            {
                EditorGUI.indentLevel++;

                // Last build + database path
                EditorGUILayout.LabelField("Last build", report.LastBuild, _dimStyle ?? EditorStyles.miniLabel);
                if (db != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Database", report.DatabasePath, _dimStyle ?? EditorStyles.miniLabel);
                    if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                        Selection.activeObject = db;
                    EditorGUILayout.EndHorizontal();
                }

                if (db == null)
                {
                    EditorGUILayout.HelpBox("Run 'Build All Tables' to create the database.", MessageType.Info);
                    EditorGUI.indentLevel--;
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    EditorGUILayout.Space(4);
                    return;
                }

                EditorGUILayout.Space(4);

                // ── Per-graph ──────────────────────────────────────────────────────
                if (db.Graphs.Count == 0)
                {
                    EditorGUILayout.LabelField("No graphs indexed.", _dimStyle ?? EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("Graphs", _subheaderStyle ?? EditorStyles.boldLabel);
                    foreach (var graph in db.Graphs)
                        DrawGraphEntry(graph);
                }

                // ── Global keys ────────────────────────────────────────────────────
                if (db.GlobalKeys.Count > 0)
                {
                    EditorGUILayout.Space(2);
                    report.GlobalExpanded = EditorGUILayout.Foldout(report.GlobalExpanded,
                        $"Global keys  ({db.GlobalKeys.Count})", true);
                    if (report.GlobalExpanded)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var key in db.GlobalKeys)
                            DrawKeyEntry(key);
                        EditorGUI.indentLevel--;
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(4);
        }

        private static void DrawGraphEntry(LocalizationGraphEntry graph)
        {
            int text = 0, choice = 0, speaker = 0;
            foreach (var k in graph.Keys)
            {
                if (k.Type == LocalizationKeyType.Text) text++;
                else if (k.Type == LocalizationKeyType.ChoiceLabel) choice++;
                else if (k.Type == LocalizationKeyType.SpeakerName) speaker++;
            }

            var summary = $"{graph.GraphName}  —  {graph.Keys.Count} keys";
            var detail = $"text:{text}  choice:{choice}  speaker:{speaker}";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(summary, _keyStyle ?? EditorStyles.label);
            EditorGUILayout.LabelField(detail, _dimStyle ?? EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawKeyEntry(LocalizationKeyEntry key)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(key.Key, _keyStyle ?? EditorStyles.label, GUILayout.ExpandWidth(true));
            EditorGUILayout.LabelField(key.Type.ToString(), _dimStyle ?? EditorStyles.miniLabel, GUILayout.Width(80));
            if (!string.IsNullOrEmpty(key.DefaultHint))
                EditorGUILayout.LabelField($"\"{key.DefaultHint}\"", _dimStyle ?? EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.EndHorizontal();
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            var invalid = Path.GetInvalidFileNameChars();
            var chars = Array.ConvertAll(name.ToCharArray(), c => Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return new string(chars);
        }
    }
}
