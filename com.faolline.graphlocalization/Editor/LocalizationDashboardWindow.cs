using System;
using System.Collections.Generic;
using System.IO;
using Faolline.GraphLogging;
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
            public LocalizationDatabase Database;
            public string LastBuild;
            public int TotalGraphs;
            public int TotalKeys;
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
            var adapters = GraphLocalizationAdapterRegistry.DiscoverAdapters();

            if (adapters.Count == 0)
            {
                _reports.Add(new LibReport { LibName = "(no adapters found)" });
                return;
            }

            var manifest = GraphLocalizationManifest.Load();

            foreach (var adapter in adapters)
            {
                var db = new LocalizationDatabase();
                adapter.ScanAndIndex(db);

                var libEntry = manifest != null
                    ? manifest.Libs as System.Collections.Generic.IReadOnlyList<GraphLocalizationManifest.LibEntry>
                    : null;
                GraphLocalizationManifest.LibEntry mEntry = null;
                if (manifest != null)
                    foreach (var e in manifest.Libs)
                        if (e.LibName == adapter.LibName) { mEntry = e; break; }

                var report = new LibReport
                {
                    LibName = adapter.LibName,
                    Database = db,
                    LastBuild = mEntry != null && !string.IsNullOrEmpty(mEntry.LastBuildTime)
                        ? mEntry.LastBuildTime
                        : "Never built",
                    TotalGraphs = db.TotalGraphsScanned,
                    TotalKeys = db.TotalKeysFound,
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
            if (settingsAsset != null)
            {
                GUILayout.Label($"Mode: {settingsAsset.Mode}  |  Validation: {settingsAsset.LocaleValidation}",
                    _dimStyle ?? EditorStyles.miniLabel);

                if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
                    Selection.activeObject = settingsAsset;
            }
            else
            {
                GUILayout.Label("No settings asset", _dimStyle ?? EditorStyles.miniLabel);
                if (GUILayout.Button("Create Settings", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    CreateOrSelectSettings();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private static void CreateOrSelectSettings()
        {
            var path = LocalizationSettingsLoader.GetDefaultAssetPath();
            var existing = AssetDatabase.LoadAssetAtPath<LocalizationSettingsAsset>(path);
            if (existing != null) { Selection.activeObject = existing; return; }

            var folder = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            var asset = ScriptableObject.CreateInstance<LocalizationSettingsAsset>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            Logging.Info("GraphLocalization.AutoBuild", $"[GraphLocalization] Created LocalizationSettingsAsset at {path}");
        }

        private void DrawLibReport(LibReport report)
        {
            var db = report.Database;
            var headerLabel = $"{report.LibName}  —  {report.TotalGraphs} graphs  •  {report.TotalKeys} keys";

            report.IsExpanded = EditorGUILayout.BeginFoldoutHeaderGroup(report.IsExpanded, headerLabel);

            if (report.IsExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Last build", report.LastBuild, _dimStyle ?? EditorStyles.miniLabel);

                EditorGUILayout.Space(4);

                if (db == null || db.Graphs.Count == 0)
                {
                    EditorGUILayout.LabelField("No graphs indexed.", _dimStyle ?? EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("Graphs", _subheaderStyle ?? EditorStyles.boldLabel);
                    foreach (var graph in db.Graphs)
                        DrawGraphEntry(graph);
                }

                if (db != null && db.GlobalKeys.Count > 0)
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
            int text = 0, choice = 0, speaker = 0, quest = 0, objective = 0;
            foreach (var k in graph.Keys)
            {
                if (k.Type == LocalizationKeyType.Text) text++;
                else if (k.Type == LocalizationKeyType.ChoiceLabel) choice++;
                else if (k.Type == LocalizationKeyType.SpeakerName) speaker++;
                else if (k.Type == LocalizationKeyType.QuestName) quest++;
                else if (k.Type == LocalizationKeyType.ObjectiveName || k.Type == LocalizationKeyType.ObjectiveDescription) objective++;
            }

            var summary = $"{graph.GraphName}  —  {graph.Keys.Count} keys";
            var parts = new System.Collections.Generic.List<string>();
            if (text > 0) parts.Add($"text:{text}");
            if (choice > 0) parts.Add($"choice:{choice}");
            if (speaker > 0) parts.Add($"speaker:{speaker}");
            if (quest > 0) parts.Add($"quest:{quest}");
            if (objective > 0) parts.Add($"objective:{objective}");
            var detail = parts.Count > 0 ? string.Join("  ", parts) : "—";
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

    }
}
