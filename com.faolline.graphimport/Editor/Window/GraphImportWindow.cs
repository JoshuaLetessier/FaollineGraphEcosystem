using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Loads quest/flow mapping data and/or a dialogue interchange JSON, builds a combined
    /// <see cref="GenerationPlan"/>, and lets the user review/edit each entry's proposed path before
    /// committing through the one shared Plan/Apply/ConflictReport pipeline. A thin view over that
    /// pipeline — it introduces no data of its own.
    /// </summary>
    public sealed class GraphImportWindow : EditorWindow
    {
        string _mappingPath = "";
        readonly Dictionary<string, string> _tablePaths = new Dictionary<string, string>();

        string _dialoguesJsonPath = "";
        string _dialoguePathTemplate = "Assets/Graphs/Dialogues/{name}.asset";
        string _speakerFolder = "Assets/Generated/Speakers";

        readonly List<PlanEntry> _questFlowEntries = new List<PlanEntry>();
        readonly List<PlanEntry> _dialogueEntries = new List<PlanEntry>();
        GenerationPlan _plan;
        ConflictReport _report;
        Vector2 _scroll;

        /// <summary>
        /// Overridable for tests. When left null, Commit builds the real generators, wired to a
        /// <see cref="ProjectAssetResolver"/> built from the full combined plan (so a dialogue's
        /// sub-dialogue link — or a quest step's content ref — can resolve to another asset
        /// generated in this same run).
        /// </summary>
        public IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> Generators { get; set; }

        [MenuItem("Window/Faolline/Graph Import")]
        public static void Open() => GetWindow<GraphImportWindow>("Graph Import");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Quest / Flow data", EditorStyles.boldLabel);
            _mappingPath = EditorGUILayout.TextField("Mapping config (JSON)", _mappingPath);
            if (GUILayout.Button("Build quest/flow plan"))
                BuildQuestFlowPlan();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dialogues", EditorStyles.boldLabel);
            _dialoguesJsonPath = EditorGUILayout.TextField("Interchange JSON", _dialoguesJsonPath);
            _dialoguePathTemplate = EditorGUILayout.TextField("Path template", _dialoguePathTemplate);
            _speakerFolder = EditorGUILayout.TextField("Speaker folder", _speakerFolder);
            if (GUILayout.Button("Build dialogue plan"))
                BuildDialoguePlan();

            EditorGUILayout.Space();

            // Dialogues MUST be applied before quest/flow: a quest step's content ref can target a
            // dialogue (resolved via ProjectAssetResolver), but a dialogue never references a quest or
            // flow asset — the dependency only ever runs this one direction. PlanApplier applies in
            // list order, so a fixed quest-first order left same-run resolution to a not-yet-created
            // dialogue always failing (found via real-data dogfood on this exact combined-plan path).
            var allEntries = _dialogueEntries.Concat(_questFlowEntries).ToList();
            if (allEntries.Count == 0)
                return;

            _plan = new GenerationPlan(allEntries);
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

        void BuildQuestFlowPlan()
        {
            if (!File.Exists(_mappingPath))
            {
                Debug.LogError($"[GraphImport] Mapping file not found: {_mappingPath}");
                return;
            }

            var mapping = MappingConfig.LoadFromJson(File.ReadAllText(_mappingPath));
            var sourceTables = new Dictionary<string, SourceTable>();
            foreach (var table in mapping.Tables)
            {
                if (!_tablePaths.TryGetValue(table.SourceTableName, out var path) || !File.Exists(path))
                {
                    Debug.LogError($"[GraphImport] No source file configured for table '{table.SourceTableName}'.");
                    return;
                }

                IRowSource source = path.EndsWith(".json") ? new JsonRowSource() : new CsvRowSource();
                sourceTables[table.SourceTableName] = source.Read(path, table.SourceTableName);
            }

            mapping.Validate(sourceTables);

            var quests = new PivotBuilder(mapping, new IdOrNameReferenceResolver()).Build(sourceTables);
            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.QuestAsset] = "Assets/Graphs/{chapter}/Quests/{name}.asset",
                [PlanEntryKind.FlowAsset] = "Assets/Graphs/{chapter}/GameFlow/{name}.asset"
            });

            _questFlowEntries.Clear();
            _questFlowEntries.AddRange(new PlanBuilder(pathResolver).Build(quests).Entries);
        }

        void BuildDialoguePlan()
        {
            if (!File.Exists(_dialoguesJsonPath))
            {
                Debug.LogError($"[GraphImport] Dialogues JSON not found: {_dialoguesJsonPath}");
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
                Debug.LogWarning($"[GraphImport] {report.Conflicts.Count} conflict(s) — those entries will be skipped, never overwritten.");

            var generators = Generators ?? BuildDefaultGenerators();
            var result = PlanApplier.Apply(_plan, report, generators);
            Debug.Log($"[GraphImport] Created {result.Created.Count} asset(s).");
            foreach (var failure in result.Failures)
                Debug.LogError($"[GraphImport] Failed to generate '{failure.Entry.ProposedPath}': {failure.Exception.Message}");
        }

        IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> BuildDefaultGenerators()
        {
            var resolver = new ProjectAssetResolver(_plan, _speakerFolder);
            return new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.QuestAsset] = new QuestAssetGenerator(),
                [PlanEntryKind.FlowAsset] = new FlowAssetGenerator(resolver),
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
        }
    }
}
