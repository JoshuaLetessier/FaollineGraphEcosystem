using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Faolline.GraphLogging;


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
        MappingConfig _loadedMapping;

        string _dialoguesJsonPath = "";
        string _dialoguePathTemplate = "Assets/Graphs/Dialogues/{name}.asset";
        string _speakerFolder = "Assets/Generated/Speakers";

        readonly List<PlanEntry> _questFlowEntries = new List<PlanEntry>();
        readonly List<PlanEntry> _dialogueEntries = new List<PlanEntry>();
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _contentFieldsById = new Dictionary<string, IReadOnlyDictionary<string, string>>();
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
            if (GUILayout.Button("Load mapping"))
                LoadMapping();

            if (_loadedMapping != null)
            {
                EditorGUILayout.LabelField("Source file per table", EditorStyles.miniBoldLabel);
                foreach (var table in _loadedMapping.Tables)
                {
                    _tablePaths.TryGetValue(table.SourceTableName, out var current);
                    var updated = EditorGUILayout.TextField(table.SourceTableName, current ?? "");
                    _tablePaths[table.SourceTableName] = updated;
                }
            }

            using (new EditorGUI.DisabledScope(_loadedMapping == null))
            {
                if (GUILayout.Button("Build quest/flow plan"))
                    BuildQuestFlowPlan();
            }

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

        void LoadMapping()
        {
            if (!File.Exists(_mappingPath))
            {
                Logging.Error("GraphImport", $"[GraphImport] Mapping file not found: {_mappingPath}");
                return;
            }

            _loadedMapping = MappingConfig.LoadFromJson(File.ReadAllText(_mappingPath));

            // Drop any stale per-table path left over from a previously loaded mapping whose table
            // isn't part of this one, so the field list shown always matches the loaded mapping.
            var currentTableNames = new HashSet<string>(_loadedMapping.Tables.Select(t => t.SourceTableName));
            foreach (var stale in _tablePaths.Keys.Where(k => !currentTableNames.Contains(k)).ToList())
                _tablePaths.Remove(stale);
        }

        void BuildQuestFlowPlan()
        {
            if (_loadedMapping == null)
            {
                Logging.Error("GraphImport", "[GraphImport] Load a mapping first.");
                return;
            }

            var mapping = _loadedMapping;
            var sourceTables = new Dictionary<string, SourceTable>();
            foreach (var table in mapping.Tables)
            {
                if (!_tablePaths.TryGetValue(table.SourceTableName, out var path) || !File.Exists(path))
                {
                    Logging.Error("GraphImport", $"[GraphImport] No source file configured for table '{table.SourceTableName}'.");
                    return;
                }

                IRowSource source = path.EndsWith(".json") ? new JsonRowSource() : new CsvRowSource();
                sourceTables[table.SourceTableName] = source.Read(path, table.SourceTableName);
            }

            mapping.Validate(sourceTables);

            var pivotBuilder = new PivotBuilder(mapping, new IdOrNameReferenceResolver());
            var quests = pivotBuilder.Build(sourceTables);
            _contentFieldsById = pivotBuilder.BuildContentFields(sourceTables);
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
                Logging.Error("GraphImport", $"[GraphImport] Dialogues JSON not found: {_dialoguesJsonPath}");
                return;
            }

            var interchange = InterchangeDialogueSet.LoadFromJson(File.ReadAllText(_dialoguesJsonPath));
            var dialogues = new DialoguePivotBuilder().Build(interchange);
            var pathResolver = new TemplatePathResolver(new Dictionary<PlanEntryKind, string>
            {
                [PlanEntryKind.DialogueAsset] = _dialoguePathTemplate
            }, _contentFieldsById);

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
            var resolver = new ProjectAssetResolver(_plan, _speakerFolder, contentFieldsBySpeakerKey: _contentFieldsById);
            return new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.QuestAsset] = new QuestAssetGenerator(),
                [PlanEntryKind.FlowAsset] = new FlowAssetGenerator(resolver, _contentFieldsById),
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
        }
    }
}
