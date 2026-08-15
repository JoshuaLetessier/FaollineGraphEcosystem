using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Faolline.GraphLogging;


namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Loads a dialogue interchange JSON, builds a <see cref="GenerationPlan"/>, and lets the user
    /// review/edit each entry's proposed path before committing through the shared
    /// Plan/Apply/ConflictReport pipeline. A thin view over that pipeline — it introduces no data
    /// of its own.
    /// </summary>
    public sealed class GraphImportWindow : EditorWindow
    {
        string _dialoguesJsonPath = "";
        string _dialoguePathTemplate = "Assets/Graphs/Dialogues/{name}.asset";
        string _speakerFolder = "Assets/Generated/Speakers";

        readonly List<PlanEntry> _dialogueEntries = new List<PlanEntry>();
        GenerationPlan _plan;
        ConflictReport _report;
        Vector2 _scroll;

        /// <summary>
        /// Overridable for tests. When left null, Commit builds the real generators, wired to a
        /// <see cref="ProjectAssetResolver"/> built from the full plan (so a dialogue's sub-dialogue
        /// link can resolve to another asset generated in this same run).
        /// </summary>
        public IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> Generators { get; set; }

        [MenuItem("Window/Faolline/Graph Import")]
        public static void Open() => GetWindow<GraphImportWindow>("Graph Import");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Dialogues", EditorStyles.boldLabel);
            _dialoguesJsonPath = EditorGUILayout.TextField("Interchange JSON", _dialoguesJsonPath);
            _dialoguePathTemplate = EditorGUILayout.TextField("Path template", _dialoguePathTemplate);
            _speakerFolder = EditorGUILayout.TextField("Speaker folder", _speakerFolder);
            if (GUILayout.Button("Build dialogue plan"))
                BuildDialoguePlan();

            EditorGUILayout.Space();

            if (_dialogueEntries.Count == 0)
                return;

            _plan = new GenerationPlan(_dialogueEntries);
            _report = PlanConflictDetector.Detect(_plan);

            EditorGUILayout.LabelField($"{_plan.Entries.Count} asset(s) proposed, {_report.Conflicts.Count} conflict(s)");
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _plan.Entries)
            {
                var isConflicting = IsConflicting(entry);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.Kind.ToString(), GUILayout.Width(90));
                entry.ProposedPath = EditorGUILayout.TextField(entry.ProposedPath);
                if (isConflicting)
                    EditorGUILayout.LabelField("⚠ conflict — will be skipped", GUILayout.Width(180));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(_report.Conflicts.Count == _plan.Entries.Count))
            {
                if (GUILayout.Button("Commit"))
                    Commit();
            }
        }

        bool IsConflicting(PlanEntry entry)
        {
            foreach (var conflict in _report.Conflicts)
                if (conflict.PlanEntry.LogicalId == entry.LogicalId)
                    return true;
            return false;
        }

        void BuildDialoguePlan()
        {
            if (!File.Exists(_dialoguesJsonPath))
            {
                Logging.Error("GraphImport", $"[GraphImport] Dialogues JSON not found: {_dialoguesJsonPath}");
                return;
            }

            var interchange = InterchangeDialogueSet.LoadFromJson(File.ReadAllText(_dialoguesJsonPath));
            var dialogues = new DialoguePivotBuilder().Build(interchange);
            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = _dialoguePathTemplate
            });

            _dialogueEntries.Clear();
            _dialogueEntries.AddRange(new PlanBuilder(pathResolver).BuildDialogues(dialogues).Entries);
        }

        void Commit()
        {
            var report = PlanConflictDetector.Detect(_plan);
            if (!report.IsClean)
                Logging.Warning("GraphImport", $"[GraphImport] {report.Conflicts.Count} conflict(s) — those entries will be skipped, never overwritten.");

            var generators = Generators ?? BuildDefaultGenerators();
            var result = PlanApplier.Apply(_plan, report, generators);
            Logging.Info("GraphImport", $"[GraphImport] Created {result.Created.Count} asset(s).");
            foreach (var failure in result.Failures)
                Logging.Error("GraphImport", $"[GraphImport] Failed to generate '{failure.Entry.ProposedPath}': {failure.Exception.Message}");
        }

        IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> BuildDefaultGenerators()
        {
            var resolver = new ProjectAssetResolver(_plan, _speakerFolder);
            return new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
        }
    }
}
