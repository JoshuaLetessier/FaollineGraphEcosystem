using System;
using System.Collections.Generic;
using UnityEngine;
using Faolline.GraphCore;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// Fluent, code-first authoring for a <see cref="QuestGraph"/>. Declare objectives, their completion/fail
    /// conditions, prerequisites (a single id for a chain, several for a DAG join), optional flags, and reward
    /// hooks, then <see cref="Build"/>. <see cref="Build"/> rejects an empty quest, an unknown prerequisite, and a
    /// cyclic prerequisite topology (with a <c>[GraphQuest]</c> diagnostic) before returning a graph.
    /// </summary>
    public sealed class QuestBuilder
    {
        private readonly string _questId;
        private string _displayName = string.Empty;
        private string _description = string.Empty;
        private BaseCondition _unlock;
        private BaseAction _questReward;
        private readonly List<ObjectiveSpec> _objectives = new List<ObjectiveSpec>();

        private QuestBuilder(string questId) => _questId = questId ?? string.Empty;

        /// <summary>Starts a quest with a stable logical id (scopes the quest's context collections).</summary>
        public static QuestBuilder Create(string questId) => new QuestBuilder(questId);

        /// <summary>Sets the quest's display title for a journal UI (falls back to the id when unset).</summary>
        public QuestBuilder Named(string displayName) { _displayName = displayName ?? string.Empty; return this; }

        /// <summary>Sets the quest's longer description for a journal UI.</summary>
        public QuestBuilder Describe(string description) { _description = description ?? string.Empty; return this; }

        /// <summary>Sets the quest-level unlock condition (the whole quest stays Locked until it holds).</summary>
        public QuestBuilder UnlockWhen(BaseCondition condition) { _unlock = condition; return this; }

        /// <summary>Declares an objective and returns its sub-builder.</summary>
        public ObjectiveBuilder AddObjective(string objectiveId)
        {
            var spec = new ObjectiveSpec(objectiveId);
            _objectives.Add(spec);
            return new ObjectiveBuilder(this, spec);
        }

        /// <summary>Sets the reward fired once when the quest completes.</summary>
        public QuestBuilder RewardQuestWith(BaseAction reward) { _questReward = reward; return this; }

        /// <summary>
        /// Validates and constructs the <see cref="QuestGraph"/>: objectives become <see cref="ObjectiveNodeData"/>
        /// nodes, prerequisites become <c>From→To</c> edges. Throws an <see cref="InvalidOperationException"/> with
        /// a <c>[GraphQuest]</c> message on an empty quest, a duplicate/unknown objective id, or a cycle.
        /// </summary>
        public QuestGraph Build()
        {
            if (_objectives.Count == 0)
                throw new InvalidOperationException($"[GraphQuest] Quest '{_questId}' has no objectives.");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in _objectives)
            {
                if (string.IsNullOrEmpty(o.Id))
                    throw new InvalidOperationException($"[GraphQuest] Quest '{_questId}' has an objective with an empty id.");
                if (!ids.Add(o.Id))
                    throw new InvalidOperationException($"[GraphQuest] Quest '{_questId}' has a duplicate objective id '{o.Id}'.");
            }
            foreach (var o in _objectives)
                foreach (var p in o.Requires)
                    if (!ids.Contains(p))
                        throw new InvalidOperationException(
                            $"[GraphQuest] Quest '{_questId}' objective '{o.Id}' requires unknown objective '{p}'.");

            DetectCycle();

            var quest = ScriptableObject.CreateInstance<QuestGraph>();
            quest.name = _questId;
            quest.QuestId = _questId;
            quest.DisplayName = _displayName;
            quest.Description = _description;
            quest.UnlockCondition = _unlock;
            quest.CompletionReward = _questReward;
            quest.CompletionRule = QuestCompletionRule.AllRequired;

            foreach (var o in _objectives)
            {
                quest.AddNode(new ObjectiveNodeData
                {
                    Id = o.Id,
                    NodeType = ObjectiveNodeData.NodeTypeId,
                    Title = o.Title,
                    Description = o.Description,
                    CompletionCondition = o.Completion,
                    FailCondition = o.Fail,
                    Required = o.Required,
                    Reward = o.Reward,
                    RequiredPrerequisiteCount = o.RequiredPrerequisiteCount
                });
            }

            // An objective requires its prerequisite, so the edge runs prerequisite → objective.
            foreach (var o in _objectives)
                foreach (var p in o.Requires)
                    quest.AddEdge(new BaseEdgeData
                    {
                        Id = Guid.NewGuid().ToString("D"),
                        FromNodeId = p,
                        ToNodeId = o.Id,
                        PortName = "prereq"
                    });

            return quest;
        }

        // DFS over the "requires" relation; throws naming the first cycle found.
        private void DetectCycle()
        {
            var deps = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var o in _objectives) deps[o.Id] = o.Requires;

            var state = new Dictionary<string, int>(StringComparer.Ordinal); // 0 unvisited, 1 visiting, 2 done
            var path = new List<string>();

            foreach (var o in _objectives)
                if (!state.ContainsKey(o.Id) && Visit(o.Id, deps, state, path, out var cycle))
                    throw new InvalidOperationException(
                        $"[GraphQuest] Quest '{_questId}' has a cyclic prerequisite: {cycle}.");
        }

        private static bool Visit(string id, Dictionary<string, List<string>> deps,
            Dictionary<string, int> state, List<string> path, out string cycle)
        {
            state[id] = 1;
            path.Add(id);
            if (deps.TryGetValue(id, out var reqs))
            {
                foreach (var next in reqs)
                {
                    state.TryGetValue(next, out var s);
                    if (s == 1)
                    {
                        int from = path.IndexOf(next);
                        var loop = path.GetRange(from, path.Count - from);
                        loop.Add(next);
                        cycle = string.Join(" → ", loop);
                        return true;
                    }
                    if (s == 0 && Visit(next, deps, state, path, out cycle))
                        return true;
                }
            }
            path.RemoveAt(path.Count - 1);
            state[id] = 2;
            cycle = null;
            return false;
        }

        /// <summary>Mutable authoring record for one objective; consumed by <see cref="Build"/>.</summary>
        internal sealed class ObjectiveSpec
        {
            public readonly string Id;
            public string Title = string.Empty;
            public string Description = string.Empty;
            public BaseCondition Completion;
            public BaseCondition Fail;
            public bool Required = true;
            public BaseAction Reward;
            public readonly List<string> Requires = new List<string>();
            public int RequiredPrerequisiteCount = -1; // -1 = all (AND); k = k-of-N
            public ObjectiveSpec(string id) => Id = id;
        }
    }
}
