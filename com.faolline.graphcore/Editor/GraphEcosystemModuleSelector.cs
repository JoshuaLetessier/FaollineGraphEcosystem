using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Faolline.GraphCore.Editor
{
    /// <summary>
    /// Lets a consumer of the Faolline graph ecosystem pick which packages to install via a checkbox UI.
    /// The installable set (the "whitelist") is declared in <c>GraphEcosystemModules.json</c> shipped beside
    /// this script — sample/verification packages are intentionally omitted, so they are never offered.
    /// Selections are applied with the official <see cref="Client.AddAndRemove(string[], string[])"/> API
    /// (UPM rewrites the manifest correctly), and module dependencies are resolved automatically.
    /// Menu: <c>Window ▸ Faolline ▸ Graph Ecosystem Modules</c>.
    /// </summary>
    public class GraphEcosystemModuleSelector : EditorWindow
    {
        [Serializable]
        private class ModuleEntry
        {
            public string displayName;
            public string package;
            public bool required;
            public string[] dependsOn;
        }

        [Serializable]
        private class ModuleConfig
        {
            public string repository;
            public string branch;
            public string basePath;
            public List<ModuleEntry> modules;
        }

        private ModuleConfig _config;
        private readonly Dictionary<string, bool> _installed = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _desired = new Dictionary<string, bool>();
        private ListRequest _listRequest;
        private AddAndRemoveRequest _modifyRequest;
        private string _status = "Loading…";

        [MenuItem("Window/Faolline/Graph Ecosystem Modules")]
        public static void ShowWindow()
        {
            var win = GetWindow<GraphEcosystemModuleSelector>("Graph Ecosystem");
            win.minSize = new Vector2(420, 240);
        }

        private void OnEnable()
        {
            LoadConfig();
            RefreshInstalled();
        }

        // ── Config ──────────────────────────────────────────────────────────

        private void LoadConfig()
        {
            string json = null;
            foreach (var guid in AssetDatabase.FindAssets("GraphEcosystemModules"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("GraphEcosystemModules.json", StringComparison.Ordinal)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (asset != null) { json = asset.text; break; }
            }

            if (string.IsNullOrEmpty(json))
            {
                _status = "GraphEcosystemModules.json not found.";
                return;
            }

            _config = JsonUtility.FromJson<ModuleConfig>(json);
            _desired.Clear();
            if (_config?.modules != null)
                foreach (var m in _config.modules)
                    _desired[m.package] = m.required;
        }

        private void RefreshInstalled()
        {
            _status = "Checking installed packages…";
            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: false);
        }

        // ── Async pump ──────────────────────────────────────────────────────

        private void Update()
        {
            if (_listRequest != null && _listRequest.IsCompleted)
            {
                if (_listRequest.Status == StatusCode.Success)
                {
                    _installed.Clear();
                    foreach (var p in _listRequest.Result) _installed[p.name] = true;

                    if (_config?.modules != null)
                        foreach (var m in _config.modules)
                            _desired[m.package] = m.required || _installed.ContainsKey(m.package);

                    _status = "Ready.";
                }
                else
                {
                    _status = "Failed to list packages: " + _listRequest.Error?.message;
                }
                _listRequest = null;
                Repaint();
            }

            if (_modifyRequest != null && _modifyRequest.IsCompleted)
            {
                bool ok = _modifyRequest.Status == StatusCode.Success;
                _status = ok ? "Applied." : "Apply failed: " + _modifyRequest.Error?.message;
                _modifyRequest = null;
                if (ok) RefreshInstalled();
                Repaint();
            }
        }

        // ── UI ──────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Faolline Graph Ecosystem — Modules", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tick the packages to install. Dependencies are added automatically. Only the packages " +
                "listed here are offered — samples and verification packages are never pulled in.",
                MessageType.None);

            bool busy = _listRequest != null || _modifyRequest != null;

            if (_config?.modules == null)
            {
                EditorGUILayout.HelpBox(_status, MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(busy))
            {
                EditorGUILayout.BeginVertical("box");
                foreach (var m in _config.modules)
                {
                    EditorGUILayout.BeginHorizontal();

                    bool current = _desired.TryGetValue(m.package, out var d) && d;
                    using (new EditorGUI.DisabledScope(m.required))
                    {
                        bool next = EditorGUILayout.ToggleLeft($"{m.displayName}   ({m.package})", current);
                        if (next != current) _desired[m.package] = next;
                    }

                    GUILayout.FlexibleSpace();
                    string tag = m.required ? "required"
                        : _installed.ContainsKey(m.package) ? "installed" : "";
                    if (!string.IsNullOrEmpty(tag))
                        GUILayout.Label(tag, EditorStyles.miniLabel);

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Apply", GUILayout.Height(28)))
                    Apply();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        // ── Apply ───────────────────────────────────────────────────────────

        private void Apply()
        {
            var byPackage = _config.modules.ToDictionary(m => m.package, m => m);

            // Final desired set = toggled-on (or required) modules + their dependency closure.
            var final = new HashSet<string>();
            void AddClosure(string pkg)
            {
                if (final.Contains(pkg) || !byPackage.TryGetValue(pkg, out var mod)) return;
                final.Add(pkg);
                if (mod.dependsOn != null)
                    foreach (var dep in mod.dependsOn) AddClosure(dep);
            }
            foreach (var m in _config.modules)
                if (m.required || (_desired.TryGetValue(m.package, out var on) && on))
                    AddClosure(m.package);

            var toAdd = new List<string>();
            var toRemove = new List<string>();
            foreach (var m in _config.modules)
            {
                bool want = final.Contains(m.package);
                bool have = _installed.ContainsKey(m.package);
                if (want && !have) toAdd.Add(BuildGitIdentifier(m.package));
                else if (!want && have && !m.required) toRemove.Add(m.package);
            }

            if (toAdd.Count == 0 && toRemove.Count == 0)
            {
                _status = "Nothing to change.";
                return;
            }

            _status = $"Applying  (+{toAdd.Count} / -{toRemove.Count})…";
            _modifyRequest = Client.AddAndRemove(toAdd.ToArray(), toRemove.ToArray());
        }

        /// <summary>Git URL UPM identifier: repo + ?path=&lt;basePath&gt;/&lt;package&gt; (+ optional #branch).</summary>
        private string BuildGitIdentifier(string package)
        {
            var url = $"{_config.repository}?path={_config.basePath}/{package}";
            if (!string.IsNullOrEmpty(_config.branch)) url += $"#{_config.branch}";
            return url;
        }
    }
}
