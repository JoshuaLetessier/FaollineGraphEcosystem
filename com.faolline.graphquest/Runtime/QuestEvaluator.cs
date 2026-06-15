using System;
using System.Collections.Generic;
using Faolline.GraphCore;
using Faolline.GraphStandard;
using Faolline.GraphLocalization;

namespace Faolline.GraphQuest
{
    /// <summary>
    /// Derives a <see cref="QuestGraph"/>'s objective and quest <see cref="QuestState"/>s from a shared
    /// <see cref="BaseContext"/>. Wraps graphstandard's <see cref="ReactiveEvaluator"/> for prerequisite gating
    /// (Locked/Active/Completed) and overlays the quest domain: completion is driven by each objective's
    /// <see cref="ObjectiveNodeData.CompletionCondition"/>; an optional <see cref="ObjectiveNodeData.FailCondition"/>
    /// gives the fourth <see cref="QuestState.Failed"/> state (fail precedes complete); rewards fire exactly once;
    /// the quest aggregates per <see cref="QuestCompletionRule"/>.
    /// <para>
    /// Call <see cref="Evaluate"/> after mutating the context (the same contract as
    /// <see cref="ReactiveEvaluator.MarkCompleted"/>). All state lives in context collections, so it persists and
    /// restores through a graphsave context snapshot, and the context may be a host's (e.g. a GameFlowContext).
    /// </para>
    /// </summary>
    public sealed class QuestEvaluator
    {
        private readonly QuestGraph _quest;
        private readonly BaseContext _context;
        private readonly ReactiveEvaluator _reactive;
        private readonly string _questId;
        private readonly string _completedKey;
        private readonly string _failedKey;
        private readonly string _rewardedKey;

        private readonly Dictionary<string, QuestState> _lastObjectiveStates = new Dictionary<string, QuestState>(StringComparer.Ordinal);
        private QuestState _lastQuestState;
        private bool _questStateKnown;
        private float _lastNow;
        private ILocalizationProvider _localization;

        /// <summary>Raised when an objective changes state: <c>(objectiveId, newState)</c>.</summary>
        public event Action<string, QuestState> OnObjectiveStateChanged;

        /// <summary>Raised when the aggregated quest state changes.</summary>
        public event Action<QuestState> OnQuestStateChanged;

        /// <summary>Raised when a reward fires: the objective id, or the quest id for the quest completion reward.</summary>
        public event Action<string> OnRewardFired;

        /// <summary>Builds an evaluator over <paramref name="quest"/> against <paramref name="context"/> (may be a host's).</summary>
        public QuestEvaluator(QuestGraph quest, BaseContext context)
        {
            _quest = quest;
            _context = context;
            _questId = quest != null ? quest.QuestId : string.Empty;
            _completedKey = QuestContextKeys.CompletedSet(_questId);
            _failedKey = QuestContextKeys.FailedSet(_questId);
            _rewardedKey = QuestContextKeys.RewardedSet(_questId);

            // Surface each objective's k-of-N prerequisite gate to the engine (unlisted ⇒ all-of-N / AND).
            Dictionary<string, int> requiredCounts = null;
            if (quest != null)
                foreach (var node in quest.Nodes)
                    if (node is ObjectiveNodeData obj && !string.IsNullOrEmpty(obj.Id) && obj.RequiredPrerequisiteCount >= 0)
                        (requiredCounts ??= new Dictionary<string, int>(StringComparer.Ordinal))[obj.Id] = obj.RequiredPrerequisiteCount;

            _reactive = new ReactiveEvaluator(quest, context, _completedKey, requiredCounts);
        }

        /// <summary>
        /// One derivation pass: records newly-failed/-completed objectives into the context, fires one-shot
        /// rewards on completed transitions, and raises change events. Idempotent — a pass with an unchanged
        /// context produces no transitions and no duplicate events. Objective time limits are NOT checked
        /// (use <see cref="Evaluate(float)"/> with a clock for that).
        /// </summary>
        public void Evaluate() => Run(0f, useTime: false);

        /// <summary>
        /// Like <see cref="Evaluate()"/>, but with a game-time clock <paramref name="now"/> so time-limited
        /// objectives are enforced: an Active objective with a <see cref="ObjectiveNodeData.TimeLimitSeconds"/>
        /// records its deadline the first time it is seen, and Fails once <paramref name="now"/> reaches it (call
        /// each tick with your running game time, e.g. <c>Time.time</c>). Completing before the deadline still wins.
        /// </summary>
        public void Evaluate(float now) => Run(now, useTime: true);

        private void Run(float now, bool useTime)
        {
            if (_quest == null || _context == null) return;
            if (useTime) _lastNow = now;

            // Quest gate: while the unlock condition is unmet, everything is Locked.
            if (!QuestUnlocked())
            {
                foreach (var obj in Objectives())
                    RaiseObjective(obj.Id, QuestState.Locked);
                RaiseQuest(QuestState.Locked);
                SyncQuestCompletionMarker(QuestState.Locked);
                return;
            }

            // Sync the engine to the current completed-set, then record fail/completion for available objectives.
            _reactive.Reevaluate();
            foreach (var obj in Objectives())
            {
                if (_context.CollectionContains(_failedKey, obj.Id)) continue;
                if (_context.CollectionContains(_completedKey, obj.Id)) continue;
                if (_reactive.GetState(obj.Id) != ReactiveNodeState.Available) continue;

                // Explicit fail precedes completion.
                if (obj.FailCondition != null && obj.FailCondition.Evaluate(_context))
                {
                    _context.AddToCollection(_failedKey, obj.Id);
                    continue;
                }
                // Completion is checked before timer expiry (completing on the deadline tick still succeeds).
                if (obj.CompletionCondition != null && obj.CompletionCondition.Evaluate(_context))
                {
                    _reactive.MarkCompleted(obj.Id);
                    continue;
                }
                // Time limit: arm a deadline the first time the objective is Active, then fail once it passes.
                if (useTime && obj.TimeLimitSeconds > 0f)
                {
                    var dKey = QuestContextKeys.DeadlineKey(_questId, obj.Id);
                    if (!_context.TryGet<float>(dKey, out var deadline) || float.IsNaN(deadline))
                    {
                        deadline = now + obj.TimeLimitSeconds;
                        _context.Set<float>(dKey, deadline);
                    }
                    if (now >= deadline)
                        _context.AddToCollection(_failedKey, obj.Id);
                }
            }

            // Re-derive, emit states, fire objective rewards.
            _reactive.Reevaluate();
            foreach (var obj in Objectives())
            {
                var state = GetObjectiveState(obj.Id);
                RaiseObjective(obj.Id, state);
                if (state == QuestState.Completed && obj.Reward != null)
                    FireReward(obj.Id, obj.Id, obj.Reward);
            }

            // Quest aggregate + quest reward.
            var questState = ComputeQuestState();
            RaiseQuest(questState);
            SyncQuestCompletionMarker(questState);
            if (questState == QuestState.Completed && _quest.CompletionReward != null)
                FireReward(QuestContextKeys.QuestRewardMarker, _questId, _quest.CompletionReward);
        }

        // Keeps this quest's id in/out of the shared CompletedQuests set so other quests can chain on it
        // (cross-quest gating). Derived from the current state, so it reverts on a context revert (replay-safe).
        private void SyncQuestCompletionMarker(QuestState state)
        {
            if (string.IsNullOrEmpty(_questId) || _context == null) return;
            bool done = state == QuestState.Completed;
            bool inSet = _context.CollectionContains(QuestContextKeys.CompletedQuests, _questId);
            if (done && !inSet) _context.AddToCollection(QuestContextKeys.CompletedQuests, _questId);
            else if (!done && inSet) _context.RemoveFromCollection(QuestContextKeys.CompletedQuests, _questId);
        }

        /// <summary>
        /// Clears this quest's progress for replay: empties its completed / failed / rewarded context sets (only
        /// the keys scoped to this quest's id — other quests sharing the context are untouched) and re-derives, so
        /// every objective returns to Locked/Active and one-shot rewards can fire again. The consumer needs no
        /// knowledge of the internal key scoping.
        /// </summary>
        public void Reset()
        {
            if (_context == null) return;
            _context.ClearCollection(_completedKey);
            _context.ClearCollection(_failedKey);
            _context.ClearCollection(_rewardedKey);
            _context.RemoveFromCollection(QuestContextKeys.CompletedQuests, _questId);
            // Disarm any timer deadlines so they re-arm fresh on the next Evaluate(now).
            if (_quest != null)
                foreach (var obj in Objectives())
                    if (obj.TimeLimitSeconds > 0f)
                        _context.Set<float>(QuestContextKeys.DeadlineKey(_questId, obj.Id), float.NaN);
            _lastObjectiveStates.Clear();
            _questStateKnown = false;
            _reactive.Reevaluate();
        }

        /// <summary>The current derived state of <paramref name="objectiveId"/>.</summary>
        public QuestState GetObjectiveState(string objectiveId)
        {
            if (string.IsNullOrEmpty(objectiveId) || _context == null) return QuestState.Locked;
            if (!QuestUnlocked()) return QuestState.Locked;
            if (_context.CollectionContains(_failedKey, objectiveId)) return QuestState.Failed;
            switch (_reactive.GetState(objectiveId))
            {
                case ReactiveNodeState.Completed: return QuestState.Completed;
                case ReactiveNodeState.Available: return QuestState.Active;
                default: return QuestState.Locked;
            }
        }

        /// <summary>The current aggregated quest state.</summary>
        public QuestState State => (_quest == null || _context == null || !QuestUnlocked())
            ? QuestState.Locked
            : ComputeQuestState();

        /// <summary>The ids of all objectives currently <see cref="QuestState.Active"/>.</summary>
        public IReadOnlyCollection<string> ActiveObjectiveIds => CollectByState(QuestState.Active);

        /// <summary>The ids of all objectives currently <see cref="QuestState.Completed"/>.</summary>
        public IReadOnlyCollection<string> CompletedObjectiveIds => CollectByState(QuestState.Completed);

        // ── Journal data layer (for a consumer quest-log UI) ──────────────────

        /// <summary>
        /// Treats the journal text (objective/quest <c>DisplayName</c> + <c>Description</c>) as localization KEYS,
        /// resolved through <paramref name="provider"/> in its current locale, instead of literal strings. Pass
        /// null to go back to literals. When localizing, set keys for everything you display.
        /// </summary>
        public QuestEvaluator UseLocalization(ILocalizationProvider provider)
        {
            _localization = provider;
            return this;
        }

        /// <summary>The quest's display title (falls back to its id), localized when a provider is set. Empty when no quest.</summary>
        public string DisplayName => _quest != null ? Resolve(_quest.DisplayName) : string.Empty;

        /// <summary>The quest's longer description, localized when a provider is set. Empty when none / no quest.</summary>
        public string Description => _quest != null ? Resolve(_quest.Description) : string.Empty;

        /// <summary>How many required objectives are currently <see cref="QuestState.Completed"/> (a progress numerator).</summary>
        public int RequiredCompleted => CountRequired(onlyCompleted: true);

        /// <summary>The total number of required objectives (a progress denominator).</summary>
        public int RequiredTotal => CountRequired(onlyCompleted: false);

        /// <summary>
        /// A read-only snapshot of every objective (in graph order) with its label, description, required flag, and
        /// current state — everything a quest-log UI needs, so the consumer keeps no id→label table of its own.
        /// </summary>
        public IReadOnlyList<ObjectiveView> GetObjectives()
        {
            var list = new List<ObjectiveView>();
            if (_quest == null) return list;
            foreach (var obj in Objectives())
                list.Add(new ObjectiveView(
                    obj.Id,
                    string.IsNullOrEmpty(obj.Title) ? obj.Id : Resolve(obj.Title),
                    Resolve(obj.Description),
                    obj.Required,
                    GetObjectiveState(obj.Id)));
            return list;
        }

        /// <summary>
        /// Seconds left before <paramref name="objectiveId"/> times out, as of the last <see cref="Evaluate(float)"/>:
        /// its full limit before the timer is armed, the live countdown while ticking, 0 once expired, and
        /// <see cref="float.PositiveInfinity"/> for an objective with no time limit.
        /// </summary>
        public float GetRemainingSeconds(string objectiveId)
        {
            var obj = FindObjective(objectiveId);
            if (obj == null || obj.TimeLimitSeconds <= 0f) return float.PositiveInfinity;
            if (_context != null
                && _context.TryGet<float>(QuestContextKeys.DeadlineKey(_questId, objectiveId), out var deadline)
                && !float.IsNaN(deadline))
                return Math.Max(0f, deadline - _lastNow);
            return obj.TimeLimitSeconds;
        }

        // Resolves journal text through the localization provider (as a key) when one is set; else returns it as-is.
        private string Resolve(string textOrKey)
            => (_localization == null || string.IsNullOrEmpty(textOrKey))
                ? textOrKey
                : _localization.Resolve(textOrKey, _localization.CurrentLocale);

        private ObjectiveNodeData FindObjective(string id)
        {
            if (_quest == null || string.IsNullOrEmpty(id)) return null;
            foreach (var obj in Objectives())
                if (obj.Id == id) return obj;
            return null;
        }

        private int CountRequired(bool onlyCompleted)
        {
            if (_quest == null || _context == null) return 0;
            int n = 0;
            foreach (var obj in Objectives())
            {
                if (!obj.Required) continue;
                if (onlyCompleted && GetObjectiveState(obj.Id) != QuestState.Completed) continue;
                n++;
            }
            return n;
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private bool QuestUnlocked()
            => _quest.UnlockCondition == null || _quest.UnlockCondition.Evaluate(_context);

        private IEnumerable<ObjectiveNodeData> Objectives()
        {
            foreach (var node in _quest.Nodes)
                if (node is ObjectiveNodeData obj && !string.IsNullOrEmpty(obj.Id))
                    yield return obj;
        }

        private QuestState ComputeQuestState()
        {
            bool anyRequiredFailed = false;
            bool allRequiredCompleted = true;
            int requiredCount = 0;
            foreach (var obj in Objectives())
            {
                if (!obj.Required) continue;
                requiredCount++;
                var s = GetObjectiveState(obj.Id);
                if (s == QuestState.Failed) anyRequiredFailed = true;
                if (s != QuestState.Completed) allRequiredCompleted = false;
            }
            if (anyRequiredFailed) return QuestState.Failed;
            if (requiredCount > 0 && allRequiredCompleted) return QuestState.Completed;
            return QuestState.Active;
        }

        private List<string> CollectByState(QuestState state)
        {
            var result = new List<string>();
            if (_quest == null || _context == null) return result;
            foreach (var obj in Objectives())
                if (GetObjectiveState(obj.Id) == state) result.Add(obj.Id);
            return result;
        }

        private void RaiseObjective(string id, QuestState state)
        {
            if (_lastObjectiveStates.TryGetValue(id, out var prev) && prev == state) return;
            _lastObjectiveStates[id] = state;
            OnObjectiveStateChanged?.Invoke(id, state);
        }

        private void RaiseQuest(QuestState state)
        {
            if (_questStateKnown && _lastQuestState == state) return;
            _lastQuestState = state;
            _questStateKnown = true;
            OnQuestStateChanged?.Invoke(state);
        }

        // Fires exactly once per guard key: guarded by the rewarded-set (persisted, so restore never re-fires).
        private void FireReward(string guardKey, string eventId, BaseAction reward)
        {
            if (reward == null || _context.CollectionContains(_rewardedKey, guardKey)) return;
            reward.Execute(_context);
            _context.AddToCollection(_rewardedKey, guardKey);
            OnRewardFired?.Invoke(eventId);
        }
    }
}
