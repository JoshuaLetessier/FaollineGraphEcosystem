using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        string _speakerTablesCsvPath = "";

        readonly List<PlanEntry> _dialogueEntries = new List<PlanEntry>();
        IReadOnlyList<PivotDialogue> _dialogues;
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
            _speakerTablesCsvPath = EditorGUILayout.TextField(new GUIContent("Speaker tables CSV (optional)",
                "SpeakerKey,Table mapping: created speakers take their mapped localization group, and existing " +
                "speakers referenced by the dialogues are realigned with it on Commit."), _speakerTablesCsvPath);
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

            _dialogues = dialogues;
            _dialogueEntries.Clear();
            _dialogueEntries.AddRange(new PlanBuilder(pathResolver).BuildDialogues(dialogues).Entries);
        }

        void Commit()
        {
            // Read and validate the speaker tables mapping before anything is written.
            SpeakerGroupMapping speakerGroups = null;
            if (!string.IsNullOrWhiteSpace(_speakerTablesCsvPath))
            {
                if (!File.Exists(_speakerTablesCsvPath))
                {
                    Logging.Error("GraphImport", $"[GraphImport] Speaker tables CSV not found: {_speakerTablesCsvPath}. Nothing was written.");
                    return;
                }
                try { speakerGroups = SpeakerGroupMapping.Parse(File.ReadAllText(_speakerTablesCsvPath)); }
                catch (SpeakerGroupMappingException ex)
                {
                    Logging.Error("GraphImport", $"[GraphImport] {_speakerTablesCsvPath}: {ex.Message} Nothing was written.");
                    return;
                }
            }

            var report = PlanConflictDetector.Detect(_plan);
            if (!report.IsClean)
                Logging.Warning("GraphImport", $"[GraphImport] {report.Conflicts.Count} conflict(s) — those entries will be skipped, never overwritten.");

            var generators = Generators ?? BuildDefaultGenerators(speakerGroups);
            var result = PlanApplier.Apply(_plan, report, generators);
            Logging.Info("GraphImport", $"[GraphImport] Created {result.Created.Count} asset(s).");
            foreach (var failure in result.Failures)
                Logging.Error("GraphImport", $"[GraphImport] Failed to generate '{failure.Entry.ProposedPath}': {failure.Exception.Message}");

            // Every speaker the dialogues reference — including in dialogues that collided and were skipped.
            if (speakerGroups != null && _dialogues != null)
            {
                var referenced = _dialogues.SelectMany(d => d.Nodes.Values).OfType<PivotLine>().Select(l => l.SpeakerKey);
                foreach (var change in SpeakerGroupApplier.Apply(referenced, speakerGroups))
                    Logging.Info("GraphImport", $"[GraphImport] Speaker '{change.SpeakerKey}' group '{change.OldGroup}' → '{change.NewGroup}' ({change.AssetPath})");
            }
        }

        IReadOnlyDictionary<PlanEntryKind, IAssetGenerator> BuildDefaultGenerators(SpeakerGroupMapping speakerGroups)
        {
            var resolver = new ProjectAssetResolver(_plan, _speakerFolder, speakerGroups);
            return new Dictionary<PlanEntryKind, IAssetGenerator>
            {
                [PlanEntryKind.DialogueAsset] = new DialogueAssetGenerator(resolver)
            };
        }
    }
}
