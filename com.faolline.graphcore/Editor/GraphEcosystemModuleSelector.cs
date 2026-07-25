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
    /// A row can also represent an external backend rather than an ecosystem package — a plain Unity
    /// registry package (<c>"registry": true</c>, e.g. the optional Unity Localization backend) or a
    /// package from a different git repo (<c>"gitUrl"</c> override, e.g. the external UnitySaveSystem
    /// backend) — those are prefixed "↳" and pulled in automatically via <c>dependsOn</c> once the
    /// bridge package that needs them is ticked.
    /// Menu: <c>Window ▸ Faolline ▸ Graph Ecosystem Modules</c>.
    /// </summary>
    public class GraphEcosystemModuleSelector : EditorWindow
    {
        [Serializable]
        private class ModuleEntry
        {
            public string displayName;
            public string package;
            public string version;
            public bool required;
            public string[] dependsOn;

            /// <summary>True for a plain registry package (e.g. Unity's own com.unity.* packages) —
            /// added by package name/version instead of a git URL built from <see cref="ModuleConfig.repository"/>.</summary>
            public bool registry;

            /// <summary>Explicit override for modules living in a different git repository than this
            /// ecosystem (e.g. the external UnitySaveSystem backend). Takes precedence over the default
            /// git URL built from <see cref="ModuleConfig.repository"/>/<see cref="ModuleConfig.basePath"/>.</summary>
            public string gitUrl;
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
        private readonly Dictionary<string, string> _installedVersions = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _installedPackageIds = new Dictionary<string, string>();
        private readonly Dictionary<string, bool> _desired = new Dictionary<string, bool>();
        private ListRequest _listRequest;
        private AddAndRemoveRequest _modifyRequest;
        private string _status = "Loading…";
        private int _updatesAvailable;

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
                    _installedVersions.Clear();
                    _installedPackageIds.Clear();
                    foreach (var p in _listRequest.Result)
                    {
                        _installed[p.name] = true;
                        _installedVersions[p.name] = p.version;
                        _installedPackageIds[p.name] = p.packageId;
                    }

                    _updatesAvailable = 0;
                    if (_config?.modules != null)
                    {
                        foreach (var m in _config.modules)
                        {
                            _desired[m.package] = m.required || _installed.ContainsKey(m.package);
                            if (IsUpdateAvailable(m)) _updatesAvailable++;
                        }
                    }

                    _status = _updatesAvailable > 0
                        ? $"Ready — {_updatesAvailable} update(s) available."
                        : "Ready — all packages up to date.";
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

            if (_config != null)
            {
                var gitRef = ResolveRef();
                var pinnedToCore = _installedPackageIds.ContainsKey("com.faolline.graphcore") &&
                                   !string.IsNullOrEmpty(gitRef) && gitRef != _config.branch;
                var refLabel = string.IsNullOrEmpty(gitRef) ? "(no ref)" : gitRef;
                EditorGUILayout.LabelField(
                    pinnedToCore
                        ? $"New/updated packages will be pinned to '{refLabel}' — matching the installed Graph Core."
                        : $"New/updated packages will be pulled from '{refLabel}' (this repo's configured default).",
                    EditorStyles.miniLabel);
            }

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

                    bool isInstalled = _installed.ContainsKey(m.package);
                    if (isInstalled && _installedVersions.TryGetValue(m.package, out var installedVer))
                    {
                        if (IsUpdateAvailable(m))
                        {
                            var prev = GUI.color;
                            GUI.color = new Color(1f, 0.75f, 0.2f);
                            GUILayout.Label($"{installedVer} → {m.version}", EditorStyles.miniLabel);
                            GUI.color = prev;
                        }
                        else
                        {
                            GUILayout.Label(installedVer, EditorStyles.miniLabel);
                        }
                    }

                    string tag = m.required ? "required"
                        : isInstalled ? "installed" : "";
                    if (!string.IsNullOrEmpty(tag))
                        GUILayout.Label(tag, EditorStyles.miniLabel);

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8);
            using (new EditorGUI.DisabledScope(busy))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Apply", GUILayout.Height(28)))
                    Apply();
                using (new EditorGUI.DisabledScope(_updatesAvailable == 0))
                {
                    if (GUILayout.Button($"Update All ({_updatesAvailable})", GUILayout.Height(28), GUILayout.Width(140)))
                        UpdateAll();
                }
                if (GUILayout.Button("Force Update All", GUILayout.Height(28), GUILayout.Width(140)))
                        ForceUpdateAll();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        // ── Version check ───────────────────────────────────────────────────

        private bool IsUpdateAvailable(ModuleEntry m)
        {
            if (string.IsNullOrEmpty(m.version)) return false;
            if (!_installedVersions.TryGetValue(m.package, out var installed)) return false;
            return CompareVersions(installed, m.version) < 0;
        }

        private static int CompareVersions(string a, string b)
        {
            var pa = ParseVersion(a);
            var pb = ParseVersion(b);
            for (int i = 0; i < 3; i++)
            {
                if (pa[i] < pb[i]) return -1;
                if (pa[i] > pb[i]) return 1;
            }
            return 0;
        }

        private static int[] ParseVersion(string v)
        {
            var result = new int[3];
            if (string.IsNullOrEmpty(v)) return result;
            var parts = v.Split('.');
            for (int i = 0; i < Mathf.Min(parts.Length, 3); i++)
                int.TryParse(parts[i], out result[i]);
            return result;
        }

        // ── Update all ─────────────────────────────────────────────────────

        private void UpdateAll()
        {
            if (_config?.modules == null) return;
            var toAdd = new List<string>();
            foreach (var m in _config.modules)
                if (IsUpdateAvailable(m))
                    toAdd.Add(BuildIdentifier(m));

            if (toAdd.Count == 0) { _status = "Nothing to update."; return; }
            _status = $"Updating {toAdd.Count} package(s)…";
            _modifyRequest = Client.AddAndRemove(toAdd.ToArray(), new string[0]);
        }

        private void ForceUpdateAll()
        {
            if (_config?.modules == null) return;
            var toAdd = new List<string>();
            foreach (var m in _config.modules)
                toAdd.Add(BuildIdentifier(m));

            _status = $"Force-updating {toAdd.Count} package(s)…";
            _modifyRequest = Client.AddAndRemove(toAdd.ToArray(), new string[0]);
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
                if (want && !have) toAdd.Add(BuildIdentifier(m));
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

        /// <summary>UPM identifier for a module: a bare package name for <see cref="ModuleEntry.registry"/>
        /// packages (resolved via the registry, e.g. com.unity.*), an explicit <see cref="ModuleEntry.gitUrl"/>
        /// override for packages living in a different repo (e.g. UnitySaveSystem), or otherwise the default
        /// git URL built from this ecosystem's repo + basePath (+ <see cref="ResolveRef"/>).</summary>
        private string BuildIdentifier(ModuleEntry m)
        {
            if (m.registry) return m.package;
            if (!string.IsNullOrEmpty(m.gitUrl)) return m.gitUrl;
            var url = $"{_config.repository}?path={_config.basePath}/{m.package}";
            var gitRef = ResolveRef();
            if (!string.IsNullOrEmpty(gitRef)) url += $"#{gitRef}";
            return url;
        }

        /// <summary>
        /// The git ref every module this window installs should be pinned to. Mirrors whatever
        /// <c>com.faolline.graphcore</c> is <em>actually</em> resolved at (read back from its own
        /// <see cref="UnityEditor.PackageManager.PackageInfo.packageId"/>, which carries the resolved
        /// <c>#ref</c> — a tag, branch, or commit — for a git-sourced package) rather than the fixed
        /// <see cref="ModuleConfig.branch"/> from the config. A project pinned to a tag (see
        /// <c>INSTALL.md</c>'s tagged-link guidance) would otherwise still pull every OTHER module from
        /// <c>#master</c>, defeating the pin. Falls back to <see cref="ModuleConfig.branch"/> only when
        /// graphcore isn't installed yet (first bootstrap) or wasn't installed from git at all (no <c>#</c>
        /// in its <c>packageId</c> — e.g. a local/embedded/tarball install).
        /// </summary>
        private string ResolveRef()
        {
            if (_installedPackageIds.TryGetValue("com.faolline.graphcore", out var packageId) &&
                !string.IsNullOrEmpty(packageId))
            {
                var hashIndex = packageId.LastIndexOf('#');
                if (hashIndex >= 0 && hashIndex < packageId.Length - 1)
                    return packageId.Substring(hashIndex + 1);
            }
            return _config?.branch;
        }
    }
}
