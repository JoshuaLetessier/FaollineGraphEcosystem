using System.IO;
using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphLocalization;
// Bool* now live in both Faolline.GraphCore (canonical) and Faolline.GraphDialogue (back-compat subclass) — pin
// to the GraphDialogue ones the sample builds.
using BoolCondition = Faolline.GraphDialogue.BoolCondition;
using SetBoolAction = Faolline.GraphDialogue.SetBoolAction;

namespace Faolline.GraphDialogue.Editor
{
    /// <summary>
    /// Editor menu utility that programmatically builds a sample dialogue: a parent
    /// <see cref="DialogueGraph"/> with a sub-dialogue, two speakers, a gated choice, inline
    /// conditions/actions, a checkpoint, typed parameters, and a 2-locale CSV table — the same shape
    /// the EditMode tests cover. Menu: <c>Faolline/GraphDialogue/Generate Sample Dialogue</c>.
    /// </summary>
    public static class DialogueSampleBuilder
    {
        private const string Folder = "Assets/GraphDialogueSamples";

        [MenuItem("Faolline/GraphDialogue/Generate Sample Dialogue")]
        public static void GenerateSample()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "GraphDialogueSamples");

            // ── Localization CSV (2 locales) ───────────────────────────────────────────
            // Keys are derived from node/choice/speaker identity (see DialogueLocalizationKeys); the
            // sample uses deterministic Ids so this static CSV matches the generated keys.
            var csv =
                "Key,en,fr\n" +
                "line_intro,Welcome traveller.,Bienvenue voyageur.\n" +
                "choice_ask,Ask about the town,Se renseigner sur la ville\n" +
                "choice_leave,Leave,Partir\n" +
                "line_town,It is a quiet place.,C'est un endroit paisible.\n" +
                "speaker_npc_mayor,Mayor,Maire\n";
            File.WriteAllText($"{Folder}/SampleDialogue_Strings.csv", csv);

            // ── Speaker ────────────────────────────────────────────────────────────────
            var mayor = ScriptableObject.CreateInstance<Speaker>();
            mayor.SpeakerId = "npc_mayor";
            mayor.DisplayNameFallback = "Mayor";
            mayor.AddExpression("neutral");   // demonstrates the node Expression dropdown
            mayor.AddExpression("happy");
            AssetDatabase.CreateAsset(mayor, $"{Folder}/SampleSpeaker_Mayor.asset");

            // ── Child sub-dialogue ──────────────────────────────────────────────────────
            var child = ScriptableObject.CreateInstance<DialogueGraph>();
            var cStart = new StartNodeData { Id = Guid(), NodeType = StartNodeData.NodeTypeId, Position = new Vector2(0, 0) };
            var cLine  = new DialogueLineNodeData { Id = "town", NodeType = DialogueLineNodeData.NodeTypeId, Title = "It is a quiet place.", SpeakerKey = "npc_mayor", Position = new Vector2(240, 0) };
            var cEnd   = new EndNodeData { Id = Guid(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed, Position = new Vector2(480, 0) };
            child.AddNode(cStart); child.AddNode(cLine); child.AddNode(cEnd);
            child.AddSpeaker(mayor);
            child.EntryNodeId = cStart.Id;
            child.AddEdge(new BaseEdgeData { Id = Guid(), FromNodeId = cStart.Id, ToNodeId = cLine.Id, PortName = "out" });
            child.AddEdge(new BaseEdgeData { Id = Guid(), FromNodeId = cLine.Id,  ToNodeId = cEnd.Id,  PortName = "out" });
            AssetDatabase.CreateAsset(child, $"{Folder}/SampleSubDialogue.asset");

            // ── Inline condition / action as sub-assets on the child (portable) ──────────
            var setVisited = ScriptableObject.CreateInstance<SetBoolAction>();
            setVisited.ParameterKey = DialogueContextKeys.Flag; setVisited.Value = true; setVisited.name = "SetVisited";
            AssetDatabase.AddObjectToAsset(setVisited, child);

            var visitedTrue = ScriptableObject.CreateInstance<BoolCondition>();
            visitedTrue.ParameterKey = DialogueContextKeys.Flag; visitedTrue.ExpectedValue = true; visitedTrue.name = "VisitedTrue";
            AssetDatabase.AddObjectToAsset(visitedTrue, child);

            // ── Parent dialogue ──────────────────────────────────────────────────────────
            var graph = ScriptableObject.CreateInstance<DialogueGraph>();
            graph.AddSpeaker(mayor);
            graph.AddParameter(ParameterData.Bool(DialogueContextKeys.Flag, false));

            var start  = new StartNodeData { Id = Guid(), NodeType = StartNodeData.NodeTypeId, Position = new Vector2(0, 0) };
            var intro  = new DialogueLineNodeData { Id = "intro", NodeType = DialogueLineNodeData.NodeTypeId, Title = "Welcome traveller.", SpeakerKey = "npc_mayor", ExpressionKey = "happy", Position = new Vector2(240, 0) };
            intro.IsCheckpoint = true;
            intro.OnEnterActions.Add(setVisited);
            var choice = new ChoiceNodeData { Id = Guid(), NodeType = ChoiceNodeData.NodeTypeId, Position = new Vector2(480, 0) };
            var sub    = new SubGraphNodeData { Id = Guid(), NodeType = SubGraphNodeData.NodeTypeId, TargetGraph = child, InheritParentContext = true, Position = new Vector2(720, 0) };
            var end    = new EndNodeData { Id = Guid(), NodeType = EndNodeData.NodeTypeId, EndReason = EndReason.Completed, Position = new Vector2(960, 0) };

            graph.AddNode(start); graph.AddNode(intro); graph.AddNode(choice); graph.AddNode(sub); graph.AddNode(end);
            graph.EntryNodeId = start.Id;

            choice.Choices.Add(new DialogueChoice { Id = "ask", Title = "Ask about the town" });
            choice.Choices.Add(new DialogueChoice { Id = "leave", Title = "Leave" });

            graph.AddEdge(new BaseEdgeData { Id = Guid(), FromNodeId = start.Id,  ToNodeId = intro.Id,  PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = Guid(), FromNodeId = intro.Id,  ToNodeId = choice.Id, PortName = "out" });
            graph.AddEdge(new BaseEdgeData { Id = Guid(), FromNodeId = choice.Id, ToNodeId = sub.Id,    PortName = choice.Choices[0].Id });
            graph.AddEdge(new BaseEdgeData { Id = Guid(), FromNodeId = choice.Id, ToNodeId = end.Id,    PortName = choice.Choices[1].Id });
            graph.AddEdge(new BaseEdgeData { Id = Guid(), FromNodeId = sub.Id,    ToNodeId = end.Id,    PortName = "out" });

            AssetDatabase.CreateAsset(graph, $"{Folder}/SampleDialogue.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = graph;
            EditorGUIUtility.PingObject(graph);
            Debug.Log($"[GraphDialogue] Sample dialogue created at {Folder}/SampleDialogue.asset");

            // Auto-load the generated CSV into the active LocalizationContext so the editor session
            // resolves keys immediately without any manual setup.
            AutoLoadSampleCsv(csv);
        }

        private static void AutoLoadSampleCsv(string csvText)
        {
            // Only override the ambient context in CSV mode. In UnityLocalization mode the real provider
            // (manifest-backed, set up by Build All Tables) must win, so we don't clobber it here.
            var settings = LocalizationSettingsLoader.Load();
            if (settings != null && settings.Mode == LocalizationMode.UnityLocalization)
            {
                Debug.Log("[GraphDialogue] UnityLocalization mode: run Faolline ▸ Localization ▸ Build All Tables " +
                    "to generate the String Tables + manifest for the sample.");
                return;
            }

            var provider = new CsvLocalizationProvider(csvText, "en");
            LocalizationContext.Current = new LocalizationSettings(provider, "en");
            Debug.Log("[GraphDialogue] Sample CSV auto-loaded into LocalizationContext (en). " +
                "Switch locale via LocalizationContext.Current.CurrentLocale = \"fr\" to test French.");
        }

        private static string Guid() => System.Guid.NewGuid().ToString("D");
    }
}
