using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Faolline.GraphImport.Editor
{
    /// <summary>
    /// Loads a mapping + source tables, builds a <see cref="GenerationPlan"/>, and lets the user
    /// review/edit each entry's proposed path before committing. A thin view over
    /// GenerationPlan/ConflictReport — it introduces no data of its own.
    /// </summary>
    public sealed class GraphImportWindow : EditorWindow
    {
        string _mappingPath = "";
        readonly Dictionary<string, string> _tablePaths = new Dictionary<string, string>();
        GenerationPlan _plan;
        ConflictReport _report;
        Vector2 _scroll;

        /// <summary>
        /// Defaults to the real graphquest/graphgameflow generators; overridable (e.g. by a test, or to
        /// supply a content resolver for <see cref="FlowAssetGenerator"/>).
        /// </summary>
        public IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> Generators { get; set; } = new Dictionary<PlanEntryKind, IAssetGenerator>
        {
            [PlanEntryKind.QuestAsset] = new QuestAssetGenerator(),
            [PlanEntryKind.FlowAsset] = new FlowAssetGenerator()
        };

        [MenuItem("Window/Faolline/Graph Import")]
        public static void Open() => GetWindow<GraphImportWindow>("Graph Import");

        void OnGUI()
        {
            _mappingPath = EditorGUILayout.TextField("Mapping config (JSON)", _mappingPath);

            if (GUILayout.Button("Build plan"))
                BuildPlan();

            if (_plan == null)
                return;

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

        void BuildPlan()
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
            _plan = new PlanBuilder(pathResolver).Build(quests);
        }

        void Commit()
        {
            var report = PlanConflictDetector.Detect(_plan);
            if (!report.IsClean)
                Debug.LogWarning($"[GraphImport] {report.Conflicts.Count} conflict(s) — those entries will be skipped, never overwritten.");

            var created = PlanApplier.Apply(_plan, report, Generators);
            Debug.Log($"[GraphImport] Created {created.Count} asset(s).");
        }
    }
}
