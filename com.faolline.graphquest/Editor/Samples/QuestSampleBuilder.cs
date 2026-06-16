using UnityEditor;
using UnityEngine;
using Faolline.GraphCore;
using Faolline.GraphStandard;

namespace Faolline.GraphQuest.Editor
{
    /// <summary>
    /// Generates a ready-made <see cref="QuestGraph"/> sample asset — "The Keep Escape" — so a newcomer can open it
    /// in the Quest Graph editor and see objectives, prerequisite edges, conditions, and rewards already wired.
    /// A chain (find clue → open door → escape) plus an optional side objective, with completion driven by
    /// graphstandard <c>BoolCondition</c>s and rewards as <c>SetBoolAction</c>s (all as sub-assets, so the sample is
    /// self-contained). Menu: <c>Faolline ▸ GraphQuest ▸ Create Sample Quest</c>.
    /// </summary>
    public static class QuestSampleBuilder
    {
        private const string PackageFolder = "Assets/FaollineGraphEcosystem/com.faolline.graphquest";
        private const string Folder        = PackageFolder + "/Samples";

        [MenuItem("Faolline/GraphQuest/Create Sample Quest")]
        public static void CreateSampleMenu() => CreateSample();

        /// <summary>Builds and saves the sample quest asset; returns the created graph.</summary>
        public static QuestGraph CreateSample()
        {
            EnsureFolder();
            var path = AssetDatabase.GenerateUniqueAssetPath(Folder + "/SampleQuest.asset");

            var g = ScriptableObject.CreateInstance<QuestGraph>();
            AssetDatabase.CreateAsset(g, path);
            g.QuestId = "keep_escape";
            g.DisplayName = "The Keep Escape";
            g.Description = "Find a way out of Aldric's keep.";
            g.CompletionReward = Reward(g, "Reward_QuestComplete", "quest_complete");

            // find clue → open door → escape ; gather supplies is an optional side objective.
            var find = Objective(g, "find_clue", "Find the clue", "Search Aldric's study for a way out.",
                Bool(g, "Cond_FoundClue", "found_clue"), new Vector2(0, 0));

            var open = Objective(g, "open_door", "Open the door", "Use what you found to unlock the cell.",
                Bool(g, "Cond_DoorOpen", "door_open"), new Vector2(260, -90));

            var supplies = Objective(g, "gather_supplies", "Gather supplies", "Optional: grab a torch and rations.",
                Bool(g, "Cond_HasSupplies", "has_supplies"), new Vector2(260, 110));
            supplies.Required = false;
            supplies.Reward = Reward(g, "Reward_Supplies", "supplies_bonus");

            var escape = Objective(g, "escape", "Escape the keep", "Slip past the gate to freedom.",
                Bool(g, "Cond_Escaped", "escaped"), new Vector2(520, -90));
            escape.Reward = Reward(g, "Reward_Freedom", "freedom");

            g.AddNode(find); g.AddNode(open); g.AddNode(supplies); g.AddNode(escape);
            // Edge From→To means "To requires From".
            g.AddEdge(Edge(find.Id, open.Id));
            g.AddEdge(Edge(open.Id, escape.Id));

            EditorUtility.SetDirty(g);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = g;
            EditorGUIUtility.PingObject(g);
            Debug.Log($"[GraphQuest] Sample created: {path}. Double-click it to open the Quest Graph editor — " +
                      "set the bool params (found_clue, door_open, escaped…) on a context and the objectives derive.");
            return g;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static ObjectiveNodeData Objective(Object owner, string id, string title, string desc,
            BaseCondition completion, Vector2 pos)
            => new ObjectiveNodeData
            {
                Id = id,
                NodeType = ObjectiveNodeData.NodeTypeId,
                Title = title,
                Description = desc,
                CompletionCondition = completion,
                Position = pos
            };

        private static BaseCondition Bool(Object owner, string assetName, string paramKey)
        {
            var c = Sub<BoolCondition>(owner, assetName);
            c.ParameterKey = paramKey;
            c.ExpectedValue = true;
            return c;
        }

        private static BaseAction Reward(Object owner, string assetName, string paramKey)
        {
            var a = Sub<SetBoolAction>(owner, assetName);
            a.ParameterKey = paramKey;
            a.Value = true;
            return a;
        }

        private static BaseEdgeData Edge(string from, string to)
            => new BaseEdgeData { Id = System.Guid.NewGuid().ToString("D"), FromNodeId = from, ToNodeId = to, PortName = "unlocks" };

        private static T Sub<T>(Object owner, string name) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            obj.name = name;
            AssetDatabase.AddObjectToAsset(obj, owner);
            return obj;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(PackageFolder, "Samples");
        }
    }
}
