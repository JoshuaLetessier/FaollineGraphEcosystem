using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Faolline.GraphCore;
using Faolline.GraphCore.Editor;
using Faolline.GraphLocalization;
using Faolline.GraphLocalization.Editor;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Editor window for dialogue graphs. Opens via <c>Faolline/Open Dialogue Graph Editor</c> or by
    /// double-clicking a <see cref="DialogueGraph"/> asset. Hosts a <see cref="DialoguePlayer"/> session
    /// so the author can Run, Choose, Continue, GoBack, and GoBackToCheckpoint from the toolbar.
    /// One window per asset (focus-or-create), so a dialogue and its sub-dialogue edit side by side.
    /// </summary>
    public class DialogueGraphEditorWindow : BaseGraphEditorWindow
    {
        private DialoguePlayer _player;
        private DialogueContext _context;
        private bool _hasSession;
        private ChoiceStep _waitingChoice;

        private DialogueNodeInspectorView _inspector;

        /// <summary>True once a playback session has started.</summary>
        public bool HasActiveSession => _hasSession;

        /// <summary>The choice step currently awaiting a selection, or null.</summary>
        public ChoiceStep WaitingChoice => _waitingChoice;

        /// <summary>True while paused at a choice awaiting a selection.</summary>
        public bool IsWaitingForChoice => _waitingChoice != null;

        /// <summary>The most recent step emitted by the session, or null.</summary>
        public DialogueStep CurrentStep => _player?.CurrentStep;

        /// <summary>The runner state of the active session (Idle when none).</summary>
        public RunnerState State => _player != null ? _player.State : RunnerState.Idle;

        /// <summary>The currently available (selectable) choice options. Empty when not at a choice.</summary>
        public IReadOnlyList<ChoiceOption> AvailableChoices
        {
            get
            {
                var list = new List<ChoiceOption>();
                if (_waitingChoice != null)
                    foreach (var o in _waitingChoice.Options)
                        if (o.Available) list.Add(o);
                return list;
            }
        }

        /// <summary>Test hook: loads a graph into the window without the asset-open flow.</summary>
        public void LoadGraphForTest(DialogueGraph graph) => LoadGraph(graph);

        /// <summary>Test hook: the graph currently loaded into this window.</summary>
        public BaseGraph LoadedGraphForTest => LoadedGraph;

        // ── Menu / asset opening ──────────────────────────────────────────────

        [MenuItem("Faolline/Open Dialogue Graph Editor")]
        public static void Open() => GetWindow<DialogueGraphEditorWindow>("Dialogue Graph Editor");

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            var asset = EditorUtility.InstanceIDToObject(instanceId) as DialogueGraph;
            if (asset == null) return false;

            foreach (var existing in Resources.FindObjectsOfTypeAll<DialogueGraphEditorWindow>())
            {
                if (existing.LoadedGraph == asset) { existing.Focus(); return true; }
            }

            var window = CreateWindow<DialogueGraphEditorWindow>();
            window.titleContent = new GUIContent(asset.name);
            window.LoadGraph(asset);
            return true;
        }

        // ── Factory methods ───────────────────────────────────────────────────

        protected override BaseGraphView CreateGraphView() => new DialogueGraphView();

        protected override BaseNodeInspectorView CreateNodeInspectorView()
        {
            _inspector = new DialogueNodeInspectorView();
            return _inspector;
        }

        protected override void OnGraphLoaded(BaseGraph graph)
        {
            _inspector?.SetGraph(graph);
            var view = GraphView as DialogueGraphView;
            _inspector?.SetGraphView(view);
            if (view != null && _inspector != null)
                view.OnEdgeSelected += _inspector.BindEdge;   // FR-021: edit a connection's condition
        }

        // ── Toolbar ───────────────────────────────────────────────────────────

        private string _locale = "en";
        private PopupField<string> _localeField;

        protected override void PopulateToolbar(Toolbar toolbar)
        {
            // Tooltips double as on-screen hints for the primary actions (accessibility, FR-011).
            toolbar.Add(new ToolbarButton(RunGraph)         { text = "Run",          tooltip = "Start playing this dialogue from its Start node." });
            toolbar.Add(new ToolbarButton(ShowChooseMenu)   { text = "Choose",       tooltip = "Pick one of the available options at a Choice node." });
            toolbar.Add(new ToolbarButton(Continue)         { text = "▶ Continue",   tooltip = "Advance past the current line." });
            toolbar.Add(new ToolbarButton(Back)             { text = "← GoBack",     tooltip = "Step back one node, restoring earlier state." });
            toolbar.Add(new ToolbarButton(BackToCheckpoint) { text = "⏮ Checkpoint", tooltip = "Jump back to the most recent checkpoint node." });
            toolbar.Add(new ToolbarButton(ValidateGraph)    { text = "✓ Validate",   tooltip = "Check this graph for structural issues (Start/End, dangling edges, empty choices…)." });

            // Locale selection for a Run: a dropdown of the project's configured languages — the Unity
            // Localization locales, or the CSV locale columns — instead of free text (FR-032/SC-004). Applied
            // on Run with no graph change. The list reloads on Save / ↻ Refresh (see OnRefresh).
            _localeField = new PopupField<string>
            {
                tooltip = "Active language for a Run. Sourced from your localization settings " +
                          "(Unity Localization locales, or the CSV locale columns). Reloads on Save / Refresh."
            };
            _localeField.style.minWidth = 56;
            _localeField.RegisterValueChangedCallback(e => _locale = string.IsNullOrEmpty(e.newValue) ? "en" : e.newValue);
            RefreshLocaleChoices();
            toolbar.Add(_localeField);
        }

        /// <summary>Reloads the toolbar's language list from the localization settings (Save / ↻ Refresh).</summary>
        protected override void OnRefresh() => RefreshLocaleChoices();

        private void RefreshLocaleChoices()
        {
            if (_localeField == null) return;
            var locales = new List<string>(LocalizationLocaleCatalog.AvailableLocales());
            _localeField.choices = locales;
            if (!locales.Contains(_locale)) _locale = locales.Count > 0 ? locales[0] : "en";
            _localeField.SetValueWithoutNotify(_locale);
        }

        // ── Public session API (also used by tests) ───────────────────────────

        /// <summary>Starts a playback session on the loaded dialogue graph.</summary>
        public void RunGraph()
        {
            var graph = LoadedGraph as DialogueGraph;
            if (graph == null)
            {
                Debug.LogError("[GraphDialogue] No DialogueGraph loaded. Open a dialogue asset first.");
                return;
            }
            if (string.IsNullOrEmpty(graph.EntryNodeId))
            {
                Debug.LogError("[GraphDialogue] Graph has no entry node. Add a Start node and save before running.");
                return;
            }

            _waitingChoice = null;
            _context = new DialogueContext();

            // Apply the toolbar locale for this Run (FR-032). The CSV provider tracks its own active
            // locale; for other providers the locale is set on the shared settings.
            LocalizationContext.Current.CurrentLocale = _locale;
            var provider = LocalizationContext.Current.Provider;
            if (provider is CsvLocalizationProvider csv) csv.SetLocale(_locale);

            _player = new DialoguePlayer(graph, _context, provider, BuildSpeakerLookup(),
                LocalizationContext.Current.StrictMode);

            _player.OnLine += step =>
                Debug.Log($"[GraphDialogue] Line [{step.SpeakerId}] {step.ResolvedText}");
            _player.OnChoices += step =>
            {
                _waitingChoice = step;
                Debug.Log($"[GraphDialogue] Choice at {step.NodeId} ({step.Options.Count} options)");
            };
            _player.OnEnded += step =>
            {
                _waitingChoice = null;
                Debug.Log($"[GraphDialogue] Ended: {step.EndReason}");
            };
            _player.OnStuck += () =>
            {
                _waitingChoice = null;
                Debug.LogWarning("[GraphDialogue] Stuck — no valid branch.");
            };

            _hasSession = true;
            _player.Start();
        }

        /// <summary>Selects a choice by id and resumes. No-op when not waiting at a choice.</summary>
        public void Choose(string choiceId)
        {
            if (!_hasSession || _player == null) { Debug.Log("[GraphDialogue] No active session — click Run first."); return; }
            _waitingChoice = null;
            _player.Choose(choiceId);
        }

        /// <summary>Advances past the current line.</summary>
        public void Continue()
        {
            if (!_hasSession || _player == null) { Debug.Log("[GraphDialogue] No active session — click Run first."); return; }
            if (IsWaitingForChoice) { Debug.Log("[GraphDialogue] Paused at a choice — use Choose."); return; }
            _player.Advance();
        }

        /// <summary>Steps back one entry.</summary>
        public void Back()
        {
            if (!_hasSession || _player == null) { Debug.Log("[GraphDialogue] No active session — click Run first."); return; }
            _waitingChoice = null;
            _player.Back();
        }

        /// <summary>Steps back to the most recent checkpoint.</summary>
        public void BackToCheckpoint()
        {
            if (!_hasSession || _player == null) { Debug.Log("[GraphDialogue] No active session — click Run first."); return; }
            _waitingChoice = null;
            _player.BackToCheckpoint();
        }

        /// <summary>Runs the structural validator on the loaded graph and logs a report to the console.</summary>
        public void ValidateGraph()
        {
            var graph = LoadedGraph;
            if (graph == null)
            {
                Debug.LogWarning("[GraphDialogue] No graph loaded to validate.");
                return;
            }
            GraphValidator.LogReport(graph.name, GraphValidator.Validate(graph));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void ShowChooseMenu()
        {
            if (!IsWaitingForChoice)
            {
                Debug.Log("[GraphDialogue] No active choice — click Run first.");
                return;
            }

            var menu = new GenericMenu();
            foreach (var opt in _waitingChoice.Options)
            {
                if (!opt.Available) continue;
                string id = opt.ChoiceId;
                string label = string.IsNullOrEmpty(opt.ResolvedLabel) ? id : opt.ResolvedLabel;
                menu.AddItem(new GUIContent(label), false, () => Choose(id));
            }
            menu.ShowAsContext();
        }

        private static System.Func<string, Speaker> BuildSpeakerLookup()
        {
            // Index Speaker assets by SpeakerId for display-name resolution during a Run.
            var byId = new Dictionary<string, Speaker>();
            foreach (var guid in AssetDatabase.FindAssets("t:Speaker"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sp = AssetDatabase.LoadAssetAtPath<Speaker>(path);
                if (sp != null && !string.IsNullOrEmpty(sp.SpeakerId) && !byId.ContainsKey(sp.SpeakerId))
                    byId[sp.SpeakerId] = sp;
            }
            return key => (key != null && byId.TryGetValue(key, out var s)) ? s : null;
        }
    }
}
